using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float runSpeed = 8f;

    [Header("Ground Settings")]
    public float gravity = -9.81f;
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;
    private float currentSpeed;
    private Transform cameraTransform;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            Debug.LogError("CharacterController component missing! Please add a CharacterController to the player."); // Helps control the character
        }

        // Finds the camera (make sure the camera is a child of the player)
        cameraTransform = GetComponentInChildren<Camera>().transform;
        if (cameraTransform == null)
        {
            Debug.LogError("Camera not found as child of player!");
        }
    }

    void Update()
    {
        // Check if player is on ground - will prevent doing jumps in the air
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        // Get input
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // Sprint
        currentSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;

        // Get camera directions for movement
        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        // Remove vertical component to keep movement on ground plane
        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();

        // Calculate movement direction
        Vector3 moveDirection = (forward * vertical) + (right * horizontal);

        // Apply movement
        controller.Move(moveDirection * currentSpeed * Time.deltaTime);

        // Apply gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
