using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using static Unity.MLAgents.Sensors.RayPerceptionOutput;

public class DriverAgent : Agent
{
    public GameObject vehicle; // ---------------
    private ForwardView forwardView;
    private VehicleControl control;
    private Vector3 lastPosition = new Vector3(201.9f, 26.02f, 555.88f);

    public float steer;
    public float acc;

    //public GameObject FinishTrigger;

    float laptime = 0;
    float distance = 0;

    [Space]
    public RayPerceptionSensorComponent3D[] sensors;
    private RayPerceptionSensorComponent3D middleSensor;
    private const int numMiddleSensors = 3;

    [Header("CRL")]
    public Transform straightStart;
    public Transform curveStart1;
    public Transform curveStart2;

    private int stepsUntilCurveTrack = 1000000; // = actually 10 000 ?!
    private int stepsUntilClosedTrack = 3000000;

    public bool chooseTrackFromInspector = false;

    float oldTime = float.MaxValue;

    public enum Track
    {
        STRAIGHT,
        CURVE,
        CLOSED
    }

    public Track track = Track.STRAIGHT;

    void Start()
    {
        if(vehicle!= null)
        {
            forwardView = vehicle.GetComponent<ForwardView>();
            control = vehicle.GetComponent<VehicleControl>();
        }

        sensors = vehicle.GetComponents<RayPerceptionSensorComponent3D>();

        foreach (RayPerceptionSensorComponent3D rays in sensors)
        {
            //Debug.Log(rays.SensorName);
            //Debug.Log(rays.name);
            if (rays.SensorName == "MiddleOfTheRoadSensor")
            {
                middleSensor = rays;
                Debug.Log("sensor found");
            }
        }

        //Debug.Log(middleSensor.SensorName);
    }

    private void FixedUpdate()
    {
        laptime += Time.fixedDeltaTime;
    }

    private void ChooseRoad() 
    { 
        int road = (int)Academy.Instance.EnvironmentParameters.GetWithDefault("RoadNumber", 0) - 1;

        if (road == 0)
        {
            track = Track.STRAIGHT;
        }
        else if (road == 1)
        {
            track = Track.CURVE;
        }
        else if (road == 2)
        {
            track = Track.CLOSED;
        }
    }

    public override void OnEpisodeBegin()
    {
        Debug.Log("Episode Ended");


        var rb = this.GetComponent<Rigidbody>();
        rb.velocity = Vector3.zero;
        laptime = 0f;
        distance = 0f;

        if(!(track == Track.CLOSED)) {
            ChooseRoad();
        }
        

        //if(!chooseTrackFromInspector && Academy.Instance.TotalStepCount > stepsUntilClosedTrack)
        //{
        //    track = Track.CLOSED;
        //}
        //else if (!chooseTrackFromInspector && Academy.Instance.TotalStepCount > stepsUntilCurveTrack)
        //{
        //    track = Track.CURVE;
        //}
        
        if (track == Track.STRAIGHT)
        {
            this.transform.localPosition = straightStart.position;
            this.transform.rotation = straightStart.rotation;
        }
        else if (track == Track.CURVE)
        {
            int x = Random.Range(0, 2);
            if (x == 0)
            {
                this.transform.localPosition = curveStart1.position;
                this.transform.rotation = curveStart1.rotation;
            }
            else if (x == 1)
            {
                this.transform.localPosition = curveStart2.position;
                this.transform.rotation = curveStart2.rotation;
            }
        }
        else if (track == Track.CLOSED)
        {
            int x = Random.Range(0, 3);
            if (x == 0)
            {
                this.transform.localPosition = new Vector3(-32.1f, 0.4f, 16.3f);
                this.transform.rotation = Quaternion.Euler(new Vector3(0f, 222.5f, 0f));
            }
            else if (x == 1)
            {
                this.transform.localPosition = new Vector3(176.6f, 0.4f, 104.6f);
                this.transform.rotation = Quaternion.Euler(new Vector3(0f, 163.9f, 0f));
            }
            else if (x == 2)
            {
                this.transform.localPosition = new Vector3(-112.2f, 0.4f, 94.5f);
                this.transform.rotation = Quaternion.Euler(new Vector3(0f, 46.5f, 0f));
            }
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        acc = Mathf.Clamp(actions.ContinuousActions[0], 0.3f, 1f);
        steer = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

        control.accel = acc;
        control.steer = steer;

        float accReward = acc * 100;
        //Debug.Log("Acceleration Reward: " + accReward);
        AddReward(accReward);

        int numHit = 0;
        //Debug.Log(middleSensor.RaySensor.RayPerceptionOutput.RayOutputs);

        if(middleSensor.RaySensor.RayPerceptionOutput.RayOutputs != null)
        {
            foreach(RayOutput output in middleSensor.RaySensor.RayPerceptionOutput.RayOutputs) {
                //Debug.Log(output);
                if(output.HasHit)
                {
                    numHit++;
                    //Debug.Log("Hit");
                }
            }
            //Debug.Log("Hit " + numHit + " Rays");
        }

        if (forwardView.OnRoad == false)
        {
            float offRoadReward = (distance / laptime);
            AddReward(offRoadReward);
            //Debug.Log("Off Road Reward: " + offRoadReward);
            //Debug.Log("Episode End: Off Road");
            EndEpisode();
        }

        Vector3 currposition = this.transform.position;
        if (Vector3.Distance(currposition, lastPosition) >= 1 )
        {
            distance += 1f;
            lastPosition = currposition;

            float speedReward = control.speed;
            //Debug.Log("Speed Reward: " + speedReward);

            float rayReward = numHit * 2;// / 13 * 15;
            //Debug.Log("Ray Reward: " + rayReward);

            AddReward(speedReward);
            AddReward(rayReward);

            //if(oldTime != float.MaxValue)
            //{
            //    AddReward(-(Time.realtimeSinceStartup - oldTime));
            //    oldTime = Time.realtimeSinceStartup;
            //} else
            //{
            //    oldTime = Time.realtimeSinceStartup;
            //}
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("FinishTrigger"))
        {
            AddReward(distance / laptime);
            EndEpisode();
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        //Debug.Log(sensor.GetBuiltInSensorType().ToString());
        //if (sensor.GetName() == "MiddleOfTheRoadSensor")
        //{
        //    Debug.Log("yes");
        //}
    }
}
