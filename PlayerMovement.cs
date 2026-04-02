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

    [Header("Slide Settings")]
    public float slideDuration = 1.2f;
    public float slideCooldown = 0.5f;
    public float slideBoost = 2f;
    public float slideTransitionSpeed = 10f;
    public float maxSlideGainSpeed = 18f;
    public float slideDecayRate = 0.7f;          // each consecutive slide reduces boost by this factor

    [Header("Wall Run Settings")]
    public bool enableWallRun = true;
    public float wallRunMaxSpeed = 35f;
    public float wallRunBaseDuration = 2.5f;
    public float wallRunJumpForce = 10f;
    public float wallRunDecayFactor = 0.97f;     // exponential duration decay
    public float wallRunCooldown = 0.4f;
    public float wallCooldownTime = 0.6f;
    public LayerMask wallLayer;
    public float wallCheckDistance = 1.0f;
    public float wallRunFOV = 75f;
    public float wallRunBaseBoost = 2f;
    public float minWallRunSpeed = 6f;
    public float wallRunHopForce = 5f;           // horizontal push when kicking off
    public float wallRunHopUpward = 6f;           // upward boost on kickoff
    public float wallRunHopSideways = 3f;
    public float wallRunHopDuration = 0.2f;
    public float wallRunCoyoteTime = 0.08f;
    public bool enableDebugLogs = false;

    [Header("Horizontal Wall Run Controls")]
    public float lookUpVerticalGain = 15f;
    public float lookDownVerticalLoss = 8f;
    public float lookUpSpeedReduction = 1.2f;
    public float lookDownSpeedBonus = 1.5f;
    public AnimationCurve heightCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float maxUpwardSpeed = 5f;
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
    public float slopeBoostMultiplier = 1.5f;
    public float maxSlopeSpeed = 25f;
    public float minSlopeAngle = 5f;
    public float slopeSlideGravity = 20f;
    public float slopeSlideFriction = 0.95f;
    public float slopeAttachForce = 2f;
    public float uphillSlowdown = 20f;

    [Header("Camera Settings")]
    public Camera playerCamera;
    public float normalFOV = 60f;
    public float slideFOV = 75f;
    public float fovTransitionSpeed = 8f;

    [Header("Gravity")]
    public float gravity = -9.81f;

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

    // Vertical motion during wall runs
    private float verticalVelocity;
    private float speedRampTimer;
    private float targetWallRunSpeed;
    private float targetUpwardVelocity;
    private float upwardBoostRampTimer;

    // Wall usage tracking
    private HashSet<int> usedWalls = new HashSet<int>();
    private Dictionary<int, float> wallCooldowns = new Dictionary<int, float>();

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

        if (enableDebugLogs) Debug.Log("PlayerMovement initialized");
    }

    void Update()
    {
        // Timers
        if (slideCooldownTimer > 0) slideCooldownTimer -= Time.deltaTime;
        if (wallRunCooldownTimer > 0) wallRunCooldownTimer -= Time.deltaTime;
        if (verticalWallRunCooldownTimer > 0) verticalWallRunCooldownTimer -= Time.deltaTime;
        if (wallHopTimer > 0) wallHopTimer -= Time.deltaTime;
        if (speedRampTimer > 0) speedRampTimer -= Time.deltaTime;
        if (upwardBoostRampTimer > 0) upwardBoostRampTimer -= Time.deltaTime;
        if (currentChainTension > 0) currentChainTension = Mathf.Max(0, currentChainTension - Time.deltaTime * 0.8f);

        bool isGrounded = controller.isGrounded;

        // End wall run on ground and reset used walls
        if (isGrounded)
        {
            if (isWallRunning) EndWallRun();
            if (usedWalls.Count > 0)
            {
                usedWalls.Clear();
                if (enableDebugLogs) Debug.Log("Used walls cleared (touched ground).");
            }
        }

        // Reset slide count after being grounded
        if (isGrounded && Time.time - lastSlideTime > 1f) slideCount = 0;

        // Wall run activation
        if (enableWallRun && !isGrounded && !isSliding && !isWallRunning && wallRunCooldownTimer <= 0)
            CheckForWallRun();

        if (isWallRunning) UpdateWallRun();

        // Ground reset
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
                else if (enableDebugLogs) Debug.Log($"Momentum preserved: {momentumSpeed:F1}");
            }
            wasJumpCancelled = false;
            hasWallRunBoosted = false;
        }

        // Input
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        bool hasMovementInput = (horizontal != 0 || vertical != 0);
        bool slideInput = Input.GetKey(KeyCode.LeftControl);
        bool jumpInput = Input.GetButtonDown("Jump");

        // Slide initiation
        if (!isSliding && slideInput && isGrounded && slideCooldownTimer <= 0)
        {
            if (lastWallRunForwardDir != Vector3.zero && Time.time - lastWallRunEndTime <= 0.5f)
            {
                slideDirection = lastWallRunForwardDir;
                lastWallRunForwardDir = Vector3.zero;
                if (enableDebugLogs) Debug.Log($"Slide using stored wall direction");
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
            StartSlide(slideDirection);
        }

        if (isSliding && !slideInput) EndSlide();
        if (isSliding && jumpInput)
        {
            CancelSlideWithMomentum();
            wasJumpCancelled = true;
        }

        // Wall jumps (coyote and active)
        if (jumpInput && !isWallRunning && !isGrounded && Time.time - lastWallRunEndTime <= wallRunCoyoteTime && lastWallRunEndTime > 0)
        {
            if (enableDebugLogs) Debug.Log($"COYOTE WALL JUMP!");
            CoyoteWallJump();
            lastWallRunEndTime = 0;
        }
        else if (isWallRunning && jumpInput && Time.time - lastWallRunJumpTime >= wallRunJumpCooldown)
        {
            if (enableDebugLogs) Debug.Log("WALL RUN JUMP!");
            lastWallRunJumpTime = Time.time;
            WallRunJump();
        }

        // Slide height transition
        if (isSliding && isGrounded)
        {
            UpdateSlide();
            float targetHeight = originalHeight * 0.6f;
            float targetCameraHeight = originalCameraHeight * 0.6f;
            currentSlideHeight = Mathf.Lerp(currentSlideHeight, targetHeight, Time.deltaTime * slideTransitionSpeed);
            currentCameraHeight = Mathf.Lerp(currentCameraHeight, targetCameraHeight, Time.deltaTime * slideTransitionSpeed);
            controller.height = currentSlideHeight;
            controller.center = new Vector3(0, currentSlideHeight * 0.5f, 0);
            cameraTransform.localPosition = new Vector3(cameraTransform.localPosition.x, currentCameraHeight, cameraTransform.localPosition.z);
        }
        else
        {
            if (isSliding) EndSlide();
            currentSlideHeight = Mathf.Lerp(currentSlideHeight, originalHeight, Time.deltaTime * slideTransitionSpeed);
            currentCameraHeight = Mathf.Lerp(currentCameraHeight, originalCameraHeight, Time.deltaTime * slideTransitionSpeed);
            controller.height = currentSlideHeight;
            controller.center = new Vector3(0, currentSlideHeight * 0.5f, 0);
            cameraTransform.localPosition = new Vector3(cameraTransform.localPosition.x, currentCameraHeight, cameraTransform.localPosition.z);
        }

        // AIR CONTROL REMOVED – no steering while airborne (skill based)

        // Ground movement
        if (!isWallRunning)
        {
            if (hasMomentum && momentumSpeed > walkSpeed && !isSliding)
            {
                currentSpeed = momentumSpeed;
                if (isGrounded) momentumSpeed = Mathf.Max(momentumSpeed - (Time.deltaTime * deceleration * 2.5f), walkSpeed);
                currentSpeed = momentumSpeed;

                if (hasMovementInput)
                {
                    Vector3 forward = cameraTransform.forward;
                    Vector3 right = cameraTransform.right;
                    forward.y = 0; right.y = 0;
                    forward.Normalize(); right.Normalize();
                    Vector3 inputDir = (forward * vertical) + (right * horizontal);
                    if (inputDir.magnitude > 0.1f)
                        momentumDirection = Vector3.Lerp(momentumDirection, inputDir.normalized, Time.deltaTime * 6f);
                }
                controller.Move(momentumDirection * currentSpeed * Time.deltaTime);
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

                moveDirection = Vector3.Lerp(moveDirection, targetMoveDirection, Time.deltaTime * acceleration);
                currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * acceleration);
                controller.Move(moveDirection * currentSpeed * Time.deltaTime);
                velocity.x = moveDirection.x * currentSpeed;
                velocity.z = moveDirection.z * currentSpeed;
            }
            else
            {
                currentSpeed = Mathf.Lerp(currentSpeed, 0, Time.deltaTime * deceleration);
                moveDirection = Vector3.Lerp(moveDirection, Vector3.zero, Time.deltaTime * deceleration);
                controller.Move(moveDirection * currentSpeed * Time.deltaTime);
                velocity.x = moveDirection.x * currentSpeed;
                velocity.z = moveDirection.z * currentSpeed;
            }
        }

        // FOV
        float targetFOV = normalFOV;
        if (isSliding) targetFOV = slideFOV;
        if (isWallRunning) targetFOV = wallRunFOV;
        currentFOV = Mathf.Lerp(currentFOV, targetFOV, Time.deltaTime * fovTransitionSpeed);
        playerCamera.fieldOfView = currentFOV;

        // Regular jump
        if (jumpInput && isGrounded && !isSliding && !isWallRunning)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (hasMomentum && momentumSpeed > walkSpeed && enableDebugLogs) Debug.Log($"Jump with momentum: {momentumSpeed:F1}");
        }

        // Gravity (applied only when not wall running)
        if (!isWallRunning)
            velocity.y += gravity * Time.deltaTime;

        controller.Move(velocity * Time.deltaTime);
    }

    void CheckForWallRun()
    {
        float vertical = Input.GetAxisRaw("Vertical");
        if (vertical <= 0) return;

        float currentHorizontalSpeed = new Vector3(velocity.x, 0, velocity.z).magnitude;
        float cameraAngle = Vector3.Angle(cameraTransform.forward, Vector3.up);
        bool lookingUp = cameraAngle < verticalWallRunAngleThreshold;

        Vector3 horizontalForward = cameraTransform.forward;
        horizontalForward.y = 0;
        horizontalForward.Normalize();

        RaycastHit forwardHit, higherHit, lowerHit, leftHit, rightHit;
        bool forwardWall = Physics.SphereCast(transform.position, 0.3f, horizontalForward, out forwardHit, wallCheckDistance, wallLayer);
        bool higherWall = Physics.SphereCast(transform.position + Vector3.up * 0.5f, 0.3f, horizontalForward, out higherHit, wallCheckDistance, wallLayer);
        bool lowerWall = Physics.SphereCast(transform.position - Vector3.up * 0.3f, 0.3f, horizontalForward, out lowerHit, wallCheckDistance, wallLayer);

        Vector3 leftDir = -cameraTransform.right; leftDir.y = 0; leftDir.Normalize();
        Vector3 rightDir = cameraTransform.right; rightDir.y = 0; rightDir.Normalize();
        bool leftWall = Physics.SphereCast(transform.position, 0.3f, leftDir, out leftHit, wallCheckDistance, wallLayer);
        bool rightWall = Physics.SphereCast(transform.position, 0.3f, rightDir, out rightHit, wallCheckDistance, wallLayer);

        if (enableDebugLogs && (forwardWall || higherWall || lowerWall || leftWall || rightWall))
        {
            if (forwardWall) Debug.Log($"Forward wall at {forwardHit.distance:F2}m, ID: {forwardHit.collider.GetInstanceID()}");
            if (leftWall) Debug.Log($"Left wall at {leftHit.distance:F2}m");
            if (rightWall) Debug.Log($"Right wall at {rightHit.distance:F2}m");
        }

        // Vertical wall run (looking up)
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

        // Horizontal wall run
        if (currentHorizontalSpeed < 3f) return;

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
                if (enableDebugLogs) Debug.Log($"Wall {wallID} on cooldown for {nextAvailable - Time.time:F2}s");
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

        float multiplier = lookingAlongWall ? downwardRunMultiplier : 1f;
        float runBaseSpeed = verticalWallRunBaseSpeed * multiplier;
        float runForwardSpeed = verticalWallRunForwardSpeed * multiplier;
        float runBoostUp = verticalWallRunSpeedBoostUp * multiplier;
        float runBoostDown = verticalWallRunSpeedBoostDown * multiplier;

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
                targetSpeed = Mathf.Min(totalSpeed + runBoostDown, verticalWallRunMaxGainSpeed);

            currentSpeed = targetSpeed;
            if (enableDebugLogs) Debug.Log($"VERTICAL WALL RUN - Speed: {totalSpeed:F1} → {currentSpeed:F1} ({(lookingAlongWall ? "downward" : "upward")})");
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

    void StartWallRun(Vector3 normal, bool isLeft, int wallID)
    {
        usedWalls.Add(wallID);
        wallCooldowns[wallID] = Time.time + wallCooldownTime;

        isWallRunning = true;
        isVerticalWallRun = false;
        wallRunSide = isLeft;
        wallNormal = normal;
        wallRunTimer = wallRunBaseDuration;
        hasWallRunBoosted = false;

        Vector3 wallForward = Vector3.Cross(normal, Vector3.up);
        if (Vector3.Dot(wallForward, cameraTransform.forward) < 0) wallForward = -wallForward;
        wallRunDirection = wallForward.normalized;
        lastWallRunForwardDir = wallRunDirection;

        float currentHorizontalSpeed = new Vector3(velocity.x, 0, velocity.z).magnitude;

        float chainPenalty = 1f + currentChainTension;
        wallRunCooldownTimer = wallRunCooldown * chainPenalty;

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
        targetWallRunSpeed = Mathf.Min(currentHorizontalSpeed + totalBoost, wallRunMaxSpeed);
        targetWallRunSpeed = Mathf.Max(targetWallRunSpeed, minWallRunSpeed);

        speedRampTimer = 0.2f;

        float upBoostFactor = Mathf.Clamp01(1f - (cameraAngle / 90f));
        upBoostFactor = Mathf.Pow(upBoostFactor, 1.5f);
        targetUpwardVelocity = wallRunHopUpward * upBoostFactor;
        upwardBoostRampTimer = 0.15f;

        // Small horizontal nudge only
        Vector3 kick = (wallRunDirection * wallRunHopForce * 0.3f) + (wallNormal * wallRunHopSideways * 0.3f);
        velocity += kick;

        wallHopTimer = wallRunHopDuration;

        hasMomentum = true;
        momentumSpeed = currentHorizontalSpeed;
        momentumDirection = wallRunDirection;

        currentChainTension += chainPenaltyIncrease;

        if (enableDebugLogs) Debug.Log($"HORIZONTAL WALL RUN - Upward boost: {targetUpwardVelocity:F1} (factor {upBoostFactor:F2})");
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

        // Force kickoff when forward key is released
        if (Input.GetAxisRaw("Vertical") <= 0 || wallRunTimer <= 0.05f)
        {
            // 45-degree forced launch away from wall
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
                    if (enableDebugLogs) Debug.Log("Wall run ended - lost wall contact");
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

    // Forces a 45-degree launch away from the wall (Titanfall 2 style)
        void ForceWallKickoff()
    {
        if (!isWallRunning) return;

        // Get the closest point on the wall
        Vector3 contactPoint = GetClosestPointOnWall();
        // Direction away from the wall (from wall to player)
        Vector3 away = (transform.position - contactPoint).normalized;
        away.y = 0;
        away.Normalize();

        // Combine with upward direction for 45-degree angle
        Vector3 launchDir = (away + Vector3.up * 0.5f).normalized;
        float launchSpeed = currentSpeed * 0.8f;

        velocity = launchDir * launchSpeed;
        verticalVelocity = velocity.y;

        hasMomentum = true;
        momentumSpeed = launchSpeed;
        momentumDirection = away;

        EndWallRun();
        wallRunCooldownTimer = wallRunCooldown;

        if (enableDebugLogs) Debug.Log($"FORCED WALL KICKOFF - Speed: {launchSpeed:F1}");
    }

    void WallRunJump()
    {
        Vector3 contactPoint = GetClosestPointOnWall();
        Vector3 away = (transform.position - contactPoint).normalized;
        away.y = 0;
        away.Normalize();

        Vector3 launchDir = (away + Vector3.up * 0.6f).normalized;
        float launchSpeed = currentSpeed * 0.9f + wallRunJumpForce * 0.5f;

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
        if (enableDebugLogs) Debug.Log($"WALL JUMP! Speed: {momentumSpeed:F1}");
    }

        void CoyoteWallJump()
    {
        // For coyote jump, we don't have current wall contact, so use last wall normal and position approximation
        Vector3 away = Vector3.ProjectOnPlane(-lastWallNormal, Vector3.up).normalized;
        Vector3 launchDir = (away + Vector3.up * 0.5f).normalized;
        float launchSpeed = (momentumSpeed > 0 ? momentumSpeed : currentSpeed) * 0.7f + wallRunJumpForce * 0.3f;

        velocity = launchDir * launchSpeed;
        verticalVelocity = velocity.y;

        hasMomentum = true;
        momentumSpeed = launchSpeed * 0.7f;
        momentumDirection = away;

        if (enableDebugLogs) Debug.Log($"COYOTE JUMP! Speed: {momentumSpeed:F1}");
    }

    // Helper to get the closest point on the current wall (using raycast)
    Vector3 GetClosestPointOnWall()
    {
        RaycastHit hit;
        // Cast from player position towards the wall (opposite of wallNormal)
        if (Physics.Raycast(transform.position, -wallNormal, out hit, wallCheckDistance + 1f, wallLayer))
        {
            return hit.point;
        }
        // Fallback: approximate point 0.5m away in the wall normal direction
        return transform.position + wallNormal * 0.5f;
    }

    void EndWallRun()
    {
        lastWallNormal = wallNormal;
        lastWallRunDir = wallRunDirection;
        lastWallRunEndTime = Time.time;

        // No extra hop here – kickoff is handled separately
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

    // --- Slide methods with fair chain penalties (no exponential decay) ---
    void StartSlide(Vector3 direction)
    {
        if (slideCooldownTimer > 0) return;

        isSliding = true;
        slideTimer = slideDuration;
        wasJumpCancelled = false;

        slideCount++;
        float slidePenalty = Mathf.Max(0.3f, 1f - (slideCount - 1) * slideDecayRate);
        float currentHorizontalSpeed = hasMomentum && momentumSpeed > currentSpeed ? momentumSpeed : new Vector3(velocity.x, 0, velocity.z).magnitude;

        float slideStartSpeed = currentHorizontalSpeed < maxSlideGainSpeed ?
            Mathf.Min(currentHorizontalSpeed + (slideBoost * slidePenalty), maxSlideGainSpeed) : currentHorizontalSpeed;

        hasMomentum = true;
        momentumSpeed = slideStartSpeed;
        momentumDirection = direction;
        slideDirection = direction;

        velocity = new Vector3(direction.x, velocity.y, direction.z) * slideStartSpeed;
        currentSpeed = slideStartSpeed;
        moveDirection = direction;

        currentSlideHeight = controller.height;
        currentCameraHeight = cameraTransform.localPosition.y;

        currentChainTension = Mathf.Max(0, currentChainTension - slideResetChain);

        if (enableDebugLogs) Debug.Log($"SLIDE #{slideCount} - Speed: {currentHorizontalSpeed:F1} → {slideStartSpeed:F1}");
    }

    void UpdateSlide()
    {
        if (!controller.isGrounded)
        {
            EndSlide();
            return;
        }

        slideTimer -= Time.deltaTime;
        if (slideTimer <= 0f)
        {
            EndSlide();
            return;
        }

        RaycastHit groundHit;
        if (!Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out groundHit, 2f))
        {
            EndSlide();
            return;
        }

        float slopeAngle = Vector3.Angle(groundHit.normal, Vector3.up);
        bool onSlope = slopeAngle > minSlopeAngle;

        Vector3 slideDirOnPlane = Vector3.ProjectOnPlane(slideDirection, groundHit.normal).normalized;
        Vector3 slopeDown = Vector3.ProjectOnPlane(Vector3.down, groundHit.normal).normalized;
        float dotWithDownhill = Vector3.Dot(slideDirOnPlane, slopeDown);
        bool slidingDownhill = onSlope && dotWithDownhill > 0.2f;
        bool slidingUphill = onSlope && dotWithDownhill < -0.2f;

        if (onSlope)
        {
            float slopeFactor = Mathf.Clamp01((slopeAngle - minSlopeAngle) / (75f - minSlopeAngle));

            if (slidingDownhill)
            {
                momentumSpeed += slopeSlideGravity * slopeFactor * Time.deltaTime;
                momentumSpeed += slopeFactor * slopeBoostMultiplier * slideBoost * Time.deltaTime;
                momentumSpeed = Mathf.Min(momentumSpeed, maxSlopeSpeed);
                momentumSpeed *= Mathf.Pow(slopeSlideFriction, Time.deltaTime);
            }
            else if (slidingUphill)
            {
                momentumSpeed = Mathf.Max(momentumSpeed - uphillSlowdown * slopeFactor * Time.deltaTime, walkSpeed);
            }

            slideDirOnPlane = Vector3.Lerp(slideDirOnPlane, slopeDown, slopeFactor * 0.4f);
        }

        // Natural speed decay during slide
        float speedDecay = deceleration * 0.6f * Time.deltaTime;
        momentumSpeed = Mathf.Max(momentumSpeed - speedDecay, walkSpeed * 0.8f);

        currentSpeed = momentumSpeed;
        Vector3 move = slideDirOnPlane * currentSpeed;
        controller.Move(move * Time.deltaTime);
        velocity.x = move.x;
        velocity.z = move.z;

        if (onSlope)
            velocity.y += -gravity * slopeAttachForce * Time.deltaTime;
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

        controller.height = originalHeight;
        controller.center = new Vector3(0, originalCenterY, 0);
        cameraTransform.localPosition = new Vector3(cameraTransform.localPosition.x, originalCameraHeight, cameraTransform.localPosition.z);
        if (enableDebugLogs) Debug.Log($"Slide cancelled! Momentum: {momentumSpeed:F1}");
    }

    void EndSlide()
    {
        isSliding = false;
        slideCooldownTimer = slideCooldown;
        lastSlideTime = Time.time;

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
    }

    // Public accessors
    public bool IsRunning() => isRunning;
    public bool IsSliding() => isSliding;
    public bool IsWallRunning() => isWallRunning;
    public bool IsVerticalWallRunning() => isVerticalWallRun;
    public float GetCurrentVelocity() => new Vector3(velocity.x, 0, velocity.z).magnitude;
}

#pragma warning restore CS0618
