using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/*
 * @Author: Sean Rannie
 * @Date: Mar/01/2019
 * 
 * This script records audio from the microphone and evaluates the volume of the audio.
 * For use in Unity ONLY.
 * 
 * Requirements:
 * 1.) Attach this script to any object
 * 2.) Make sure to include a UI Text object in the attached script component (or change the script)
 * 3.) If you want the audio to be played back, an AudioSource component needs to
 *     be attached to the same object the script is attached to.
 * 4.) Adjust the public parameters to your liking, every microphone acts differently and I doubt
 *     your results will be the same as mine
 * 5.) Attach a UI Slider and RawImage object to view the debug information
 *     
 * Make sure to adjust liveFrameMin to make the live evaluator more stable.
 * 
 * --- This script can be altered to suit anyone's needs ---
 */

public class Sentence
{
    private Queue<float> _volumeChart;  //The volume over the course of the sentence
    private double _startTime;          //The starting time of the sentence
    private double _endTime;            //The ending time of the sentence

    //Constructor
    public Sentence(double start)
    {
        _volumeChart = new Queue<float>();
        _startTime = start;
        _endTime = -1;
        Debug.Log("Sentence Starting");
    }

    //Marks the end of the sentence
    public void EndSentence(double end)
    {
        //Can only end sentence once
        if (IsEnded())
        { 
            _endTime = end;
            Debug.Log("Sentence Concluded (" + _volumeChart.Count + " samples)");
        }
    }

    //Indication that the sentence has ended
    public bool IsEnded() { return _endTime == -1; }
    
    //Difference in time from when the sentence started and ended
    public double ElapsedTime {
        get{ return _endTime - _startTime;}
    }

    //Data can be streamed in and out of the chart queue
    public float StreamChart {
        get{return _volumeChart.Dequeue();}
        set{_volumeChart.Enqueue(value);}
    }

    //The size of the sample chart is retrieved
    public int SampleSize{
        get { return _volumeChart.Count; }
    }
}

public class VolumeEvaluator : MonoBehaviour
{
    private static int _DISCARDED_SAMPLES = 3;
    private static int _VOLUME_SCALE = 4;
    private static float _VOLUME_SILENCE = 0.008f;

    public Text prompt;                 //Text Object that is displayed
    public Slider slider;               //The indicator of the current volume
    public RawImage graph;              //The graph that records the volume
    public int graphWidth = 200;        //The width of the graph component
    public int graphHeight = 100;       //The height of the graph component
    public int sampleRate = 44100;      //CD-Quality, I do not recommend changing this
    public int sentenceCapacity = 20;   //The number of sentences that are maintained in the list
    public int recordingLength = 1;     //Length of chunk in seconds
    public int playbackTimer = 60;      //The length of delay between the recording and playback (varies based on recordingLength)
    public int liveFrameMin = 5;        //The number of frames the live evaluator waits before evaluating
    public int noiseSamples = 60;       //The number of samples used to combat noise
    public float minSilenceTime = 0.75f;//The minimum time needed to declare that the sentence is broken
    public float maxSilenceTime = 1.5f; //The maximum time needed for the speech to be considered paused for too long
    public float maximumPeak = 0.9f;    //The maximum that the recording is allowed to reach (0.0 - 1.0)
    public float maximumAvg = 0.10f;    //The maximum avg to be considered too loud (0.0 - 1.0)
    public float minimumAvg = 0.05f;    //The minimum avg to be considered too quiet (0.0 - 1.0)
    public bool live = true;            //Which evaluation method should be used (live or chunck based)
    public bool debug = false;          //Whether information should be written to the console
    public bool playback = false;       //Whether the audio should get played back to the user
    
    private LinkedList<Sentence> _sentences = new LinkedList<Sentence>();
    private AudioSource _audioSource;   //The audio source used to output audio
    private AudioClip _audioClip;       //The clip that is recorded
    private Texture2D _image;           //The graphical component of the graph
    private float[] _data;              //The data of the clip
    private float _max = 0;             //The maximum point in the waveform
    private float _min = 0;             //The minimum point in the waveform
    private float _avg = 0;             //The avarage volume of the waveform
    private float _avgNoise = 0;        //The average volume of the noise
    private float _sum = 0;             //The average volume minus noise
    private float _silenceTimer = 0;    //The time accumulated from silence
    private int _lastPosition = 0;      //The last position of the recording
    private int _currentPosition = 0;   //The current position of the recording
    private int _chunckSize;            //The size of the recording chunk
    private int _timer = 0;             //Used for various purposes
    private int _evaluationNum = 0;     //Number of times the audio has been evaluated
    private bool _peakFlag = false;     //Triggered when the speaker peaks their microphone
    private bool _loudFlag = false;     //Triggered when the speaker is too loud
    private bool _quietFlag = false;    //Triggered when the speaker is too quiet
    private bool _breakFlag = false;    //Triggered when the sentence ends
    private bool _pauseFlag = false;    //Triggered when the speaker pauses too long
    private bool _sentenceStart = false;//Triggered when the sentence starts
    private bool _recordedFlag = true;  //Triggered when the sentence is recorded

    // Start is called before the first frame update
    void Start()
    {
        _chunckSize = sampleRate * recordingLength;
        _audioClip = Microphone.Start(Microphone.devices[0], true, recordingLength, sampleRate);
        
        _sentences.AddFirst(new Sentence(0));
        _sentences.First.Value.EndSentence(0);

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
                _image.SetPixel(i, graphHeight/2, Color.black);
            _image.Apply();

            graph.texture = _image;
        }
    }

    // Update is called once per frame
    void Update()
    {
        _timer++;
        _lastPosition = _currentPosition;
        _currentPosition = Microphone.GetPosition(Microphone.devices[0]);
        _data = new float[_chunckSize];

        //Determine evalutation method
        if (live)
            LiveEvaluation();   //The system evaluates the new samples as they are added (smaller sample average)
        else
            ChunkEvaluation();  //Evaulation occurs when chunk has been recorded (delay in evaluation)

        //Interperet resuts
        _peakFlag = _max > maximumPeak;
        _loudFlag = _avgNoise > maximumAvg;
        _quietFlag = _avgNoise < minimumAvg;

        _sum = _avg - _avgNoise;
        SentenceEvaluator();    //Evaluates breaks in sentences
    }

    // Called after Update()
    void LateUpdate()
    {
        //Average Noise Adjuster (note: first sample is discarded)
        if (_timer < noiseSamples + _DISCARDED_SAMPLES)
            _avgNoise += _avg;
        else if (_timer == noiseSamples + _DISCARDED_SAMPLES)
            _avgNoise = _avgNoise / noiseSamples;
        else
            Refresh();
    }

    //Modifies components that are on screen
    private void Refresh()
    {
        //On-screen indicator
        if (slider != null)
            slider.value = _sum * _VOLUME_SCALE * 2;

        //On-screen text
        if (prompt != null)
            SetPrompt();

        //Waveform Graph
        if (graph != null)
            SetGraph();
    }

    //Sets on-screen text
    private void SetPrompt() { 
        if(prompt != null)
        {
            if (_peakFlag) prompt.text = "You're peaking your microphone!";
            else if (_pauseFlag) prompt.text = "You're pausing for too long!";
            else if (_quietFlag) prompt.text = "You're too quiet!";
            else if (_loudFlag) prompt.text = "You're too loud!";
            else prompt.text = "Perfect!";
        }
    }

    //Sets the graph waveform
    private void SetGraph()
    {
        //Average
        for (int i = 0; i < graphHeight / 2; i++)
        {
            if(i < _sum * graphHeight * _VOLUME_SCALE)
                _image.SetPixel(_evaluationNum % graphWidth, graphHeight / 2 + i, Color.red);
            else
                _image.SetPixel(_evaluationNum % graphWidth, graphHeight / 2 + i, Color.white);

        }
        _image.SetPixel(_evaluationNum % graphWidth, graphHeight / 2, Color.black);

        //If sentence needs to be recorded
        if (!(_sentenceStart || _recordedFlag))
        {
            _recordedFlag = true;
            int samples = _sentences.Last.Value.SampleSize;
            int rate = samples / graphWidth;
            int iRate = graphWidth / samples + 1;
            float total = 0;

            //Fill waveform graph
            if (rate < 1) //More pixels than samples
                for (int x = 0; x < graphWidth; x++)
                {
                    if (x % iRate == 0)
                        total = _sentences.Last.Value.StreamChart;

                    for (int s = 0; s < graphHeight / 2; s++)
                    {
                        if (s < Mathf.Abs(total * graphHeight * _VOLUME_SCALE))
                            _image.SetPixel(x, s, Color.blue);
                        else
                            _image.SetPixel(x, s, Color.white);
                    }
                }

            else //More samples than pixels
                for (int x = 0; x < graphWidth; x++, total = 0)
                {
                    for (int i = 0; i < rate; i++)
                        total += _sentences.Last.Value.StreamChart;

                    for (int s = 0; s < graphHeight / 2; s++)
                    {
                        if (s < Mathf.Abs(total / rate * graphHeight * _VOLUME_SCALE))
                            _image.SetPixel(x, s, Color.blue);
                        else
                            _image.SetPixel(x, s, Color.white);
                    }
                }

            if (_sentences.Count > sentenceCapacity)
                _sentences.RemoveFirst();
        }
        else if (_sentenceStart)
            _recordedFlag = false;

        _image.Apply();
    }

    //Evaulates after a recording chunk
    private void ChunkEvaluation()
    {
        //When the chunk of recording has been completed
        if (_currentPosition < _lastPosition && _audioClip.GetData(_data, 0))
        {
            _max = 0;
            _min = _data[0];
            _avg = 0;

            //Reading the waveform data
            for (int i = 0; i < _chunckSize; i++)
            {
                if (_data[i] > _max)
                    _max = _data[i];
                if (_data[i] < Mathf.Abs(_min))
                    _min = Mathf.Abs(_data[i]);
                _avg += Mathf.Abs(_data[i]);
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
            _max = 0;
            _min = 0;
            _avg = 0;

            //Reading the waveform data
            int i = 0;
            for (int pos = 0; i < GetRecordingSize(); i++)
            {
                pos = (i + _lastPosition) % _chunckSize;
                if (_data[pos] > _max)
                    _max = _data[pos];
                if (_data[pos] < Mathf.Abs(_min))
                    _min = Mathf.Abs(_data[pos]);
                _avg += Mathf.Abs(_data[pos]);
            }
            _avg /= i;
            _evaluationNum++;
            Debugger();
        }
    }

    //Evaluates and controls the sentence breaks of the audio
    private void SentenceEvaluator()
    {
        //If volume is below silence threshold
        if(_sum < _VOLUME_SILENCE)
        {
            _silenceTimer += Time.deltaTime;

            //If silence time has reached minimum threshold
            if (_silenceTimer > minSilenceTime)
            {
                _breakFlag = true;
                _sentenceStart = false;
                _sentences.Last.Value.EndSentence(_silenceTimer);
            }

            //If silent for too long
            if(_silenceTimer > maxSilenceTime)
                _pauseFlag = true;
        }
        //If sound is detected
        else
        {
            _silenceTimer = 0;
            _breakFlag = _pauseFlag = false;

            //If sentence hasn't been started
            if (!_sentenceStart)
            {
                _sentences.AddLast(new Sentence(_silenceTimer));
                _sentenceStart = true;
            }

            _sentences.Last.Value.StreamChart = _sum;
        }
    }

    //Displays important data in the console
    private void Debugger()
    {
        //If in debug mode
        if (debug)
        {
            //USEFUL DEBUG COMMANDS:
            //Debug.Log("LAST: " + _lastPosition);
            //Debug.Log("CURRENT: " + _currentPosition);
            //Debug.Log("SIZE: " + _data.Length);
            Debug.Log("PEAK: " + _max);
            Debug.Log("ANTIPEAK: " + _min);
            Debug.Log("AVG: " + _avg);
            Debug.Log("NOISE: " + _avgNoise);
            Debug.Log("SUM: " + (_avg - _avgNoise));
        }

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
}
