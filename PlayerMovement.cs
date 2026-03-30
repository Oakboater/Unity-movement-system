using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float runSpeed = 8f;

    [Header("Slide Settings")]
    public float slideSpeed = 12f;
    public float slideDuration = 1.2f;
    public float slideCooldown = 0.8f;
    public float slideFriction = 0.96f; // Gentler friction for smoother slide

    [Header("Camera Settings")]
    public Camera playerCamera;
    public float normalFOV = 60f;
    public float slideFOV = 70f; // Increase FOV by 10 during slide
    public float fovTransitionSpeed = 8f; // How fast FOV changes

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

        // Simple ground check using CharacterController's built-in flag
        bool isGrounded = controller.isGrounded;

        // Reset velocity when grounded
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        // Get input
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // Slide input (hold Left Control)
        bool slideInput = Input.GetKey(KeyCode.LeftControl);

        // Handle slide initiation
        if (!isSliding && slideInput && isGrounded && slideCooldownTimer <= 0 && (horizontal != 0 || vertical != 0))
        {
            StartSlide();
        }

        // Handle slide logic
        if (isSliding)
        {
            UpdateSlide();
        }
        else
        {
            // Normal movement
            isRunning = Input.GetKey(KeyCode.LeftShift) && (horizontal != 0 || vertical != 0);
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
            }
        }

        // Update camera FOV smoothly
        if (playerCamera != null)
        {
            float targetFOV = isSliding ? slideFOV : normalFOV;
            currentFOV = Mathf.Lerp(currentFOV, targetFOV, Time.deltaTime * fovTransitionSpeed);
            playerCamera.fieldOfView = currentFOV;
        }

        // Jump (can't jump while sliding)
        if (Input.GetButtonDown("Jump") && isGrounded && !isSliding)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // Apply gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    void StartSlide()
    {
        isSliding = true;
        slideTimer = slideDuration;

        // Calculate slide direction based on current movement direction
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

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

        // Reduce character controller height for sliding
        controller.height = originalHeight * 0.6f; // Slightly taller than before
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

        // Gentle slide speed - no boost, just maintain momentum
        float currentHorizontalSpeed = new Vector3(velocity.x, 0, velocity.z).magnitude;
        float startSpeed = Mathf.Max(currentHorizontalSpeed, slideSpeed * 0.8f);
        velocity = new Vector3(slideDirection.x, velocity.y, slideDirection.z) * startSpeed;

        Debug.Log("Slide started!");
    }

    void UpdateSlide()
    {
        // Update slide timer
        slideTimer -= Time.deltaTime;

        // Apply slide movement with smooth deceleration
        float currentSlideSpeed = Mathf.Lerp(slideSpeed, 3f, 1 - (slideTimer / slideDuration));
        controller.Move(slideDirection * currentSlideSpeed * Time.deltaTime);

        // End slide if timer runs out
        if (slideTimer <= 0)
        {
            EndSlide();
        }
    }

    void EndSlide()
    {
        isSliding = false;
        slideCooldownTimer = slideCooldown;
        currentSpeed = walkSpeed;

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

        Debug.Log("Slide ended!");
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
