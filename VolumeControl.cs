using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/*
 * @Author: Sean Rannie
 * @Date: Feb/15/2019
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
 * I highly advised NOT using the live evalution, both evaluation methods will require different
 * parameters to function properly. Only use the live evualtion method if you need a faster
 * response time the the microphone input. Keeping the recording length small allows the chunk
 * evaluation method to be just as responsive.
 * 
 * --- This script can be altered to suit anyone's needs ---
 */

public class VolumeControl : MonoBehaviour
{
    public Text prompt;                 //Text Object that is displayed
    public Slider slider;               //The indicator of the current volume
    public RawImage graph;              //The graph that records the volume
    public int graphWidth = 200;        //The width of the graph component
    public int graphHeight = 100;       //The height of the graph component
    public int sampleRate = 44100;      //CD-Quality, I do not recommend changing this
    public int recordingLength = 1;     //Length of chunk in seconds
    public int playbackTimer = 60;      //The length of delay between the recording and playback (varies based on recordingLength)
    public int liveFrameMin = 5;        //The number of frames the live evaluator waits before evaluating
    public int graphScale = 3;          //The relative scale that the audio is displayed on-screen
    public int graphPrecision = 4;      //The precision of the graph (lower value = higher precision = lower performance)
    public float maximumPeak = 0.9f;    //The maximum that the recording is allowed to reach (0.0 - 1.0)
    public float maximumAvg = 0.10f;    //The maximum avg to be considered too loud (0.0 - 1.0)
    public float minimumAvg = 0.05f;    //The minimum avg to be considered too quiet (0.0 - 1.0)
    public bool live = true;            //Which evaluation method should be used (live or chunck based)
    public bool debug = false;          //Whether information should be written to the console
    public bool playback = true;        //Whether the audio should get played back to the user
    public bool liveData = false;       //Whether the lve audio is graphed (performance intensive)

    private AudioSource _audioSource;   //The audio source used to output audio
    private AudioClip _audioClip;       //The clip that is recorded
    private Texture2D g;                //The graphical component of the graph
    private float[] _data;              //The data of the clip
    private float _max;                 //The maximum point in the waveform
    private float _min;                 //The minimum point in the waveform
    private float _avg;                 //The avarage volume of the waveform
    private int _lastPosition;          //The last position of the recording
    private int _currentPosition;       //The current position of the recording
    private int _chunckSize;            //The size of the recording chunk
    private int _timer;                 //Used for various purposes
    private int _evaluationNum;         //Number of times the audio has been evaluated
    private int _dataCounter;           //Number of data samples read

    // Start is called before the first frame update
    void Start()
    {
        _chunckSize = sampleRate * recordingLength;
        _currentPosition = 0;
        _dataCounter = 0;

        _audioClip = Microphone.Start(Microphone.devices[0], true, recordingLength, sampleRate);
        
        //This program picks the first microphone available by default
        if (GetComponent<AudioSource>() != null)
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.clip = _audioClip;
        }

        //Initializing graphics
        if (graph != null)
        {
            g = new Texture2D(graphWidth, graphHeight);

            for (int i = 0; i < graphWidth; i++)
                g.SetPixel(i, graphHeight/2, Color.black);
            g.Apply();

            graph.texture = g;
        }
        else
            liveData = false;
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

        Refresh();
    }

    //Modifies components that are on screen
    private void Refresh()
    {
        //On-screen indicator
        if (slider != null)
        { 
            slider.value = _avg * graphScale * 2;
        }

        //On-screen text
        if(prompt != null)
        {
            //Interperet resuts
            if (_max > maximumPeak)
                prompt.text = "You're peaking your microphone!";
            else if (_avg > maximumAvg)
                prompt.text = "You're too loud!";
            else if (_avg > minimumAvg)
                prompt.text = "Perfect!";
            else
                prompt.text = "You're too quiet!";
        }

        //Waveform Graph
        if (graph != null)
        {
            //Average
            for (int i = graphHeight / 2; i < graphHeight; i++)
                g.SetPixel(_evaluationNum % graphWidth, i, Color.white);
            
            for (int i = 0; i < _avg * graphHeight * graphScale; i++)
                g.SetPixel(_evaluationNum % graphWidth, graphHeight / 2 + i, Color.red);

            g.SetPixel(_evaluationNum % graphWidth, graphHeight / 2, Color.black);

            //Data
            if (liveData && _timer % liveFrameMin == 0) //Not efficient
            {
                for (int i = 0; i < GetRecordingSize() / graphPrecision; i++, _dataCounter += graphPrecision)
                {
                    for (int s = 0; s < graphHeight / 2; s++)
                    {
                        for (int x = 0; x < graphPrecision; x++)
                        {
                            if (s < Mathf.Abs(_data[(_lastPosition + i * graphPrecision) % _chunckSize]) * graphHeight * graphScale)
                                g.SetPixel((_dataCounter + x) % graphWidth, s, Color.blue);
                            else
                                g.SetPixel((_dataCounter + x) % graphWidth, s, Color.white);
                        }
                    }
                }
            }

            g.Apply();
        }
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
