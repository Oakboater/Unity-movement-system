using UnityEngine;
using System.Collections.Generic;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float runSpeed = 20f;
    public float acceleration = 10f;
    public float deceleration = 10f;

    [Header("Slide Settings")]
    public float slideDuration = 1.5f;
    public float slideCooldown = 0.3f;
    public float slideBoost = 3f;
    public float slideTransitionSpeed = 10f;
    public float maxSlideGainSpeed = 20f;
    public float slideDecayRate = 0.5f;

    [Header("Wall Run Settings")]
    public bool enableWallRun = true;
    public float wallRunSpeed = 18f;
    public float wallRunGravity = -4f;
    public float wallRunJumpForce = 12f;
    public float wallRunDuration = 2f;
    public float wallRunCooldown = 0.3f;          // global wall run cooldown (time between wall runs)
    public LayerMask wallLayer;
    public float wallCheckDistance = 0.8f;
    public float wallRunFOV = 75f;
    public float wallRunBaseBoost = 5f;
    public float minWallRunSpeed = 8f;
    public float wallRunHopForce = 5f;
    public float wallRunCoyoteTime = 0.15f;
    public bool enableDebugLogs = false;

    [Header("Horizontal Wall Run Angle Control")]
    public float lookUpVerticalGain = 18f;
    public float lookDownVerticalLoss = 12f;
    public float lookUpSpeedLoss = 0.5f;
    public float lookDownSpeedGain = 2f;
    public float wallRunGravityStrength = 1.5f;

    [Header("Vertical Wall Run Settings")]
    public bool enableVerticalWallRun = true;
    public float verticalWallRunBaseSpeed = 12f;
    public float verticalWallRunGravity = -8f;
    public float verticalWallRunDuration = 2f;
    public float verticalWallRunJumpForce = 18f;
    public float verticalWallRunAngleThreshold = 45f;
    public float verticalWallRunForwardSpeed = 8f;
    public float verticalWallRunCooldown = 0.5f;
    public float verticalWallRunMaxGainSpeed = 20f;
    public float verticalWallRunSpeedBoostUp = 2f;
    public float verticalWallRunSpeedBoostDown = 3f;
    public float verticalWallRunDecayRate = 2f;
    public float verticalMomentumTransfer = 0.5f;
    public float wallRunJumpCooldown = 0.3f;

    [Header("Slope Slide Settings")]
    public float slopeBoostMultiplier = 2f;
    public float maxSlopeSpeed = 25f;
    public float minSlopeAngle = 5f;

    [Header("Camera Settings")]
    public Camera playerCamera;
    public float normalFOV = 60f;
    public float slideFOV = 75f;
    public float fovTransitionSpeed = 8f;

    [Header("Gravity")]
    public float gravity = -9.81f;

    [Header("Jump Settings")]
    public float jumpHeight = 1.5f;

    private CharacterController controller;
    private Vector3 velocity;
    private Vector3 moveDirection;
    private Vector3 targetMoveDirection;
    private float currentSpeed;
    private Transform cameraTransform;
    private bool isRunning;
    private bool isSliding;
    private float slideTimer;
    private float slideCooldownTimer;
    private Vector3 slideDirection;
    private float originalHeight;
    private float originalCenterY;
    private float originalCameraHeight;
    private float currentFOV;
    private float currentSlideHeight;
    private float currentCameraHeight;
    private int slideCount;
    private float lastSlideTime;

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
    private Vector3 wallNormal;
    private Vector3 wallRunDirection;
    private Vector3 wallUpDirection;
    private bool wallRunSide;
    private bool hasWallRunBoosted;
    private Vector3 lastWallRunForwardDir;
    private Vector3 lastWallNormal;
    private Vector3 lastWallRunDir;
    private int currentWallID;

    // Walls used in current air session (reset on ground)
    private HashSet<int> usedWalls = new HashSet<int>();

    void Start()
    {
        controller = GetComponent<CharacterController>();

        originalHeight = controller.height;
        originalCenterY = controller.center.y;

        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera != null)
        {
            cameraTransform = playerCamera.transform;
            originalCameraHeight = cameraTransform.localPosition.y;
            currentFOV = normalFOV;
            playerCamera.fieldOfView = normalFOV;
            currentCameraHeight = originalCameraHeight;
        }

        currentSlideHeight = originalHeight;
        moveDirection = Vector3.zero;
        targetMoveDirection = Vector3.zero;
        slideCount = 0;

        if (enableDebugLogs) Debug.Log("PlayerMovement initialized");
    }

    void Update()
    {
        // Cooldown timers
        if (slideCooldownTimer > 0) slideCooldownTimer -= Time.deltaTime;
        if (wallRunCooldownTimer > 0) wallRunCooldownTimer -= Time.deltaTime;
        if (verticalWallRunCooldownTimer > 0) verticalWallRunCooldownTimer -= Time.deltaTime;

        bool isGrounded = controller.isGrounded;

        // Force end wall run on ground
        if (isGrounded && isWallRunning) EndWallRun();

        // Reset used walls when grounded (new air session)
        if (isGrounded && !isWallRunning && usedWalls.Count > 0)
        {
            usedWalls.Clear();
            if (enableDebugLogs) Debug.Log("Used walls cleared (touched ground).");
        }

        // Reset slide count after being grounded for a while
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
                else if (enableDebugLogs) Debug.Log($"Momentum preserved on landing: {momentumSpeed:F1}");
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

        // Slide initiation with stored wall direction (prefer stored for a short window)
        if (!isSliding && slideInput && isGrounded && slideCooldownTimer <= 0)
        {
            // Use stored direction if available and recent
            if (lastWallRunForwardDir != Vector3.zero && Time.time - lastWallRunEndTime <= 0.5f)
            {
                slideDirection = lastWallRunForwardDir;
                lastWallRunForwardDir = Vector3.zero;
                if (enableDebugLogs) Debug.Log($"Slide using stored wall direction: {slideDirection}");
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

        // Wall jump with coyote time
        if (jumpInput && !isWallRunning && !isGrounded && Time.time - lastWallRunEndTime <= wallRunCoyoteTime && lastWallRunEndTime > 0)
        {
            if (enableDebugLogs) Debug.Log($"COYOTE WALL JUMP! Time since wall end: {Time.time - lastWallRunEndTime:F3}s");
            CoyoteWallJump();
            lastWallRunEndTime = 0;
        }
        else if (isWallRunning && jumpInput && Time.time - lastWallRunJumpTime >= wallRunJumpCooldown)
        {
            if (enableDebugLogs) Debug.Log("WALL RUN JUMP!");
            lastWallRunJumpTime = Time.time;
            WallRunJump();
        }

        // Slide state handling (height transition)
        if (isSliding && isGrounded)
        {
            UpdateSlide();

            float targetHeight = originalHeight * 0.6f;
            float targetCameraHeight = originalCameraHeight * 0.6f;
            currentSlideHeight = Mathf.Lerp(currentSlideHeight, targetHeight, Time.deltaTime * slideTransitionSpeed);
            currentCameraHeight = Mathf.Lerp(currentCameraHeight, targetCameraHeight, Time.deltaTime * slideTransitionSpeed);
            controller.height = currentSlideHeight;
            controller.center = new Vector3(0, currentSlideHeight * 0.5f, 0);
            if (playerCamera != null)
                cameraTransform.localPosition = new Vector3(cameraTransform.localPosition.x, currentCameraHeight, cameraTransform.localPosition.z);
        }
        else
        {
            if (isSliding) EndSlide();

            currentSlideHeight = Mathf.Lerp(currentSlideHeight, originalHeight, Time.deltaTime * slideTransitionSpeed);
            currentCameraHeight = Mathf.Lerp(currentCameraHeight, originalCameraHeight, Time.deltaTime * slideTransitionSpeed);
            controller.height = currentSlideHeight;
            controller.center = new Vector3(0, currentSlideHeight * 0.5f, 0);
            if (playerCamera != null)
                cameraTransform.localPosition = new Vector3(cameraTransform.localPosition.x, currentCameraHeight, cameraTransform.localPosition.z);
        }

        // Ground movement (only when not wall running)
        if (!isWallRunning)
        {
            if (hasMomentum && momentumSpeed > walkSpeed && !isSliding)
            {
                currentSpeed = momentumSpeed;
                if (isGrounded) momentumSpeed = Mathf.Max(momentumSpeed - (Time.deltaTime * deceleration * 2f), walkSpeed);
                currentSpeed = momentumSpeed;

                if (cameraTransform != null)
                {
                    if (hasMovementInput)
                    {
                        Vector3 forward = cameraTransform.forward;
                        Vector3 right = cameraTransform.right;
                        forward.y = 0; right.y = 0;
                        forward.Normalize(); right.Normalize();
                        Vector3 inputDir = (forward * vertical) + (right * horizontal);
                        if (inputDir.magnitude > 0.1f)
                            momentumDirection = Vector3.Lerp(momentumDirection, inputDir.normalized, Time.deltaTime * 6f); // faster turning
                    }
                    controller.Move(momentumDirection * currentSpeed * Time.deltaTime);
                    velocity.x = momentumDirection.x * currentSpeed;
                    velocity.z = momentumDirection.z * currentSpeed;
                }
                if (momentumSpeed <= walkSpeed && isGrounded) hasMomentum = false;
            }
            else if (hasMovementInput)
            {
                isRunning = Input.GetKey(KeyCode.LeftShift) && hasMovementInput;
                float targetSpeed = isRunning ? runSpeed : walkSpeed;
                if (cameraTransform != null)
                {
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
        if (playerCamera != null)
        {
            float targetFOV = normalFOV;
            if (isSliding) targetFOV = slideFOV;
            if (isWallRunning) targetFOV = wallRunFOV;
            currentFOV = Mathf.Lerp(currentFOV, targetFOV, Time.deltaTime * fovTransitionSpeed);
            playerCamera.fieldOfView = currentFOV;
        }

        // Regular jump
        if (jumpInput && isGrounded && !isSliding && !isWallRunning)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (hasMomentum && momentumSpeed > walkSpeed && enableDebugLogs) Debug.Log($"Jump with momentum: {momentumSpeed:F1}");
        }

        // Gravity
        if (!isWallRunning)
            velocity.y += gravity * Time.deltaTime;
        else if (!isVerticalWallRun)
            velocity.y += wallRunGravity * Time.deltaTime;
        else
            velocity.y += verticalWallRunGravity * Time.deltaTime;

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

        RaycastHit forwardHit;
        bool forwardWall = Physics.Raycast(transform.position, horizontalForward, out forwardHit, wallCheckDistance, wallLayer);

        Vector3 higherPosition = transform.position + Vector3.up * 0.5f;
        RaycastHit higherForwardHit;
        bool higherForwardWall = Physics.Raycast(higherPosition, horizontalForward, out higherForwardHit, wallCheckDistance, wallLayer);

        Vector3 lowerPosition = transform.position - Vector3.up * 0.3f;
        RaycastHit lowerForwardHit;
        bool lowerForwardWall = Physics.Raycast(lowerPosition, horizontalForward, out lowerForwardHit, wallCheckDistance, wallLayer);

        Vector3 leftDir = -cameraTransform.right;
        Vector3 rightDir = cameraTransform.right;
        leftDir.y = 0; rightDir.y = 0;
        leftDir.Normalize(); rightDir.Normalize();

        RaycastHit leftHit, rightHit;
        bool leftWall = Physics.Raycast(transform.position, leftDir, out leftHit, wallCheckDistance, wallLayer);
        bool rightWall = Physics.Raycast(transform.position, rightDir, out rightHit, wallCheckDistance, wallLayer);

        if (enableDebugLogs && (forwardWall || higherForwardWall || lowerForwardWall || leftWall || rightWall))
        {
            Debug.Log($"Wall Check - Camera Angle: {cameraAngle:F1}°, Looking Up: {lookingUp}, Speed: {currentHorizontalSpeed:F1}");
            if (forwardWall) Debug.Log($"Forward wall at {forwardHit.distance:F2}m, ID: {forwardHit.collider.GetInstanceID()}");
            if (leftWall) Debug.Log($"Left wall at {leftHit.distance:F2}m, ID: {leftHit.collider.GetInstanceID()}");
            if (rightWall) Debug.Log($"Right wall at {rightHit.distance:F2}m, ID: {rightHit.collider.GetInstanceID()}");
        }

        // Try vertical wall run first
        if (enableVerticalWallRun && lookingUp && (forwardWall || higherForwardWall || lowerForwardWall))
        {
            RaycastHit hit = forwardWall ? forwardHit : (higherForwardWall ? higherForwardHit : lowerForwardHit);
#pragma warning disable 0618
            int wallID = hit.collider.GetInstanceID();
#pragma warning restore 0618
            if (usedWalls.Contains(wallID))
            {
                if (enableDebugLogs) Debug.Log($"Wall {wallID} already used in this air session - cannot vertical wall run.");
                return;
            }
            StartVerticalWallRun(hit.normal, wallID);
            return;
        }

        // Horizontal wall run
        if (currentHorizontalSpeed < 2f) return;

        if (leftWall && !rightWall)
        {
#pragma warning disable 0618
            int wallID = leftHit.collider.GetInstanceID();
#pragma warning restore 0618
            if (usedWalls.Contains(wallID))
            {
                if (enableDebugLogs) Debug.Log($"Wall {wallID} already used in this air session - cannot horizontal wall run.");
                return;
            }
            StartWallRun(leftHit.normal, true, wallID);
        }
        else if (rightWall && !leftWall)
        {
#pragma warning disable 0618
            int wallID = rightHit.collider.GetInstanceID();
#pragma warning restore 0618
            if (usedWalls.Contains(wallID))
            {
                if (enableDebugLogs) Debug.Log($"Wall {wallID} already used in this air session - cannot horizontal wall run.");
                return;
            }
            StartWallRun(rightHit.normal, false, wallID);
        }
        else if (forwardWall && !leftWall && !rightWall)
        {
#pragma warning disable 0618
            int wallID = forwardHit.collider.GetInstanceID();
#pragma warning restore 0618
            if (usedWalls.Contains(wallID))
            {
                if (enableDebugLogs) Debug.Log($"Wall {wallID} already used in this air session - cannot horizontal wall run.");
                return;
            }
            StartWallRun(forwardHit.normal, false, wallID);
        }
    }

    void StartVerticalWallRun(Vector3 normal, int wallID)
    {
        if (verticalWallRunCooldownTimer > 0) return;
        float timeSinceLast = Time.time - lastVerticalWallRunTime;
        if (timeSinceLast < verticalWallRunCooldown && lastVerticalWallRunTime > 0) return;

        float totalSpeed = new Vector3(velocity.x, velocity.y, velocity.z).magnitude;
        bool alreadyFast = totalSpeed > verticalWallRunMaxGainSpeed;

        currentWallID = wallID;
        usedWalls.Add(wallID);
        isWallRunning = true;
        isVerticalWallRun = true;
        wallNormal = normal;
        wallRunTimer = verticalWallRunDuration;
        hasWallRunBoosted = false;

        wallUpDirection = Vector3.ProjectOnPlane(Vector3.up, normal).normalized;
        Vector3 wallForward = Vector3.ProjectOnPlane(cameraTransform.forward, normal).normalized;
        wallRunDirection = (wallUpDirection * verticalWallRunBaseSpeed + wallForward * verticalWallRunForwardSpeed).normalized;

        if (!hasWallRunBoosted)
        {
            hasWallRunBoosted = true;
            float targetSpeed;

            if (alreadyFast)
                targetSpeed = totalSpeed;
            else if (totalSpeed < verticalWallRunBaseSpeed)
                targetSpeed = verticalWallRunBaseSpeed;
            else if (totalSpeed < verticalWallRunMaxGainSpeed)
                targetSpeed = Mathf.Min(totalSpeed + verticalWallRunSpeedBoostDown, verticalWallRunMaxGainSpeed);
            else
                targetSpeed = totalSpeed;

            currentSpeed = targetSpeed;
            if (enableDebugLogs) Debug.Log($"VERTICAL WALL RUN - Wall {wallID}, Speed: {totalSpeed:F1} → {currentSpeed:F1} m/s, Duration: {verticalWallRunDuration}s");
        }
        else
            currentSpeed = totalSpeed;

        velocity = wallRunDirection * currentSpeed;
        hasMomentum = true;
        momentumSpeed = currentSpeed;
        momentumDirection = wallRunDirection;
        lastVerticalWallRunTime = Time.time;
        verticalWallRunCooldownTimer = verticalWallRunCooldown;
    }

    void StartWallRun(Vector3 normal, bool isLeft, int wallID)
    {
        currentWallID = wallID;
        usedWalls.Add(wallID);
        isWallRunning = true;
        isVerticalWallRun = false;
        wallRunSide = isLeft;
        wallNormal = normal;
        wallRunTimer = wallRunDuration;
        hasWallRunBoosted = false;

        Vector3 wallForward = Vector3.Cross(normal, Vector3.up);
        float dot = Vector3.Dot(wallForward, cameraTransform.forward);
        if (dot < 0) wallForward = -wallForward;
        wallRunDirection = wallForward.normalized;

        lastWallRunForwardDir = wallRunDirection;

        float currentHorizontalSpeed = new Vector3(velocity.x, 0, velocity.z).magnitude;

        if (!hasWallRunBoosted)
        {
            hasWallRunBoosted = true;
            float cameraAngle = Vector3.Angle(cameraTransform.forward, Vector3.up);
            float angleFactor, speedModifier;
            if (cameraAngle < 45f)
            {
                angleFactor = 1f - (cameraAngle / 45f);
                speedModifier = -3f * angleFactor; // subtract up to 3 from base boost
            }
            else
            {
                angleFactor = (cameraAngle - 45f) / 45f;
                speedModifier = 2f * angleFactor; // add up to 2 extra speed
            }

            float totalBoost = Mathf.Max(0, wallRunBaseBoost + speedModifier);
            float newSpeed;
            if (currentHorizontalSpeed < minWallRunSpeed)
            {
                newSpeed = minWallRunSpeed;
                if (enableDebugLogs) Debug.Log($"Wall run - below min speed: {currentHorizontalSpeed:F1} -> {newSpeed:F1}");
            }
            else if (currentHorizontalSpeed < wallRunSpeed)
            {
                newSpeed = Mathf.Min(currentHorizontalSpeed + totalBoost, wallRunSpeed);
                if (enableDebugLogs) Debug.Log($"Wall run - boosting: {currentHorizontalSpeed:F1} + {totalBoost:F1} = {newSpeed:F1}");
            }
            else
            {
                newSpeed = currentHorizontalSpeed;
                if (enableDebugLogs) Debug.Log($"Wall run - preserving high speed: {currentHorizontalSpeed:F1}");
            }

            currentSpeed = newSpeed;
        }
        else
            currentSpeed = currentHorizontalSpeed;

        velocity.x = wallRunDirection.x * currentSpeed;
        velocity.z = wallRunDirection.z * currentSpeed;
        velocity += wallRunDirection * wallRunHopForce * 0.5f;

        hasMomentum = true;
        momentumSpeed = currentSpeed;
        momentumDirection = wallRunDirection;

        if (enableDebugLogs) Debug.Log($"HORIZONTAL WALL RUN - Wall {wallID}, Side: {(isLeft ? "LEFT" : "RIGHT")}, Speed: {currentSpeed:F1}, Direction: {wallRunDirection}");
    }

    void UpdateWallRun()
    {
        wallRunTimer -= Time.deltaTime;

        if (Input.GetAxisRaw("Vertical") <= 0)
        {
            if (enableDebugLogs) Debug.Log("Wall run ended - no forward input");
            EndWallRun();
            return;
        }

        float cameraAngle = Vector3.Angle(cameraTransform.forward, Vector3.up);

        if (!isVerticalWallRun)
        {
            // Natural gravity pull
            float gravityPull = wallRunGravityStrength * Time.deltaTime;
            controller.Move(-Vector3.up * gravityPull);
            velocity.y -= gravityPull / Time.deltaTime;

            // Camera-based vertical movement
            float verticalMovement = 0f;
            if (cameraAngle < 45f)
            {
                float upFactor = 1f - (cameraAngle / 45f);
                verticalMovement = lookUpVerticalGain * upFactor * Time.deltaTime;
                float speedLoss = lookUpSpeedLoss * upFactor * Time.deltaTime;
                currentSpeed = Mathf.Max(currentSpeed - speedLoss, minWallRunSpeed);
                if (enableDebugLogs && upFactor > 0.1f)
                    Debug.Log($"Looking up - Vertical movement: {verticalMovement:F2}, Speed: {currentSpeed:F1}");
            }
            else if (cameraAngle > 45f)
            {
                float downFactor = (cameraAngle - 45f) / 45f;
                verticalMovement = -lookDownVerticalLoss * downFactor * Time.deltaTime;
                float speedGain = lookDownSpeedGain * downFactor * Time.deltaTime;
                currentSpeed = Mathf.Min(currentSpeed + speedGain, wallRunSpeed);
                if (enableDebugLogs && downFactor > 0.1f)
                    Debug.Log($"Looking down - Vertical movement: {verticalMovement:F2}, Speed: {currentSpeed:F1}");
            }

            if (Mathf.Abs(verticalMovement) > 0.01f)
            {
                Vector3 wallVertical = Vector3.ProjectOnPlane(Vector3.up, wallNormal).normalized;
                controller.Move(wallVertical * verticalMovement);
                velocity += wallVertical * verticalMovement / Time.deltaTime;
            }
        }
        else
        {
            // Vertical wall run speed decay
            float speedDecay = verticalWallRunDecayRate * Time.deltaTime;
            currentSpeed = Mathf.Max(currentSpeed - speedDecay, verticalWallRunBaseSpeed * 0.5f);
            if (enableDebugLogs && Time.frameCount % 30 == 0)
                Debug.Log($"Vertical wall run - Speed decay: {currentSpeed:F1}");
        }

        // Wall contact check
        Vector3 checkDir = isVerticalWallRun
            ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized
            : (wallRunSide ? -cameraTransform.right : cameraTransform.right);

        RaycastHit hit;
        bool stillOnWall = Physics.Raycast(transform.position, checkDir, out hit, wallCheckDistance + 0.2f, wallLayer);

        if (!stillOnWall && !isVerticalWallRun)
        {
            Vector3 higherPos = transform.position + Vector3.up * 0.5f;
            Vector3 lowerPos = transform.position - Vector3.up * 0.3f;
            stillOnWall = Physics.Raycast(higherPos, checkDir, out hit, wallCheckDistance + 0.2f, wallLayer) ||
                          Physics.Raycast(lowerPos, checkDir, out hit, wallCheckDistance + 0.2f, wallLayer);
        }
        else if (isVerticalWallRun && !stillOnWall)
        {
            Vector3 higherPos = transform.position + Vector3.up * 0.5f;
            stillOnWall = Physics.Raycast(higherPos, checkDir, out hit, wallCheckDistance + 0.2f, wallLayer);
        }

        if (!stillOnWall)
        {
            if (enableDebugLogs) Debug.Log("Wall run ended - lost wall contact");
            EndWallRun();
            return;
        }

        if (wallRunTimer <= 0)
        {
            if (enableDebugLogs) Debug.Log("Wall run ended - duration expired");
            EndWallRun();
            return;
        }

        if (isVerticalWallRun)
        {
            wallNormal = hit.normal;
            wallUpDirection = Vector3.ProjectOnPlane(Vector3.up, wallNormal).normalized;
            Vector3 wallForward = Vector3.ProjectOnPlane(cameraTransform.forward, wallNormal).normalized;
            wallRunDirection = (wallUpDirection * verticalWallRunBaseSpeed + wallForward * verticalWallRunForwardSpeed).normalized;
        }

        Vector3 move = wallRunDirection * currentSpeed;
        controller.Move(move * Time.deltaTime);
        velocity = move;
        momentumSpeed = currentSpeed;
        momentumDirection = move.normalized;

        if (enableDebugLogs)
            Debug.DrawRay(transform.position, wallRunDirection * 2f, isVerticalWallRun ? Color.green : Color.cyan);
    }

    void WallRunJump()
    {
        Vector3 jumpDir = (wallNormal + Vector3.up).normalized;
        float jumpForce = isVerticalWallRun ? verticalWallRunJumpForce + currentSpeed * verticalMomentumTransfer : wallRunJumpForce;
        velocity = jumpDir * jumpForce;
        hasMomentum = true;
        momentumSpeed = currentSpeed;
        momentumDirection = wallRunDirection;
        EndWallRun();
        wallRunCooldownTimer = wallRunCooldown;
        if (enableDebugLogs) Debug.Log($"WALL JUMP! Force: {jumpForce:F1}, Speed: {currentSpeed:F1}");
    }

    void CoyoteWallJump()
    {
        Vector3 jumpDir = (lastWallNormal + Vector3.up).normalized;
        velocity = jumpDir * wallRunJumpForce;
        hasMomentum = true;
        momentumSpeed = momentumSpeed > 0 ? momentumSpeed : currentSpeed;
        momentumDirection = lastWallRunDir;
        if (enableDebugLogs) Debug.Log($"COYOTE WALL JUMP! Speed: {momentumSpeed:F1}");
    }

    void EndWallRun()
    {
        // Store last wall info for coyote jump
        lastWallNormal = wallNormal;
        lastWallRunDir = wallRunDirection;
        lastWallRunEndTime = Time.time;

        // Forward hop when ending (if not jumping)
        if (isWallRunning && !Input.GetButtonDown("Jump"))
        {
            Vector3 forwardHop = wallRunDirection * wallRunHopForce;
            velocity += forwardHop;
            if (enableDebugLogs) Debug.Log($"Wall Run ended - forward hop: {wallRunHopForce}");
        }

        isWallRunning = false;
        if (isVerticalWallRun) verticalWallRunCooldownTimer = verticalWallRunCooldown;
        isVerticalWallRun = false;
        hasWallRunBoosted = false;

        if (enableDebugLogs) Debug.Log($"Wall run ended - Coyote time window: {wallRunCoyoteTime}s");
    }

    // Slide methods (unchanged, but with debug logs)
    void StartSlide(Vector3 direction)
    {
        isSliding = true;
        slideTimer = slideDuration;
        wasJumpCancelled = false;

        slideCount++;
        float slidePenalty = Mathf.Max(0, 1f - (slideCount - 1) * slideDecayRate);

        float currentHorizontalSpeed = hasMomentum && momentumSpeed > currentSpeed ?
            momentumSpeed : new Vector3(velocity.x, 0, velocity.z).magnitude;

        float slideStartSpeed;
        if (currentHorizontalSpeed < maxSlideGainSpeed)
            slideStartSpeed = Mathf.Min(currentHorizontalSpeed + (slideBoost * slidePenalty), maxSlideGainSpeed);
        else
            slideStartSpeed = currentHorizontalSpeed;

        hasMomentum = true;
        momentumSpeed = slideStartSpeed;
        momentumDirection = direction;
        slideDirection = direction;

        velocity = new Vector3(direction.x, velocity.y, direction.z) * slideStartSpeed;
        currentSpeed = slideStartSpeed;
        moveDirection = direction;

        currentSlideHeight = controller.height;
        currentCameraHeight = cameraTransform != null ? cameraTransform.localPosition.y : originalCameraHeight;

        if (enableDebugLogs) Debug.Log($"SLIDE #{slideCount} - Speed: {currentHorizontalSpeed:F1} → {slideStartSpeed:F1}, Penalty: {(1f - slidePenalty) * 100:F0}%");
    }

    void UpdateSlide()
    {
        if (!controller.isGrounded)
        {
            if (enableDebugLogs) Debug.Log("Slide ended - left ground");
            EndSlide();
            return;
        }

        // Slope boost
        RaycastHit groundHit;
        bool onSlope = false;
        float slopeAngle = 0f;
        if (Physics.Raycast(transform.position, Vector3.down, out groundHit, 2f))
        {
            slopeAngle = Vector3.Angle(groundHit.normal, Vector3.up);
            if (slopeAngle > minSlopeAngle)
            {
                onSlope = true;
                float slopeFactor = Mathf.Clamp01((slopeAngle - minSlopeAngle) / (75f - minSlopeAngle));
                float slopeBonus = slopeFactor * slopeBoostMultiplier * slideBoost * Time.deltaTime;
                momentumSpeed = Mathf.Min(momentumSpeed + slopeBonus, maxSlopeSpeed);
                if (enableDebugLogs && slopeFactor > 0.1f)
                    Debug.Log($"Slope slide - Angle: {slopeAngle:F1}°, Factor: {slopeFactor:F2}, Speed: {momentumSpeed:F1}");
            }
        }

        // Project movement direction onto the ground plane for smooth slope movement
        Vector3 moveVector = slideDirection * momentumSpeed;
        if (onSlope)
        {
            // Project the movement vector onto the ground plane (to slide along slope)
            Vector3 groundNormal = groundHit.normal;
            moveVector = Vector3.ProjectOnPlane(moveVector, groundNormal).normalized * momentumSpeed;
        }

        // Timer-based speed reduction only when not on steep slope
        float currentSlideSpeed;
        if (!onSlope)
        {
            slideTimer -= Time.deltaTime;
            float t = 1 - (slideTimer / slideDuration);
            if (t < 0.6f)
                currentSlideSpeed = momentumSpeed;
            else
            {
                float slowdownT = (t - 0.6f) / 0.4f;
                currentSlideSpeed = Mathf.Lerp(momentumSpeed, walkSpeed * 1.2f, slowdownT);
            }
            momentumSpeed = currentSlideSpeed;
        }
        else
        {
            currentSlideSpeed = momentumSpeed;
            // On slope, keep the timer from decaying but don't let it expire while sliding on slope
            if (slideTimer < slideDuration) slideTimer = slideDuration; // optional: reset timer while on slope
        }

        currentSpeed = currentSlideSpeed;
        controller.Move(moveVector * Time.deltaTime);
        velocity.x = moveVector.x;
        velocity.z = moveVector.z;

        if (!onSlope && slideTimer <= 0) EndSlide();
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
        if (playerCamera != null)
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
        if (playerCamera != null)
            cameraTransform.localPosition = new Vector3(cameraTransform.localPosition.x, originalCameraHeight, cameraTransform.localPosition.z);
    }

    public bool IsRunning() => isRunning;
    public bool IsSliding() => isSliding;
    public bool IsWallRunning() => isWallRunning;
    public bool IsVerticalWallRunning() => isVerticalWallRun;
    public float GetCurrentVelocity() => new Vector3(velocity.x, 0, velocity.z).magnitude;
}
