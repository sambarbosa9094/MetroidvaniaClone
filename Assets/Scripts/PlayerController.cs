using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor.U2D.Path.GUIFramework;
using UnityEngine;

[RequireComponent(typeof(Controller2D))]
public class PlayerController : MonoBehaviour
{
    //note to self, add the ears to this script to be able to move them if acey ever needs to teleport

    Controller2D controller;

    [Header("Player Attributes")]
    public float moveSpeed = 1f;
    public float jumpForce = 12f;
    public float wallJumpForce = 5f;
    public int wallJumpTimer = 3;
    public float wallJumpCounter = 0;
    public float gravity = 5;
    public float jumpPeakGravityScale = 2;

    public float maxSlopeAngle = 60f;
    public float slopeSideAngle = 0f;
    public float slopeRayLength = 1;
    public float coyoteBufferLength = 5;
    public float jumpBufferLength = 5;
    public float groundSnapLength = 1f;

    [Header("Player Variables")]
    private float jump = 0;
    public bool isDashing = false;
    private bool isDashJumping = false;
    public bool hasDashed = false;
    private bool isSliding = false;
    public bool isGrounded = false;
    public bool hasJumped = false;
    public bool onSlope = false;
    public bool canWalkOnSlope = true;
    public bool isJumping = false;
    public bool isWallJumping = false;
    public float jumpBuffer = 0;
    public float coyoteBuffer = 0;
    private float coyoteY = 0;

    public float swingFirstLength = 1;
    public float swingTimer = 0;
    public float swingFirstCancel = .8f;

    public float jumpVelocity = 0;
    public float timeToJumpApex = .5f;
    public float jumpHeight = 1.25f;


    //this should be able to do sword buffering
    //used for the sword script to reference
    public bool flipX = false;

    public bool startJump = false;

    [Header("Player Components")]


    [SerializeField]
    private PhysicsMaterial2D noFriction;
    [SerializeField]
    private PhysicsMaterial2D fullFriction;

    private Vector2 inputs = Vector2.zero;
    private bool jumpKey = false;
    private bool dashKey = false;
    public Rigidbody2D rb;

    public float groundCheckSize = .25f;

    public float boxSizeX = .95f;

    private bool lastOnGround = false;

    public LayerMask ground;

    Vector2 velocity;

    float velocityXSmoothing;

    float currentFriction = 0;
    float friction = .02f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        controller = GetComponent<Controller2D>();

        gravity = -(jumpHeight * 2) / Mathf.Pow(timeToJumpApex, 2);
        jumpVelocity = Mathf.Abs(gravity) * timeToJumpApex;
    }
    //these are here to drastically cut down on the amount of GetComponent calls 
    private void Update()
    {

        //debug thing,remove later please
        if (Input.GetKeyDown(KeyCode.B))
        {

            if (Time.timeScale == .1f)
            {
                Time.timeScale = 1;
            }
            else
            {
                Time.timeScale = .1f;
            }
        }
        jumpKey = Input.GetKey(KeyCode.Z);
        dashKey = Input.GetKey(KeyCode.X);


        //allows you to cancel the wall jump cooldown thing
        if (!jumpKey)
            wallJumpCounter = 0;


        inputs = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        //add a seperate thing later on to support custom controls
        if (Input.GetKeyDown(KeyCode.Z) && hasJumped)
        {
            jumpBuffer = jumpBufferLength;
            Debug.Log("button pushed");
        }

        if (inputs.x != 0)
        {
            controller.collisions.faceDir = (int)Mathf.Sign(inputs.x);
        }



        if (controller.collisions.below)
            currentFriction = 0;
        else
            currentFriction = friction;
        float targetX = inputs.x * moveSpeed;
        //adds a slight amount of momentum to movement
        velocity.x = Mathf.SmoothDamp(velocity.x, targetX, ref velocityXSmoothing, currentFriction);



        //the player will reset to this position upon attempting a coyote jump
        isGrounded = controller.collisions.below;

        if (isGrounded)
        {
            coyoteY = transform.position.y;
        }
        if (controller.collisions.above || controller.collisions.below)
            velocity.y = 0;

        MovePlayer();




        velocity.y += gravity * Time.deltaTime;

        controller.Move(velocity * Time.deltaTime);



    }

    //countdown til the end of a dash
    public float dashTimer = 0;
    //basically just a little thing to do the dash particles
    private void MovePlayer()
    {

        Jump();

        if (isDashJumping)
        {
            if (isGrounded || isSliding)
            {
                isDashJumping = false;
                isDashing = false;
                dashTimer = 0;
            }
        }

        if (dashTimer <= 0 || !dashKey)
        {
            //allows you to extend a dash by jumping
            if (!isDashJumping)
            {
                isDashing = false;
                if (!dashKey)
                {
                    hasDashed = false;
                    dashTimer = -1;
                }
            }
        }


        if (dashKey && isGrounded && !hasDashed && dashTimer < 0)
        {
            Debug.Log("dash start baby");
            isDashing = true;
            hasDashed = true;
            dashTimer = .05f;
        }

        dashTimer -= .11f * Time.deltaTime;
        if (isDashing)
        {
            //is this needed lol

            if (isJumping)
            {
                //just to make sure you can jump from a slope
                if (controller.collisions.fallen && !isGrounded)
                {
                    controller.collisions.below = false;
                }
            }

            //done to prevent some funky shit in the air
            //basically emulating megaman zero/x's dash jump where you only move if you input
            float flip = (flipX) ? -1 : 1;
            if (isDashJumping)
            {
                velocity.x = 10 * inputs.x;
            }
            else
            {
                velocity.x = 10 * flip;
            }
        }
        else
        {
            velocity.x = inputs.x * moveSpeed;
        }
        //just some funky shit for dash jumping wait why is this split up and not a single or what the fuck

        coyoteBuffer -= 60 * Time.deltaTime;
        //if you fall off the ground like an idiot, attempt to snap back to the ground 

        if (lastOnGround != isGrounded)
        {
            if (!isGrounded)
            {

                if (!isJumping)
                {
                    coyoteBuffer = coyoteBufferLength;
                }
            }
        }
        lastOnGround = isGrounded;
    }

    private void Jump()
    {
        bool leftWall = controller.collisions.left;
        bool rightWall = controller.collisions.right;
        if (isJumping && isGrounded)
            isJumping = false;

        //lower gravity at the height of a jump but only while holding the jump key like in celeste
        if (velocity.y > -1 && velocity.y < 1 && !isGrounded && !isSliding && jumpKey && hasJumped)
        {
            //rb.gravityScale = jumpPeakGravityScale;
        }
        else
        // rb.gravityScale = gravity;


        if ((jumpKey && (isGrounded) && !hasJumped) || (((jumpBuffer > 0 && isGrounded) || coyoteBuffer > 0) && jumpKey))
        {
            if (coyoteBuffer > 0)
            {
                //this part was inspired by this tweet here https://twitter.com/dhindes/status/1238348754790440961?s=20
                transform.position = new Vector3(transform.position.x, coyoteY, transform.position.z);
            }
            coyoteBuffer = -100;
            jump = jumpForce;

            if (controller.collisions.descendingSlope)
            {
                controller.collisions.descendingSlope = false;
                controller.collisions.below = false;
            }


            //this makes it function a tad better on slopes. there's a big of spaghetti rn but everything mostly works
            velocity.y = jumpVelocity;
            startJump = true;
            hasJumped = true;
            isJumping = true;
            isGrounded = false;
        }
        //prevents holding jump
        else if (!jumpKey && (isGrounded || isSliding))
        {
            hasJumped = false;
            jump = 0;
        }
        //variable jump, cut the jump when you release the button
        if (!jumpKey && hasJumped && velocity.y > 0 && !(isGrounded || isSliding))
        {
            velocity = new Vector2(velocity.x, 0);
            jump = 0;
        }
        //if you are pushing into a wall in the air
        if (((leftWall && inputs.x < 0) || (rightWall && inputs.x > 0)) && !isGrounded)
        {
            if (velocity.y < 0)
                isSliding = true;
        }
        else
        {
            isSliding = false;
        }
        /*
        if (leftWall || rightWall)
        {
            if (jumpKey && !hasJumped)
            {
                //walljumping stuff
                if (leftWall)
                {
                    //rb.AddRelativeForce(new Vector2((1 * moveSpeed) + jumpForce + 5, jumpForce));
                    //rb.velocity = new Vector2(1 * jumpForce, jumpForce * 1.2f);
                    //rb.AddForce(new Vector2(jumpForce / 2, wallJumpForce), ForceMode2D.Force);
                }
                if (rightWall)
                {
                    //rb.AddRelativeForce(new Vector2((-1 * moveSpeed) + -jumpForce - 5, jumpForce));
                    //rb.velocity = new Vector2(-1 * jumpForce, jumpForce * 1.2f);
                    //rb.AddForce(new Vector2(-jumpForce / 2, wallJumpForce), ForceMode2D.Force);
                }
                isWallJumping = true;
                isSliding = false;
                hasJumped = true;
                wallJumpCount = .15f;
            }
            else if (!jumpKey)
            {
                hasJumped = false;
            }
        }
        wallJumpCounter -= Time.deltaTime;

        
        */



        jumpBuffer -= Time.deltaTime;
        //stupid but just for testing
        if (isSliding)
        {



            //rb.gravityScale = slideGravity;
            if (!jumpKey)
            {
                hasJumped = false;
                isWallJumping = false;
            }
        }
    }
}