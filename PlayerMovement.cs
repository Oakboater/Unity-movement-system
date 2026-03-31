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

    // Momentum system
    private bool hasMomentum;
    private float momentumSpeed;
    private Vector3 momentumDirection;
    private bool wasJumpCancelled;

    // Wall run system
    private bool isWallRunning;
    private float wallRunTimer;
    private float wallRunCooldownTimer;
    private Vector3 wallNormal;
    private Vector3 wallRunDirection;
    private bool wallRunSide; // true = left, false = right

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

        Debug.Log("PlayerMovement initialized");
    }

    void Update()
    {
        // Update cooldown timers
        if (slideCooldownTimer > 0) slideCooldownTimer -= Time.deltaTime;
        if (wallRunCooldownTimer > 0) wallRunCooldownTimer -= Time.deltaTime;

        // Ground check
        bool isGrounded = controller.isGrounded;

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
                else
                {
                    Debug.Log($"Momentum preserved on landing: {momentumSpeed:F1} m/s");
                }
            }

            // End wall run when grounded
            if (isWallRunning) EndWallRun();
            wasJumpCancelled = false;
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
                    momentumSpeed = Mathf.Max(momentumSpeed - (Time.deltaTime * 5f), walkSpeed);
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
            if (hasMomentum && momentumSpeed > walkSpeed)
            {
                Debug.Log($"Jumped with momentum: {momentumSpeed:F1} m/s");
            }
        }

        // Apply gravity
        if (!isWallRunning)
            velocity.y += gravity * Time.deltaTime;
        else
            velocity.y += wallRunGravity * Time.deltaTime;

        controller.Move(velocity * Time.deltaTime);
    }

    void CheckForWallRun()
    {
        // Only wall run if moving forward
        float vertical = Input.GetAxisRaw("Vertical");
        if (vertical <= 0) return;

        // Check for walls on left and right
        Vector3 leftDir = -cameraTransform.right;
        Vector3 rightDir = cameraTransform.right;

        RaycastHit leftHit, rightHit;
        bool leftWall = Physics.Raycast(transform.position, leftDir, out leftHit, wallCheckDistance, wallLayer);
        bool rightWall = Physics.Raycast(transform.position, rightDir, out rightHit, wallCheckDistance, wallLayer);

        // Also check forward for better detection
        RaycastHit forwardHit;
        bool forwardWall = Physics.Raycast(transform.position, cameraTransform.forward, out forwardHit, wallCheckDistance, wallLayer);

        if (leftWall && !rightWall)
        {
            StartWallRun(leftHit.normal, true);
        }
        else if (rightWall && !leftWall)
        {
            StartWallRun(rightHit.normal, false);
        }
        else if (forwardWall && !leftWall && !rightWall)
        {
            // If only forward wall, use that
            StartWallRun(forwardHit.normal, false);
        }
    }

    void StartWallRun(Vector3 normal, bool isLeft)
    {
        isWallRunning = true;
        wallRunSide = isLeft;
        wallNormal = normal;
        wallRunTimer = wallRunDuration;

        // Calculate wall run direction (forward along the wall)
        Vector3 wallForward = Vector3.Cross(normal, Vector3.up);

        // Determine direction based on camera facing
        float dot = Vector3.Dot(wallForward, cameraTransform.forward);
        if (dot < 0) wallForward = -wallForward;

        wallRunDirection = wallForward.normalized;

        // Set speed from momentum or default
        float currentHorizontalSpeed = new Vector3(velocity.x, 0, velocity.z).magnitude;
        currentSpeed = Mathf.Max(currentHorizontalSpeed, wallRunSpeed);

        // Apply velocity along wall
        velocity.x = wallRunDirection.x * currentSpeed;
        velocity.z = wallRunDirection.z * currentSpeed;

        // Store momentum
        hasMomentum = true;
        momentumSpeed = currentSpeed;
        momentumDirection = wallRunDirection;

        Debug.Log($"Wall Run Started! Speed: {currentSpeed:F1} m/s");
    }

    void UpdateWallRun()
    {
        // Update timer
        wallRunTimer -= Time.deltaTime;

        // Check if still next to wall
        Vector3 checkDir = wallRunSide ? -cameraTransform.right : cameraTransform.right;
        RaycastHit hit;
        bool stillNextToWall = Physics.Raycast(transform.position, checkDir, out hit, wallCheckDistance + 0.2f, wallLayer);

        if (!stillNextToWall || wallRunTimer <= 0)
        {
            EndWallRun();
            return;
        }

        // Get input for movement along wall
        float vertical = Input.GetAxisRaw("Vertical");
        float horizontal = Input.GetAxisRaw("Horizontal");

        // Move along wall direction
        Vector3 moveAlongWall = wallRunDirection * currentSpeed;

        // Allow slight steering
        if (vertical != 0)
        {
            moveAlongWall = wallRunDirection * currentSpeed;
        }

        // Apply movement
        controller.Move(moveAlongWall * Time.deltaTime);

        // Update velocity
        velocity.x = moveAlongWall.x;
        velocity.z = moveAlongWall.z;

        // Keep momentum
        momentumSpeed = currentSpeed;
        momentumDirection = moveAlongWall.normalized;

        // Visual debug
        Debug.DrawRay(transform.position, wallRunDirection * 2f, Color.cyan);
    }

    void WallRunJump()
    {
        // Jump away from wall
        Vector3 jumpDirection = wallNormal + Vector3.up;
        jumpDirection.Normalize();

        // Apply jump force
        velocity = jumpDirection * wallRunJumpForce;

        // Preserve momentum for air movement
        hasMomentum = true;
        momentumSpeed = currentSpeed;
        momentumDirection = wallRunDirection;

        // End wall run
        EndWallRun();
        wallRunCooldownTimer = wallRunCooldown;

        Debug.Log($"Wall Run Jump! Speed: {momentumSpeed:F1} m/s");
    }

    void EndWallRun()
    {
        isWallRunning = false;
        Debug.Log("Wall Run Ended");
    }

    void StartSlide(float horizontal, float vertical)
    {
        isSliding = true;
        slideTimer = slideDuration;
        wasJumpCancelled = false;

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

        float slideStartSpeed = currentHorizontalSpeed + slideBoost;

        hasMomentum = true;
        momentumSpeed = slideStartSpeed;
        momentumDirection = slideDirection;

        velocity = new Vector3(slideDirection.x, velocity.y, slideDirection.z) * slideStartSpeed;
        currentSpeed = slideStartSpeed;
        moveDirection = slideDirection;

        currentSlideHeight = controller.height;
        currentCameraHeight = cameraTransform != null ? cameraTransform.localPosition.y : originalCameraHeight;

        Debug.Log($"Slide started! Speed: {slideStartSpeed:F1} m/s");
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

        Debug.Log($"Slide cancelled! Momentum: {momentumSpeed:F1} m/s");
    }

    void EndSlide()
    {
        isSliding = false;
        slideCooldownTimer = slideCooldown;

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
    public float GetCurrentVelocity() => new Vector3(velocity.x, 0, velocity.z).magnitude;
}
