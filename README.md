# Unity_Microphone_Evaluator

Author: Sean Rannie
Date: Feb/02/14

This script records audio from the microphone and evaluates the volume of the audio.
For use in Unity ONLY.

Requirements: 

1.) Attach this script to any object

2.) Make sure to include a UI Text object in the attached script component (or change the script)

3.) If you want the audio to be played back, an AudioSource component needs to be attached to the same object the script is attached to.
    
4.) Adjust the public parameters to your liking, every microphone acts differently and I doubt your results will be the same as mine
    
I highly advised NOT using the live evalution, both evaluation methods will require different parameters to function properly. Only use the live evualtion method if you need a faster response time the the microphone input. Keeping the recording length small allows the chunk evaluation method to be just as responsive.
    
This version of the script uses the maximum point of the waveform as the evaulation data. If you want to change the statistic used to a different variable, please note:

The average volume of the waveform includes positive AND negative amplitudes; therefore, a majority of the time the average will remain at ~ 0.0 because the positive and nagative sides cancel out each other.


--- This script can be altered to suit anyone's needs ---
