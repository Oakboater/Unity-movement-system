using UnityEngine;
using System.Collections.Generic;

#pragma warning disable CS0618

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float runSpeed = 20f;
    public float acceleration = 10f;
    public float deceleration = 10f;
    public float airborneGravityMultiplier = 1f;

    [Header("Slam Settings")]
    public bool enableSlam = true;
    public float slamSpeed = 200f;
    public float slamCooldown = 1f;
    public float doubleTapWindow = 0.3f;
    public float minAirTimeForSlam = 0.4f;
    private float lastSlamTime = -999f;
    private float lastCtrlPressTime = -999f;
    private bool ctrlWasReleased = true;
    private bool slamAirborneFlag = false;
    private float airTimeForSlam;
    private bool slamDisabled;

    [Header("Slide Settings")]
    public float slideDuration = 1.2f;
    public float slideCooldown = 0.5f;
    public float slideBoost = 2f;
    public float slideTransitionSpeed = 10f;
    public float maxSlideGainSpeed = 18f;
    public float slideDecayRate = 0.7f;
    public float slideSteeringFactor = 0.15f;

    [Header("Wall Run Settings")]
    public bool enableWallRun = true;
    public float wallRunMaxSpeed = 35f;
    public float wallRunBaseDuration = 2.5f;
    public float wallRunJumpForce = 10f;
    public float wallRunDecayFactor = 0.97f;
    public float wallRunCooldown = 0.4f;
    public float wallCooldownTime = 0.6f;
    public float backwardWallRunSpeedMultiplier = 0.6f;
    public float backwardWallRunDurationMultiplier = 1.5f;
    public float backwardWallRunTimeAdd = 0.5f;
    public float directionSwitchCooldown = 0.3f;
    public float wallRunStartJumpDelay = 0.25f;
    public LayerMask wallLayer;
    public float wallCheckDistance = 1.2f;
    public float wallRunFOV = 75f;
    public float wallRunBaseBoost = 2f;
    public float minWallRunSpeed = 6f;
    public float wallRunHopForce = 5f;
    public float wallRunHopUpward = 8f;
    public float wallRunHopSideways = 3f;
    public float wallRunHopDuration = 0.2f;
    public float wallRunCoyoteTime = 0.08f;
    public bool enableDebugLogs = true;

    [Header("Backward Wallrun Behavior")]
    [Tooltip("If true, backward wallrun reverses direction along the wall (Titanfall style). If false, you continue moving in the same world direction while facing backward.")]
    public bool backwardWallrunReversesDirection = true;

    [Header("Horizontal Wall Run Controls")]
    public float lookUpVerticalGain = 18f;
    public float lookDownVerticalLoss = 6f;
    public float lookUpSpeedReduction = 1.2f;
    public float lookDownSpeedBonus = 1.5f;
    public AnimationCurve heightCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float maxUpwardSpeed = 8f;
    public float maxDownwardSpeed = -8f;

    [Header("Chaining & Decay")]
    public float chainPenaltyIncrease = 0.3f;
    public float slideResetChain = 0.3f;
    public float verticalResetChain = 0.2f;
    private float currentChainTension = 0f;

    [Header("Vertical Wall Run Settings")]
    public bool enableVerticalWallRun = true;
    public float verticalWallRunBaseSpeed = 8f;
    public float verticalWallRunGravity = -9f;
    public float verticalWallRunDuration = 1.2f;
    public float verticalWallRunJumpForce = 12f;
    public float verticalWallRunAngleThreshold = 45f;
    public float verticalWallRunForwardSpeed = 4f;
    public float verticalWallRunCooldown = 0.6f;
    public float verticalWallRunMaxGainSpeed = 20f;
    public float verticalWallRunSpeedBoostUp = 1.5f;
    public float verticalWallRunSpeedBoostDown = 2f;
    public float verticalWallRunDecayRate = 2f;
    public float wallRunJumpCooldown = 0.3f;
    public float downwardRunMultiplier = 0.4f;
    public float verticalWallJumpExtra = 2f;

    [Header("Slope Slide Settings")]
    public float slopeBoostMultiplier = 3f;
    public float maxSlopeSpeed = 35f;
    public float minSlopeAngle = 5f;
    public float slopeSlideGravity = 30f;
    public float slopeSlideFriction = 0.95f;
    public float uphillSlowdown = 20f;
    public float slopeStickForce = 15f;
    public float uphillReverseThreshold = 1.0f;      // Lowered for quicker reversal
    public float uphillMinimumSpeed = 1.5f;

    [Header("Camera Settings")]
    public Camera playerCamera;
    public float normalFOV = 60f;
    public float slideFOV = 75f;
    public float fovTransitionSpeed = 8f;
    public float wallRunCameraTilt = 8f;

    [Header("Gravity")]
    public float gravity = -9.81f;
    public AnimationCurve fallGravityCurve = AnimationCurve.Linear(0, 0.1f, 2, 1f);
    public float maxFallSpeed = -50f;

    [Header("Jump Settings")]
    public float jumpHeight = 1.2f;
    public float jumpBufferTime = 0.15f;
    public float groundCoyoteTime = 0.1f;

    // Components
    private CharacterController controller;
    private Transform cameraTransform;

    // Movement state
    private Vector3 velocity;
    private Vector3 moveDirection;
    private Vector3 targetMoveDirection;
    private float currentSpeed;
    private bool isRunning;

    // Slide state
    private bool isSliding;
    private float slideTimer;
    private float slideCooldownTimer;
    private Vector3 slideDirection;
    private int slideCount;
    private float lastSlideTime;
    private bool wasSlidingOnSlope;
    private Vector3 slideForwardDir;

    // Height/scale
    private float originalHeight;
    private float originalCenterY;
    private float originalCameraHeight;
    private float currentSlideHeight;
    private float currentCameraHeight;
    private float currentFOV;

    // Momentum
    private bool hasMomentum;
    private float momentumSpeed;
    private Vector3 momentumDirection;
    private bool wasJumpCancelled;

    // Wall run system
    private bool isWallRunning;
    private bool isVerticalWallRun;
    private float wallRunCooldownTimer;
    private float verticalWallRunCooldownTimer;
    private float lastVerticalWallRunTime;
    private float lastWallRunJumpTime;
    private float lastWallRunEndTime;
    private float wallClingTimer;
    private Vector3 wallNormal;
    private Vector3 wallRunDirection;
    private Vector3 wallUpDirection;
    private bool wallRunSide;
    private bool hasWallRunBoosted;
    private Vector3 lastWallRunForwardDir;
    private Vector3 lastWallNormal;
    private int currentWallID;
    private float wallHopTimer;
    private float wallRunStartTime;

    // Backwards wallrun reattachment
    private int lastTouchedWallSurfaceID;
    private float lastTouchedWallTime;
    private Vector3 lastTouchedWallNormal;
    private float backwardsReattachWindow = 0.7f;

    // Vertical to horizontal chaining
    private bool canHorizontalAfterVertical;
    private float verticalEndTime;

    // Vertical motion during wall runs
    private float verticalVelocity;
    private float speedRampTimer;
    private float targetWallRunSpeed;
    private float targetUpwardVelocity;
    private float upwardBoostRampTimer;

    // Direction switching
    private float lastDirectionSwitchTime = -999f;

    // Wall usage tracking
    private HashSet<int> usedWalls = new HashSet<int>();
    private Dictionary<int, float> wallCooldowns = new Dictionary<int, float>();

    // Jump buffering & coyote
    private float jumpBufferCounter;
    private float groundCoyoteCounter;

    // Air time
    private float airTime;
    private bool wasGroundedLastFrame;

    // Camera tilt
    private float currentCameraTilt;

    // Input
    private float inputHorizontal;
    private float inputVertical;
    private bool inputJump;
    private bool inputRun;
    private bool inputSlide;

    // Debug throttling
    private Dictionary<string, float> lastLogTimes = new Dictionary<string, float>();
    private float logThrottle = 0.2f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        cameraTransform = playerCamera ? playerCamera.transform : GetComponentInChildren<Camera>().transform;

        originalHeight = controller.height;
        originalCenterY = controller.center.y;
        originalCameraHeight = cameraTransform.localPosition.y;

        currentFOV = normalFOV;
        playerCamera.fieldOfView = normalFOV;
        currentCameraHeight = originalCameraHeight;
        currentSlideHeight = originalHeight;

        moveDirection = Vector3.zero;
        targetMoveDirection = Vector3.zero;
        slideCount = 0;
        wasGroundedLastFrame = controller.isGrounded;
    }

    void Update()
    {
        float delta = Time.deltaTime;

        // Input capture
        inputHorizontal = Input.GetAxisRaw("Horizontal");
        inputVertical = Input.GetAxisRaw("Vertical");
        inputJump = Input.GetButtonDown("Jump");
        inputRun = Input.GetKey(KeyCode.LeftShift);
        bool slideKey = Input.GetKey(KeyCode.LeftControl);
        inputSlide = slideKey;

        // Slam double‑tap (release required)
        if (enableSlam)
        {
            if (slideKey)
            {
                if (ctrlWasReleased)
                {
                    if (!controller.isGrounded && !isWallRunning && !isSliding)
                        slamAirborneFlag = true;

                    float timeSinceLastPress = Time.time - lastCtrlPressTime;
                    if (timeSinceLastPress <= doubleTapWindow && Time.time - lastSlamTime > slamCooldown)
                    {
                        if (slamAirborneFlag && airTimeForSlam >= minAirTimeForSlam && !slamDisabled)
                        {
                            velocity.y = -slamSpeed;
                            lastSlamTime = Time.time;
                            slamDisabled = true;
                            slamAirborneFlag = false;
                            ThrottledLog($"SLAM! Speed: {slamSpeed} m/s down");
                        }
                        else if (!slamAirborneFlag)
                            ThrottledLog("Slam blocked - must start double‑tap in air");
                        else if (airTimeForSlam < minAirTimeForSlam)
                            ThrottledLog($"Slam blocked - need more air time ({airTimeForSlam:F2}s)");
                    }
                    lastCtrlPressTime = Time.time;
                    ctrlWasReleased = false;
                }
            }
            else
            {
                ctrlWasReleased = true;
            }
        }

        // Jump buffer
        if (inputJump) jumpBufferCounter = jumpBufferTime;
        if (jumpBufferCounter > 0) jumpBufferCounter -= delta;

        bool isGrounded = controller.isGrounded;

        // Coyote time
        if (isGrounded)
            groundCoyoteCounter = groundCoyoteTime;
        else if (groundCoyoteCounter > 0)
            groundCoyoteCounter -= delta;

        if (isGrounded != wasGroundedLastFrame)
        {
            ThrottledLog(isGrounded ? "Landed on ground" : "Left ground");
            wasGroundedLastFrame = isGrounded;
        }

        // Air time
        if (!isGrounded && !isWallRunning)
        {
            airTime += delta;
            airTimeForSlam += delta;
        }
        else if (isGrounded)
        {
            airTime = 0f;
            airTimeForSlam = 0f;
            slamAirborneFlag = false;
        }

        // Ground reset
        if (isGrounded)
        {
            if (isWallRunning) EndWallRun();
            usedWalls.Clear();
            slamDisabled = false;
            canHorizontalAfterVertical = false;
            wasSlidingOnSlope = false;
            lastWallNormal = Vector3.zero;
        }

        if (isGrounded && Time.time - lastSlideTime > 1f) slideCount = 0;

        // Timers
        if (slideCooldownTimer > 0) slideCooldownTimer -= delta;
        if (wallRunCooldownTimer > 0) wallRunCooldownTimer -= delta;
        if (verticalWallRunCooldownTimer > 0) verticalWallRunCooldownTimer -= delta;
        if (wallHopTimer > 0) wallHopTimer -= delta;
        if (speedRampTimer > 0) speedRampTimer -= delta;
        if (upwardBoostRampTimer > 0) upwardBoostRampTimer -= delta;
        if (currentChainTension > 0) currentChainTension = Mathf.Max(0, currentChainTension - delta * 0.8f);

        // Wall run detection
        if (enableWallRun && !isGrounded && !isSliding && !isWallRunning && wallRunCooldownTimer <= 0)
            CheckForWallRun();

        if (isWallRunning) UpdateWallRun(delta);

        // Ground stick
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
            }
            wasJumpCancelled = false;
            hasWallRunBoosted = false;
        }

        bool hasMovementInput = (inputHorizontal != 0 || inputVertical != 0);

        // Slide initiation
        if (!isSliding && inputSlide && isGrounded && slideCooldownTimer <= 0)
        {
            RaycastHit groundHit;
            bool onSlope = false;
            float slopeAngle = 0f;
            if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out groundHit, 2f))
            {
                slopeAngle = Vector3.Angle(groundHit.normal, Vector3.up);
                onSlope = slopeAngle > minSlopeAngle;
            }

            if (lastWallRunForwardDir != Vector3.zero && Time.time - lastWallRunEndTime <= 0.5f)
            {
                slideDirection = lastWallRunForwardDir;
                lastWallRunForwardDir = Vector3.zero;
                ThrottledLog("Slide using stored wall direction");
            }
            else
            {
                Vector3 forward = cameraTransform.forward;
                Vector3 right = cameraTransform.right;
                forward.y = 0; right.y = 0;
                forward.Normalize(); right.Normalize();
                slideDirection = (forward * inputVertical) + (right * inputHorizontal);
                if (slideDirection.magnitude < 0.1f) slideDirection = forward;
                slideDirection.Normalize();
            }
            StartSlide(slideDirection, onSlope, slopeAngle);
        }

        if (isSliding)
        {
            if (!inputSlide && !wasSlidingOnSlope)
                EndSlide();
            else if (inputJump)
            {
                CancelSlideWithMomentum();
                wasJumpCancelled = true;
            }
        }

        // Wall jumps
        if (inputJump && !isWallRunning && !isGrounded && Time.time - lastWallRunEndTime <= wallRunCoyoteTime && lastWallRunEndTime > 0)
        {
            ThrottledLog("COYOTE WALL JUMP!");
            CoyoteWallJump();
            lastWallRunEndTime = 0;
        }
        else if (isWallRunning && inputJump && Time.time - lastWallRunJumpTime >= wallRunJumpCooldown)
        {
            if (Time.time - wallRunStartTime >= wallRunStartJumpDelay)
            {
                ThrottledLog("WALL RUN JUMP!");
                lastWallRunJumpTime = Time.time;
                WallRunJump();
            }
            else
            {
                ThrottledLog("Wall jump blocked - too early");
            }
        }

        // Slide height transition
        if (isSliding && (isGrounded || wasSlidingOnSlope))
        {
            UpdateSlide(delta);
            float targetHeight = originalHeight * 0.6f;
            float targetCameraHeight = originalCameraHeight * 0.6f;
            currentSlideHeight = Mathf.Lerp(currentSlideHeight, targetHeight, delta * slideTransitionSpeed);
            currentCameraHeight = Mathf.Lerp(currentCameraHeight, targetCameraHeight, delta * slideTransitionSpeed);
            controller.height = currentSlideHeight;
            controller.center = new Vector3(0, currentSlideHeight * 0.5f, 0);
            cameraTransform.localPosition = new Vector3(cameraTransform.localPosition.x, currentCameraHeight, cameraTransform.localPosition.z);
        }
        else
        {
            if (isSliding && !isGrounded && !wasSlidingOnSlope)
                EndSlide();
            currentSlideHeight = Mathf.Lerp(currentSlideHeight, originalHeight, delta * slideTransitionSpeed);
            currentCameraHeight = Mathf.Lerp(currentCameraHeight, originalCameraHeight, delta * slideTransitionSpeed);
            controller.height = currentSlideHeight;
            controller.center = new Vector3(0, currentSlideHeight * 0.5f, 0);
            cameraTransform.localPosition = new Vector3(cameraTransform.localPosition.x, currentCameraHeight, cameraTransform.localPosition.z);
        }

        // Ground movement
        if (!isWallRunning)
        {
            if (hasMomentum && momentumSpeed > walkSpeed && !isSliding)
            {
                currentSpeed = momentumSpeed;
                if (isGrounded) momentumSpeed = Mathf.Max(momentumSpeed - (delta * deceleration * 2.5f), walkSpeed);
                currentSpeed = momentumSpeed;

                if (hasMovementInput)
                {
                    Vector3 forward = cameraTransform.forward;
                    Vector3 right = cameraTransform.right;
                    forward.y = 0; right.y = 0;
                    forward.Normalize(); right.Normalize();
                    Vector3 inputDir = (forward * inputVertical) + (right * inputHorizontal);
                    if (inputDir.magnitude > 0.1f)
                        momentumDirection = Vector3.Lerp(momentumDirection, inputDir.normalized, delta * 6f);
                }
                controller.Move(momentumDirection * currentSpeed * delta);
                velocity.x = momentumDirection.x * currentSpeed;
                velocity.z = momentumDirection.z * currentSpeed;

                if (momentumSpeed <= walkSpeed && isGrounded) hasMomentum = false;
            }
            else if (hasMovementInput)
            {
                isRunning = inputRun && hasMovementInput;
                float targetSpeed = isRunning ? runSpeed : walkSpeed;

                Vector3 forward = cameraTransform.forward;
                Vector3 right = cameraTransform.right;
                forward.y = 0; right.y = 0;
                forward.Normalize(); right.Normalize();
                targetMoveDirection = (forward * inputVertical) + (right * inputHorizontal);
                targetMoveDirection.Normalize();

                moveDirection = Vector3.Lerp(moveDirection, targetMoveDirection, delta * acceleration);
                currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, delta * acceleration);
                controller.Move(moveDirection * currentSpeed * delta);
                velocity.x = moveDirection.x * currentSpeed;
                velocity.z = moveDirection.z * currentSpeed;
            }
            else
            {
                currentSpeed = Mathf.Lerp(currentSpeed, 0, delta * deceleration);
                moveDirection = Vector3.Lerp(moveDirection, Vector3.zero, delta * deceleration);
                controller.Move(moveDirection * currentSpeed * delta);
                velocity.x = moveDirection.x * currentSpeed;
                velocity.z = moveDirection.z * currentSpeed;
            }
        }

        // Jump
        if (jumpBufferCounter > 0 && (isGrounded || groundCoyoteCounter > 0) && !isSliding && !isWallRunning)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpBufferCounter = 0;
            groundCoyoteCounter = 0;
            if (hasMomentum && momentumSpeed > walkSpeed)
                ThrottledLog($"Jump with momentum: {momentumSpeed:F1}");
        }

        // Gravity
        if (!isWallRunning)
        {
            if (!isGrounded)
            {
                float curveValue = fallGravityCurve.Evaluate(Mathf.Min(airTime, 2f));
                float currentGravity = gravity * curveValue * airborneGravityMultiplier;
                velocity.y += currentGravity * delta;
                velocity.y = Mathf.Max(velocity.y, maxFallSpeed);
            }
            else
            {
                velocity.y += gravity * delta;
            }
        }

        controller.Move(velocity * delta);

        // FOV and camera tilt
        float targetFOV = normalFOV;
        if (isSliding) targetFOV = slideFOV;
        if (isWallRunning) targetFOV = wallRunFOV;
        currentFOV = Mathf.Lerp(currentFOV, targetFOV, delta * fovTransitionSpeed);
        playerCamera.fieldOfView = currentFOV;

        float targetTilt = 0f;
        if (isWallRunning && !isVerticalWallRun)
            targetTilt = wallRunSide ? -wallRunCameraTilt : wallRunCameraTilt;
        currentCameraTilt = Mathf.Lerp(currentCameraTilt, targetTilt, delta * 8f);
        Vector3 camEuler = cameraTransform.localEulerAngles;
        cameraTransform.localRotation = Quaternion.Euler(camEuler.x, camEuler.y, currentCameraTilt);
    }

    void CheckForWallRun()
    {
        float currentHorizontalSpeed = new Vector3(velocity.x, 0, velocity.z).magnitude;
        if (currentHorizontalSpeed < 3f) return;

        float cameraAngle = Vector3.Angle(cameraTransform.forward, Vector3.up);
        bool lookingUp = cameraAngle < verticalWallRunAngleThreshold;

        Collider[] nearbyWalls = Physics.OverlapSphere(transform.position, wallCheckDistance, wallLayer);
        if (nearbyWalls.Length == 0) return;

        Vector3 horizontalForward = cameraTransform.forward;
        horizontalForward.y = 0;
        horizontalForward.Normalize();

        bool forwardWall = Physics.SphereCast(transform.position, 0.3f, horizontalForward, out RaycastHit forwardHit, wallCheckDistance, wallLayer);
        bool higherWall = Physics.SphereCast(transform.position + Vector3.up * 0.5f, 0.3f, horizontalForward, out RaycastHit higherHit, wallCheckDistance, wallLayer);
        bool lowerWall = Physics.SphereCast(transform.position - Vector3.up * 0.3f, 0.3f, horizontalForward, out RaycastHit lowerHit, wallCheckDistance, wallLayer);

        Vector3 leftDir = -cameraTransform.right; leftDir.y = 0; leftDir.Normalize();
        Vector3 rightDir = cameraTransform.right; rightDir.y = 0; rightDir.Normalize();
        bool leftWall = Physics.SphereCast(transform.position, 0.3f, leftDir, out RaycastHit leftHit, wallCheckDistance, wallLayer);
        bool rightWall = Physics.SphereCast(transform.position, 0.3f, rightDir, out RaycastHit rightHit, wallCheckDistance, wallLayer);

        // Vertical wall run
        if (enableVerticalWallRun && lookingUp && (forwardWall || higherWall || lowerWall))
        {
            RaycastHit hit = forwardWall ? forwardHit : (higherWall ? higherHit : lowerHit);
            int wallID = hit.collider.GetInstanceID();
            if (!usedWalls.Contains(wallID) && IsWallOffCooldown(wallID))
            {
                Vector3 cameraDir = cameraTransform.forward;
                float dot = Vector3.Dot(cameraDir, hit.normal);
                bool isLookingAlongWall = Mathf.Abs(dot) < 0.3f;
                StartVerticalWallRun(hit.normal, wallID, isLookingAlongWall);
            }
            return;
        }

        // Horizontal after vertical
        if (canHorizontalAfterVertical)
        {
            if (leftWall || rightWall || forwardWall)
            {
                RaycastHit hit = leftWall ? leftHit : (rightWall ? rightHit : forwardHit);
                int wallID = hit.collider.GetInstanceID();
                if (!usedWalls.Contains(wallID) && IsWallOffCooldown(wallID))
                {
                    StartWallRun(hit.normal, leftWall, wallID, false, true);
                    canHorizontalAfterVertical = false;
                    return;
                }
            }
        }

        // Backwards reattachment
        bool wantsBackwards = inputVertical < 0;
        bool hasRecentWallTouch = (Time.time - lastTouchedWallTime <= backwardsReattachWindow) && lastTouchedWallSurfaceID != 0;

        if (wantsBackwards && hasRecentWallTouch)
        {
            ThrottledLog($"Attempting backward reattach to wall ID {lastTouchedWallSurfaceID}");
            foreach (var col in nearbyWalls)
            {
                if (col.GetInstanceID() == lastTouchedWallSurfaceID)
                {
                    Vector3 dirToWall = (col.ClosestPoint(transform.position) - transform.position).normalized;
                    if (Physics.SphereCast(transform.position, 0.5f, dirToWall, out RaycastHit hit, wallCheckDistance + 1.0f, wallLayer))
                    {
                        if (hit.collider.GetInstanceID() == lastTouchedWallSurfaceID)
                        {
                            usedWalls.Remove(lastTouchedWallSurfaceID);
                            wallCooldowns.Remove(lastTouchedWallSurfaceID);
                            ThrottledLog($"Reattaching backwards on wall ID {lastTouchedWallSurfaceID}");
                            StartWallRun(hit.normal, false, lastTouchedWallSurfaceID, true);
                            lastTouchedWallSurfaceID = 0;
                            return;
                        }
                    }
                }
            }

            Vector3 backDir = -cameraTransform.forward;
            backDir.y = 0;
            backDir.Normalize();
            if (Physics.SphereCast(transform.position, 0.5f, backDir, out RaycastHit backHit, wallCheckDistance + 1.0f, wallLayer))
            {
                int wallID = backHit.collider.GetInstanceID();
                if (IsWallOffCooldown(wallID) && !usedWalls.Contains(wallID))
                {
                    ThrottledLog($"Backwards wallrun on new surface ID {wallID} (fallback)");
                    StartWallRun(backHit.normal, false, wallID, true);
                    lastTouchedWallSurfaceID = 0;
                    return;
                }
            }
            ThrottledLog("Backward reattach failed - no suitable wall");
            return;
        }

        // Regular horizontal wallrun
        if (leftWall && !rightWall)
        {
            int wallID = leftHit.collider.GetInstanceID();
            if (!usedWalls.Contains(wallID) && IsWallOffCooldown(wallID))
                StartWallRun(leftHit.normal, true, wallID);
        }
        else if (rightWall && !leftWall)
        {
            int wallID = rightHit.collider.GetInstanceID();
            if (!usedWalls.Contains(wallID) && IsWallOffCooldown(wallID))
                StartWallRun(rightHit.normal, false, wallID);
        }
        else if (forwardWall && !leftWall && !rightWall)
        {
            int wallID = forwardHit.collider.GetInstanceID();
            if (!usedWalls.Contains(wallID) && IsWallOffCooldown(wallID))
                StartWallRun(forwardHit.normal, false, wallID);
        }
    }

    bool IsWallOffCooldown(int wallID)
    {
        if (wallCooldowns.TryGetValue(wallID, out float nextAvailable))
            return Time.time >= nextAvailable;
        return true;
    }

    void StartVerticalWallRun(Vector3 normal, int wallID, bool lookingAlongWall)
    {
        if (verticalWallRunCooldownTimer > 0) return;
        if (Time.time - lastVerticalWallRunTime < verticalWallRunCooldown) return;

        float totalSpeed = velocity.magnitude;
        bool alreadyFast = totalSpeed > verticalWallRunMaxGainSpeed;

        usedWalls.Add(wallID);
        wallCooldowns[wallID] = Time.time + wallCooldownTime;

        isWallRunning = true;
        isVerticalWallRun = true;
        wallNormal = normal;
        hasWallRunBoosted = false;
        wallRunStartTime = Time.time;
        currentWallID = wallID;

        float multiplier = lookingAlongWall ? downwardRunMultiplier : 1f;
        float runBaseSpeed = verticalWallRunBaseSpeed * multiplier;
        float runForwardSpeed = verticalWallRunForwardSpeed * multiplier;

        wallUpDirection = Vector3.ProjectOnPlane(Vector3.up, normal).normalized;
        Vector3 wallForward = Vector3.ProjectOnPlane(cameraTransform.forward, normal).normalized;
        wallRunDirection = (wallUpDirection * runBaseSpeed + wallForward * runForwardSpeed).normalized;

        if (!hasWallRunBoosted)
        {
            hasWallRunBoosted = true;
            float targetSpeed = alreadyFast ? Mathf.Min(totalSpeed, verticalWallRunMaxGainSpeed)
                : (totalSpeed < runBaseSpeed ? runBaseSpeed : Mathf.Min(totalSpeed + verticalWallRunSpeedBoostDown, verticalWallRunMaxGainSpeed));
            currentSpeed = targetSpeed;
        }
        else currentSpeed = totalSpeed;

        velocity = wallRunDirection * currentSpeed;
        hasMomentum = true;
        momentumSpeed = currentSpeed;
        momentumDirection = wallRunDirection;
        lastVerticalWallRunTime = Time.time;
        verticalWallRunCooldownTimer = verticalWallRunCooldown;
        currentChainTension = Mathf.Max(0, currentChainTension - verticalResetChain);
        slamDisabled = false;
        ThrottledLog($"VERTICAL WALL RUN - ID:{wallID} Speed:{currentSpeed:F1}");
    }

    void StartWallRun(Vector3 normal, bool isLeft, int wallID, bool forceBackwards = false, bool afterVertical = false)
    {
        if (!forceBackwards)
            usedWalls.Add(wallID);
        else
            usedWalls.Remove(wallID);

        wallCooldowns[wallID] = Time.time + wallCooldownTime;

        isWallRunning = true;
        isVerticalWallRun = false;
        wallRunSide = isLeft;
        wallNormal = normal;
        hasWallRunBoosted = false;
        wallRunStartTime = Time.time;
        currentWallID = wallID;

        Vector3 wallForward = Vector3.Cross(normal, Vector3.up);
        if (Vector3.Dot(wallForward, cameraTransform.forward) < 0) wallForward = -wallForward;
        wallRunDirection = wallForward.normalized;

        bool isBackwards = forceBackwards || inputVertical < 0;
        if (!isBackwards)
        {
            lastWallRunForwardDir = wallRunDirection;
        }
        else
        {
            // Toggle for direction reversal
            if (backwardWallrunReversesDirection)
                wallRunDirection = -wallRunDirection;
            lastWallRunForwardDir = wallRunDirection;
        }

        float currentHorizontalSpeed = new Vector3(velocity.x, 0, velocity.z).magnitude;
        float chainPenalty = 1f + currentChainTension;
        wallRunCooldownTimer = wallRunCooldown * chainPenalty;

        float speedMultiplier = afterVertical ? 0.7f : 1f;
        float cameraAngle = Vector3.Angle(cameraTransform.forward, Vector3.up);
        float speedModifier = cameraAngle < 45f ? -lookUpSpeedReduction * (1f - cameraAngle/45f) : lookDownSpeedBonus * ((cameraAngle-45f)/45f);
        float totalBoost = Mathf.Clamp(wallRunBaseBoost + speedModifier, 1f, 3f);
        targetWallRunSpeed = Mathf.Min(currentHorizontalSpeed + totalBoost, wallRunMaxSpeed) * speedMultiplier;
        targetWallRunSpeed = Mathf.Max(targetWallRunSpeed, minWallRunSpeed + 2f);

        if (isBackwards)
            targetWallRunSpeed *= backwardWallRunSpeedMultiplier;

        speedRampTimer = 0.2f;

        float upBoostFactor = Mathf.Pow(Mathf.Clamp01(1f - cameraAngle/90f), 1.5f);
        targetUpwardVelocity = wallRunHopUpward * upBoostFactor;
        upwardBoostRampTimer = 0.15f;

        float speedScaledHop = Mathf.Lerp(wallRunHopForce * 0.3f, wallRunHopForce * 0.8f, currentHorizontalSpeed / wallRunMaxSpeed);
        Vector3 kick = (wallRunDirection * speedScaledHop) + (wallNormal * wallRunHopSideways * 0.3f);
        velocity += kick;

        wallHopTimer = wallRunHopDuration;
        hasMomentum = true;
        momentumSpeed = currentHorizontalSpeed;
        momentumDirection = wallRunDirection;
        currentChainTension += chainPenaltyIncrease;
        slamDisabled = false;

        ThrottledLog($"HORIZONTAL WALL RUN - ID:{wallID} Speed:{targetWallRunSpeed:F1} Dir:{(isBackwards?"Back":"Forward")}");
    }

    void UpdateWallRun(float delta)
    {
        if (speedRampTimer > 0)
        {
            float t = 1f - (speedRampTimer / 0.2f);
            currentSpeed = Mathf.Lerp(new Vector3(velocity.x, 0, velocity.z).magnitude, targetWallRunSpeed, t);
            speedRampTimer -= delta;
        }
        else currentSpeed = targetWallRunSpeed;

        if (!isVerticalWallRun)
        {
            bool wantsBackwards = inputVertical < 0;
            bool wantsForward = inputVertical > 0;
            bool isCurrentlyBackwards = Vector3.Dot(wallRunDirection, lastWallRunForwardDir) < 0;

            if (Time.time - lastDirectionSwitchTime > directionSwitchCooldown)
            {
                if ((isCurrentlyBackwards && wantsForward) || (!isCurrentlyBackwards && wantsBackwards))
                {
                    wallRunDirection = -wallRunDirection;
                    lastWallRunForwardDir = wallRunDirection;
                    lastDirectionSwitchTime = Time.time;

                    float oldSpeed = currentSpeed;
                    float newSpeed = wantsBackwards ? oldSpeed * backwardWallRunSpeedMultiplier : oldSpeed / backwardWallRunSpeedMultiplier;
                    newSpeed = Mathf.Clamp(newSpeed, minWallRunSpeed + 2f, wallRunMaxSpeed);
                    targetWallRunSpeed = newSpeed;
                    speedRampTimer = 0.2f;
                    ThrottledLog($"DIRECTION SWITCH - New speed: {newSpeed:F1}");
                }
            }
        }
        else
        {
            if (inputVertical <= 0) { ForceWallKickoff(); return; }
        }

        // Vertical motion
        if (!isVerticalWallRun)
        {
            verticalVelocity += gravity * delta;
            if (upwardBoostRampTimer > 0)
            {
                float t = 1f - (upwardBoostRampTimer / 0.15f);
                verticalVelocity = Mathf.Max(verticalVelocity, Mathf.Lerp(0, targetUpwardVelocity, t));
                upwardBoostRampTimer -= delta;
            }

            float cameraAngle = Vector3.Angle(cameraTransform.forward, Vector3.up);
            float tCurve = Mathf.Clamp01(cameraAngle / 90f);
            float curveValue = heightCurve.Evaluate(tCurve);

            if (cameraAngle < 45f)
            {
                float upFactor = 1f - (cameraAngle / 45f);
                verticalVelocity += lookUpVerticalGain * curveValue * upFactor * delta * 0.5f;
                verticalVelocity = Mathf.Min(verticalVelocity, maxUpwardSpeed);
            }
            else if (cameraAngle > 45f && GetGroundDistance() > 3f)
            {
                float downFactor = (cameraAngle - 45f) / 45f;
                verticalVelocity -= lookDownVerticalLoss * curveValue * downFactor * delta * 0.5f;
                verticalVelocity = Mathf.Max(verticalVelocity, maxDownwardSpeed);
            }

            controller.Move(Vector3.up * verticalVelocity * delta);
            velocity.y = verticalVelocity;

            float speedMod = cameraAngle < 45f ? -lookUpSpeedReduction * (1f - cameraAngle/45f) * curveValue * 0.5f
                : lookDownSpeedBonus * ((cameraAngle-45f)/45f) * curveValue * 0.5f;
            currentSpeed = Mathf.Clamp(currentSpeed + speedMod * delta, minWallRunSpeed, wallRunMaxSpeed);
        }
        else
        {
            currentSpeed = Mathf.Max(currentSpeed - verticalWallRunDecayRate * delta, verticalWallRunBaseSpeed * 0.5f);
            wallUpDirection = Vector3.ProjectOnPlane(Vector3.up, wallNormal).normalized;
            Vector3 wallForward = Vector3.ProjectOnPlane(cameraTransform.forward, wallNormal).normalized;
            wallRunDirection = (wallUpDirection * verticalWallRunBaseSpeed + wallForward * verticalWallRunForwardSpeed).normalized;
        }

        // Wall contact check
        Vector3 checkDir = isVerticalWallRun ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized
            : (wallRunSide ? -cameraTransform.right : cameraTransform.right);
        bool stillOnWall = Physics.SphereCast(transform.position, 0.3f, checkDir, out RaycastHit hit, wallCheckDistance + 0.5f, wallLayer);
        if (!stillOnWall && !isVerticalWallRun)
            stillOnWall = Physics.SphereCast(transform.position + Vector3.up * 0.5f, 0.3f, checkDir, out hit, wallCheckDistance + 0.5f, wallLayer)
                       || Physics.SphereCast(transform.position - Vector3.up * 0.3f, 0.3f, checkDir, out hit, wallCheckDistance + 0.5f, wallLayer);

        if (!stillOnWall)
        {
            wallClingTimer -= delta;
            if (wallClingTimer <= 0f)
            {
                ThrottledLog("Wall run ended - lost wall contact");
                ForceWallKickoff();
                return;
            }
        }
        else
        {
            wallClingTimer = 0.25f;
            wallNormal = hit.normal;
        }

        if (!isVerticalWallRun)
        {
            Vector3 wallForward = Vector3.Cross(wallNormal, Vector3.up);
            if (Vector3.Dot(wallForward, cameraTransform.forward) < 0) wallForward = -wallForward;
            wallRunDirection = wallForward.normalized;
        }

        Vector3 move = wallRunDirection * currentSpeed;
        controller.Move(move * delta);
        velocity.x = move.x;
        velocity.z = move.z;
        momentumSpeed = currentSpeed;
        momentumDirection = move.normalized;
    }

    void ForceWallKickoff()
    {
        if (!isWallRunning) return;
        Vector3 away = (transform.position - GetClosestPointOnWall()).normalized;
        away.y = 0; away.Normalize();
        float cameraAngle = Vector3.Angle(cameraTransform.forward, Vector3.up);
        float verticalFactor = Mathf.Clamp01(cameraAngle / 90f);
        Vector3 launchDir = (away * (1 - verticalFactor * 0.5f) + Vector3.up * (0.3f + verticalFactor * 0.7f)).normalized;
        float launchSpeed = Mathf.Lerp(5f, currentSpeed * 0.8f, currentSpeed / wallRunMaxSpeed);
        velocity = launchDir * launchSpeed;
        verticalVelocity = velocity.y;
        hasMomentum = true;
        momentumSpeed = launchSpeed;
        momentumDirection = away;
        EndWallRun();
        wallRunCooldownTimer = wallRunCooldown;
        ThrottledLog($"FORCED KICKOFF - Speed:{launchSpeed:F1}");
    }

    void WallRunJump()
    {
        Vector3 away = (transform.position - GetClosestPointOnWall()).normalized;
        away.y = 0; away.Normalize();
        float cameraAngle = Vector3.Angle(cameraTransform.forward, Vector3.up);
        float verticalFactor = Mathf.Clamp01(cameraAngle / 90f);
        Vector3 launchDir = (away * (1 - verticalFactor * 0.4f) + Vector3.up * (0.4f + verticalFactor * 0.6f)).normalized;
        float launchSpeed = currentSpeed * 0.7f + Mathf.Lerp(2f, wallRunJumpForce * 0.8f, currentSpeed / wallRunMaxSpeed);
        velocity = launchDir * launchSpeed;
        if (isVerticalWallRun) velocity += velocity.normalized * verticalWallJumpExtra;
        verticalVelocity = velocity.y;
        hasMomentum = true;
        momentumSpeed = launchSpeed * 0.8f;
        momentumDirection = away;
        EndWallRun();
        wallRunCooldownTimer = wallRunCooldown;
        ThrottledLog($"WALL JUMP - Speed:{momentumSpeed:F1}");
    }

    void CoyoteWallJump()
    {
        Vector3 away = Vector3.ProjectOnPlane(-lastWallNormal, Vector3.up).normalized;
        float cameraAngle = Vector3.Angle(cameraTransform.forward, Vector3.up);
        float verticalFactor = Mathf.Clamp01(cameraAngle / 90f);
        Vector3 launchDir = (away * (1 - verticalFactor * 0.4f) + Vector3.up * (0.3f + verticalFactor * 0.7f)).normalized;
        float launchSpeed = (momentumSpeed > 0 ? momentumSpeed : currentSpeed) * 0.6f + wallRunJumpForce * 0.3f;
        velocity = launchDir * launchSpeed;
        verticalVelocity = velocity.y;
        hasMomentum = true;
        momentumSpeed = launchSpeed * 0.7f;
        momentumDirection = away;
    }

    Vector3 GetClosestPointOnWall()
    {
        if (Physics.Raycast(transform.position, -wallNormal, out RaycastHit hit, wallCheckDistance + 1f, wallLayer))
            return hit.point;
        return transform.position + wallNormal * 0.5f;
    }

    void EndWallRun()
    {
        lastWallNormal = wallNormal;
        lastWallRunEndTime = Time.time;

        if (!isVerticalWallRun)
        {
            lastTouchedWallSurfaceID = currentWallID;
            lastTouchedWallTime = Time.time;
            lastTouchedWallNormal = wallNormal;
            ThrottledLog($"Stored wall touch ID:{currentWallID} for backward reattach");
        }

        if (isVerticalWallRun)
        {
            canHorizontalAfterVertical = true;
            verticalEndTime = Time.time;
        }

        isWallRunning = false;
        if (isVerticalWallRun) verticalWallRunCooldownTimer = verticalWallRunCooldown;
        isVerticalWallRun = false;
        hasWallRunBoosted = false;
        wallClingTimer = 0f;
        speedRampTimer = 0f;
        upwardBoostRampTimer = 0f;
        verticalVelocity = 0;
    }

    float GetGroundDistance()
    {
        if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 20f))
            return hit.distance - 0.1f;
        return 20f;
    }

    void StartSlide(Vector3 direction, bool onSlope, float slopeAngle)
    {
        if (slideCooldownTimer > 0) return;

        isSliding = true;
        slideTimer = slideDuration;
        wasJumpCancelled = false;
        wasSlidingOnSlope = onSlope;

        slideCount++;
        float slidePenalty = Mathf.Max(0.3f, 1f - (slideCount - 1) * slideDecayRate);
        float currentHorizontalSpeed = new Vector3(velocity.x, 0, velocity.z).magnitude;
        float slideStartSpeed = currentHorizontalSpeed < maxSlideGainSpeed ?
            Mathf.Min(currentHorizontalSpeed + slideBoost * slidePenalty, maxSlideGainSpeed) : currentHorizontalSpeed;

        hasMomentum = true;
        momentumSpeed = slideStartSpeed;
        momentumDirection = direction;
        slideDirection = direction;
        slideForwardDir = direction;

        velocity = new Vector3(direction.x * slideStartSpeed, velocity.y, direction.z * slideStartSpeed);
        currentSpeed = slideStartSpeed;
        moveDirection = direction;

        currentSlideHeight = controller.height;
        currentCameraHeight = cameraTransform.localPosition.y;

        currentChainTension = Mathf.Max(0, currentChainTension - slideResetChain);
        slamDisabled = false;
        ThrottledLog($"SLIDE #{slideCount} Speed:{slideStartSpeed:F1} OnSlope:{onSlope} Angle:{slopeAngle:F1}");
    }

    void UpdateSlide(float delta)
    {
        if (!controller.isGrounded && !wasSlidingOnSlope) { EndSlide(); return; }

        slideTimer -= delta;
        if (!Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out RaycastHit groundHit, 2f))
        { EndSlide(); return; }

        float slopeAngle = Vector3.Angle(groundHit.normal, Vector3.up);
        bool onSlope = slopeAngle > minSlopeAngle;
        wasSlidingOnSlope = onSlope;

        if (slideTimer <= 0f && !onSlope) { EndSlide(); return; }

        Vector3 slopeDown = Vector3.ProjectOnPlane(Vector3.down, groundHit.normal).normalized;
        Vector3 slideDirOnPlane = Vector3.ProjectOnPlane(slideForwardDir, groundHit.normal).normalized;
        float dot = Vector3.Dot(slideDirOnPlane, slopeDown);
        bool slidingDownhill = onSlope && dot > 0.2f;
        bool slidingUphill = onSlope && dot < -0.2f;

        if (onSlope)
        {
            float slopeFactor = Mathf.Clamp01((slopeAngle - minSlopeAngle) / (75f - minSlopeAngle));
            if (slidingDownhill)
            {
                float acceleration = (slopeSlideGravity * 2.5f * slopeFactor) + (slopeFactor * slopeBoostMultiplier * slideBoost * 2f);
                momentumSpeed += acceleration * delta;
                momentumSpeed = Mathf.Min(momentumSpeed, maxSlopeSpeed);
                slideTimer = slideDuration;
                ThrottledLog($"Downhill slope - angle:{slopeAngle:F1} factor:{slopeFactor:F2} accel:{acceleration:F2} speed:{momentumSpeed:F1}");
            }
            else if (slidingUphill)
            {
                momentumSpeed = Mathf.Max(momentumSpeed - uphillSlowdown * slopeFactor * delta, uphillMinimumSpeed);
                ThrottledLog($"Uphill slope - speed:{momentumSpeed:F1}");
                if (momentumSpeed < uphillReverseThreshold)
                {
                    slideForwardDir = slopeDown;
                    momentumSpeed = Mathf.Max(momentumSpeed, walkSpeed * 0.8f);
                    ThrottledLog("Uphill slide reversed - now going downhill");
                }
            }
            // Stick to slope
            controller.Move(groundHit.normal * slopeStickForce * delta);
            velocity.y = Mathf.Max(velocity.y, -2f);
        }
        else
        {
            momentumSpeed = Mathf.Max(momentumSpeed - deceleration * 0.6f * delta, walkSpeed * 0.8f);
        }

        currentSpeed = momentumSpeed;
        Vector3 move = slideDirOnPlane * currentSpeed;
        controller.Move(move * delta);
        velocity.x = move.x;
        velocity.z = move.z;
        slideDirection = slideForwardDir;
    }

    void CancelSlideWithMomentum()
    {
        float currentSpeedVal = velocity.magnitude;
        velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        hasMomentum = true;
        momentumSpeed = currentSpeedVal;
        momentumDirection = slideDirection;
        isSliding = false;
        slideCooldownTimer = slideCooldown;
        lastSlideTime = Time.time;
        wasSlidingOnSlope = false;
        ResetHeight();
        ThrottledLog($"Slide cancelled! Momentum: {momentumSpeed:F1}");
    }

    void EndSlide()
    {
        isSliding = false;
        slideCooldownTimer = slideCooldown;
        lastSlideTime = Time.time;
        wasSlidingOnSlope = false;

        float currentSlideSpeed = velocity.magnitude;
        if (currentSlideSpeed > walkSpeed)
        {
            hasMomentum = true;
            momentumSpeed = currentSlideSpeed;
            momentumDirection = slideDirection;
            currentSpeed = currentSlideSpeed;
            moveDirection = slideDirection;
        }
        ResetHeight();
        ThrottledLog($"Slide ended - speed: {currentSlideSpeed:F1}");
    }

    void ResetHeight()
    {
        controller.height = originalHeight;
        controller.center = new Vector3(0, originalCenterY, 0);
        cameraTransform.localPosition = new Vector3(cameraTransform.localPosition.x, originalCameraHeight, cameraTransform.localPosition.z);
    }

    void ThrottledLog(string msg)
    {
        if (!enableDebugLogs) return;
        string key = msg.Substring(0, Mathf.Min(msg.Length, 40));
        if (!lastLogTimes.ContainsKey(key) || Time.time - lastLogTimes[key] > logThrottle)
        {
            Debug.Log(msg);
            lastLogTimes[key] = Time.time;
        }
    }

    public bool IsRunning() => isRunning;
    public bool IsSliding() => isSliding;
    public bool IsWallRunning() => isWallRunning;
    public bool IsVerticalWallRunning() => isVerticalWallRun;
    public float GetCurrentVelocity() => velocity.magnitude;
}
