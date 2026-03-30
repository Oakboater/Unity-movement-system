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
    public float slideTransitionSpeed = 10f; // Smooth transition into slide

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
    private float currentSlideHeight;
    private float currentCameraHeight;
    private float slideStartTime;

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
            currentCameraHeight = originalCameraHeight;
        }

        currentSlideHeight = originalHeight;

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

        // Slide input - more responsive
        bool slideInput = Input.GetKey(KeyCode.LeftControl);
        bool slideJustPressed = Input.GetKeyDown(KeyCode.LeftControl);
        bool jumpInput = Input.GetButtonDown("Jump");

        // Handle slide initiation - immediate but smooth
        if (!isSliding && slideInput && isGrounded && slideCooldownTimer <= 0 && hasMovementInput)
        {
            if (slideJustPressed || !isSliding)
            {
                StartSlide(horizontal, vertical);
            }
        }

        // Handle slide release (optional - can make slide stop when releasing key)
        if (isSliding && !slideInput)
        {
            EndSlide();
        }

        // Handle jump to cancel slide
        if (isSliding && jumpInput)
        {
            CancelSlide();
        }

        // Smooth transitions for slide state
        if (isSliding)
        {
            UpdateSlide();

            // Smoothly transition height and camera position
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
        else
        {
            // Smoothly transition back to normal
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
        slideStartTime = Time.time;

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

            // If there's no input, slide forward
            if (slideDirection.magnitude < 0.1f)
            {
                slideDirection = forward;
            }

            slideDirection.Normalize();
        }

        // Calculate slide speed with boost
        float initialSpeed = Mathf.Max(currentHorizontalSpeed, walkSpeed);
        float slideStartSpeed = initialSpeed + slideBoost;

        // Apply slide velocity with smooth start
        velocity = new Vector3(slideDirection.x, velocity.y, slideDirection.z) * slideStartSpeed;

        // Store current speeds for smooth transition
        currentSlideHeight = controller.height;
        currentCameraHeight = cameraTransform != null ? cameraTransform.localPosition.y : originalCameraHeight;

        Debug.Log($"Slide started smoothly! Speed: {slideStartSpeed}");
    }

    void UpdateSlide()
    {
        // Update slide timer
        slideTimer -= Time.deltaTime;

        // Smooth deceleration curve (ease out)
        float t = 1 - (slideTimer / slideDuration);
        float smoothT = 1 - Mathf.Pow(1 - t, 2); // Ease out curve

        float currentSlideSpeed = Mathf.Lerp(walkSpeed + slideBoost, walkSpeed * 0.8f, smoothT);

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

        Debug.Log($"Slide cancelled with jump! Speed: {currentSpeed}");
    }

    void EndSlide()
    {
        isSliding = false;
        slideCooldownTimer = slideCooldown;

        // Get current slide speed
        float currentSlideSpeed = new Vector3(velocity.x, 0, velocity.z).magnitude;

        // Set velocity to a reasonable speed
        float endSpeed = Mathf.Min(currentSlideSpeed, walkSpeed * 1.2f);
        velocity.x = slideDirection.x * endSpeed;
        velocity.z = slideDirection.z * endSpeed;

        Debug.Log($"Slide ended smoothly!");
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
