/*
 * @Author: Sean Rannie
 * @Date: Mar/19/2019
 */

public class LoudFlag : VolumeFlag
{
    public float maximum = 0.10f;    //The maximum avg to be considered too loud (0.0 - 1.0)

    //Checks a single volume sample
    protected override bool CheckVolume(float avg)
    {
        if (avg > maximum)
            _flag = true;
        else
            _flag = false;

        return _flag;
    }
}
