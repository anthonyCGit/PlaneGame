using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlaneController : MonoBehaviour
{
    [Header("Plane Stats")]
    [Tooltip("Controls throttle ramping.")]
    public float throttleIncrement = 0.1f;
    [Tooltip("Percent Throttle to rest at.")]
    public float throttleRest = 50f;
    [Tooltip("Maximum thrust at 100% throttle.")]
    public float maxThrust = 200f;
    [Tooltip("Plane responsiveness when rolling, pitching, yawing.")]
    public float responsiveness = 10f;
    [Tooltip("Additional plane responsiveness when pitching with high G turns.")]
    public float highGResponse = 1000f;
    [Tooltip("How much lift force this plane generates as it gains speed.")]
    public float lift = 135f;
    [Tooltip("Angular drag change coefficient when inputs are smaller.")]
    public float lowInputDrag = 1.2f;
    [Tooltip("Pitch down coeffient relative to pitch up.")]
    public float pitchDownSpeed = 0.7f;
    [Tooltip("Yaw coeffient relative to general responsiveness.")]
    public float yawSpeed = 0.5f;
    [Tooltip("Side force when dashing.")]
    public float dashForce = 100f;
    [Tooltip("Seconds between double input actions.")]
    public float doubleInputTime = 0.2f;
    [Tooltip("Seconds for a dash to recharge.")]
    public float dashRechargeTime = 1f;
    [Tooltip("Maximum number of dashes.")]
    public int maxDashes = 2;

    private float throttle;             // Percent of maximum thrust currently being used.
    private float roll;                 // Tilting left to right.
    private float pitch;                // Tilting front to back.
    private float yaw;                  // "Turning" left to right.
    private float lastYawRightInput;    // Time since last yaw right input.
    private float lastYawLeftInput;     // Time since last yaw left input.
    private float initialADrag;         // Initial angular drag value.
    private float currentDashes;        // Current number of dashes.
    private bool dashRight;             // Whether or not to dash right.
    private bool dashLeft;              // Whether or not to dash left.
    private bool highG;                 // Whether high G turns are being used.

    Rigidbody rb;
    PlayerControls controls;
    [SerializeField] TextMeshProUGUI hud;
    [SerializeField] GameObject leftThruster;
    [SerializeField] GameObject rightThruster;

    // Value used to tweak responsiveness to suit plane's mass.
    private float responseModifier
    {
        get
        {
            return (rb.mass / 10f) * responsiveness;
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        controls = new PlayerControls();
        initialADrag = rb.angularDrag;
        currentDashes = maxDashes * dashRechargeTime;
    }

    private void HandleInputs()
    {
        // Set rotational values from our axis inputs.
        roll = Input.GetAxis("Roll");
        pitch = Input.GetAxis("Pitch");
        yaw = Input.GetAxis("Yaw");

        // High G pitching enabled when both throttle inputs are held.
        if (Input.GetAxis("ThrottleUp") > 0.5 && Input.GetAxis("ThrottleDown") > 0.5)
        {
            highG = true;
        } else
        {
            highG = false;
        }

        // Left and Right Double Tap timing
        if (Input.GetButtonDown("Yaw") && Input.GetAxis("Yaw") > 0)
        {
            float timeSinceLastYR = Time.time - lastYawRightInput;
            if (timeSinceLastYR <= doubleInputTime)
            {
                dashRight = true;
            }
            lastYawRightInput = Time.time;
        }

        if (Input.GetButtonDown("Yaw") && Input.GetAxis("Yaw") < 0)
        {
            float timeSinceLastYL = Time.time - lastYawLeftInput;
            if (timeSinceLastYL <= doubleInputTime)
            {
                dashLeft = true;
            }
            lastYawLeftInput = Time.time;
        }

    }

    private void Update()
    {
        HandleInputs();
        UpdateHUD();

        // Particle Scaling
        leftThruster.gameObject.transform.localScale = new Vector3(1, 1, 1) * (throttle / 100);
        rightThruster.gameObject.transform.localScale = new Vector3(1, 1, 1) * (throttle / 100);
    }

    private void FixedUpdate()
    {
        // Thurst: Forward movement
        rb.AddForce(-transform.forward * maxThrust * throttle);
        
        // Yaw: Left/Right "turning"
        rb.AddTorque(rb.transform.up * yaw * responseModifier * yawSpeed);

        // Roll: Left/Right rotation
        rb.AddTorque(rb.transform.forward * roll * responseModifier);

        UpdatePitch();
        UpdateLift();
        UpdateDash();
        RampDrag();
        UpdateThrottle();
        DashRecharge();
    }

    /// <summary>
    /// Updates the information overlay.
    /// </summary>
    private void UpdateHUD()
    {
        hud.text = "Throttle: " + throttle.ToString("F0") + "%\n"
                    + "AirSpeed: " + (rb.velocity.magnitude * 3.6f).ToString("F0") + " km/h\n"
                    + "Altitude: " + (transform.position.y).ToString("F0") + "m\n"
                    + "Dashes: " + Mathf.Floor(currentDashes / dashRechargeTime).ToString("F0");

    }

    /// <summary>
    /// Updates the pitch (up/down rotation).
    /// </summary>
    private void UpdatePitch()
    {
        Vector3 pitching;
        if (highG)
        {
            pitching = rb.transform.right * pitch * (responseModifier + highGResponse);
        }
        else
        {
            pitching = rb.transform.right * pitch * responseModifier;
        }

        if (pitch < 0)
        {
            pitching *= pitchDownSpeed;
        }
        rb.AddTorque(pitching);
    }

    /// <summary>
    /// Updates the lift force depending on angle from the horizon (angle of attack).
    /// </summary>
    private void UpdateLift()
    {
        float aoa = (Mathf.Abs(Vector3.Angle(Vector3.up, transform.up) - 90.0f)) / 90f;
        rb.AddForce(transform.up * rb.velocity.magnitude * lift * aoa);
        rb.AddForce(5 * Physics.gravity * rb.mass * Mathf.Pow(1 - aoa, 2));
    }

    /// <summary>
    /// Dashes plane to the side when prompted.
    /// </summary>
    private void UpdateDash()
    {
        if (dashRight && currentDashes >= dashRechargeTime)
        {
            rb.AddForce(-rb.transform.right * Mathf.Clamp(rb.velocity.magnitude * dashForce, maxThrust * dashForce / 24, maxThrust * dashForce / 12));
            currentDashes -= dashRechargeTime;
            currentDashes = Mathf.Clamp(currentDashes, 0, maxDashes * dashRechargeTime);
        }
        dashRight = false;

        if (dashLeft && currentDashes >= dashRechargeTime)
        {
            rb.AddForce(rb.transform.right * Mathf.Clamp(rb.velocity.magnitude * dashForce, maxThrust * dashForce / 24, maxThrust * dashForce / 12));
            currentDashes -= dashRechargeTime;
            currentDashes = Mathf.Clamp(currentDashes, 0, maxDashes * dashRechargeTime);
        }
        dashLeft = false;
    }

    /// <summary>
    /// Ramps up the angular drag when inputs are small to enable finer control.
    /// </summary>
    private void RampDrag()
    {
        if (roll < 0.5 && roll > -0.5 && pitch < 0.5 && pitch > -0.5 && yaw < 0.5 && yaw > -0.5)
        {
            rb.angularDrag *= lowInputDrag;
        }
        else
        {
            rb.angularDrag = initialADrag;
        }
        rb.angularDrag = Mathf.Clamp(rb.angularDrag, 0, 100);
    }

    /// <summary>
    /// Changes throttle amount when input.
    /// </summary>
    private void UpdateThrottle()
    {
        if (Input.GetAxis("ThrottleUp") > 0)
        {
            throttle += throttleIncrement;
        }
        if (Input.GetAxis("ThrottleDown") > 0)
        {
            throttle -= throttleIncrement;
        }
        if (Input.GetAxis("ThrottleUp") == -1 && Input.GetAxis("ThrottleDown") == -1)
        {
            if (throttle < throttleRest)
            {
                throttle += throttleIncrement;
            }
            else if (throttle > throttleRest)
            {
                throttle -= throttleIncrement;
            }
        }
        throttle = Mathf.Clamp(throttle, 0f, 100f);
    }

    /// <summary>
    /// Controls dash recharging.
    /// </summary>
    private void DashRecharge()
    {
        if (currentDashes < maxDashes * dashRechargeTime)
        {
            currentDashes += Time.deltaTime;
            currentDashes = Mathf.Clamp(currentDashes, 0, maxDashes * dashRechargeTime);
        }
    }
}
