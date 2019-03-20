using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/*
 * @Author: Sean Rannie
 * @Date: Mar/19/2019
 * 
 * This script records audio from the microphone and evaluates the volume of the audio.
 * For use in Unity ONLY.
 * 
 * Make sure to adjust liveFrameMin to make the live evaluator more stable.
 * 
 * --- This script can be altered to suit anyone's needs ---
 */
 
public class MicrophoneReader : MonoBehaviour
{
    private static int _DISCARDED_SAMPLES = 3;

    private delegate void Evaluate(float value);
    private delegate void EvaluateAll(Queue<float> value);

    [Header("Set in Inspector")]
    [SerializeField]
    public Slider slider;               //The indicator of the current volume
    public RawImage graph;              //The graph that records the volume
    public int graphWidth = 200;        //The width of the graph component
    public int graphHeight = 100;       //The height of the graph component
    public int volumeScale = 4;         //Vertical scale of the graph
    public int recordingLength = 1;     //Length of chunk in seconds
    public int playbackTimer = 60;      //The length of delay between the recording and playback (varies based on recordingLength)
    public int liveFrameMin = 5;        //The number of frames the live evaluator waits before evaluating
    public int noiseSamples = 60;       //The number of samples used to combat noise
    public bool live = true;            //Which evaluation method should be used (live or chunck based)
    public bool evaluateAvg = true;     //Switches between avg and per sample evaluations
    public bool debug = false;          //Whether information should be written to the console
    public bool playback = false;       //Whether the audio should get played back to the user
    public bool pause = false;          //Pauses the program when possible

    private Evaluate _evaluator;        //Evaluates average sample
    private EvaluateAll _evaluatorAll;  //Evaluates sample collections
    private SentenceEvaluator _sentenceEvaluator; //The sentence evaluator
    private Queue<float> _sampleList = new Queue<float>();  //List of samples recorded since last frame
    private AudioSource _audioSource;   //The audio source used to output audio
    private AudioClip _audioClip;       //The clip that is recorded
    private Texture2D _image;           //The graphical component of the graph
    private float[] _data;              //The data of the clip
    private float _avg = 0;             //The avarage volume of the waveform
    private float _avgNoise = 0;        //The average volume of the noise
    private int _sampleRate = 44100;    //CD-Quality, I do not recommend changing this
    private int _lastPosition = 0;      //The last position of the recording
    private int _currentPosition = 0;   //The current position of the recording
    private int _chunckSize;            //The size of the recording chunk
    private int _timer = 0;             //Used for various purposes
    private int _evaluationNum = 0;     //Number of times the audio has been evaluated

    // Start is called before the first frame update
    void Start()
    {
        _chunckSize = _sampleRate * recordingLength;
        _audioClip = Microphone.Start(Microphone.devices[0], true, recordingLength, _sampleRate);

        //This program picks the first microphone available by default
        if (GetComponent<AudioSource>() != null)
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.clip = _audioClip;
        }

        //Initializing graphics
        if (graph != null)
        {
            _image = new Texture2D(graphWidth, graphHeight);

            for (int i = 0; i < graphWidth; i++)
                _image.SetPixel(i, graphHeight / 2, Color.black);
            _image.Apply();

            graph.texture = _image;
        }
    }

    // Update is called once per frame
    void Update()
    {
        _lastPosition = _currentPosition;
        _currentPosition = Microphone.GetPosition(Microphone.devices[0]);

        //If not paused
        if (!pause)
        {
            _timer++;
            _data = new float[_chunckSize];

            //Determine evalutation method
            if (live)
                LiveEvaluation();   //The system evaluates the new samples as they are added (smaller sample average)
            else
                ChunkEvaluation();  //Evaulation occurs when chunk has been recorded (delay in evaluation)

            //Average Noise Adjuster (note: first sample is discarded)
            if (_timer < noiseSamples + _DISCARDED_SAMPLES)
                _avgNoise += _avg;
            else if (_timer == noiseSamples + _DISCARDED_SAMPLES)
                _avgNoise = _avgNoise / noiseSamples;
            else
                Refresh();

            //Throw out remainder of queue
            while (_sampleList.Count > 0)
                _sampleList.Dequeue();
        }
    }

    //Modifies components that are on screen
    private void Refresh()
    {
        //Interperet resuts
        if (evaluateAvg)
            _evaluator(_avg - _avgNoise);
        else
            _evaluatorAll(_sampleList);

        if (_sentenceEvaluator != null)
        {
            _sentenceEvaluator.Evaluate(_avg, _sampleList);    //Evaluates breaks in sentences
            _sentenceEvaluator.DrawSentenceGraph(_image, 0, graphHeight / 2, graphWidth, graphHeight);
        }

        //On-screen indicator
        if (slider != null)
            slider.value = _avg * volumeScale * 2;

        //Waveform Graph
        if (graph != null)
            SetGraph();
    }

    //Sets the graph waveform
    private void SetGraph()
    {
        //Average
        for (int i = 0; i < graphHeight / 2; i++)
        {
            if (i < _avg * graphHeight * volumeScale)
                _image.SetPixel(_evaluationNum % graphWidth, graphHeight / 2 + i, Color.red);
            else
                _image.SetPixel(_evaluationNum % graphWidth, graphHeight / 2 + i, Color.white);

        }
        _image.SetPixel(_evaluationNum % graphWidth, graphHeight / 2, Color.black);
        
        _image.Apply();
    }

    //Evaulates after a recording chunk
    private void ChunkEvaluation()
    {
        //When the chunk of recording has been completed
        if (_currentPosition < _lastPosition && _audioClip.GetData(_data, 0))
        {
            _avg = 0;
            //Reading the waveform data
            for (int i = 0; i < _chunckSize; i++)
            {
                _avg += Mathf.Abs(_data[i]);
                _sampleList.Enqueue(_data[i]);
            }
            _avg /= _chunckSize;
            _evaluationNum++;
            Debugger();
        }
    }

    //Evaluates every frame
    private void LiveEvaluation()
    {
        //If the data is valid
        if (_timer % liveFrameMin == 0 && _lastPosition != _currentPosition && _audioClip.GetData(_data, _lastPosition))
        {
            _avg = 0;

            //Reading the waveform data
            int i = 0;
            for (int pos = 0; i < GetRecordingSize(); i++)
            {
                pos = (i + _lastPosition) % _chunckSize;
                _avg += Mathf.Abs(_data[pos]);
                _sampleList.Enqueue(_data[pos]);
            }
            _avg /= i;
            _evaluationNum++;
            Debugger();
        }
    }

    //Displays important data in the console
    private void Debugger()
    {
        //If in debug mode
        if (debug)
            Debug.Log("AVG: " + _avg);

        //Plays back the audio when the conditions have been met
        if (playback && _timer > playbackTimer && !_audioSource.isPlaying && _audioSource != null)
            _audioSource.Play(0);
    }

    //Returns the size of the newly recorded samples
    private int GetRecordingSize()
    {
        //Special case if position overflows
        if (_currentPosition < _lastPosition)
            return _currentPosition + (_chunckSize - _lastPosition);
        else
            return _currentPosition - _lastPosition;
    }

    //Sets the delegate function of the evaluator
    public void AddToEvaluatorDelegate(VolumeFlag flag)
    {
        _evaluator += flag.Evaluate;
        _evaluatorAll += flag.Evaluate;
    }

    //Sets the sentence evaluator
    public void SetSenetenceEvaluator(SentenceEvaluator ev)
    {
        _sentenceEvaluator = ev;
        ev.volumeScale = volumeScale;
        ev.SampleRate = _sampleRate;
    }
}
