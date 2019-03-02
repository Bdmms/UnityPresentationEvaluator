using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/*
 * @Author: Sean Rannie
 * @Date: Mar/01/2019
 * 
 * This script evaluates the effectiveness of a speaker through the viewpoint
 * of the camera. Please only attach this script to a Camera object.
 * 
 * --- This script can be altered to suit anyone's needs ---
 */

public class CameraEvaluator : MonoBehaviour
{
    public enum Direction {straight, upward, downward};

    //Public variables
    public float maxCameraXAngle = 0.3f;    //The highest angle before the speeker is no longer looking at audience
    public float minCameraXAngle = -0.3f;   //The lowest angle before the speeker is no longer looking at audience
    public float maxTimeLookAway = 1;       //Maximum time that the camera is allowed to look up or down
    public float maxTimeAnchor = 3;         //Maximum time that the camera is allowed to stay in one location
    public float anchorRange = 0.05f;       //The range that two angles are considered equal in
    public bool debug = false;              //Determines if the debugger is used
    public Text prompt;                     //The text that the evaluator uses

    //These are variables related to looking away from the audience
    private float _awayTime = 0;                            //The recorded time that the camera is not facing the audience
    private bool _lookAwayFlag = false;                     //Flag for determining if the camera is looking away
    private Direction _newDirection = Direction.straight;   //The new direction to be set
    private Direction _direction = Direction.straight;      //The current direction the camera is facing

    //These are variables related to looking at one location for an extended time
    private float _anchorTime = 0;                          //The recorded time that the camera is stuck in a position
    private bool _anchorFlag = false;                       //Flag for determining if the camera is too stiff
    private Quaternion _anchorPoint;                        //The last anchor point of the camera

    // Start is called before the first frame update
    void Start()
    {
        _anchorPoint = gameObject.transform.rotation;
    }

    // Update is called once per frame
    void Update()
    {
        //Conditions that give the direction of the camera
        if (gameObject.transform.rotation.x > maxCameraXAngle)
            _newDirection = Direction.downward;
        else if (gameObject.transform.rotation.x < minCameraXAngle)
            _newDirection = Direction.upward;
        else
            _direction = Direction.straight;

        //Conditions that determine if the camera is anchored to a position
        if(IsAngleWithinAnchor(_anchorPoint))
        {
            _anchorTime += Time.deltaTime;
            if (_anchorTime > maxTimeAnchor)
                _anchorFlag = true;
        }
        else
        {
            _anchorTime = 0;
            _anchorPoint = gameObject.transform.rotation;
            _anchorFlag = false;
        }

        //Response to holding a particular direction
        if (_direction != _newDirection)
        {
            _direction = _newDirection;
            _awayTime = 0;
            _lookAwayFlag = false;
        }
        else if(_direction != Direction.straight)
        {
            _awayTime += Time.deltaTime;
            if(_awayTime > maxTimeLookAway)
                _lookAwayFlag = true;
        }

        Refresh();
        Debugger();
     }

    //Updates visual components
    void Refresh()
    {
        //On-screen text
        if (prompt != null)
        {
            if (_lookAwayFlag)
            {
                if (_direction == Direction.downward)
                    prompt.text = "Look up!";
                else if (_direction == Direction.upward)
                    prompt.text = "Look down!";
                else
                    Debug.LogError("Impossible Situation Reached: Straight Direction with Flag");
            }
            else if(_anchorFlag)
                prompt.text = "You are fixated!";
            else
                prompt.text = "Good!";
        }
    }

    //Determines if new angle is within anchor range
    bool IsAngleWithinAnchor(Quaternion anchor)
    {
        float _xDiff = Mathf.Abs(gameObject.transform.rotation.x - anchor.x);
        float _yDiff = Mathf.Abs(gameObject.transform.rotation.y - anchor.y);
        float _zDiff = Mathf.Abs(gameObject.transform.rotation.z - anchor.z);
        Debug.Log(Mathf.Sqrt(_xDiff * _xDiff + _yDiff * _yDiff + _zDiff * _zDiff));
        return (Mathf.Sqrt(_xDiff * _xDiff + _yDiff * _yDiff + _zDiff * _zDiff) < anchorRange);
    }

    // Debug Funciton
    void Debugger()
    {
        //If debug mode is on
        if (debug)
            Debug.Log(gameObject.transform.rotation.x);
    }
}
