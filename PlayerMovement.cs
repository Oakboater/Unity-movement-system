using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float runSpeed = 8f;

    [Header("Slide Settings")]
    public float slideDuration = 1.25f;
    public float slideCooldown = 0.5f;
    public float slideBoost = 3f;

    [Header("Camera Settings")]
    public Camera playerCamera;
    public float normalFOV = 60f;
    public float slideFOV = 70f;
    public float fovTransitionSpeed = 8f;

    [Header("Gravity")]
    public float gravity = -9.81f;

    [Header("Jump Settings")]
    public float jumpHeight = 1.5f;

    private CharacterController controller;
    private Vector3 velocity;
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

    void Start()
    {
        controller = GetComponent<CharacterController>();

        // Store original capsule height and center
        originalHeight = controller.height;
        originalCenterY = controller.center.y;

        // Find camera if not assigned
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
        }

        Debug.Log("PlayerMovement initialized");
    }

    void Update()
    {
        // Update cooldown timer
        if (slideCooldownTimer > 0)
        {
            slideCooldownTimer -= Time.deltaTime;
        }

        // Simple ground check
        bool isGrounded = controller.isGrounded;

        // Reset velocity when grounded
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        // Get input
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        bool hasMovementInput = (horizontal != 0 || vertical != 0);

        // Slide input (hold Left Control)
        bool slideInput = Input.GetKey(KeyCode.LeftControl);
        bool jumpInput = Input.GetButtonDown("Jump");

        // Handle slide initiation
        if (!isSliding && slideInput && isGrounded && slideCooldownTimer <= 0 && hasMovementInput)
        {
            StartSlide(horizontal, vertical);
        }

        // Handle jump to cancel slide
        if (isSliding && jumpInput)
        {
            CancelSlide();
        }

        // Handle slide logic
        if (isSliding)
        {
            UpdateSlide();
        }
        else
        {
            // Normal movement - only move if there's input
            if (hasMovementInput)
            {
                isRunning = Input.GetKey(KeyCode.LeftShift) && hasMovementInput;
                currentSpeed = isRunning ? runSpeed : walkSpeed;

                // Movement relative to camera
                if (cameraTransform != null)
                {
                    Vector3 forward = cameraTransform.forward;
                    Vector3 right = cameraTransform.right;

                    forward.y = 0;
                    right.y = 0;
                    forward.Normalize();
                    right.Normalize();

                    Vector3 moveDirection = (forward * vertical) + (right * horizontal);
                    controller.Move(moveDirection * currentSpeed * Time.deltaTime);

                    // Update velocity for physics
                    velocity.x = moveDirection.x * currentSpeed;
                    velocity.z = moveDirection.z * currentSpeed;
                }
            }
            else
            {
                // No input - stop moving horizontally
                velocity.x = 0;
                velocity.z = 0;
            }
        }

        // Update camera FOV smoothly
        if (playerCamera != null)
        {
            float targetFOV = isSliding ? slideFOV : normalFOV;
            currentFOV = Mathf.Lerp(currentFOV, targetFOV, Time.deltaTime * fovTransitionSpeed);
            playerCamera.fieldOfView = currentFOV;
        }

        // Jump
        if (jumpInput && isGrounded && !isSliding)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // Apply gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    void StartSlide(float horizontal, float vertical)
    {
        isSliding = true;
        slideTimer = slideDuration;

        // Get current movement direction and speed
        float currentHorizontalSpeed = new Vector3(velocity.x, 0, velocity.z).magnitude;

        if (cameraTransform != null)
        {
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;

            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();

            slideDirection = (forward * vertical) + (right * horizontal);
            slideDirection.Normalize();
        }

        // Calculate slide speed: initial momentum + boost
        float initialSpeed = Mathf.Max(currentHorizontalSpeed, walkSpeed);
        float slideStartSpeed = initialSpeed + slideBoost;

        // Apply slide velocity
        velocity = new Vector3(slideDirection.x, velocity.y, slideDirection.z) * slideStartSpeed;

        // Reduce character controller height for sliding
        controller.height = originalHeight * 0.6f;
        controller.center = new Vector3(0, originalCenterY * 0.6f, 0);

        // Lower camera position for sliding
        if (playerCamera != null)
        {
            cameraTransform.localPosition = new Vector3(
                cameraTransform.localPosition.x,
                originalCameraHeight * 0.6f,
                cameraTransform.localPosition.z
            );
        }

        Debug.Log($"Slide started! Speed: {slideStartSpeed}");
    }

    void UpdateSlide()
    {
        // Update slide timer
        slideTimer -= Time.deltaTime;

        // Calculate current slide speed (smooth deceleration)
        float t = 1 - (slideTimer / slideDuration);
        float currentSlideSpeed = Mathf.Lerp(walkSpeed + slideBoost, walkSpeed * 0.8f, t);

        // Apply slide movement
        controller.Move(slideDirection * currentSlideSpeed * Time.deltaTime);

        // Update velocity for physics
        velocity.x = slideDirection.x * currentSlideSpeed;
        velocity.z = slideDirection.z * currentSlideSpeed;

        // End slide if timer runs out
        if (slideTimer <= 0)
        {
            EndSlide();
        }
    }

    void CancelSlide()
    {
        // Jump cancel - preserve current speed
        float currentSpeed = new Vector3(velocity.x, 0, velocity.z).magnitude;

        // Apply jump
        velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        // End slide
        isSliding = false;
        slideCooldownTimer = slideCooldown;

        // Restore character controller height
        controller.height = originalHeight;
        controller.center = new Vector3(0, originalCenterY, 0);

        // Restore camera position
        if (playerCamera != null)
        {
            cameraTransform.localPosition = new Vector3(
                cameraTransform.localPosition.x,
                originalCameraHeight,
                cameraTransform.localPosition.z
            );
        }

        Debug.Log($"Slide cancelled with jump! Speed: {currentSpeed}");
    }

    void EndSlide()
    {
        isSliding = false;
        slideCooldownTimer = slideCooldown;

        // Get current slide speed
        float currentSlideSpeed = new Vector3(velocity.x, 0, velocity.z).magnitude;

        // Restore character controller height
        controller.height = originalHeight;
        controller.center = new Vector3(0, originalCenterY, 0);

        // Restore camera position
        if (playerCamera != null)
        {
            cameraTransform.localPosition = new Vector3(
                cameraTransform.localPosition.x,
                originalCameraHeight,
                cameraTransform.localPosition.z
            );
        }

        // Set velocity to a reasonable speed, but not too fast
        float endSpeed = Mathf.Min(currentSlideSpeed, walkSpeed * 1.2f);
        velocity.x = slideDirection.x * endSpeed;
        velocity.z = slideDirection.z * endSpeed;

        Debug.Log($"Slide ended! Speed: {endSpeed}");
    }

    public bool IsRunning()
    {
        return isRunning;
    }

    public bool IsSliding()
    {
        return isSliding;
    }
}
