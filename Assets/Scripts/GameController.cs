using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

public class GameController : MonoBehaviour
{
    
    public float speed;
    public float maxSpeed;
    public float speedIncrement;
    public int viewDistance;
    public int segmentsBetweenObstacles;
    public int secondsBeforeExtraLife;
    public int maxHealth;
    public int health;
    public float gameTimer = 0f;
    public float spinSpeed = 2f;
    public bool bigWooshMode = false;

    public GameObject vrRig;
    public GameObject headset;
    public GameObject tunnelSegmentPrefab;
    public GameObject tunnelSegmentCollidablePrefab;
    public GameObject tunnelEndCapPrefab;
    public GameObject cubePrefab;
    public GameObject rectanglePrefab;
    public AudioSource woosh;

    private LinkedList<GameObject> wallSegments = new LinkedList<GameObject>();
    private PostProcessVolume postProcessing;
    private Bloom bloomLayer = null;
    private ColorGrading colorLayer = null;
    private GameObject effectsManager;
    private EffectsController effectsController;

    private bool intensityIncreasing = true;
    private float hueShiftSpeed;
    private float currentZRotation = 0f;
    private bool isInvincible = false;
    private float lastHitLocation;
    private float lastHitTime;
    private int stage = 1;

    private System.Random random = new System.Random();

    // Start is called before the first frame update
    void Start()
    {
        effectsManager = GameObject.Find("EffectsManager");
        effectsController = effectsManager.GetComponent<EffectsController>();

        postProcessing = headset.GetComponent<PostProcessVolume>();
        postProcessing.profile.TryGetSettings(out bloomLayer);
        postProcessing.profile.TryGetSettings(out colorLayer);

        wallSegments.AddFirst(Instantiate(tunnelEndCapPrefab, new Vector3(0, 1, 0), Quaternion.identity));
        if (segmentsBetweenObstacles <= 0) segmentsBetweenObstacles = 2;

        if (maxHealth <= 0) maxHealth = 6;
        health = maxHealth;

        //Changes play style from close-in to far out.
        if (bigWooshMode)
        {
            speed = 0.4f;
            segmentsBetweenObstacles = 30;
            maxSpeed = 4.5f;
            viewDistance = 200;
            speedIncrement = 0.001f;
        }
    }

    // Update is called once per frame
    void Update()
    {
        gameTimer += Time.deltaTime;
        Vector3 playerPos = vrRig.transform.position;
        TunnelInstantiator(playerPos);
    }

    void FixedUpdate()
    {
        //Move Player
        if (speed < maxSpeed) speed += speedIncrement;

        Vector3 playerPos = vrRig.transform.position;
        vrRig.transform.position = new Vector3(playerPos.x, playerPos.y, playerPos.z + speed);

        //Remove player invincibility if needed
        if (isInvincible && playerPos.z > lastHitLocation + 2.5f) isInvincible = false;

        //Extra Life if applicable
        if (health < maxHealth && gameTimer > (lastHitTime + secondsBeforeExtraLife)) GainLife();

        //Flash the neon intensity, may get rid of this...
        if (bloomLayer != null)
        {
            if (intensityIncreasing && bloomLayer.intensity.value < 9.5f) bloomLayer.intensity.value += 0.2208148f;
            else if (intensityIncreasing && bloomLayer.intensity.value >= 9.5f) intensityIncreasing = false;
            else if (!intensityIncreasing && bloomLayer.intensity.value > 6.5f) bloomLayer.intensity.value -= 0.2208148f;
            else if (!intensityIncreasing && bloomLayer.intensity.value <= 6.5f) intensityIncreasing = true;

            //More RGBS!
            if(bigWooshMode) colorLayer.hueShift.value += speed;
            else colorLayer.hueShift.value += speed*10f;

            if (colorLayer.hueShift.value == 180f) colorLayer.hueShift.value = -180f;
        }

        //Spin That Thang!
        //Note to self, do not uncomment on pain of barf.  It was nice to know it could be done.
        //if(stage == 4)
        //{
        //    currentZRotation += spinSpeed;
        //    foreach (GameObject segment in wallSegments)
        //    {
        //        Vector3 euler = segment.transform.eulerAngles;
        //        segment.transform.eulerAngles = new Vector3(euler.x, euler.y, currentZRotation);
        //    }
        //}

        //Increase Woosh speed to match speed increase
        if(woosh.pitch < 3f) woosh.pitch += 0.00042857f;

        //Increase Obstacle Difficulty
        if (speed / maxSpeed > 0.25f && stage < 2) stage = 2;
        else if (speed / maxSpeed > 0.5f && stage < 3) stage = 3;
        else if (speed / maxSpeed > 0.75f && stage < 4) stage = 4;
        else if (speed / maxSpeed >= 1f && stage < 5) stage = 5;
    }

    //Manages the Creation and Deletion of Tunnel Segments for the Player
    private void TunnelInstantiator(Vector3 playerPos)
    {
        //Check distance to last node in LL, and add a new node if it is under view distance
        while (wallSegments.Last.Value.transform.position.z - playerPos.z < viewDistance)
        {
            GameObject segment;
            float newZ = wallSegments.Last.Value.transform.position.z + 2.5f;

            bool isObstacleSegment = false;

            //Check if its been x empty segments since last cubes, if so we are generating obstacles
            if (newZ % (2.5f * segmentsBetweenObstacles + 2.5f) == 0) isObstacleSegment = true;

            //Instantiate Obstacles
            if (isObstacleSegment)
            {
                segment = Instantiate(tunnelSegmentCollidablePrefab, new Vector3(0, 1, newZ), Quaternion.identity);
                int chosenType = random.Next(0, stage);

                if (chosenType == 0) InstantiateStageOne(segment, newZ);
                else if (chosenType == 1) InstantiateStageTwo(segment, newZ);
                else if (chosenType == 2) InstantiateStageThree(segment, newZ);
                //Stage Four spins, which is in FixedUpdate
                else if (chosenType == 4) InstantiateStageFive(segment, newZ);
            }
            else segment = Instantiate(tunnelSegmentPrefab, new Vector3(0, 1, newZ), Quaternion.identity);

            wallSegments.AddLast(segment);
        }
        //Check distance from first node in LL, and remove if exceeds view distance
        while (playerPos.z - wallSegments.First.Value.transform.position.z > viewDistance)
        {
            Destroy(wallSegments.First.Value);
            wallSegments.RemoveFirst();
        }
    }

    //Generates the Blocking Cubes and makes them children of the new segment.
    private void InstantiateStageOne(GameObject segment, float newZ)
    {
        int chosenNum = random.Next(1, 5);

        if (chosenNum != 1)
        {
            GameObject topLeftA = Instantiate(cubePrefab, new Vector3(-.625f, 1.625f, newZ - 0.625f), Quaternion.identity);
            GameObject topLeftB = Instantiate(cubePrefab, new Vector3(-.625f, 1.625f, newZ + 0.625f), Quaternion.identity);
            topLeftA.transform.parent = segment.transform;
            topLeftB.transform.parent = segment.transform;
        }

        if (chosenNum != 2)
        {
            GameObject topRightA = Instantiate(cubePrefab, new Vector3(.625f, 1.625f, newZ - 0.625f), Quaternion.identity);
            GameObject topRightB = Instantiate(cubePrefab, new Vector3(.625f, 1.625f, newZ + 0.625f), Quaternion.identity);
            topRightA.transform.parent = segment.transform;
            topRightB.transform.parent = segment.transform;
        }

        if (chosenNum != 3)
        {
            GameObject bottomLeftA = Instantiate(cubePrefab, new Vector3(-.625f, 0.374f, newZ - 0.625f), Quaternion.identity);
            GameObject bottomLeftB = Instantiate(cubePrefab, new Vector3(-.625f, 0.374f, newZ + 0.625f), Quaternion.identity);
            bottomLeftA.transform.parent = segment.transform;
            bottomLeftB.transform.parent = segment.transform;
        }

        if (chosenNum != 4)
        {
            GameObject bottomRightA = Instantiate(cubePrefab, new Vector3(.625f, 0.374f, newZ - 0.625f), Quaternion.identity);
            GameObject bottomRightB = Instantiate(cubePrefab, new Vector3(.625f, 0.374f, newZ + 0.625f), Quaternion.identity);
            bottomRightA.transform.parent = segment.transform;
            bottomRightB.transform.parent = segment.transform;
        }
    }

    //Generates four wide Rectangular Tunnels and makes them children of the new segment
    private void InstantiateStageTwo(GameObject segment, float newZ)
    {
        

        int chosenNum = random.Next(1, 6);

        //Middile Tunnel
        if(chosenNum == 1)
        {
            GameObject rectangle1 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle2 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle3 = Instantiate(rectanglePrefab, new Vector3(.3125f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle4 = Instantiate(rectanglePrefab, new Vector3(.9375f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle5 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 1.312f, newZ), Quaternion.identity);
            GameObject rectangle8 = Instantiate(rectanglePrefab, new Vector3(.9375f, 1.312f, newZ), Quaternion.identity);

            GameObject rectangle9 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle12 = Instantiate(rectanglePrefab, new Vector3(.9375f, 0.687f, newZ), Quaternion.identity);

            GameObject rectangle13 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle14 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle15 = Instantiate(rectanglePrefab, new Vector3(.3125f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle16 = Instantiate(rectanglePrefab, new Vector3(.9375f, 0.062f, newZ), Quaternion.identity);

            rectangle1.transform.parent = segment.transform;
            rectangle2.transform.parent = segment.transform;
            rectangle3.transform.parent = segment.transform;
            rectangle4.transform.parent = segment.transform;
            rectangle5.transform.parent = segment.transform;
            rectangle8.transform.parent = segment.transform;
            rectangle9.transform.parent = segment.transform;
            rectangle12.transform.parent = segment.transform;
            rectangle13.transform.parent = segment.transform;
            rectangle14.transform.parent = segment.transform;
            rectangle15.transform.parent = segment.transform;
            rectangle16.transform.parent = segment.transform;
        }

        //Top Middle
        else if (chosenNum == 2)
        {
            GameObject rectangle1 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle4 = Instantiate(rectanglePrefab, new Vector3(.9375f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle5 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 1.312f, newZ), Quaternion.identity);
            GameObject rectangle8 = Instantiate(rectanglePrefab, new Vector3(.9375f, 1.312f, newZ), Quaternion.identity);
            GameObject rectangle9 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle10 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle11 = Instantiate(rectanglePrefab, new Vector3(.3125f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle12 = Instantiate(rectanglePrefab, new Vector3(.9375f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle13 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle14 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle15 = Instantiate(rectanglePrefab, new Vector3(.3125f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle16 = Instantiate(rectanglePrefab, new Vector3(.9375f, 0.062f, newZ), Quaternion.identity);

            rectangle1.transform.parent = segment.transform;
            rectangle4.transform.parent = segment.transform;
            rectangle5.transform.parent = segment.transform;
            rectangle8.transform.parent = segment.transform;
            rectangle9.transform.parent = segment.transform;
            rectangle10.transform.parent = segment.transform;
            rectangle11.transform.parent = segment.transform;
            rectangle12.transform.parent = segment.transform;
            rectangle13.transform.parent = segment.transform;
            rectangle14.transform.parent = segment.transform;
            rectangle15.transform.parent = segment.transform;
            rectangle16.transform.parent = segment.transform;
        }

        //Left Middle
        if (chosenNum == 3)
        {
            GameObject rectangle1 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle2 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle3 = Instantiate(rectanglePrefab, new Vector3(.3125f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle4 = Instantiate(rectanglePrefab, new Vector3(.9375f, 1.937f, newZ), Quaternion.identity);

            GameObject rectangle7 = Instantiate(rectanglePrefab, new Vector3(.3125f, 1.312f, newZ), Quaternion.identity);
            GameObject rectangle8 = Instantiate(rectanglePrefab, new Vector3(.9375f, 1.312f, newZ), Quaternion.identity);

            GameObject rectangle11 = Instantiate(rectanglePrefab, new Vector3(.3125f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle12 = Instantiate(rectanglePrefab, new Vector3(.9375f, 0.687f, newZ), Quaternion.identity);

            GameObject rectangle13 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle14 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle15 = Instantiate(rectanglePrefab, new Vector3(.3125f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle16 = Instantiate(rectanglePrefab, new Vector3(.9375f, 0.062f, newZ), Quaternion.identity);

            rectangle1.transform.parent = segment.transform;
            rectangle2.transform.parent = segment.transform;
            rectangle3.transform.parent = segment.transform;
            rectangle4.transform.parent = segment.transform;
            rectangle7.transform.parent = segment.transform;
            rectangle8.transform.parent = segment.transform;
            rectangle11.transform.parent = segment.transform;
            rectangle12.transform.parent = segment.transform;
            rectangle13.transform.parent = segment.transform;
            rectangle14.transform.parent = segment.transform;
            rectangle15.transform.parent = segment.transform;
            rectangle16.transform.parent = segment.transform;
        }

        //Right Middle
        if (chosenNum == 4)
        {
            GameObject rectangle1 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle2 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle3 = Instantiate(rectanglePrefab, new Vector3(.3125f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle4 = Instantiate(rectanglePrefab, new Vector3(.9375f, 1.937f, newZ), Quaternion.identity);

            GameObject rectangle5 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 1.312f, newZ), Quaternion.identity);
            GameObject rectangle6 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 1.312f, newZ), Quaternion.identity);


            GameObject rectangle9 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle10 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 0.687f, newZ), Quaternion.identity);

            GameObject rectangle13 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle14 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle15 = Instantiate(rectanglePrefab, new Vector3(.3125f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle16 = Instantiate(rectanglePrefab, new Vector3(.9375f, 0.062f, newZ), Quaternion.identity);

            rectangle1.transform.parent = segment.transform;
            rectangle2.transform.parent = segment.transform;
            rectangle3.transform.parent = segment.transform;
            rectangle4.transform.parent = segment.transform;
            rectangle5.transform.parent = segment.transform;
            rectangle6.transform.parent = segment.transform;
            rectangle9.transform.parent = segment.transform;
            rectangle10.transform.parent = segment.transform;
            rectangle13.transform.parent = segment.transform;
            rectangle14.transform.parent = segment.transform;
            rectangle15.transform.parent = segment.transform;
            rectangle16.transform.parent = segment.transform;
        }

        if (chosenNum == 5)
        {
            GameObject rectangle1 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle2 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle3 = Instantiate(rectanglePrefab, new Vector3(.3125f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle4 = Instantiate(rectanglePrefab, new Vector3(.9375f, 1.937f, newZ), Quaternion.identity);

            GameObject rectangle5 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 1.312f, newZ), Quaternion.identity);
            GameObject rectangle6 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 1.312f, newZ), Quaternion.identity);
            GameObject rectangle7 = Instantiate(rectanglePrefab, new Vector3(.3125f, 1.312f, newZ), Quaternion.identity);
            GameObject rectangle8 = Instantiate(rectanglePrefab, new Vector3(.9375f, 1.312f, newZ), Quaternion.identity);

            GameObject rectangle9 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle12 = Instantiate(rectanglePrefab, new Vector3(.9375f, 0.687f, newZ), Quaternion.identity);

            GameObject rectangle13 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle16 = Instantiate(rectanglePrefab, new Vector3(.9375f, 0.062f, newZ), Quaternion.identity);

            rectangle1.transform.parent = segment.transform;
            rectangle2.transform.parent = segment.transform;
            rectangle3.transform.parent = segment.transform;
            rectangle4.transform.parent = segment.transform;
            rectangle5.transform.parent = segment.transform;
            rectangle6.transform.parent = segment.transform;
            rectangle7.transform.parent = segment.transform;
            rectangle8.transform.parent = segment.transform;
            rectangle9.transform.parent = segment.transform;
            rectangle12.transform.parent = segment.transform;
            rectangle13.transform.parent = segment.transform;
            rectangle16.transform.parent = segment.transform;
        }

    }

    //Generates two wide rectangular tunnels
    private void InstantiateStageThree(GameObject segment, float newZ)
    {


        int chosenNum = random.Next(1, 5);

        //Top Middle
        if (chosenNum == 1)
        {
            GameObject rectangle1 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle2 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle3 = Instantiate(rectanglePrefab, new Vector3(.3125f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle4 = Instantiate(rectanglePrefab, new Vector3(.9375f, 1.937f, newZ), Quaternion.identity);

            GameObject rectangle5 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 1.312f, newZ), Quaternion.identity);

            GameObject rectangle8 = Instantiate(rectanglePrefab, new Vector3(.9375f, 1.312f, newZ), Quaternion.identity);

            GameObject rectangle9 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle10 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle11 = Instantiate(rectanglePrefab, new Vector3(.3125f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle12 = Instantiate(rectanglePrefab, new Vector3(.9375f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle13 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle14 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle15 = Instantiate(rectanglePrefab, new Vector3(.3125f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle16 = Instantiate(rectanglePrefab, new Vector3(.9375f, 0.062f, newZ), Quaternion.identity);

            rectangle1.transform.parent = segment.transform;
            rectangle2.transform.parent = segment.transform;
            rectangle3.transform.parent = segment.transform;
            rectangle4.transform.parent = segment.transform;
            rectangle5.transform.parent = segment.transform;

            rectangle8.transform.parent = segment.transform;
            rectangle9.transform.parent = segment.transform;
            rectangle10.transform.parent = segment.transform;
            rectangle11.transform.parent = segment.transform;
            rectangle12.transform.parent = segment.transform;
            rectangle13.transform.parent = segment.transform;
            rectangle14.transform.parent = segment.transform;
            rectangle15.transform.parent = segment.transform;
            rectangle16.transform.parent = segment.transform;
        }

        //Bottom Middle
        else if (chosenNum == 2)
        {
            GameObject rectangle1 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle2 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle3 = Instantiate(rectanglePrefab, new Vector3(.3125f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle4 = Instantiate(rectanglePrefab, new Vector3(.9375f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle5 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 1.312f, newZ), Quaternion.identity);
            GameObject rectangle6 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 1.312f, newZ), Quaternion.identity);
            GameObject rectangle7 = Instantiate(rectanglePrefab, new Vector3(.3125f, 1.312f, newZ), Quaternion.identity);
            GameObject rectangle8 = Instantiate(rectanglePrefab, new Vector3(.9375f, 1.312f, newZ), Quaternion.identity);

            GameObject rectangle9 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 0.687f, newZ), Quaternion.identity);

            GameObject rectangle12 = Instantiate(rectanglePrefab, new Vector3(.9375f, 0.687f, newZ), Quaternion.identity);

            GameObject rectangle13 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle14 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle15 = Instantiate(rectanglePrefab, new Vector3(.3125f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle16 = Instantiate(rectanglePrefab, new Vector3(.9375f, 0.062f, newZ), Quaternion.identity);

            rectangle1.transform.parent = segment.transform;
            rectangle2.transform.parent = segment.transform;
            rectangle3.transform.parent = segment.transform;
            rectangle4.transform.parent = segment.transform;
            rectangle5.transform.parent = segment.transform;
            rectangle6.transform.parent = segment.transform;
            rectangle7.transform.parent = segment.transform;
            rectangle8.transform.parent = segment.transform;
            rectangle9.transform.parent = segment.transform;

            rectangle12.transform.parent = segment.transform;
            rectangle13.transform.parent = segment.transform;
            rectangle14.transform.parent = segment.transform;
            rectangle15.transform.parent = segment.transform;
            rectangle16.transform.parent = segment.transform;
        }

        //Left Gap
        if (chosenNum == 3)
        {
            GameObject rectangle1 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle2 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle3 = Instantiate(rectanglePrefab, new Vector3(.3125f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle4 = Instantiate(rectanglePrefab, new Vector3(.9375f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle5 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 1.312f, newZ), Quaternion.identity);
            GameObject rectangle7 = Instantiate(rectanglePrefab, new Vector3(.3125f, 1.312f, newZ), Quaternion.identity);
            GameObject rectangle8 = Instantiate(rectanglePrefab, new Vector3(.9375f, 1.312f, newZ), Quaternion.identity);
            GameObject rectangle9 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle11 = Instantiate(rectanglePrefab, new Vector3(.3125f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle12 = Instantiate(rectanglePrefab, new Vector3(.9375f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle13 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle14 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle15 = Instantiate(rectanglePrefab, new Vector3(.3125f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle16 = Instantiate(rectanglePrefab, new Vector3(.9375f, 0.062f, newZ), Quaternion.identity);

            rectangle1.transform.parent = segment.transform;
            rectangle2.transform.parent = segment.transform;
            rectangle3.transform.parent = segment.transform;
            rectangle4.transform.parent = segment.transform;
            rectangle5.transform.parent = segment.transform;
            rectangle7.transform.parent = segment.transform;
            rectangle8.transform.parent = segment.transform;
            rectangle9.transform.parent = segment.transform;
            rectangle11.transform.parent = segment.transform;
            rectangle12.transform.parent = segment.transform;
            rectangle13.transform.parent = segment.transform;
            rectangle14.transform.parent = segment.transform;
            rectangle15.transform.parent = segment.transform;
            rectangle16.transform.parent = segment.transform;
        }

        //Right Gap
        if (chosenNum == 4)
        {
            GameObject rectangle1 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle2 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle3 = Instantiate(rectanglePrefab, new Vector3(.3125f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle4 = Instantiate(rectanglePrefab, new Vector3(.9375f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle5 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 1.312f, newZ), Quaternion.identity);
            GameObject rectangle6 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 1.312f, newZ), Quaternion.identity);
            GameObject rectangle8 = Instantiate(rectanglePrefab, new Vector3(.9375f, 1.312f, newZ), Quaternion.identity);
            GameObject rectangle9 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle10 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle12 = Instantiate(rectanglePrefab, new Vector3(.9375f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle13 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle14 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle15 = Instantiate(rectanglePrefab, new Vector3(.3125f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle16 = Instantiate(rectanglePrefab, new Vector3(.9375f, 0.062f, newZ), Quaternion.identity);

            rectangle1.transform.parent = segment.transform;
            rectangle2.transform.parent = segment.transform;
            rectangle3.transform.parent = segment.transform;
            rectangle4.transform.parent = segment.transform;
            rectangle5.transform.parent = segment.transform;
            rectangle6.transform.parent = segment.transform;
            rectangle8.transform.parent = segment.transform;
            rectangle9.transform.parent = segment.transform;
            rectangle10.transform.parent = segment.transform;
            rectangle12.transform.parent = segment.transform;
            rectangle13.transform.parent = segment.transform;
            rectangle14.transform.parent = segment.transform;
            rectangle15.transform.parent = segment.transform;
            rectangle16.transform.parent = segment.transform;
        }
    }

    //Generates one wide rectangular tunnels
    private void InstantiateStageFive(GameObject segment, float newZ)
    {


        int chosenNum = random.Next(1, 5);

        //Top Left Single
        if (chosenNum == 1)
        {
            GameObject rectangle1 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle2 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle3 = Instantiate(rectanglePrefab, new Vector3(.3125f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle4 = Instantiate(rectanglePrefab, new Vector3(.9375f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle5 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 1.312f, newZ), Quaternion.identity);
            GameObject rectangle7 = Instantiate(rectanglePrefab, new Vector3(.3125f, 1.312f, newZ), Quaternion.identity);
            GameObject rectangle8 = Instantiate(rectanglePrefab, new Vector3(.9375f, 1.312f, newZ), Quaternion.identity);
            GameObject rectangle9 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle10 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle11 = Instantiate(rectanglePrefab, new Vector3(.3125f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle12 = Instantiate(rectanglePrefab, new Vector3(.9375f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle13 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle14 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle15 = Instantiate(rectanglePrefab, new Vector3(.3125f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle16 = Instantiate(rectanglePrefab, new Vector3(.9375f, 0.062f, newZ), Quaternion.identity);

            rectangle1.transform.parent = segment.transform;
            rectangle2.transform.parent = segment.transform;
            rectangle3.transform.parent = segment.transform;
            rectangle4.transform.parent = segment.transform;
            rectangle5.transform.parent = segment.transform;
            rectangle7.transform.parent = segment.transform;
            rectangle8.transform.parent = segment.transform;
            rectangle9.transform.parent = segment.transform;
            rectangle10.transform.parent = segment.transform;
            rectangle11.transform.parent = segment.transform;
            rectangle12.transform.parent = segment.transform;
            rectangle13.transform.parent = segment.transform;
            rectangle14.transform.parent = segment.transform;
            rectangle15.transform.parent = segment.transform;
            rectangle16.transform.parent = segment.transform;
        }

        //Top Right Single
        else if (chosenNum == 2)
        {
            GameObject rectangle1 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle2 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle3 = Instantiate(rectanglePrefab, new Vector3(.3125f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle4 = Instantiate(rectanglePrefab, new Vector3(.9375f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle5 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 1.312f, newZ), Quaternion.identity);
            GameObject rectangle6 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 1.312f, newZ), Quaternion.identity);
            GameObject rectangle8 = Instantiate(rectanglePrefab, new Vector3(.9375f, 1.312f, newZ), Quaternion.identity);
            GameObject rectangle9 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle10 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle11 = Instantiate(rectanglePrefab, new Vector3(.3125f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle12 = Instantiate(rectanglePrefab, new Vector3(.9375f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle13 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle14 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle15 = Instantiate(rectanglePrefab, new Vector3(.3125f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle16 = Instantiate(rectanglePrefab, new Vector3(.9375f, 0.062f, newZ), Quaternion.identity);

            rectangle1.transform.parent = segment.transform;
            rectangle2.transform.parent = segment.transform;
            rectangle3.transform.parent = segment.transform;
            rectangle4.transform.parent = segment.transform;
            rectangle5.transform.parent = segment.transform;
            rectangle6.transform.parent = segment.transform;
            rectangle8.transform.parent = segment.transform;
            rectangle9.transform.parent = segment.transform;
            rectangle10.transform.parent = segment.transform;
            rectangle11.transform.parent = segment.transform;
            rectangle12.transform.parent = segment.transform;
            rectangle13.transform.parent = segment.transform;
            rectangle14.transform.parent = segment.transform;
            rectangle15.transform.parent = segment.transform;
            rectangle16.transform.parent = segment.transform;
        }

        //Bottom Left Single
        else if (chosenNum == 3)
        {
            GameObject rectangle1 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle2 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle3 = Instantiate(rectanglePrefab, new Vector3(.3125f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle4 = Instantiate(rectanglePrefab, new Vector3(.9375f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle5 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 1.312f, newZ), Quaternion.identity);
            GameObject rectangle6 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 1.312f, newZ), Quaternion.identity);
            GameObject rectangle7 = Instantiate(rectanglePrefab, new Vector3(.3125f, 1.312f, newZ), Quaternion.identity);
            GameObject rectangle8 = Instantiate(rectanglePrefab, new Vector3(.9375f, 1.312f, newZ), Quaternion.identity);
            GameObject rectangle9 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle11 = Instantiate(rectanglePrefab, new Vector3(.3125f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle12 = Instantiate(rectanglePrefab, new Vector3(.9375f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle13 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle14 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle15 = Instantiate(rectanglePrefab, new Vector3(.3125f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle16 = Instantiate(rectanglePrefab, new Vector3(.9375f, 0.062f, newZ), Quaternion.identity);

            rectangle1.transform.parent = segment.transform;
            rectangle2.transform.parent = segment.transform;
            rectangle3.transform.parent = segment.transform;
            rectangle4.transform.parent = segment.transform;
            rectangle5.transform.parent = segment.transform;
            rectangle6.transform.parent = segment.transform;
            rectangle7.transform.parent = segment.transform;
            rectangle8.transform.parent = segment.transform;
            rectangle9.transform.parent = segment.transform;
            rectangle11.transform.parent = segment.transform;
            rectangle12.transform.parent = segment.transform;
            rectangle13.transform.parent = segment.transform;
            rectangle14.transform.parent = segment.transform;
            rectangle15.transform.parent = segment.transform;
            rectangle16.transform.parent = segment.transform;
        }

        //Bottom Right Single
        else if (chosenNum == 4)
        {
            GameObject rectangle1 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle2 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle3 = Instantiate(rectanglePrefab, new Vector3(.3125f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle4 = Instantiate(rectanglePrefab, new Vector3(.9375f, 1.937f, newZ), Quaternion.identity);
            GameObject rectangle5 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 1.312f, newZ), Quaternion.identity);
            GameObject rectangle6 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 1.312f, newZ), Quaternion.identity);
            GameObject rectangle7 = Instantiate(rectanglePrefab, new Vector3(.3125f, 1.312f, newZ), Quaternion.identity);
            GameObject rectangle8 = Instantiate(rectanglePrefab, new Vector3(.9375f, 1.312f, newZ), Quaternion.identity);
            GameObject rectangle9 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle10 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle12 = Instantiate(rectanglePrefab, new Vector3(.9375f, 0.687f, newZ), Quaternion.identity);
            GameObject rectangle13 = Instantiate(rectanglePrefab, new Vector3(-.9375f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle14 = Instantiate(rectanglePrefab, new Vector3(-.3125f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle15 = Instantiate(rectanglePrefab, new Vector3(.3125f, 0.062f, newZ), Quaternion.identity);
            GameObject rectangle16 = Instantiate(rectanglePrefab, new Vector3(.9375f, 0.062f, newZ), Quaternion.identity);

            rectangle1.transform.parent = segment.transform;
            rectangle2.transform.parent = segment.transform;
            rectangle3.transform.parent = segment.transform;
            rectangle4.transform.parent = segment.transform;
            rectangle5.transform.parent = segment.transform;
            rectangle6.transform.parent = segment.transform;
            rectangle7.transform.parent = segment.transform;
            rectangle8.transform.parent = segment.transform;
            rectangle9.transform.parent = segment.transform;
            rectangle10.transform.parent = segment.transform;
            rectangle12.transform.parent = segment.transform;
            rectangle13.transform.parent = segment.transform;
            rectangle14.transform.parent = segment.transform;
            rectangle15.transform.parent = segment.transform;
            rectangle16.transform.parent = segment.transform;
        }
    }

    public void DamagePlayer()
    {
        if (!isInvincible)
        {
            health--;
            effectsController.playDamage();
            if (health <= 0)
            {
                speed = 0f;
                maxSpeed = 0f;
                StartCoroutine(Death());
            }
            else
            {
                isInvincible = true;
                lastHitLocation = headset.transform.position.z;
                lastHitTime = gameTimer;
            }
        }
    }

    //Handles Player Death
    IEnumerator Death()
    {
        //yield on a new YieldInstruction that waits for 5 seconds.
        yield return new WaitForSeconds(3);

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void GainLife()
    {
        health++;
        lastHitTime = gameTimer;
        effectsController.playExtraLife();
    }
}
