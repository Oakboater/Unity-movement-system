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
    public float wallRunSpeed = 15f;
    public float wallRunGravity = -2f;
    public float wallRunJumpForce = 8f;
    public float wallRunDuration = 2f;
    public float wallRunCooldown = 0.5f;
    public LayerMask wallLayer;
    public float wallCheckDistance = 1f;
    public float wallRunFOV = 80f;

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
    private bool isWallRunningLeft;
    private bool isWallRunningRight;
    private float wallRunTimer;
    private float wallRunCooldownTimer;
    private Vector3 wallNormal;
    private Vector3 wallRunDirection;

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
        if (slideCooldownTimer > 0)
        {
            slideCooldownTimer -= Time.deltaTime;
        }

        if (wallRunCooldownTimer > 0)
        {
            wallRunCooldownTimer -= Time.deltaTime;
        }

        // Ground check
        bool isGrounded = controller.isGrounded;

        // Wall run check
        if (enableWallRun && !isSliding && !isGrounded && wallRunCooldownTimer <= 0 && !isWallRunning)
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

            // Only clear momentum if we're going slow and didn't jump cancel
            if (hasMomentum && !isSliding && !wasJumpCancelled)
            {
                if (momentumSpeed <= walkSpeed)
                {
                    hasMomentum = false;
                    momentumSpeed = 0;
                    Debug.Log("Momentum cleared on landing (slow)");
                }
                else
                {
                    Debug.Log($"Momentum preserved on landing: {momentumSpeed:F1} m/s");
                }
            }

            // End wall run when grounded
            if (isWallRunning)
            {
                EndWallRun();
            }

            wasJumpCancelled = false;
        }

        // Get input
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        bool hasMovementInput = (horizontal != 0 || vertical != 0);

        // Slide input
        bool slideInput = Input.GetKey(KeyCode.LeftControl);
        bool jumpInput = Input.GetButtonDown("Jump");

        // Handle slide initiation
        if (!isSliding && slideInput && isGrounded && slideCooldownTimer <= 0 && hasMovementInput)
        {
            StartSlide(horizontal, vertical);
        }

        // Handle slide release
        if (isSliding && !slideInput)
        {
            EndSlide();
        }

        // Handle jump to cancel slide (preserve momentum)
        if (isSliding && jumpInput)
        {
            CancelSlideWithMomentum();
            wasJumpCancelled = true;
        }

        // Handle wall run jump
        if (isWallRunning && jumpInput)
        {
            WallRunJump();
        }

        // Smooth transitions for slide state
        if (isSliding)
        {
            UpdateSlide();

            // Smooth height transitions
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

            // Movement logic with momentum priority (only when not wall running)
            if (hasMomentum && momentumSpeed > walkSpeed && !isSliding)
            {
                // Use momentum for movement - NO DECAY IN AIR!
                currentSpeed = momentumSpeed;

                // ONLY decay momentum if on ground (friction)
                if (isGrounded)
                {
                    momentumSpeed = Mathf.Max(momentumSpeed - (Time.deltaTime * 5f), walkSpeed);
                    currentSpeed = momentumSpeed;
                }

                if (cameraTransform != null)
                {
                    // Allow slight steering while in momentum
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

                // Stop momentum when speed drops below walk speed AND grounded
                if (momentumSpeed <= walkSpeed && isGrounded)
                {
                    hasMomentum = false;
                    Debug.Log("Momentum faded out on ground");
                }
            }
            else if (hasMovementInput)
            {
                // Normal movement
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
                // No input - decelerate
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

        // Jump - preserve momentum
        if (jumpInput && isGrounded && !isSliding && !isWallRunning)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

            if (hasMomentum && momentumSpeed > walkSpeed)
            {
                Debug.Log($"Jumped with momentum: {momentumSpeed:F1} m/s - Speed will be preserved in air!");
            }
        }

        // Apply gravity (different when wall running)
        if (!isWallRunning)
        {
            velocity.y += gravity * Time.deltaTime;
        }
        else
        {
            velocity.y += wallRunGravity * Time.deltaTime;
        }

        controller.Move(velocity * Time.deltaTime);

        // Debug momentum in air
        if (Time.frameCount % 60 == 0 && hasMomentum && !isGrounded && !isWallRunning)
        {
            Debug.Log($"Momentum in air: {momentumSpeed:F1} m/s - NO DECAY!");
        }
    }

    void CheckForWallRun()
    {
        // Check for walls on left and right
        Vector3 leftDir = -cameraTransform.right;
        Vector3 rightDir = cameraTransform.right;

        RaycastHit leftHit, rightHit;
        bool leftWall = Physics.Raycast(transform.position, leftDir, out leftHit, wallCheckDistance, wallLayer);
        bool rightWall = Physics.Raycast(transform.position, rightDir, out rightHit, wallCheckDistance, wallLayer);

        // Only wall run if moving forward
        float vertical = Input.GetAxisRaw("Vertical");

        if (vertical > 0)
        {
            if (leftWall && !rightWall)
            {
                StartWallRun(leftHit.normal, true);
            }
            else if (rightWall && !leftWall)
            {
                StartWallRun(rightHit.normal, false);
            }
        }
    }

    void StartWallRun(Vector3 normal, bool isLeft)
    {
        isWallRunning = true;
        isWallRunningLeft = isLeft;
        isWallRunningRight = !isLeft;
        wallNormal = normal;
        wallRunTimer = wallRunDuration;

        // Calculate wall run direction (along the wall)
        Vector3 wallForward = Vector3.Cross(normal, Vector3.up);
        wallRunDirection = wallForward;

        // Apply wall run speed
        currentSpeed = wallRunSpeed;
        hasMomentum = true;
        momentumSpeed = wallRunSpeed;
        momentumDirection = wallRunDirection;

        // Apply slight push away from wall
        Vector3 pushAway = normal * 0.5f;
        controller.Move(pushAway);

        // Tilt camera slightly for wall run effect
        if (playerCamera != null)
        {
            // Optional: Add camera tilt effect
        }

        Debug.Log($"Wall run started! Direction: {(isLeft ? "Left" : "Right")}");
    }

    void UpdateWallRun()
    {
        // Update timer
        wallRunTimer -= Time.deltaTime;

        // Check if still next to wall
        Vector3 checkDir = isWallRunningLeft ? -cameraTransform.right : cameraTransform.right;
        RaycastHit hit;
        bool stillNextToWall = Physics.Raycast(transform.position, checkDir, out hit, wallCheckDistance + 0.2f, wallLayer);

        if (!stillNextToWall || wallRunTimer <= 0)
        {
            EndWallRun();
            return;
        }

        // Move along the wall
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 moveAlongWall = wallRunDirection * currentSpeed;

        // Allow slight control
        if (vertical != 0)
        {
            moveAlongWall = wallRunDirection * currentSpeed * Mathf.Abs(vertical);
        }

        controller.Move(moveAlongWall * Time.deltaTime);

        // Update velocity
        velocity.x = moveAlongWall.x;
        velocity.z = moveAlongWall.z;

        // Update momentum
        momentumSpeed = currentSpeed;
        momentumDirection = moveAlongWall.normalized;
    }

    void WallRunJump()
    {
        // Jump away from wall
        Vector3 jumpDirection = wallNormal + Vector3.up;
        jumpDirection.Normalize();

        velocity = jumpDirection * wallRunJumpForce;

        // Preserve horizontal momentum
        hasMomentum = true;
        momentumSpeed = wallRunSpeed;
        momentumDirection = wallRunDirection;

        // End wall run
        EndWallRun();
        wallRunCooldownTimer = wallRunCooldown;

        Debug.Log($"Wall run jump! Speed: {wallRunSpeed:F1} m/s");
    }

    void EndWallRun()
    {
        isWallRunning = false;
        isWallRunningLeft = false;
        isWallRunningRight = false;

        // Keep momentum after wall run
        if (currentSpeed > walkSpeed)
        {
            hasMomentum = true;
            momentumSpeed = currentSpeed;
            momentumDirection = wallRunDirection;
        }

        Debug.Log("Wall run ended");
    }

    void StartSlide(float horizontal, float vertical)
    {
        isSliding = true;
        slideTimer = slideDuration;
        wasJumpCancelled = false;

        // Get current speed (either from momentum or normal movement)
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

        // Calculate slide speed: current speed + boost
        float slideStartSpeed = currentHorizontalSpeed + slideBoost;

        // Store as momentum
        hasMomentum = true;
        momentumSpeed = slideStartSpeed;
        momentumDirection = slideDirection;

        // Apply slide velocity
        velocity = new Vector3(slideDirection.x, velocity.y, slideDirection.z) * slideStartSpeed;
        currentSpeed = slideStartSpeed;
        moveDirection = slideDirection;

        currentSlideHeight = controller.height;
        currentCameraHeight = cameraTransform != null ? cameraTransform.localPosition.y : originalCameraHeight;

        Debug.Log($"Slide started! Speed: {slideStartSpeed:F1} m/s (Boost: +{slideBoost})");
    }

    void UpdateSlide()
    {
        // Update slide timer
        slideTimer -= Time.deltaTime;

        // Keep speed high for longer, then gradually decrease
        float t = 1 - (slideTimer / slideDuration);
        float currentSlideSpeed;

        if (t < 0.6f)
        {
            // First 60% of slide: maintain high speed
            currentSlideSpeed = momentumSpeed;
        }
        else
        {
            // Last 40%: gradually slow down
            float slowdownT = (t - 0.6f) / 0.4f;
            currentSlideSpeed = Mathf.Lerp(momentumSpeed, walkSpeed * 1.2f, slowdownT);
        }

        currentSpeed = currentSlideSpeed;
        momentumSpeed = currentSlideSpeed;

        // Apply slide movement
        controller.Move(slideDirection * currentSlideSpeed * Time.deltaTime);

        // Update velocity
        velocity.x = slideDirection.x * currentSlideSpeed;
        velocity.z = slideDirection.z * currentSlideSpeed;

        // End slide if timer runs out
        if (slideTimer <= 0)
        {
            EndSlide();
        }
    }

    void CancelSlideWithMomentum()
    {
        // Preserve current speed as momentum
        float currentSpeedValue = new Vector3(velocity.x, 0, velocity.z).magnitude;

        // Apply jump
        velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        // Keep momentum with NO DECAY
        hasMomentum = true;
        momentumSpeed = currentSpeedValue;
        momentumDirection = slideDirection;

        // End slide
        isSliding = false;
        slideCooldownTimer = slideCooldown;

        // Restore height
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

       // Debug.Log($"Slide cancelled with jump! Momentum preserved: {momentumSpeed:F1} m/s - Will NOT decay in air!"); - ADD FOR DEBUGGING
    }

    void EndSlide()
    {
        isSliding = false;
        slideCooldownTimer = slideCooldown;

        // Keep momentum when ending slide naturally
        float currentSlideSpeed = new Vector3(velocity.x, 0, velocity.z).magnitude;

        if (currentSlideSpeed > walkSpeed)
        {
            hasMomentum = true;
            momentumSpeed = currentSlideSpeed;
            momentumDirection = slideDirection;
            currentSpeed = currentSlideSpeed;
            moveDirection = slideDirection;
            Debug.Log($"Slide ended with momentum: {momentumSpeed:F1} m/s");
        }

        // Restore height
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

    public bool IsRunning()
    {
        return isRunning;
    }

    public bool IsSliding()
    {
        return isSliding;
    }

    public bool IsWallRunning()
    {
        return isWallRunning;
    }

    public float GetCurrentVelocity()
    {
        return new Vector3(velocity.x, 0, velocity.z).magnitude;
    }
}
