using UnityEngine;
using System.Collections.Generic;

#pragma warning disable CS0618

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float runSpeed = 20f;
    public float acceleration = 10f;
    public float deceleration = 10f;
    public float turnPenalty = 0.5f;
    public float airborneGravityMultiplier = 1f;
    public float terminalVelocity = 50f;

    [Header("Slam Settings")]
    public bool enableSlam = true;
    public float slamSpeed = 200f;
    public float slamCooldown = 1f;
    public float doubleTapWindow = 0.3f;
    public float minAirTimeForSlam = 0.4f;
    private float lastSlamTime = -999f;
    private float lastCtrlPressTime = -999f;
    private bool ctrlWasReleased = true;
    private float airTimeForSlam;
    private bool slamDisabled;

    [Header("Slide Settings")]
    public float slideDuration = 1.2f;
    public float slideCooldown = 0.5f;
    public float slideBoost = 2f;
    public float slideTransitionSpeed = 10f;
    public float maxSlideGainSpeed = 18f;
    public float slideDecayRate = 0.7f;
    public float slideSteeringFactor = 0.15f;

    [Header("Wall Run Settings")]
    public bool enableWallRun = true;
    public float wallRunMaxSpeed = 35f;
    public float wallRunBaseDuration = 2.5f;
    public float wallRunJumpForce = 10f;
    public float wallRunDecayFactor = 0.97f;
    public float wallRunCooldown = 0.4f;
    public float wallCooldownTime = 0.6f;
    public float backwardWallRunSpeedMultiplier = 0.6f;
    public float backwardWallRunDurationMultiplier = 1.5f;
    public float backwardWallRunTimeAdd = 0.5f;
    public float directionSwitchCooldown = 0.3f;
    public float minSpeedForBackwardsWallRun = 15f;
    public float wallRunStartJumpDelay = 0.25f;
    public LayerMask wallLayer;
    public float wallCheckDistance = 1.0f;
    public float wallRunFOV = 75f;
    public float wallRunBaseBoost = 2f;
    public float minWallRunSpeed = 6f;
    public float wallRunHopForce = 5f;
    public float wallRunHopUpward = 8f;
    public float wallRunHopSideways = 3f;
    public float wallRunHopDuration = 0.2f;
    public float wallRunCoyoteTime = 0.08f;
    public bool enableDebugLogs = false;

    [Header("Horizontal Wall Run Controls")]
    public float lookUpVerticalGain = 18f;
    public float lookDownVerticalLoss = 6f;
    public float lookUpSpeedReduction = 1.2f;
    public float lookDownSpeedBonus = 1.5f;
    public AnimationCurve heightCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float maxUpwardSpeed = 8f;
    public float maxDownwardSpeed = -8f;

    [Header("Chaining & Decay")]
    public float chainPenaltyIncrease = 0.3f;
    public float slideResetChain = 0.3f;
    public float verticalResetChain = 0.2f;
    private float currentChainTension = 0f;

    [Header("Vertical Wall Run Settings")]
    public bool enableVerticalWallRun = true;
    public float verticalWallRunBaseSpeed = 8f;
    public float verticalWallRunGravity = -9f;
    public float verticalWallRunDuration = 1.2f;
    public float verticalWallRunJumpForce = 12f;
    public float verticalWallRunAngleThreshold = 45f;
    public float verticalWallRunForwardSpeed = 4f;
    public float verticalWallRunCooldown = 0.6f;
    public float verticalWallRunMaxGainSpeed = 20f;
    public float verticalWallRunSpeedBoostUp = 1.5f;
    public float verticalWallRunSpeedBoostDown = 2f;
    public float verticalWallRunDecayRate = 2f;
    public float verticalMomentumTransfer = 0.3f;
    public float wallRunJumpCooldown = 0.3f;
    public float downwardRunMultiplier = 0.4f;
    public float verticalWallJumpExtra = 2f;

    [Header("Slope Slide Settings")]
    public float slopeBoostMultiplier = 3f;
    public float maxSlopeSpeed = 35f;
    public float minSlopeAngle = 5f;
    public float slopeSlideGravity = 30f;
    public float slopeSlideFriction = 0.95f;
    public float slopeAttachForce = 8f;
    public float uphillSlowdown = 20f;
    public float slopeStickForce = 15f;
    public float uphillReverseThreshold = 1.5f;

    [Header("Camera Settings")]
    public Camera playerCamera;
    public float normalFOV = 60f;
    public float slideFOV = 75f;
    public float fovTransitionSpeed = 8f;

    [Header("Gravity")]
    public float gravity = -9.81f;
    public AnimationCurve fallGravityCurve = AnimationCurve.Linear(0, 0.1f, 2, 1f);
    public float maxFallSpeed = -50f;

    [Header("Jump Settings")]
    public float jumpHeight = 1.2f;

    // Core components
    private CharacterController controller;
    private Transform cameraTransform;

    // Movement state
    private Vector3 velocity;
    private Vector3 moveDirection;
    private Vector3 targetMoveDirection;
    private float currentSpeed;
    private bool isRunning;

    // Slide state
    private bool isSliding;
    private float slideTimer;
    private float slideCooldownTimer;
    private Vector3 slideDirection;
    private int slideCount;
    private float lastSlideTime;
    private bool wasSlidingOnSlope;
    private Vector3 slideForwardDir;
    private Vector3 slideRightDir;
    private bool isUphillSliding;

    // Height/scale adjustments
    private float originalHeight;
    private float originalCenterY;
    private float originalCameraHeight;
    private float currentSlideHeight;
    private float currentCameraHeight;
    private float currentFOV;

    // Momentum system
    private bool hasMomentum;
    private float momentumSpeed;
    private Vector3 momentumDirection;
    private bool wasJumpCancelled;

    // Wall run system
    private bool isWallRunning;
    private bool isVerticalWallRun;
    private float wallRunTimer;
    private float wallRunCooldownTimer;
    private float verticalWallRunCooldownTimer;
    private float lastVerticalWallRunTime;
    private float lastWallRunJumpTime;
    private float lastWallRunEndTime;
    private float wallClingTimer;
    private Vector3 wallNormal;
    private Vector3 wallRunDirection;
    private Vector3 wallUpDirection;
    private bool wallRunSide;
    private bool hasWallRunBoosted;
    private Vector3 lastWallRunForwardDir;
    private Vector3 lastWallNormal;
    private Vector3 lastWallRunDir;
    private int currentWallID;
    private float wallHopTimer;
    private float wallRunStartTime;

    // Backwards wallrun transition
    private bool canBackwardsWallrun;
    private float backwardsWallrunWindow = 0.5f;
    private Vector3 previousWallNormal;
    private Vector3 previousWallPosition;
    private float previousWallRunEndTime;
    private bool wasForwardWallrun;
    private Collider previousWallCollider;      // stores collider of forward wallrun wall

    // Vertical to horizontal chaining
    private bool canHorizontalAfterVertical;
    private float verticalToHorizontalWindow = 0.4f;
    private float verticalEndTime;

    // Vertical motion during wall runs
    private float verticalVelocity;
    private float speedRampTimer;
    private float targetWallRunSpeed;
    private float targetUpwardVelocity;
    private float upwardBoostRampTimer;

    // Direction switching
    private float lastDirectionSwitchTime = -999f;

    // Wall usage tracking
    private HashSet<int> usedWalls = new HashSet<int>();
    private Dictionary<int, float> wallCooldowns = new Dictionary<int, float>();

    // Debug throttling
    private float lastDebugLogTime;
    private float debugLogInterval = 0.1f;
    private Dictionary<string, int> messageCounts = new Dictionary<string, int>();
    private Dictionary<string, float> messageLastTime = new Dictionary<string, float>();
    private const int maxMessageRepeats = 3;
    private const float messageThrottleWindow = 1.0f;

    // Air time tracking for gravity curve
    private float airTime;
    private bool wasGroundedLastFrame;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        cameraTransform = playerCamera != null ? playerCamera.transform : GetComponentInChildren<Camera>().transform;

        originalHeight = controller.height;
        originalCenterY = controller.center.y;
        originalCameraHeight = cameraTransform.localPosition.y;

        currentFOV = normalFOV;
        playerCamera.fieldOfView = normalFOV;
        currentCameraHeight = originalCameraHeight;
        currentSlideHeight = originalHeight;

        moveDirection = Vector3.zero;
        targetMoveDirection = Vector3.zero;
        slideCount = 0;
        wasGroundedLastFrame = controller.isGrounded;
    }

    void Update()
    {
        float delta = Time.deltaTime;

        // Timers
        if (slideCooldownTimer > 0) slideCooldownTimer -= delta;
        if (wallRunCooldownTimer > 0) wallRunCooldownTimer -= delta;
        if (verticalWallRunCooldownTimer > 0) verticalWallRunCooldownTimer -= delta;
        if (wallHopTimer > 0) wallHopTimer -= delta;
        if (speedRampTimer > 0) speedRampTimer -= delta;
        if (upwardBoostRampTimer > 0) upwardBoostRampTimer -= delta;
        if (currentChainTension > 0) currentChainTension = Mathf.Max(0, currentChainTension - delta * 0.8f);

        bool isGrounded = controller.isGrounded;

        if (isGrounded != wasGroundedLastFrame)
        {
            SmartLog(isGrounded ? "Landed on ground" : "Left ground");
            wasGroundedLastFrame = isGrounded;
        }

        if (!isGrounded && !isWallRunning)
        {
            airTime += delta;
            airTimeForSlam += delta;
        }
        else if (isGrounded)
        {
            airTime = 0f;
            airTimeForSlam = 0f;
        }

        if (isGrounded)
        {
            if (isWallRunning) EndWallRun();
            if (usedWalls.Count > 0)
            {
                usedWalls.Clear();
                SmartLog("Used walls cleared (touched ground).");
            }
            slamDisabled = false;
            canBackwardsWallrun = false;
            canHorizontalAfterVertical = false;
            wasSlidingOnSlope = false;
            previousWallCollider = null;
        }

        if (isGrounded && Time.time - lastSlideTime > 1f) slideCount = 0;

        if (enableWallRun && !isGrounded && !isSliding && !isWallRunning && wallRunCooldownTimer <= 0)
            CheckForWallRun();

        if (isWallRunning) UpdateWallRun();

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
            if (hasMomentum && !isSliding && !wasJumpCancelled)
            {
                if (momentumSpeed <= walkSpeed)
                {
                    hasMomentum = false;
                    momentumSpeed = 0;
                    lastWallRunForwardDir = Vector3.zero;
                }
                else if (enableDebugLogs && Time.time - lastDebugLogTime >= debugLogInterval)
                {
                    SmartLog($"Momentum preserved: {momentumSpeed:F1}");
                }
            }
            wasJumpCancelled = false;
            hasWallRunBoosted = false;
        }

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        bool hasMovementInput = (horizontal != 0 || vertical != 0);
        bool slideInput = Input.GetKey(KeyCode.LeftControl);
        bool jumpInput = Input.GetButtonDown("Jump");

        // Slam double-tap with release requirement
        if (enableSlam && !isGrounded && !isWallRunning && !isSliding && !slamDisabled)
        {
            if (slideInput)
            {
                if (ctrlWasReleased && Time.time - lastCtrlPressTime <= doubleTapWindow && Time.time - lastSlamTime > slamCooldown)
                {
                    if (airTimeForSlam >= minAirTimeForSlam)
                    {
                        velocity.y = -slamSpeed;
                        lastSlamTime = Time.time;
                        slamDisabled = true;
                        SmartLog($"SLAM! Speed: {slamSpeed} m/s down (airtime: {airTimeForSlam:F2}s)");
                    }
                    else
                    {
                        SmartLog($"Slam blocked - need more air time (current: {airTimeForSlam:F2}s)");
                    }
                    lastCtrlPressTime = Time.time;
                    ctrlWasReleased = false;
                }
                else
                {
                    lastCtrlPressTime = Time.time;
                    ctrlWasReleased = false;
                }
            }
            else
            {
                ctrlWasReleased = true;
            }
        }
        else
        {
            if (!slideInput) ctrlWasReleased = true;
        }

        // Slide initiation
        if (!isSliding && slideInput && isGrounded && slideCooldownTimer <= 0)
        {
            RaycastHit groundHit;
            bool onSlope = false;
            float slopeAngle = 0f;
            if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out groundHit, 2f))
            {
                slopeAngle = Vector3.Angle(groundHit.normal, Vector3.up);
                onSlope = slopeAngle > minSlopeAngle;
            }

            if (lastWallRunForwardDir != Vector3.zero && Time.time - lastWallRunEndTime <= 0.5f)
            {
                slideDirection = lastWallRunForwardDir;
                lastWallRunForwardDir = Vector3.zero;
                SmartLog("Slide using stored wall direction");
            }
            else if (cameraTransform != null)
            {
                Vector3 forward = cameraTransform.forward;
                Vector3 right = cameraTransform.right;
                forward.y = 0; right.y = 0;
                forward.Normalize(); right.Normalize();
                slideDirection = (forward * vertical) + (right * horizontal);
                if (slideDirection.magnitude < 0.1f) slideDirection = forward;
                slideDirection.Normalize();
            }
            StartSlide(slideDirection, onSlope, slopeAngle);
        }

        if (isSliding)
        {
            if (!slideInput && !wasSlidingOnSlope)
            {
                EndSlide();
            }
            else if (jumpInput)
            {
                CancelSlideWithMomentum();
                wasJumpCancelled = true;
            }
        }

        // Backwards wallrun (S+Jump)
        if (canBackwardsWallrun && !isWallRunning && !isGrounded && Time.time - previousWallRunEndTime <= backwardsWallrunWindow)
        {
            if (vertical < 0 && jumpInput)
            {
                SmartLog($"Attempting backwards wallrun (window: {Time.time - previousWallRunEndTime:F2}s)");
                CheckForWallRun();
            }
        }

        // Vertical to horizontal chaining
        if (canHorizontalAfterVertical && !isWallRunning && !isGrounded && Time.time - verticalEndTime <= verticalToHorizontalWindow)
        {
            if (hasMovementInput && !isVerticalWallRun)
            {
                SmartLog("Attempting horizontal wallrun after vertical");
                CheckForWallRun();
            }
        }

        // Wall jumps
        if (jumpInput && !isWallRunning && !isGrounded && Time.time - lastWallRunEndTime <= wallRunCoyoteTime && lastWallRunEndTime > 0)
        {
            SmartLog("COYOTE WALL JUMP!");
            CoyoteWallJump();
            lastWallRunEndTime = 0;
        }
        else if (isWallRunning && jumpInput && Time.time - lastWallRunJumpTime >= wallRunJumpCooldown)
        {
            if (Time.time - wallRunStartTime >= wallRunStartJumpDelay)
            {
                SmartLog("WALL RUN JUMP!");
                lastWallRunJumpTime = Time.time;
                WallRunJump();
            }
            else
            {
                SmartLog("Wall jump blocked - too early");
            }
        }

        // Slide height transition
        if (isSliding && (isGrounded || wasSlidingOnSlope))
        {
            UpdateSlide(horizontal, vertical);
            float targetHeight = originalHeight * 0.6f;
            float targetCameraHeight = originalCameraHeight * 0.6f;
            currentSlideHeight = Mathf.Lerp(currentSlideHeight, targetHeight, delta * slideTransitionSpeed);
            currentCameraHeight = Mathf.Lerp(currentCameraHeight, targetCameraHeight, delta * slideTransitionSpeed);
            controller.height = currentSlideHeight;
            controller.center = new Vector3(0, currentSlideHeight * 0.5f, 0);
            cameraTransform.localPosition = new Vector3(cameraTransform.localPosition.x, currentCameraHeight, cameraTransform.localPosition.z);
        }
        else
        {
            if (isSliding && !isGrounded && !wasSlidingOnSlope)
                EndSlide();
            currentSlideHeight = Mathf.Lerp(currentSlideHeight, originalHeight, delta * slideTransitionSpeed);
            currentCameraHeight = Mathf.Lerp(currentCameraHeight, originalCameraHeight, delta * slideTransitionSpeed);
            controller.height = currentSlideHeight;
            controller.center = new Vector3(0, currentSlideHeight * 0.5f, 0);
            cameraTransform.localPosition = new Vector3(cameraTransform.localPosition.x, currentCameraHeight, cameraTransform.localPosition.z);
        }

        // Ground movement
        if (!isWallRunning)
        {
            if (hasMomentum && momentumSpeed > walkSpeed && !isSliding)
            {
                currentSpeed = momentumSpeed;
                if (isGrounded) momentumSpeed = Mathf.Max(momentumSpeed - (delta * deceleration * 2.5f), walkSpeed);
                currentSpeed = momentumSpeed;

                if (hasMovementInput)
                {
                    Vector3 forward = cameraTransform.forward;
                    Vector3 right = cameraTransform.right;
                    forward.y = 0; right.y = 0;
                    forward.Normalize(); right.Normalize();
                    Vector3 inputDir = (forward * vertical) + (right * horizontal);
                    if (inputDir.magnitude > 0.1f)
                        momentumDirection = Vector3.Lerp(momentumDirection, inputDir.normalized, delta * 6f);
                }
                controller.Move(momentumDirection * currentSpeed * delta);
                velocity.x = momentumDirection.x * currentSpeed;
                velocity.z = momentumDirection.z * currentSpeed;

                if (momentumSpeed <= walkSpeed && isGrounded) hasMomentum = false;
            }
            else if (hasMovementInput)
            {
                isRunning = Input.GetKey(KeyCode.LeftShift) && hasMovementInput;
                float targetSpeed = isRunning ? runSpeed : walkSpeed;

                Vector3 forward = cameraTransform.forward;
                Vector3 right = cameraTransform.right;
                forward.y = 0; right.y = 0;
                forward.Normalize(); right.Normalize();
                targetMoveDirection = (forward * vertical) + (right * horizontal);
                targetMoveDirection.Normalize();

                moveDirection = Vector3.Lerp(moveDirection, targetMoveDirection, delta * acceleration);
                currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, delta * acceleration);
                controller.Move(moveDirection * currentSpeed * delta);
                velocity.x = moveDirection.x * currentSpeed;
                velocity.z = moveDirection.z * currentSpeed;
            }
            else
            {
                currentSpeed = Mathf.Lerp(currentSpeed, 0, delta * deceleration);
                moveDirection = Vector3.Lerp(moveDirection, Vector3.zero, delta * deceleration);
                controller.Move(moveDirection * currentSpeed * delta);
                velocity.x = moveDirection.x * currentSpeed;
                velocity.z = moveDirection.z * currentSpeed;
            }
        }

        // FOV
        float targetFOV = normalFOV;
        if (isSliding) targetFOV = slideFOV;
        if (isWallRunning) targetFOV = wallRunFOV;
        currentFOV = Mathf.Lerp(currentFOV, targetFOV, delta * fovTransitionSpeed);
        playerCamera.fieldOfView = currentFOV;

        // Jump
        if (jumpInput && isGrounded && !isSliding && !isWallRunning)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (hasMomentum && momentumSpeed > walkSpeed)
                SmartLog($"Jump with momentum: {momentumSpeed:F1}");
        }

        // Gravity
        if (!isWallRunning)
        {
            if (!isGrounded)
            {
                float curveValue = fallGravityCurve.Evaluate(Mathf.Min(airTime, 2f));
                float currentGravity = gravity * curveValue * airborneGravityMultiplier;
                velocity.y += currentGravity * delta;
                velocity.y = Mathf.Max(velocity.y, maxFallSpeed);
            }
            else
            {
                velocity.y += gravity * delta;
            }
        }

        controller.Move(velocity * delta);
    }

    void CheckForWallRun()
    {
        float vertical = Input.GetAxisRaw("Vertical");
        if (vertical == 0 && !canBackwardsWallrun && !canHorizontalAfterVertical) return;

        float currentHorizontalSpeed = new Vector3(velocity.x, 0, velocity.z).magnitude;
        float cameraAngle = Vector3.Angle(cameraTransform.forward, Vector3.up);
        bool lookingUp = cameraAngle < verticalWallRunAngleThreshold;

        Collider[] nearbyWalls = Physics.OverlapSphere(transform.position, wallCheckDistance, wallLayer);
        if (nearbyWalls.Length == 0) return;

        Vector3 horizontalForward = cameraTransform.forward;
        horizontalForward.y = 0;
        horizontalForward.Normalize();

        bool forwardWall = Physics.SphereCast(transform.position, 0.3f, horizontalForward, out RaycastHit forwardHit, wallCheckDistance, wallLayer);
        bool higherWall = Physics.SphereCast(transform.position + Vector3.up * 0.5f, 0.3f, horizontalForward, out RaycastHit higherHit, wallCheckDistance, wallLayer);
        bool lowerWall = Physics.SphereCast(transform.position - Vector3.up * 0.3f, 0.3f, horizontalForward, out RaycastHit lowerHit, wallCheckDistance, wallLayer);

        Vector3 leftDir = -cameraTransform.right; leftDir.y = 0; leftDir.Normalize();
        Vector3 rightDir = cameraTransform.right; rightDir.y = 0; rightDir.Normalize();
        bool leftWall = Physics.SphereCast(transform.position, 0.3f, leftDir, out RaycastHit leftHit, wallCheckDistance, wallLayer);
        bool rightWall = Physics.SphereCast(transform.position, 0.3f, rightDir, out RaycastHit rightHit, wallCheckDistance, wallLayer);

        if (enableDebugLogs)
        {
            if (forwardWall) SmartLog($"Forward wall at {forwardHit.distance:F2}m, ID: {forwardHit.collider.GetInstanceID()}");
            if (leftWall) SmartLog($"Left wall at {leftHit.distance:F2}m, ID: {leftHit.collider.GetInstanceID()}");
            if (rightWall) SmartLog($"Right wall at {rightHit.distance:F2}m, ID: {rightHit.collider.GetInstanceID()}");
        }

        // Vertical wall run
        if (enableVerticalWallRun && lookingUp && (forwardWall || higherWall || lowerWall))
        {
            RaycastHit hit = forwardWall ? forwardHit : (higherWall ? higherHit : lowerHit);
            int wallID = hit.collider.GetInstanceID();
            if (!usedWalls.Contains(wallID) && IsWallOffCooldown(wallID))
            {
                Vector3 cameraDir = cameraTransform.forward;
                float dot = Vector3.Dot(cameraDir, hit.normal);
                bool isLookingAlongWall = Mathf.Abs(dot) < 0.3f;
                StartVerticalWallRun(hit.normal, wallID, isLookingAlongWall);
            }
            return;
        }

        if (currentHorizontalSpeed < 3f)
        {
            SmartLog($"Wallrun blocked - speed too low ({currentHorizontalSpeed:F1})");
            return;
        }

        // Horizontal wallrun after vertical (weaker)
        if (canHorizontalAfterVertical)
        {
            if (leftWall || rightWall || forwardWall)
            {
                RaycastHit hit = leftWall ? leftHit : (rightWall ? rightHit : forwardHit);
                int wallID = hit.collider.GetInstanceID();
                if (!usedWalls.Contains(wallID) && IsWallOffCooldown(wallID))
                {
                    SmartLog("Starting horizontal wallrun after vertical (weaker)");
                    StartWallRun(hit.normal, leftWall, wallID, false, true);
                    canHorizontalAfterVertical = false;
                    return;
                }
            }
        }

        // BACKWARDS WALLRUN DETECTION (IMPROVED)
        if (canBackwardsWallrun && vertical < 0)
        {
            // First, check if we are still near the same wall we ran forward on
            if (previousWallCollider != null)
            {
                Vector3 closestPoint = previousWallCollider.ClosestPoint(transform.position);
                float distanceToWall = Vector3.Distance(transform.position, closestPoint);

                if (distanceToWall <= wallCheckDistance + 0.5f)
                {
                    Vector3 wallNormal = (transform.position - closestPoint).normalized;
                    int wallID = previousWallCollider.GetInstanceID();

                    if (!usedWalls.Contains(wallID) && IsWallOffCooldown(wallID))
                    {
                        SmartLog($"Starting backwards wallrun on same wall (distance: {distanceToWall:F2}m)");
                        StartWallRun(wallNormal, false, wallID, true);
                        canBackwardsWallrun = false;
                        return;
                    }
                }
            }

            // Fallback: sphere cast behind player
            Vector3 backDir = -cameraTransform.forward;
            backDir.y = 0;
            backDir.Normalize();

            if (Physics.SphereCast(transform.position, 0.5f, backDir, out RaycastHit backHit, wallCheckDistance + 0.5f, wallLayer))
            {
                int wallID = backHit.collider.GetInstanceID();
                if (!usedWalls.Contains(wallID) && IsWallOffCooldown(wallID))
                {
                    SmartLog($"Starting backwards wallrun via camera behind - Wall ID: {wallID}");
                    StartWallRun(backHit.normal, false, wallID, true);
                    canBackwardsWallrun = false;
                    return;
                }
            }

            // Last resort: check any nearby wall that's behind relative to velocity
            Vector3 velocityDir = velocity.normalized;
            foreach (var col in nearbyWalls)
            {
                Vector3 dirToWall = (col.ClosestPoint(transform.position) - transform.position).normalized;
                if (Vector3.Dot(dirToWall, velocityDir) < -0.5f)
                {
                    int wallID = col.GetInstanceID();
                    if (!usedWalls.Contains(wallID) && IsWallOffCooldown(wallID))
                    {
                        SmartLog($"Starting backwards wallrun via velocity behind - Wall ID: {wallID}");
                        StartWallRun((transform.position - col.ClosestPoint(transform.position)).normalized, false, wallID, true);
                        canBackwardsWallrun = false;
                        return;
                    }
                }
            }

            SmartLog("Backwards wallrun failed - no suitable wall behind");
        }

        bool isBackwardsAttempt = vertical < 0;
        if (isBackwardsAttempt && !canBackwardsWallrun)
        {
            SmartLog("Backwards wall run requires prior forward wallrun");
            return;
        }

        // Regular horizontal wallrun
        if (leftWall && !rightWall)
        {
            int wallID = leftHit.collider.GetInstanceID();
            if (!usedWalls.Contains(wallID) && IsWallOffCooldown(wallID))
                StartWallRun(leftHit.normal, true, wallID);
        }
        else if (rightWall && !leftWall)
        {
            int wallID = rightHit.collider.GetInstanceID();
            if (!usedWalls.Contains(wallID) && IsWallOffCooldown(wallID))
                StartWallRun(rightHit.normal, false, wallID);
        }
        else if (forwardWall && !leftWall && !rightWall)
        {
            int wallID = forwardHit.collider.GetInstanceID();
            if (!usedWalls.Contains(wallID) && IsWallOffCooldown(wallID))
                StartWallRun(forwardHit.normal, false, wallID);
        }
    }

    bool IsWallOffCooldown(int wallID)
    {
        if (wallCooldowns.TryGetValue(wallID, out float nextAvailable))
        {
            if (Time.time < nextAvailable)
            {
                SmartLog($"Wall {wallID} on cooldown for {nextAvailable - Time.time:F2}s");
                return false;
            }
            else
            {
                wallCooldowns.Remove(wallID);
            }
        }
        return true;
    }

    void StartVerticalWallRun(Vector3 normal, int wallID, bool lookingAlongWall)
    {
        if (verticalWallRunCooldownTimer > 0) return;
        if (Time.time - lastVerticalWallRunTime < verticalWallRunCooldown && lastVerticalWallRunTime > 0) return;

        float totalSpeed = new Vector3(velocity.x, velocity.y, velocity.z).magnitude;
        bool alreadyFast = totalSpeed > verticalWallRunMaxGainSpeed;

        usedWalls.Add(wallID);
        wallCooldowns[wallID] = Time.time + wallCooldownTime;

        isWallRunning = true;
        isVerticalWallRun = true;
        wallNormal = normal;
        wallRunTimer = verticalWallRunDuration;
        hasWallRunBoosted = false;
        wallRunStartTime = Time.time;
        canBackwardsWallrun = false;
        wasForwardWallrun = false;
        previousWallCollider = null;

        float multiplier = lookingAlongWall ? downwardRunMultiplier : 1f;
        float runBaseSpeed = verticalWallRunBaseSpeed * multiplier;
        float runForwardSpeed = verticalWallRunForwardSpeed * multiplier;

        wallUpDirection = Vector3.ProjectOnPlane(Vector3.up, normal).normalized;
        Vector3 wallForward = Vector3.ProjectOnPlane(cameraTransform.forward, normal).normalized;
        wallRunDirection = (wallUpDirection * runBaseSpeed + wallForward * runForwardSpeed).normalized;

        if (!hasWallRunBoosted)
        {
            hasWallRunBoosted = true;
            float targetSpeed;
            if (alreadyFast)
                targetSpeed = Mathf.Min(totalSpeed, verticalWallRunMaxGainSpeed);
            else if (totalSpeed < runBaseSpeed)
                targetSpeed = runBaseSpeed;
            else
                targetSpeed = Mathf.Min(totalSpeed + verticalWallRunSpeedBoostDown, verticalWallRunMaxGainSpeed);

            currentSpeed = targetSpeed;
            SmartLog($"VERTICAL WALL RUN - Wall ID: {wallID}, Speed: {totalSpeed:F1} → {currentSpeed:F1} ({(lookingAlongWall ? "downward" : "upward")})");
        }
        else currentSpeed = totalSpeed;

        velocity = wallRunDirection * currentSpeed;
        hasMomentum = true;
        momentumSpeed = currentSpeed;
        momentumDirection = wallRunDirection;
        lastVerticalWallRunTime = Time.time;
        verticalWallRunCooldownTimer = verticalWallRunCooldown;

        currentChainTension = Mathf.Max(0, currentChainTension - verticalResetChain);
    }

    void StartWallRun(Vector3 normal, bool isLeft, int wallID, bool forceBackwards = false, bool afterVertical = false)
    {
        usedWalls.Add(wallID);
        wallCooldowns[wallID] = Time.time + wallCooldownTime;

        isWallRunning = true;
        isVerticalWallRun = false;
        wallRunSide = isLeft;
        wallNormal = normal;
        hasWallRunBoosted = false;
        wallRunStartTime = Time.time;

        Vector3 wallForward = Vector3.Cross(normal, Vector3.up);
        if (Vector3.Dot(wallForward, cameraTransform.forward) < 0) wallForward = -wallForward;
        wallRunDirection = wallForward.normalized;

        bool isBackwards = forceBackwards || Input.GetAxisRaw("Vertical") < 0;
        if (!isBackwards)
        {
            canBackwardsWallrun = true;
            previousWallNormal = normal;
            previousWallPosition = GetClosestPointOnWall();
            previousWallRunEndTime = Time.time;
            wasForwardWallrun = true;

            // Store collider for backwards detection
            Collider[] cols = Physics.OverlapSphere(previousWallPosition, 0.5f, wallLayer);
            if (cols.Length > 0) previousWallCollider = cols[0];

            SmartLog($"Forward wallrun started - can backwards within {backwardsWallrunWindow}s");
        }
        else
        {
            canBackwardsWallrun = false;
            wallRunDirection = -wallRunDirection;
            wasForwardWallrun = false;
            previousWallCollider = null;
            SmartLog($"Backwards wallrun started - direction: {wallRunDirection}");
        }

        lastWallRunForwardDir = wallRunDirection;

        float currentHorizontalSpeed = new Vector3(velocity.x, 0, velocity.z).magnitude;
        float chainPenalty = 1f + currentChainTension;
        wallRunCooldownTimer = wallRunCooldown * chainPenalty;

        float speedMultiplier = afterVertical ? 0.7f : 1f;
        float durationMultiplier = afterVertical ? 0.7f : 1f;

        float cameraAngle = Vector3.Angle(cameraTransform.forward, Vector3.up);
        float speedModifier = 0f;
        if (cameraAngle < 45f)
        {
            float upFactor = 1f - (cameraAngle / 45f);
            speedModifier = -lookUpSpeedReduction * upFactor;
        }
        else
        {
            float downFactor = (cameraAngle - 45f) / 45f;
            speedModifier = lookDownSpeedBonus * downFactor;
        }
        float totalBoost = Mathf.Clamp(wallRunBaseBoost + speedModifier, 1f, 3f);
        targetWallRunSpeed = Mathf.Min(currentHorizontalSpeed + totalBoost, wallRunMaxSpeed) * speedMultiplier;
        targetWallRunSpeed = Mathf.Max(targetWallRunSpeed, minWallRunSpeed);

        if (isBackwards)
        {
            targetWallRunSpeed *= backwardWallRunSpeedMultiplier;
            wallRunTimer = wallRunBaseDuration * backwardWallRunDurationMultiplier * durationMultiplier;
            targetWallRunSpeed = Mathf.Min(targetWallRunSpeed + 2f, wallRunMaxSpeed);
            SmartLog($"Backwards wall run - speed: {targetWallRunSpeed:F1}, duration: {wallRunTimer:F1}s");
        }
        else
        {
            wallRunTimer = wallRunBaseDuration * durationMultiplier;
        }

        speedRampTimer = 0.2f;

        float upBoostFactor = Mathf.Clamp01(1f - (cameraAngle / 90f));
        upBoostFactor = Mathf.Pow(upBoostFactor, 1.5f);
        targetUpwardVelocity = wallRunHopUpward * upBoostFactor;
        upwardBoostRampTimer = 0.15f;

        float speedScaledHop = Mathf.Lerp(wallRunHopForce * 0.3f, wallRunHopForce * 0.8f, currentHorizontalSpeed / wallRunMaxSpeed);
        Vector3 kick = (wallRunDirection * speedScaledHop) + (wallNormal * wallRunHopSideways * 0.3f);
        velocity += kick;

        wallHopTimer = wallRunHopDuration;

        hasMomentum = true;
        momentumSpeed = currentHorizontalSpeed;
        momentumDirection = wallRunDirection;

        currentChainTension += chainPenaltyIncrease;

        SmartLog($"HORIZONTAL WALL RUN - Wall ID: {wallID}, Upward boost: {targetUpwardVelocity:F1} (factor {upBoostFactor:F2})");
    }

    void StartWallRun(Vector3 normal, bool isLeft, int wallID)
    {
        StartWallRun(normal, isLeft, wallID, false, false);
    }

    void UpdateWallRun()
    {
        float delta = Time.deltaTime;
        wallRunTimer *= wallRunDecayFactor;

        if (speedRampTimer > 0)
        {
            float t = 1f - (speedRampTimer / 0.2f);
            currentSpeed = Mathf.Lerp(new Vector3(velocity.x, 0, velocity.z).magnitude, targetWallRunSpeed, t);
            speedRampTimer -= delta;
        }
        else
        {
            currentSpeed = targetWallRunSpeed;
        }

        if (!isVerticalWallRun)
        {
            float verticalInput = Input.GetAxisRaw("Vertical");
            bool wantsBackwards = verticalInput < 0;
            bool wantsForward = verticalInput > 0;
            bool isCurrentlyBackwards = Vector3.Dot(wallRunDirection, lastWallRunForwardDir) < 0;

            if (Time.time - lastDirectionSwitchTime > directionSwitchCooldown)
            {
                if ((isCurrentlyBackwards && wantsForward) || (!isCurrentlyBackwards && wantsBackwards))
                {
                    wallRunDirection = -wallRunDirection;
                    lastWallRunForwardDir = wallRunDirection;
                    lastDirectionSwitchTime = Time.time;

                    float oldSpeed = currentSpeed;
                    float newSpeed = wantsBackwards ? oldSpeed * backwardWallRunSpeedMultiplier : oldSpeed / backwardWallRunSpeedMultiplier;
                    newSpeed = Mathf.Clamp(newSpeed, minWallRunSpeed, wallRunMaxSpeed);
                    targetWallRunSpeed = newSpeed;
                    speedRampTimer = 0.2f;

                    if (wantsBackwards) wallRunTimer += backwardWallRunTimeAdd;
                    SmartLog($"DIRECTION SWITCH - new speed target: {newSpeed:F1}, timer +{backwardWallRunTimeAdd}s");
                }
            }

            if (verticalInput == 0)
            {
                ForceWallKickoff();
                return;
            }
        }
        else
        {
            if (Input.GetAxisRaw("Vertical") <= 0)
            {
                ForceWallKickoff();
                return;
            }
        }

        if (wallRunTimer <= 0.05f)
        {
            ForceWallKickoff();
            return;
        }

        if (!isVerticalWallRun)
        {
            verticalVelocity += gravity * delta;

            if (upwardBoostRampTimer > 0)
            {
                float t = 1f - (upwardBoostRampTimer / 0.15f);
                float rampedUpward = Mathf.Lerp(0, targetUpwardVelocity, t);
                verticalVelocity = Mathf.Max(verticalVelocity, rampedUpward);
                upwardBoostRampTimer -= delta;
            }

            float cameraAngle = Vector3.Angle(cameraTransform.forward, Vector3.up);
            float tCurve = Mathf.Clamp01(cameraAngle / 90f);
            float curveValue = heightCurve.Evaluate(tCurve);

            if (cameraAngle < 45f)
            {
                float upFactor = 1f - (cameraAngle / 45f);
                float upwardAccel = lookUpVerticalGain * curveValue * upFactor * delta * 0.5f;
                verticalVelocity += upwardAccel;
                if (verticalVelocity > maxUpwardSpeed) verticalVelocity = maxUpwardSpeed;
            }
            else if (cameraAngle > 45f)
            {
                float downFactor = (cameraAngle - 45f) / 45f;
                float groundDist = GetGroundDistance();
                if (groundDist > 3f)
                {
                    float downwardAccel = -lookDownVerticalLoss * curveValue * downFactor * delta * 0.5f;
                    verticalVelocity += downwardAccel;
                    if (verticalVelocity < maxDownwardSpeed) verticalVelocity = maxDownwardSpeed;
                }
            }

            controller.Move(Vector3.up * verticalVelocity * delta);
            velocity.y = verticalVelocity;

            float speedMod = 0f;
            if (cameraAngle < 45f)
            {
                float upFactor = 1f - (cameraAngle / 45f);
                speedMod = -lookUpSpeedReduction * upFactor * curveValue * 0.5f;
            }
            else if (cameraAngle > 45f)
            {
                float downFactor = (cameraAngle - 45f) / 45f;
                speedMod = lookDownSpeedBonus * downFactor * curveValue * 0.5f;
            }
            currentSpeed += speedMod * delta;
            currentSpeed = Mathf.Clamp(currentSpeed, minWallRunSpeed, wallRunMaxSpeed);
        }
        else
        {
            currentSpeed = Mathf.Max(currentSpeed - verticalWallRunDecayRate * delta, verticalWallRunBaseSpeed * 0.5f);
            wallUpDirection = Vector3.ProjectOnPlane(Vector3.up, wallNormal).normalized;
            Vector3 wallForward = Vector3.ProjectOnPlane(cameraTransform.forward, wallNormal).normalized;
            wallRunDirection = (wallUpDirection * verticalWallRunBaseSpeed + wallForward * verticalWallRunForwardSpeed).normalized;
        }

        Vector3 checkDir = isVerticalWallRun
            ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized
            : (wallRunSide ? -cameraTransform.right : cameraTransform.right);

        RaycastHit hit;
        bool stillOnWall = Physics.SphereCast(transform.position, 0.3f, checkDir, out hit, wallCheckDistance + 0.5f, wallLayer);

        if (!stillOnWall && !isVerticalWallRun)
        {
            stillOnWall = Physics.SphereCast(transform.position + Vector3.up * 0.5f, 0.3f, checkDir, out hit, wallCheckDistance + 0.5f, wallLayer) ||
                          Physics.SphereCast(transform.position - Vector3.up * 0.3f, 0.3f, checkDir, out hit, wallCheckDistance + 0.5f, wallLayer);
        }
        else if (isVerticalWallRun && !stillOnWall)
        {
            stillOnWall = Physics.SphereCast(transform.position + Vector3.up * 0.5f, 0.3f, checkDir, out hit, wallCheckDistance + 0.5f, wallLayer);
        }

        if (!stillOnWall)
        {
            if (wallClingTimer <= 0f)
                wallClingTimer = 0.1f;
            else
            {
                wallClingTimer -= delta;
                if (wallClingTimer <= 0f)
                {
                    SmartLog("Wall run ended - lost wall contact");
                    ForceWallKickoff();
                    return;
                }
            }
        }
        else
        {
            wallClingTimer = 0f;
            wallNormal = hit.normal;
        }

        if (!isVerticalWallRun)
        {
            Vector3 wallForward = Vector3.Cross(wallNormal, Vector3.up);
            if (Vector3.Dot(wallForward, cameraTransform.forward) < 0) wallForward = -wallForward;
            wallRunDirection = wallForward.normalized;
        }

        Vector3 move = wallRunDirection * currentSpeed;
        controller.Move(move * delta);
        velocity.x = move.x;
        velocity.z = move.z;

        momentumSpeed = currentSpeed;
        momentumDirection = move.normalized;
    }

    void ForceWallKickoff()
    {
        if (!isWallRunning) return;

        Vector3 contactPoint = GetClosestPointOnWall();
        Vector3 away = (transform.position - contactPoint).normalized;
        away.y = 0;
        away.Normalize();

        float cameraAngle = Vector3.Angle(cameraTransform.forward, Vector3.up);
        float verticalFactor = Mathf.Clamp01(cameraAngle / 90f);
        Vector3 launchDir = (away * (1 - verticalFactor * 0.5f) + Vector3.up * (0.3f + verticalFactor * 0.7f)).normalized;

        float speedFactor = Mathf.Clamp01(currentSpeed / wallRunMaxSpeed);
        float launchSpeed = Mathf.Lerp(5f, currentSpeed * 0.8f, speedFactor);

        velocity = launchDir * launchSpeed;
        verticalVelocity = velocity.y;

        hasMomentum = true;
        momentumSpeed = launchSpeed;
        momentumDirection = away;

        EndWallRun();
        wallRunCooldownTimer = wallRunCooldown;

        SmartLog($"FORCED WALL KICKOFF - Angle: {cameraAngle:F1}°, Speed: {launchSpeed:F1}");
    }

    void WallRunJump()
    {
        Vector3 contactPoint = GetClosestPointOnWall();
        Vector3 away = (transform.position - contactPoint).normalized;
        away.y = 0;
        away.Normalize();

        float cameraAngle = Vector3.Angle(cameraTransform.forward, Vector3.up);
        float verticalFactor = Mathf.Clamp01(cameraAngle / 90f);
        Vector3 launchDir = (away * (1 - verticalFactor * 0.4f) + Vector3.up * (0.4f + verticalFactor * 0.6f)).normalized;

        float speedFactor = Mathf.Clamp01(currentSpeed / wallRunMaxSpeed);
        float jumpBonus = Mathf.Lerp(2f, wallRunJumpForce * 0.8f, speedFactor);
        float launchSpeed = currentSpeed * 0.7f + jumpBonus;

        velocity = launchDir * launchSpeed;

        if (isVerticalWallRun)
        {
            velocity += velocity.normalized * verticalWallJumpExtra;
        }

        verticalVelocity = velocity.y;

        hasMomentum = true;
        momentumSpeed = launchSpeed * 0.8f;
        momentumDirection = away;

        EndWallRun();
        wallRunCooldownTimer = wallRunCooldown;
        SmartLog($"WALL JUMP - Angle: {cameraAngle:F1}°, Speed: {momentumSpeed:F1}");
    }

    void CoyoteWallJump()
    {
        Vector3 away = Vector3.ProjectOnPlane(-lastWallNormal, Vector3.up).normalized;
        float cameraAngle = Vector3.Angle(cameraTransform.forward, Vector3.up);
        float verticalFactor = Mathf.Clamp01(cameraAngle / 90f);
        Vector3 launchDir = (away * (1 - verticalFactor * 0.4f) + Vector3.up * (0.3f + verticalFactor * 0.7f)).normalized;
        float launchSpeed = (momentumSpeed > 0 ? momentumSpeed : currentSpeed) * 0.6f + wallRunJumpForce * 0.3f;

        velocity = launchDir * launchSpeed;
        verticalVelocity = velocity.y;

        hasMomentum = true;
        momentumSpeed = launchSpeed * 0.7f;
        momentumDirection = away;

        SmartLog($"COYOTE JUMP - Angle: {cameraAngle:F1}°, Speed: {momentumSpeed:F1}");
    }

    Vector3 GetClosestPointOnWall()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, -wallNormal, out hit, wallCheckDistance + 1f, wallLayer))
        {
            return hit.point;
        }
        return transform.position + wallNormal * 0.5f;
    }

    void EndWallRun()
    {
        lastWallNormal = wallNormal;
        lastWallRunDir = wallRunDirection;
        lastWallRunEndTime = Time.time;

        if (isVerticalWallRun)
        {
            canHorizontalAfterVertical = true;
            verticalEndTime = Time.time;
            SmartLog($"Vertical wallrun ended - horizontal possible within {verticalToHorizontalWindow}s");
        }
        else if (wasForwardWallrun)
        {
            canBackwardsWallrun = true;
            previousWallNormal = wallNormal;
            previousWallPosition = GetClosestPointOnWall();
            previousWallRunEndTime = Time.time;
            // previousWallCollider already set during StartWallRun
            SmartLog($"Wallrun ended - backwards window open for {backwardsWallrunWindow}s");
        }
        else
        {
            previousWallCollider = null;
        }

        isWallRunning = false;
        if (isVerticalWallRun) verticalWallRunCooldownTimer = verticalWallRunCooldown;
        isVerticalWallRun = false;
        hasWallRunBoosted = false;
        wallClingTimer = 0f;
        speedRampTimer = 0f;
        upwardBoostRampTimer = 0f;
        verticalVelocity = 0;
    }

    float GetGroundDistance()
    {
        RaycastHit groundHit;
        Vector3 rayStart = transform.position + Vector3.up * 0.1f;
        if (Physics.Raycast(rayStart, Vector3.down, out groundHit, 20f, ~0))
            return groundHit.distance - 0.1f;
        return 20f;
    }

    void StartSlide(Vector3 direction, bool onSlope, float slopeAngle)
    {
        if (slideCooldownTimer > 0) return;

        isSliding = true;
        slideTimer = slideDuration;
        wasJumpCancelled = false;
        wasSlidingOnSlope = onSlope;
        isUphillSliding = false;

        slideCount++;
        float slidePenalty = Mathf.Max(0.3f, 1f - (slideCount - 1) * slideDecayRate);
        float currentHorizontalSpeed = hasMomentum && momentumSpeed > currentSpeed ? momentumSpeed : new Vector3(velocity.x, 0, velocity.z).magnitude;
        if (currentHorizontalSpeed < 0.1f && velocity.magnitude > 0.1f)
            currentHorizontalSpeed = velocity.magnitude;

        float slideStartSpeed = currentHorizontalSpeed < maxSlideGainSpeed ?
            Mathf.Min(currentHorizontalSpeed + (slideBoost * slidePenalty), maxSlideGainSpeed) : currentHorizontalSpeed;

        hasMomentum = true;
        momentumSpeed = slideStartSpeed;
        momentumDirection = direction;
        slideDirection = direction;
        slideForwardDir = direction;
        slideRightDir = Vector3.Cross(Vector3.up, direction).normalized;

        velocity = new Vector3(direction.x, velocity.y, direction.z) * slideStartSpeed;
        currentSpeed = slideStartSpeed;
        moveDirection = direction;

        currentSlideHeight = controller.height;
        currentCameraHeight = cameraTransform.localPosition.y;

        currentChainTension = Mathf.Max(0, currentChainTension - slideResetChain);

        SmartLog($"SLIDE #{slideCount} - OnSlope: {onSlope}, SlopeAngle: {slopeAngle:F1}°, Speed: {currentHorizontalSpeed:F1} → {slideStartSpeed:F1}");
    }

    void UpdateSlide(float inputHorizontal, float inputVertical)
    {
        if (!controller.isGrounded && !wasSlidingOnSlope)
        {
            SmartLog("Slide ended - left ground while not on slope");
            EndSlide();
            return;
        }

        slideTimer -= Time.deltaTime;

        RaycastHit groundHit;
        bool hitGround = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out groundHit, 2f);
        if (!hitGround)
        {
            SmartLog("Slide ended - no ground beneath");
            EndSlide();
            return;
        }

        float slopeAngle = Vector3.Angle(groundHit.normal, Vector3.up);
        bool onSlope = slopeAngle > minSlopeAngle;
        wasSlidingOnSlope = onSlope;

        if (slideTimer <= 0f && !onSlope)
        {
            SmartLog($"Slide timer expired ({slideTimer:F2}), not on slope - ending");
            EndSlide();
            return;
        }

        Vector3 slopeDown = Vector3.ProjectOnPlane(Vector3.down, groundHit.normal).normalized;
        Vector3 slideDirOnPlane = Vector3.ProjectOnPlane(slideForwardDir, groundHit.normal).normalized;
        float dotWithDownhill = Vector3.Dot(slideDirOnPlane, slopeDown);
        bool slidingDownhill = onSlope && dotWithDownhill > 0.2f;
        bool slidingUphill = onSlope && dotWithDownhill < -0.2f;

        if (onSlope)
        {
            // Only forward/back input affects direction on slopes
            if (Mathf.Abs(inputVertical) > 0.1f)
            {
                Vector3 inputDir = cameraTransform.forward * inputVertical;
                inputDir.y = 0;
                inputDir.Normalize();
                slideForwardDir = Vector3.Lerp(slideForwardDir, inputDir, Time.deltaTime * slideSteeringFactor * 0.5f).normalized;
            }
        }
        else
        {
            Vector3 inputDir = (cameraTransform.forward * inputVertical) + (cameraTransform.right * inputHorizontal);
            inputDir.y = 0;
            if (inputDir.magnitude > 0.1f)
            {
                inputDir.Normalize();
                slideForwardDir = Vector3.Lerp(slideForwardDir, inputDir, Time.deltaTime * slideSteeringFactor * 4f).normalized;
            }
        }

        slideDirOnPlane = Vector3.ProjectOnPlane(slideForwardDir, groundHit.normal).normalized;

        if (onSlope)
        {
            float slopeFactor = Mathf.Clamp01((slopeAngle - minSlopeAngle) / (75f - minSlopeAngle));
            if (slidingDownhill)
            {
                float slopeGain = slopeSlideGravity * slopeFactor * Time.deltaTime;
                float extraBoost = slopeFactor * slopeBoostMultiplier * slideBoost * Time.deltaTime;
                momentumSpeed += slopeGain + extraBoost;
                momentumSpeed = Mathf.Min(momentumSpeed, maxSlopeSpeed);
                slideTimer = slideDuration;
                isUphillSliding = false;
                SmartLog($"Slope slide downhill - Angle: {slopeAngle:F1}°, Gain: {slopeGain+extraBoost:F2}, Speed: {momentumSpeed:F1}");
            }
            else if (slidingUphill)
            {
                momentumSpeed = Mathf.Max(momentumSpeed - uphillSlowdown * slopeFactor * Time.deltaTime, 0);
                isUphillSliding = true;
                SmartLog($"Slope slide uphill - slowing to {momentumSpeed:F1}");

                if (momentumSpeed < uphillReverseThreshold)
                {
                    slideForwardDir = slopeDown;
                    momentumSpeed = Mathf.Max(momentumSpeed, 2f);
                    SmartLog("Uphill slide reversed - now going downhill");
                }
            }
            else
            {
                isUphillSliding = false;
            }

            Vector3 stickForce = (groundHit.normal * -slopeStickForce) * Time.deltaTime;
            controller.Move(stickForce);
            velocity.y = Mathf.Max(velocity.y, -2f);
        }
        else
        {
            float speedDecay = deceleration * 0.6f * Time.deltaTime;
            momentumSpeed = Mathf.Max(momentumSpeed - speedDecay, walkSpeed * 0.8f);
            isUphillSliding = false;
        }

        currentSpeed = momentumSpeed;
        Vector3 move = slideDirOnPlane * currentSpeed;
        controller.Move(move * Time.deltaTime);
        velocity.x = move.x;
        velocity.z = move.z;

        slideDirection = slideForwardDir;
    }

    void CancelSlideWithMomentum()
    {
        float currentSpeedVal = new Vector3(velocity.x, 0, velocity.z).magnitude;
        velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        hasMomentum = true;
        momentumSpeed = currentSpeedVal;
        momentumDirection = slideDirection;
        isSliding = false;
        slideCooldownTimer = slideCooldown;
        lastSlideTime = Time.time;
        wasSlidingOnSlope = false;

        controller.height = originalHeight;
        controller.center = new Vector3(0, originalCenterY, 0);
        cameraTransform.localPosition = new Vector3(cameraTransform.localPosition.x, originalCameraHeight, cameraTransform.localPosition.z);
        SmartLog($"Slide cancelled! Momentum: {momentumSpeed:F1}");
    }

    void EndSlide()
    {
        isSliding = false;
        slideCooldownTimer = slideCooldown;
        lastSlideTime = Time.time;
        wasSlidingOnSlope = false;
        isUphillSliding = false;

        float currentSlideSpeed = new Vector3(velocity.x, 0, velocity.z).magnitude;
        if (currentSlideSpeed > walkSpeed)
        {
            hasMomentum = true;
            momentumSpeed = currentSlideSpeed;
            momentumDirection = slideDirection;
            currentSpeed = currentSlideSpeed;
            moveDirection = slideDirection;
        }

        controller.height = originalHeight;
        controller.center = new Vector3(0, originalCenterY, 0);
        cameraTransform.localPosition = new Vector3(cameraTransform.localPosition.x, originalCameraHeight, cameraTransform.localPosition.z);
        SmartLog($"Slide ended - final speed: {currentSlideSpeed:F1}");
    }

    void SmartLog(string message)
    {
        if (!enableDebugLogs) return;

        string key = message.Substring(0, Mathf.Min(message.Length, 50));

        if (!messageCounts.ContainsKey(key))
        {
            messageCounts[key] = 0;
            messageLastTime[key] = 0f;
        }

        if (Time.time - messageLastTime[key] > messageThrottleWindow)
            messageCounts[key] = 0;

        if (messageCounts[key] < maxMessageRepeats)
        {
            Debug.Log(message);
            messageCounts[key]++;
            messageLastTime[key] = Time.time;
            lastDebugLogTime = Time.time;
        }
    }

    public bool IsRunning() => isRunning;
    public bool IsSliding() => isSliding;
    public bool IsWallRunning() => isWallRunning;
    public bool IsVerticalWallRunning() => isVerticalWallRun;
    public float GetCurrentVelocity() => new Vector3(velocity.x, 0, velocity.z).magnitude;
}

#pragma warning restore CS0618
