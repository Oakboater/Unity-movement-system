using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float runSpeed = 8f;

    [Header("Gravity")]
    public float gravity = -9.81f;

    [Header("Jump Settings")]
    public float jumpHeight = 1.5f;

    private CharacterController controller;
    private Vector3 velocity;
    private float currentSpeed;
    private Transform cameraTransform;
    private bool isRunning;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        // Find camera
        Camera cam = GetComponentInChildren<Camera>();
        if (cam != null)
        {
            cameraTransform = cam.transform;
        }

        Debug.Log("PlayerMovement initialized"); // Just an log in console to show us that our script is working.
        //  Feel free to remove as for the current time I am still adding features.
    }

    void Update()
    {
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

        // Sprint
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

        // Jump - using controller.isGrounded
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
           // Debug.Log("Jumped!");  -- this is optional for debugging, I kept it in just incase of werid mesh collsions ect.
        }

        // Apply gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

    public bool IsRunning()
    {
        return isRunning;
    }
}
