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
    
    [Header("Jump Settings")]
    public float jumpHeight = 1.5f;
    
    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;
    private float currentSpeed;
    private Transform cameraTransform;
    private bool isRunning;
    
    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            Debug.LogError("CharacterController component missing! Please add a CharacterController to the player.");
        }
        
        // Find the camera (assumes camera is a child of the player)
        Camera cam = GetComponentInChildren<Camera>();
        if (cam != null)
        {
            cameraTransform = cam.transform;
        }
        else
        {
            Debug.LogError("Camera not found as child of player!");
        }
    }
    
    void Update()
    {
        // Check if player is on ground
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
        
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
        
        // Get camera directions for movement
        if (cameraTransform != null)
        {
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
        }
        
        // Jump
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
        
        // Apply gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
    
    // Public method for other scripts to check running state
    public bool IsRunning()
    {
        return isRunning;
    }
}
