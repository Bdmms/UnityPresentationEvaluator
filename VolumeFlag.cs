using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/*
 * @Author: Sean Rannie
 * @Date: Mar/19/2019
 * 
 * An abstract class that evaluates the recording
 */

public abstract class VolumeFlag : MonoBehaviour
{
    [Header("Set in Inspector")]
    public Text prompt;                                         //Text that is written to
    public string triggeredText = "Flag has been triggered!";   //The string that will be written to text field
    public string untriggeredText = "Flag has not been triggered!";
    public float minPercentTrigger = 0.5f;                      //Percent needed to trigger flag
    public bool enable = true;                                  //Enables evaluation
    public bool debug = false;                                  //Enables debug methods

    protected bool _flag;                                       //Flag value

    private int _numTriggered;                                  //Number of samples triggered
    private int _sampleSize;                                    //Number of samples

    //Triggered at starting time
    public void Start()
    {
        if (GetComponent<MicrophoneReader>() != null)
            GetComponent<MicrophoneReader>().AddToEvaluatorDelegate(this);
        else
            Debug.LogError("ERROR: VolumeFlag added to object without microphone listener.");
    }

    //Evaluates a single average
    public void Evaluate(float avg)
    {
        //If enabled
        if (enable)
        {
            CheckVolume(avg);

            if (prompt != null)
                prompt.text = _flag ? triggeredText : untriggeredText;
            
            if (debug && _flag)
                Debug.Log(triggeredText);
        }
    }

    //Evaluates a collection of samples
    public void Evaluate(Queue<float> samples)
    {
        //If enabled
        if (enable)
        {
            _sampleSize = samples.Count;
            foreach(float sample in samples)
            { 
                if (CheckVolume(sample))
                    _numTriggered++;
            }

            //Flag is triggered by percent threshold
            _flag = PercentFlag >= minPercentTrigger;
            
            if(prompt != null)
                prompt.text = _flag ? triggeredText : untriggeredText;

            if (debug && _flag)
                Debug.Log(triggeredText);
        }
    }

    //Checks a single volume sample
    protected abstract bool CheckVolume(float avg);

    //Property of the flag's value
    public bool Flag
    {
        get { return _flag; }
    }

    //Property of the percent flag's value
    public virtual float PercentFlag
    {
        get { return (float)_numTriggered / _sampleSize; }
    }
}
