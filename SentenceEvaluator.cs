using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/*
 * @Author: Sean Rannie
 * @Date: Mar/19/2019
 * 
 * This script contains a pair of classes that evaluates sentences
 */

public class Sentence
{
    private Queue<float> _avgVolumeSamples; //The avg volume over the course of the sentence
    private Queue<float> _volumeSamples;    //The volume over the course of the sentence
    private double _startTime;              //The starting time of the sentence
    private double _endTime;                //The ending time of the sentence

    //Constructor
    public Sentence(double start)
    {
        _avgVolumeSamples = new Queue<float>();
        _volumeSamples = new Queue<float>();
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
            Debug.Log("Sentence Concluded (" + _avgVolumeSamples.Count + " samples)");
        }
    }

    //Indication that the sentence has ended
    public bool IsEnded() { return _endTime == -1; }

    //Difference in time from when the sentence started and ended
    public double ElapsedTime
    {
        get { return _endTime - _startTime; }
    }

    //Data can be streamed in and out of the chart queue
    public float StreamChart
    {
        get { return _avgVolumeSamples.Dequeue(); }
        set { _avgVolumeSamples.Enqueue(value); }
    }

    //Sets the samples of the audio
    public Queue<float> Samples
    {
        get { return _volumeSamples; }
        set
        {
            while (value.Count > 0)
                _volumeSamples.Enqueue(value.Dequeue());
        }
    }

    //The size of the sample chart is retrieved
    public int SampleSize
    {
        get { return _avgVolumeSamples.Count; }
    }
}

public class SentenceEvaluator: MonoBehaviour
{
    private static float _VOLUME_SILENCE = 0.008f;//Minimum to be considered silent

    [Header("Set in Inspector")]
    public Text prompt;                 //Text that is written to
    public string triggeredText = "Sentence has been paused!";
    public string untriggeredText = "Sentence is fine!";
    public string filename = "SEN #";   //The filename that is used for creating .wav file 
    public int volumeScale = 4;         //Vertical scale of the graph (may get overwritten)
    public float minSilenceTime = 0.75f;//The minimum time needed to declare that the sentence is broken
    public float maxSilenceTime = 1.5f; //The maximum time needed for the speech to be considered paused for too long
    public bool writeToWAV = true;      //Creates .wav files of sentence
    public bool debug = false;          //Whether information should be written to the console

    private LinkedList<Sentence> _sentences = new LinkedList<Sentence>();   //The recorded sentences
    private float _silenceTimer = 0;    //The time accumulated from silence
    
    private int _sampleRate;            //Sample rate of audio
    private int _writtenSentences = 0;  //Number of sentences written to .wav file
    private bool _breakFlag = false;    //Triggered when the sentence ends
    private bool _pauseFlag = false;    //Triggered when the speaker pauses too long
    private bool _sentenceStart = false;//Triggered when the sentence starts
    private bool _recordedFlag = false; //Triggered when the sentence is recorded

    //Starting method
    public void Start()
    {
        if (GetComponent<MicrophoneReader>() != null)
            GetComponent<MicrophoneReader>().SetSenetenceEvaluator(this);
        else
            Debug.LogError("ERROR: SentenceEvaluator added to object without microphone listener.");
    }

    //Evaluates both the average and collectino of samples
    public void Evaluate(float avg, Queue<float> sampleList)
    {
        if(_sentences.Count > 0)
            _sentences.Last.Value.Samples = sampleList;

        //If volume is below silence threshold
        if (avg < _VOLUME_SILENCE)
        {
            _silenceTimer += Time.deltaTime;

            //If silence time has reached minimum threshold
            if (_silenceTimer > minSilenceTime)
            {
                _breakFlag = true;
                _sentenceStart = false;

                if (_sentences.Count > 0)
                {
                    _sentences.Last.Value.EndSentence(_silenceTimer);

                    if (writeToWAV)
                    {
                        _writtenSentences++;
                        string s = AudioWriter.WriteToWAV(filename + _writtenSentences + ".wav", _sampleRate, _sentences.Last.Value.Samples);
                        Debug.Log("Sentence successfully written to " + s);
                        Debug.Log("# of samples = " + _sentences.Last.Value.Samples.Count);
                    }
                }
            }

            //If silent for too long
            if (_silenceTimer > maxSilenceTime)
            {
                _pauseFlag = true;
                prompt.text = triggeredText;
            }
            else
                prompt.text = untriggeredText;
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

            _sentences.Last.Value.StreamChart = avg;
        }
    }

    //Draws the last sentence on a texture canvas
    public void DrawSentenceGraph(Texture2D canvas, int x1, int y1, int x2, int y2)
    {
        //If sentence needs to be recorded
        if (_sentences.Count > 0 && !(_sentenceStart || _recordedFlag))
        {
            int graphWidth = x2 - x1;
            int graphHeight = y2 - y1;
            int samples = _sentences.Last.Value.SampleSize;
            int rate = samples / graphWidth;
            int iRate = graphWidth / samples + 1;
            float total = 0;

            //Fill waveform graph
            if (rate < 1) //More pixels than samples
                for (int x = 0; x < graphWidth; x++)
                {
                    if (x % iRate == 0)
                        total = _sentences.First.Value.StreamChart;

                    for (int s = 0; s < graphHeight / 2; s++)
                    {
                        if (s < Mathf.Abs(total * graphHeight * volumeScale))
                            canvas.SetPixel(x, s, Color.blue);
                        else
                            canvas.SetPixel(x, s, Color.white);
                    }
                }

            else //More samples than pixels
                for (int x = 0; x < graphWidth; x++, total = 0)
                {
                    for (int i = 0; i < rate; i++)
                        total += _sentences.First.Value.StreamChart;

                    for (int s = 0; s < graphHeight / 2; s++)
                    {
                        if (s < Mathf.Abs(total / rate * graphHeight * volumeScale))
                            canvas.SetPixel(x, s, Color.blue);
                        else
                            canvas.SetPixel(x, s, Color.white);
                    }
                }

            //Sentence has been recorded
            _sentences.RemoveFirst();
            _recordedFlag = true;
        }
        else if (_sentenceStart)
            _recordedFlag = false;
    }

    public int SampleRate { set { _sampleRate = value; } }
    public bool Flag { get { return _pauseFlag; } }
}
