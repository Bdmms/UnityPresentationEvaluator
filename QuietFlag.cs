/*
 * @Author: Sean Rannie
 * @Date: Mar/19/2019
 */

public class QuietFlag : VolumeFlag
{
    public float minimum = 0.05f;    //The minimum avg to be considered too quiet (0.0 - 1.0)

    //Checks a single volume sample
    protected override bool CheckVolume(float avg)
    {
        if (avg < minimum)
            _flag = true;
        else
            _flag = false;

        return _flag;
    }
}
