using UnityEngine;

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
    public float slideBoost = 5f;
    public float slideTransitionSpeed = 10f;
    public float maxSlideGainSpeed = 20f;
    public float slideDecayRate = 0.5f;

    [Header("Wall Run Settings")]
    public bool enableWallRun = true;
    public float wallRunSpeed = 18f;
    public float wallRunGravity = -2f;
    public float wallRunJumpForce = 12f;
    public float wallRunDuration = 2f;
    public float wallRunCooldown = 0.3f;
    public LayerMask wallLayer;
    public float wallCheckDistance = 0.8f;
    public float wallRunFOV = 75f;
    public float wallRunSpeedBoost = 5f;
    public float minWallRunSpeed = 8f;
    public bool enableDebugLogs = false;

    [Header("Horizontal Wall Run Angle Control")]
    public float lookUpSpeedLoss = 3f; // Speed loss when looking up
    public float lookUpVerticalGain = 5f; // Vertical gain when looking up
    public float lookDownSpeedGain = 3f; // Speed gain when looking down
    public float lookDownVerticalLoss = 5f; // Vertical loss when looking down
    public float angleTransitionSpeed = 5f; // How fast angle affects movement

    [Header("Vertical Wall Run Settings")]
    public bool enableVerticalWallRun = true;
    public float verticalWallRunBaseSpeed = 12f;
    public float verticalWallRunGravity = -12f; // Increased gravity
    public float verticalWallRunDuration = 2f;
    public float verticalWallRunJumpForce = 18f; // Increased jump force
    public float verticalWallRunAngleThreshold = 45f;
    public float verticalWallRunForwardSpeed = 8f;
    public float verticalWallRunCooldown = 0.5f;
    public float verticalWallRunMaxGainSpeed = 20f;
    public float verticalWallRunSpeedBoostUp = 3f;
    public float verticalWallRunSpeedBoostDown = 7f;
    public float verticalWallRunDecayRate = 2f; // Speed decay when climbing
    public float verticalMomentumTransfer = 0.5f; // Transfer speed to height on fall

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
    private Vector3 wallNormal;
    private Vector3 wallRunDirection;
    private bool wallRunSide;
    private bool hasWallRunBoosted;
    private float currentVerticalOffset; // For horizontal wall run height control
    private float targetVerticalOffset;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        originalHeight = controller.height;
        originalCenterY = controller.center.y;

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }

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
        // Update cooldown timers
        if (slideCooldownTimer > 0) slideCooldownTimer -= Time.deltaTime;
        if (wallRunCooldownTimer > 0) wallRunCooldownTimer -= Time.deltaTime;
        if (verticalWallRunCooldownTimer > 0) verticalWallRunCooldownTimer -= Time.deltaTime;

        // Ground check
        bool isGrounded = controller.isGrounded;

        // Reset slide count when grounded for a while
        if (isGrounded && Time.time - lastSlideTime > 1f)
        {
            slideCount = 0;
        }

        // Wall run check (only if not grounded, not sliding, and not on cooldown)
        if (enableWallRun && !isGrounded && !isSliding && !isWallRunning && wallRunCooldownTimer <= 0)
        {
            CheckForWallRun();
        }

        // Handle wall running
        if (isWallRunning)
        {
            UpdateWallRun();
        }

        // Reset velocity when grounded
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;

            if (hasMomentum && !isSliding && !wasJumpCancelled)
            {
                if (momentumSpeed <= walkSpeed)
                {
                    hasMomentum = false;
                    momentumSpeed = 0;
                }
                else if (enableDebugLogs)
                {
                    Debug.Log($"Momentum preserved on landing: {momentumSpeed:F1} m/s");
                }
            }

            // End wall run when grounded
            if (isWallRunning) EndWallRun();
            wasJumpCancelled = false;
            hasWallRunBoosted = false;
        }

        // Get input
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        bool hasMovementInput = (horizontal != 0 || vertical != 0);

        bool slideInput = Input.GetKey(KeyCode.LeftControl);
        bool jumpInput = Input.GetButtonDown("Jump");

        // Handle slide initiation
        if (!isSliding && slideInput && isGrounded && slideCooldownTimer <= 0 && hasMovementInput)
        {
            StartSlide(horizontal, vertical);
        }

        // Handle slide release
        if (isSliding && !slideInput) EndSlide();

        // Handle jump to cancel slide
        if (isSliding && jumpInput)
        {
            CancelSlideWithMomentum();
            wasJumpCancelled = true;
        }

        // Handle wall run jump
        if (isWallRunning && jumpInput) WallRunJump();

        // Smooth transitions for slide state
        if (isSliding)
        {
            UpdateSlide();

            float targetHeight = originalHeight * 0.6f;
            float targetCameraHeight = originalCameraHeight * 0.6f;

            currentSlideHeight = Mathf.Lerp(currentSlideHeight, targetHeight, Time.deltaTime * slideTransitionSpeed);
            currentCameraHeight = Mathf.Lerp(currentCameraHeight, targetCameraHeight, Time.deltaTime * slideTransitionSpeed);

            controller.height = currentSlideHeight;
            controller.center = new Vector3(0, currentSlideHeight * 0.5f, 0);

            if (playerCamera != null)
            {
                cameraTransform.localPosition = new Vector3(
                    cameraTransform.localPosition.x,
                    currentCameraHeight,
                    cameraTransform.localPosition.z
                );
            }
        }
        else if (!isWallRunning)
        {
            // Smooth transition back to normal
            currentSlideHeight = Mathf.Lerp(currentSlideHeight, originalHeight, Time.deltaTime * slideTransitionSpeed);
            currentCameraHeight = Mathf.Lerp(currentCameraHeight, originalCameraHeight, Time.deltaTime * slideTransitionSpeed);

            controller.height = currentSlideHeight;
            controller.center = new Vector3(0, currentSlideHeight * 0.5f, 0);

            if (playerCamera != null)
            {
                cameraTransform.localPosition = new Vector3(
                    cameraTransform.localPosition.x,
                    currentCameraHeight,
                    cameraTransform.localPosition.z
                );
            }

            // Movement logic with momentum priority
            if (hasMomentum && momentumSpeed > walkSpeed && !isSliding)
            {
                currentSpeed = momentumSpeed;

                if (isGrounded)
                {
                    momentumSpeed = Mathf.Max(momentumSpeed - (Time.deltaTime * deceleration * 2f), walkSpeed);
                    currentSpeed = momentumSpeed;
                }

                if (cameraTransform != null)
                {
                    if (hasMovementInput)
                    {
                        Vector3 forward = cameraTransform.forward;
                        Vector3 right = cameraTransform.right;
                        forward.y = 0;
                        right.y = 0;
                        forward.Normalize();
                        right.Normalize();

                        Vector3 inputDir = (forward * vertical) + (right * horizontal);
                        if (inputDir.magnitude > 0.1f)
                        {
                            momentumDirection = Vector3.Lerp(momentumDirection, inputDir.normalized, Time.deltaTime * 3f);
                        }
                    }

                    controller.Move(momentumDirection * currentSpeed * Time.deltaTime);
                    velocity.x = momentumDirection.x * currentSpeed;
                    velocity.z = momentumDirection.z * currentSpeed;
                }

                if (momentumSpeed <= walkSpeed && isGrounded)
                {
                    hasMomentum = false;
                }
            }
            else if (hasMovementInput)
            {
                isRunning = Input.GetKey(KeyCode.LeftShift) && hasMovementInput;
                float targetSpeed = isRunning ? runSpeed : walkSpeed;

                if (cameraTransform != null)
                {
                    Vector3 forward = cameraTransform.forward;
                    Vector3 right = cameraTransform.right;

                    forward.y = 0;
                    right.y = 0;
                    forward.Normalize();
                    right.Normalize();

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

        // Update camera FOV
        if (playerCamera != null)
        {
            float targetFOV = normalFOV;
            if (isSliding) targetFOV = slideFOV;
            if (isWallRunning) targetFOV = wallRunFOV;

            currentFOV = Mathf.Lerp(currentFOV, targetFOV, Time.deltaTime * fovTransitionSpeed);
            playerCamera.fieldOfView = currentFOV;
        }

        // Jump
        if (jumpInput && isGrounded && !isSliding && !isWallRunning)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (hasMomentum && momentumSpeed > walkSpeed && enableDebugLogs)
            {
                Debug.Log($"Jumped with momentum: {momentumSpeed:F1} m/s");
            }
        }

        // Apply gravity
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
        float currentHorizontalSpeed = new Vector3(velocity.x, 0, velocity.z).magnitude;

        // Check camera angle for vertical wall run
        float cameraAngle = Vector3.Angle(cameraTransform.forward, Vector3.up);
        bool lookingUp = cameraAngle < verticalWallRunAngleThreshold;

        // For vertical wall run, we need to check forward horizontally
        Vector3 horizontalForward = cameraTransform.forward;
        horizontalForward.y = 0;
        horizontalForward.Normalize();

        RaycastHit forwardHit;
        bool forwardWall = Physics.Raycast(transform.position, horizontalForward, out forwardHit, wallCheckDistance, wallLayer);

        Vector3 higherPosition = transform.position + Vector3.up * 0.5f;
        RaycastHit higherForwardHit;
        bool higherForwardWall = Physics.Raycast(higherPosition, horizontalForward, out higherForwardHit, wallCheckDistance, wallLayer);

        Vector3 leftDir = -cameraTransform.right;
        Vector3 rightDir = cameraTransform.right;
        leftDir.y = 0;
        rightDir.y = 0;
        leftDir.Normalize();
        rightDir.Normalize();

        RaycastHit leftHit, rightHit;
        bool leftWall = Physics.Raycast(transform.position, leftDir, out leftHit, wallCheckDistance, wallLayer);
        bool rightWall = Physics.Raycast(transform.position, rightDir, out rightHit, wallCheckDistance, wallLayer);

        RaycastHit forwardHorizontalHit;
        bool forwardHorizontalWall = Physics.Raycast(transform.position, horizontalForward, out forwardHorizontalHit, wallCheckDistance, wallLayer);

        if (enableDebugLogs)
        {
            Debug.Log($"Wall Run Check - LookingUp: {lookingUp} (Angle: {cameraAngle:F1}°), " +
                      $"ForwardWall: {forwardWall}, Speed: {currentHorizontalSpeed:F1}");
        }

        // Try vertical wall run first
        if (enableVerticalWallRun && lookingUp && vertical > 0 && (forwardWall || higherForwardWall))
        {
            Vector3 hitNormal = forwardWall ? forwardHit.normal : higherForwardHit.normal;
            if (enableDebugLogs) Debug.Log("ACTIVATING VERTICAL WALL RUN!");
            StartVerticalWallRun(hitNormal);
            return;
        }

        // Horizontal wall run
        if (vertical <= 0 || currentHorizontalSpeed < 2f) return;

        if (leftWall && !rightWall)
        {
            StartWallRun(leftHit.normal, true);
        }
        else if (rightWall && !leftWall)
        {
            StartWallRun(rightHit.normal, false);
        }
        else if (forwardHorizontalWall && !leftWall && !rightWall)
        {
            StartWallRun(forwardHorizontalHit.normal, false);
        }
    }

    void StartVerticalWallRun(Vector3 normal)
    {
        if (verticalWallRunCooldownTimer > 0)
        {
            if (enableDebugLogs) Debug.Log("Vertical Wall Run on cooldown!");
            return;
        }

        float timeSinceLastVertical = Time.time - lastVerticalWallRunTime;
        if (timeSinceLastVertical < 0.5f && lastVerticalWallRunTime > 0)
        {
            if (enableDebugLogs) Debug.Log("Too soon for another vertical wall run!");
            return;
        }

        isWallRunning = true;
        isVerticalWallRun = true;
        wallNormal = normal;
        wallRunTimer = verticalWallRunDuration;
        hasWallRunBoosted = false;

        Vector3 wallUp = Vector3.ProjectOnPlane(Vector3.up, normal).normalized;
        Vector3 wallForward = Vector3.ProjectOnPlane(cameraTransform.forward, normal).normalized;
        wallRunDirection = (wallUp * verticalWallRunBaseSpeed + wallForward * verticalWallRunForwardSpeed).normalized;

        float currentHorizontalSpeed = new Vector3(velocity.x, 0, velocity.z).magnitude;
        float currentVerticalSpeed = Mathf.Abs(velocity.y);
        float totalSpeed = Mathf.Sqrt(currentHorizontalSpeed * currentHorizontalSpeed + currentVerticalSpeed * currentVerticalSpeed);

        if (!hasWallRunBoosted)
        {
            hasWallRunBoosted = true;

            float targetSpeed;
            if (totalSpeed < verticalWallRunBaseSpeed)
            {
                targetSpeed = verticalWallRunBaseSpeed;
            }
            else if (totalSpeed < verticalWallRunMaxGainSpeed)
            {
                targetSpeed = Mathf.Min(totalSpeed + verticalWallRunSpeedBoostDown, verticalWallRunMaxGainSpeed);
            }
            else
            {
                targetSpeed = totalSpeed;
            }

            currentSpeed = targetSpeed;

            if (enableDebugLogs)
            {
                Debug.Log($"Vertical Wall Run Started! Speed: {totalSpeed:F1} -> {currentSpeed:F1} m/s");
            }
        }
        else
        {
            currentSpeed = totalSpeed;
        }

        velocity = wallRunDirection * currentSpeed;
        hasMomentum = true;
        momentumSpeed = currentSpeed;
        momentumDirection = wallRunDirection;
        lastVerticalWallRunTime = Time.time;
    }

    void StartWallRun(Vector3 normal, bool isLeft)
    {
        isWallRunning = true;
        isVerticalWallRun = false;
        wallRunSide = isLeft;
        wallNormal = normal;
        wallRunTimer = wallRunDuration;
        hasWallRunBoosted = false;
        currentVerticalOffset = 0;
        targetVerticalOffset = 0;

        Vector3 wallForward = Vector3.Cross(normal, Vector3.up);
        float dot = Vector3.Dot(wallForward, cameraTransform.forward);
        if (dot < 0) wallForward = -wallForward;
        wallRunDirection = wallForward.normalized;

        float currentHorizontalSpeed = new Vector3(velocity.x, 0, velocity.z).magnitude;

        if (!hasWallRunBoosted)
        {
            hasWallRunBoosted = true;

            if (currentHorizontalSpeed < minWallRunSpeed)
            {
                currentSpeed = minWallRunSpeed;
            }
            else if (currentHorizontalSpeed < wallRunSpeed)
            {
                currentSpeed = Mathf.Min(currentHorizontalSpeed + wallRunSpeedBoost, wallRunSpeed);
            }
            else
            {
                currentSpeed = currentHorizontalSpeed;
            }

            if (enableDebugLogs)
            {
                Debug.Log($"Horizontal Wall Run Started! Speed: {currentHorizontalSpeed:F1} -> {currentSpeed:F1} m/s");
            }
        }
        else
        {
            currentSpeed = currentHorizontalSpeed;
        }

        velocity.x = wallRunDirection.x * currentSpeed;
        velocity.z = wallRunDirection.z * currentSpeed;
        hasMomentum = true;
        momentumSpeed = currentSpeed;
        momentumDirection = wallRunDirection;
    }

    void UpdateWallRun()
    {
        wallRunTimer -= Time.deltaTime;

        // Get camera angle for horizontal wall run vertical movement
        float cameraAngle = Vector3.Angle(cameraTransform.forward, Vector3.up);
        float angleNormalized = Mathf.Clamp01(cameraAngle / 90f);

        if (!isVerticalWallRun)
        {
            // Horizontal wall run - angle controls vertical movement and speed
            float verticalSpeedModifier = 1f;
            float verticalMovement = 0f;

            if (cameraAngle < 45f) // Looking up
            {
                float upFactor = 1f - (cameraAngle / 45f);
                verticalMovement = lookUpVerticalGain * upFactor * Time.deltaTime;
                verticalSpeedModifier = 1f - (lookUpSpeedLoss / 10f) * upFactor;
                currentSpeed *= verticalSpeedModifier;

                if (enableDebugLogs && upFactor > 0.1f)
                {
                    Debug.Log($"Looking up - Gaining height: {verticalMovement:F2}, Speed loss: {(1f - verticalSpeedModifier) * 100:F0}%");
                }
            }
            else if (cameraAngle > 45f) // Looking down
            {
                float downFactor = (cameraAngle - 45f) / 45f;
                verticalMovement = -lookDownVerticalLoss * downFactor * Time.deltaTime;
                float speedGain = lookDownSpeedGain * downFactor * Time.deltaTime;
                currentSpeed = Mathf.Min(currentSpeed + speedGain, wallRunSpeed);

                if (enableDebugLogs && downFactor > 0.1f)
                {
                    Debug.Log($"Looking down - Losing height: {-verticalMovement:F2}, Speed gain: {speedGain:F2}");
                }
            }

            // Apply vertical movement
            Vector3 verticalMove = wallNormal * verticalMovement;
            controller.Move(verticalMove);
            velocity += verticalMove / Time.deltaTime;
        }
        else
        {
            // Vertical wall run - apply speed decay
            float speedDecay = verticalWallRunDecayRate * Time.deltaTime;
            currentSpeed = Mathf.Max(currentSpeed - speedDecay, verticalWallRunBaseSpeed * 0.5f);

            if (enableDebugLogs)
            {
                Debug.Log($"Vertical wall run - Speed decay: {currentSpeed:F1} m/s");
            }
        }

        // Check if still next to wall
        Vector3 checkDir;
        if (isVerticalWallRun)
        {
            checkDir = cameraTransform.forward;
            checkDir.y = 0;
            checkDir.Normalize();
        }
        else
        {
            checkDir = wallRunSide ? -cameraTransform.right : cameraTransform.right;
        }

        RaycastHit hit;
        bool stillNextToWall = Physics.Raycast(transform.position, checkDir, out hit, wallCheckDistance + 0.2f, wallLayer);

        if (isVerticalWallRun && !stillNextToWall)
        {
            Vector3 higherPos = transform.position + Vector3.up * 0.5f;
            stillNextToWall = Physics.Raycast(higherPos, checkDir, out hit, wallCheckDistance + 0.2f, wallLayer);
        }

        if (isVerticalWallRun && (transform.position.y > 20f || transform.position.y < 2f))
        {
            stillNextToWall = false;
        }

        if (!stillNextToWall || wallRunTimer <= 0)
        {
            EndWallRun();
            return;
        }

        // Update directions
        if (isVerticalWallRun)
        {
            wallNormal = hit.normal;
            Vector3 wallUp = Vector3.ProjectOnPlane(Vector3.up, wallNormal).normalized;
            Vector3 wallForward = Vector3.ProjectOnPlane(cameraTransform.forward, wallNormal).normalized;
            wallRunDirection = (wallUp * verticalWallRunBaseSpeed + wallForward * verticalWallRunForwardSpeed).normalized;
        }

        Vector3 moveAlongWall = wallRunDirection * currentSpeed;
        controller.Move(moveAlongWall * Time.deltaTime);
        velocity = moveAlongWall;

        momentumSpeed = currentSpeed;
        momentumDirection = moveAlongWall.normalized;

        if (enableDebugLogs)
        {
            Debug.DrawRay(transform.position, wallRunDirection * 2f, isVerticalWallRun ? Color.green : Color.cyan);
        }
    }

    void WallRunJump()
    {
        Vector3 jumpDirection;
        float jumpForce;
        float momentumTransfer = 0f;

        if (isVerticalWallRun)
        {
            // On jump: gain extra momentum
            jumpDirection = (wallNormal + Vector3.up).normalized;
            jumpForce = verticalWallRunJumpForce;

            // Transfer current speed to jump height
            momentumTransfer = currentSpeed * verticalMomentumTransfer;
            jumpForce += momentumTransfer;

            if (enableDebugLogs)
            {
                Debug.Log($"Vertical Wall Run Jump! Speed: {currentSpeed:F1} -> Jump force: {jumpForce:F1} (+{momentumTransfer:F1} from momentum)");
            }
        }
        else
        {
            // On jump: normal jump
            jumpDirection = wallNormal + Vector3.up;
            jumpDirection.Normalize();
            jumpForce = wallRunJumpForce;

            if (enableDebugLogs)
            {
                Debug.Log($"Horizontal Wall Run Jump! Speed: {currentSpeed:F1} m/s");
            }
        }

        velocity = jumpDirection * jumpForce;

        hasMomentum = true;
        momentumSpeed = currentSpeed;
        momentumDirection = wallRunDirection;

        EndWallRun();
        wallRunCooldownTimer = wallRunCooldown;
    }

    void EndWallRun()
    {
        if (isWallRunning && !isVerticalWallRun && !Input.GetButtonDown("Jump"))
        {
            // If not jumping off, transfer some horizontal speed to vertical height
            float verticalTransfer = currentSpeed * verticalMomentumTransfer * 0.3f;
            velocity.y += verticalTransfer;

            if (enableDebugLogs && verticalTransfer > 0.1f)
            {
                Debug.Log($"Wall Run ended - Transferred {verticalTransfer:F1} m/s to vertical height");
            }
        }

        isWallRunning = false;

        if (isVerticalWallRun)
        {
            verticalWallRunCooldownTimer = verticalWallRunCooldown;
            if (enableDebugLogs) Debug.Log($"Vertical Wall Run Ended - Speed preserved: {currentSpeed:F1} m/s");
        }

        isVerticalWallRun = false;
        hasWallRunBoosted = false;
        if (enableDebugLogs) Debug.Log("Wall Run Ended");
    }

    void StartSlide(float horizontal, float vertical)
    {
        isSliding = true;
        slideTimer = slideDuration;
        wasJumpCancelled = false;

        slideCount++;
        float slidePenalty = Mathf.Max(0, 1f - (slideCount - 1) * slideDecayRate);

        float currentHorizontalSpeed = hasMomentum && momentumSpeed > currentSpeed ?
            momentumSpeed : new Vector3(velocity.x, 0, velocity.z).magnitude;

        if (cameraTransform != null)
        {
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;

            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();

            slideDirection = (forward * vertical) + (right * horizontal);

            if (slideDirection.magnitude < 0.1f)
            {
                slideDirection = forward;
            }

            slideDirection.Normalize();
        }

        float slideStartSpeed;
        if (currentHorizontalSpeed < maxSlideGainSpeed)
        {
            slideStartSpeed = Mathf.Min(currentHorizontalSpeed + (slideBoost * slidePenalty), maxSlideGainSpeed);
        }
        else
        {
            slideStartSpeed = currentHorizontalSpeed;
        }

        hasMomentum = true;
        momentumSpeed = slideStartSpeed;
        momentumDirection = slideDirection;

        velocity = new Vector3(slideDirection.x, velocity.y, slideDirection.z) * slideStartSpeed;
        currentSpeed = slideStartSpeed;
        moveDirection = slideDirection;

        currentSlideHeight = controller.height;
        currentCameraHeight = cameraTransform != null ? cameraTransform.localPosition.y : originalCameraHeight;

        if (enableDebugLogs)
        {
            Debug.Log($"Slide #{slideCount} started! Speed: {currentHorizontalSpeed:F1} -> {slideStartSpeed:F1} m/s (Penalty: {(1f - slidePenalty) * 100:F0}%)");
        }
    }

    void UpdateSlide()
    {
        slideTimer -= Time.deltaTime;

        float t = 1 - (slideTimer / slideDuration);
        float currentSlideSpeed;

        if (t < 0.6f)
        {
            currentSlideSpeed = momentumSpeed;
        }
        else
        {
            float slowdownT = (t - 0.6f) / 0.4f;
            currentSlideSpeed = Mathf.Lerp(momentumSpeed, walkSpeed * 1.2f, slowdownT);
        }

        currentSpeed = currentSlideSpeed;
        momentumSpeed = currentSlideSpeed;

        controller.Move(slideDirection * currentSlideSpeed * Time.deltaTime);

        velocity.x = slideDirection.x * currentSlideSpeed;
        velocity.z = slideDirection.z * currentSlideSpeed;

        if (slideTimer <= 0) EndSlide();
    }

    void CancelSlideWithMomentum()
    {
        float currentSpeedValue = new Vector3(velocity.x, 0, velocity.z).magnitude;

        velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        hasMomentum = true;
        momentumSpeed = currentSpeedValue;
        momentumDirection = slideDirection;

        isSliding = false;
        slideCooldownTimer = slideCooldown;
        lastSlideTime = Time.time;

        controller.height = originalHeight;
        controller.center = new Vector3(0, originalCenterY, 0);

        if (playerCamera != null)
        {
            cameraTransform.localPosition = new Vector3(
                cameraTransform.localPosition.x,
                originalCameraHeight,
                cameraTransform.localPosition.z
            );
        }

        if (enableDebugLogs) Debug.Log($"Slide cancelled! Momentum: {momentumSpeed:F1} m/s");
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
        {
            cameraTransform.localPosition = new Vector3(
                cameraTransform.localPosition.x,
                originalCameraHeight,
                cameraTransform.localPosition.z
            );
        }
    }

    public bool IsRunning() => isRunning;
    public bool IsSliding() => isSliding;
    public bool IsWallRunning() => isWallRunning;
    public bool IsVerticalWallRunning() => isVerticalWallRun;
    public float GetCurrentVelocity() => new Vector3(velocity.x, 0, velocity.z).magnitude;
}
