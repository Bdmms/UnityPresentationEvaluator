/*
 * @Author: Sean Rannie
 * @Date: Mar/19/2019
 */

public class PeakFlag : VolumeFlag
{
    public float maximumPeak = 0.9f;    //The maximum that the recording is allowed to reach (0.0 - 1.0)

    //Checks a single volume sample
    protected override bool CheckVolume(float avg)
    {
        if (avg > maximumPeak)
            _flag = true;
        else
            _flag = false;

        return _flag;
    }

    //Override PercentFlag property in order to ensure nothing is overwritten
    public override float PercentFlag
    {
        get { return minPercentTrigger; }
    }
}
