using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Performs the movement of characters. Another script is required to actually
/// call the functions.
///
/// <br/>
///
/// Authors: Ryan Chang (2023)
/// </summary>
public class CharacterMovementModule : Module
{
    #region Enums
    public enum GroundedStatus
    {
        /// <summary>
        /// The character is grounded.
        /// </summary>
        Grounded,
        /// <summary>
        /// The character is not grounded and has issued the jump command right
        /// before becoming not grounded. This is set whenever a jump command
        /// succeeds and is replaced by <see cref="Grounded"/> once grounded.
        /// </summary>
        AirbornFromJump,
        /// <summary>
        /// The character is not grounded and has not issued any recent
        /// (successful) jump commands. The is set when the character is in the
        /// <see cref="Grounded"/> state and loses contact with the ground.
        /// </summary>
        AirbornFromFall
    }

    public enum MovementStatus
    {
        Normal,
        Dashing
    }
    #endregion

    #region Variables
    #region User Settings

    [Header("Movement Settings")]

    #region Global Movement Toggle
    [Tooltip("Variable to turn the ability to read inputs on or off from the player")]
    public bool canInput;
    #endregion
    #region General Movement
    [InfoBox("NOTE: maxMoveSpeed.y is NOT the max jump speed! Jumping is " +
            "controlled by jumpForce.")]
    [Tooltip("The maximum move speed for this character in both horizontal " +
            "and vertical directions.")]
    public Vector2 maxMoveSpeed = new(10f, 0);

    [InfoBox("NOTE: moveAcceleration.y is NOT the jump force! Jumping is " +
        "controlled by jumpForce.")]
    [Tooltip("The move acceleration for this character in both horizontal " +
        "and vertical directions.")]
    public Vector2 moveAcceleration = new(1f, 0);

    [Tooltip("What percentage of the moveAcceleration will the character " +
        "decelerate?")]
    [Range(0, 0.95f)]
    public float decelerationPercentage = 0.95f;

    private readonly float originalGravityScale = 0.75f;
    #endregion

    #region Jumping
    [Tooltip("The upward force produced by jumping.")]
    [Min(0)]
    public float jumpForce = 12f;

    public Duration jumpCooldown = new(0.5f);

    [Tooltip("How long after leaving the ground is this character still " +
        "considered to be grounded?")]
    public Duration coyoteTimer = new(0.5f);
    #endregion

    #region Dashing
    [Tooltip("Speed of dashing.")]
    public float dashSpeed = 20;

    [Tooltip("The cooldown for dashing.")]
    public float dashCooldown = 2;

    [Tooltip("The duration of dashing.")]
    public float dashTimer = 0.5f;
    public bool canDash = true;
    public bool isDashing = false;
    public float dashTime = -10f;
    #endregion

    #region Ground Check
    [Tooltip("The collider responsible for checking if the character is " +
        "grounded.")]
    [Required]
    public Collider2D groundCheck;

    [Tooltip("The collider responsible for checking if the player is touching grass for the shotgun dash")]
    public Collider2D shotgunDashGroundCheck;
    #endregion

    #region Animation
    [Header("Animation Settings")]
    public Animator characterAnimator;

    [Tooltip("If true, then animParamMoveX and animParamMoveY will be " +
        "bounded to be positive.")]
    [SerializeField]
    private bool positiveAnimParamOnly;

    [Tooltip("Animator Parameter - Speed X.")]
    [AnimatorParam(nameof(characterAnimator), AnimatorControllerParameterType.Float)]
    [SerializeField]
    private string animParamSpeedX;

    [Tooltip("Animator Parameter - Speed Y.")]
    [AnimatorParam(nameof(characterAnimator), AnimatorControllerParameterType.Float)]
    [SerializeField]
    private string animParamSpeedY;

    [Tooltip("Animator Parameter - Jump")]
    [AnimatorParam(nameof(characterAnimator), AnimatorControllerParameterType.Bool)]
    [SerializeField]
    private string animParamJump;
    #endregion
    #endregion

    #region Autogenerated
    [Header("Inputs")]
    [Tooltip("The inputted movement of this character. The x component " +
        "controls the horizontal movement and the vertical component " +
        "controls the vertical movement. Will be normalized.")]
    [ReadOnly]
    public Vector2 inputtedMovement;

    [Tooltip("If true, the input to jump has been pressed.")]
    [ReadOnly]
    public bool inputtedJump;

    [Tooltip("If false, the player has jumped for one input.")]
    [ReadOnly]
    public bool oneJump;

    [Tooltip("The inputted dash. Will be normalized.")]
    [ReadOnly]
    public Vector2 inputtedDash;

    [Header("Grounded Checks")]
    [ReadOnly]
    [SerializeField]
    private Collider2D[] touchingGroundColliders = new Collider2D[1];

    [ReadOnly]
    [SerializeField]
    private ContactFilter2D groundCheckCF2D;

    [FormerlySerializedAs("touchGrassCheckCF2D")]
    [ReadOnly] 
    [SerializeField] 
    private ContactFilter2D shotgunDashGroundCheckCF2D;

    [ReadOnly]
    public GroundedStatus groundedStatus;

    [ReadOnly]
    public MovementStatus movementStatus;

    private Vector2 lockedDashInput;

    [SerializeField] private int totalJumps = 1; //Number of additional jumps the player can perform

    private int numJumps = 0; //current jumps available
    public bool isShotgunDashing = false;
    #endregion
    #endregion

    #region Parameters
    /// <summary>
    /// True if the character's <see cref="groundCheck"/> is touching at least
    /// one collider with a layer contained in the layermask <see
    /// cref="GameManager.canJumpLayers"/>. The coyote timer is NOT factored
    /// into this.
    /// </summary>
    public bool GroundCheck => groundCheck.OverlapCollider(
        groundCheckCF2D, touchingGroundColliders
    ) > 0;
    
    public bool ShotgunDashGroundCheck => shotgunDashGroundCheck.OverlapCollider(
        shotgunDashGroundCheckCF2D, touchingGroundColliders
    ) > 0;
    #endregion

    #region Methods
    #region Instantiation
    private void Start()
    {
        numJumps = totalJumps;
        canInput = true;
        // Make sure max speed is positive.
        maxMoveSpeed = maxMoveSpeed.Abs();

        // Set the contact filter for the grounded check.
        groundCheckCF2D = new()
        {
            layerMask = GameManager.Instance.canJumpLayers,
            useLayerMask = true
        };
        
        shotgunDashGroundCheckCF2D = new()
        {
            layerMask = GameManager.Instance.touchGrassLayers,
            useLayerMask = true
        };
        
        
    }
    #endregion

    #region Main Loop
    /// <summary>
    /// Runs every frame (60 FPS)
    /// </summary>
    private void FixedUpdate()
    {
        if (canInput)
        {
            // Set up variables first.
            inputtedMovement.Normalize();
            Vector2 velocity = Master.r2d.velocity;

            UpdateOWPCollision();
            UpdateWalk(velocity);
            UpdateDash();
            UpdateJumping();
        }
    }

    #region One Way Collision
    /// <summary>
    /// Update One Way Platform Collision.
    /// </summary>
    private void UpdateOWPCollision()
    {
        // Toggle collisions with one-way platforms if down is pressed.
        LayersManager.Instance.IgnoreCollisionsWithPlatforms(
            Master.c2d,
            !inputtedMovement.y.Approx(0) && inputtedMovement.y < 0
        );
    }
    #endregion

    #region Walk
    private void UpdateWalk(Vector2 velocity)
    {
        Vector2 force = new();

        if (inputtedMovement.x.Approx(0) && !velocity.x.Approx(0))
        {
            // Apply a backwards force to stop the player.
            force.x = -velocity.x * decelerationPercentage * moveAcceleration.x;
        }
        else if (CanMoveInDirection(inputtedMovement.x, velocity.x, maxMoveSpeed.x))
        {
            // The horizontal speed is less than the max horizontal speed. Allow
            // character to move.
            force.x = inputtedMovement.x * moveAcceleration.x;
        }

        if (CanMoveInDirection(inputtedMovement.y, velocity.y, maxMoveSpeed.y))
        {
            // Ditto for max vertical speed.
            force.y = inputtedMovement.y * moveAcceleration.y;
        }

        // Note: This is the "correct" way to change the velocity of a
        // rigidbody. Directly setting the velocity often leads to weird
        // results, such as the shotgun not being able to properly move the
        // character.
        //
        // Another note: ForceMode2D.Impulse is only for instantaneous changes in
        // force. For continuous force, use ForceMode2D.Force.
        Master.r2d.AddForce(force);

        // Handle animations.
        UpdateWalkAnim(velocity);
    }

    private void UpdateWalkAnim(Vector2 velocity)
    {
        // Walk animation.
        if (characterAnimator)
        {
            Vector2 paramVal = positiveAnimParamOnly ? velocity.Abs() : velocity;
            if (!string.IsNullOrWhiteSpace(animParamSpeedX))
            {
                characterAnimator.SetFloat(animParamSpeedX, paramVal.x);
            }

            if (!string.IsNullOrWhiteSpace(animParamSpeedY))
            {
                characterAnimator.SetFloat(animParamSpeedY, paramVal.y);
            }
        }
    }
    #endregion

    #region Jumping
    private void UpdateJumping()
    {
        // Handle jumping.
        jumpCooldown.IncrementFixedUpdate(false);
        coyoteTimer.IncrementFixedUpdate(false);

        if (!inputtedJump && CanJump())
            oneJump = true;

        if (inputtedJump && CanJump() && oneJump)
        {
            if(!GroundCheck)
            {
                numJumps -= 1;
            }
            // Since a jump is only performed for one fixed update, it must be
            // an impulse.
            Master.r2d.velocity = new Vector2(Master.r2d.velocity.x, 0);
            Master.r2d.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);

            groundedStatus = GroundedStatus.AirbornFromJump;

            oneJump = false;

            coyoteTimer.Reset();
            jumpCooldown.Reset();
        }
        else if (GroundCheck)
        {
            numJumps = totalJumps;
            groundedStatus = GroundedStatus.Grounded;
            coyoteTimer.Reset();
        }
        else if (groundedStatus != GroundedStatus.AirbornFromJump)
        {
            groundedStatus = GroundedStatus.AirbornFromFall;
        }

        // Handle animation.
        UpdateJumpAnim();
    }

    private void UpdateJumpAnim()
    {
        // Jump animation.
        if (!string.IsNullOrWhiteSpace(animParamJump) && characterAnimator)
        {
            characterAnimator.SetBool(animParamJump,
                groundedStatus == GroundedStatus.AirbornFromJump);
        }
    }
    #endregion

    #region Dash
    private void UpdateDash()
    {

        if (movementStatus == MovementStatus.Dashing)
        {
            if (isDashing)
            {
                //Master.r2d.gravityScale = 0;

                // We are dashing.

                Master.r2d.velocity = lockedDashInput * dashSpeed;
                return;
            }
            else
            {
                // Is dash time done? (do NOT reset DashTimer)
                Master.r2d.bodyType = RigidbodyType2D.Dynamic;
                Master.r2d.gravityScale = originalGravityScale;
                movementStatus = MovementStatus.Normal;

            }
        }

        if (!inputtedDash.ApproxZero() && canDash)
        {
            // Do dash here.
            inputtedDash.Normalize();

            // Now do dash.
            isDashing = true;
            canDash = false;
            dashTime = Time.time;
            Invoke(nameof(ResetDash), dashTimer);
            Invoke(nameof(ResetDashTimer), dashCooldown);
            lockedDashInput = inputtedDash;
            movementStatus = MovementStatus.Dashing;

            // Switch to a kinematic collider so the player is forced in one
            // direction. Also shoves all enemies out of the way.
            Master.r2d.bodyType = RigidbodyType2D.Dynamic;
            Master.r2d.velocity = inputtedDash * dashSpeed;

            
        }
    }

    private void ResetDashTimer()
    {
        canDash = true;
    }

    private void ResetDash()
    {
        isDashing = false;
    }

    #endregion
    #endregion

    #region Helper Methods
    private bool CanJump()
    {
        return jumpCooldown.IsDone &&
            (GroundCheck || numJumps > 0);
    }

    private bool CanMoveInDirection(float input, float velocity, float maxSpeed)
    {
        if (input.Sign() != velocity.Sign())
        {
            // Try to slow down. Always allow this.
            return true;
        }

        // Check if moving slower than max speed.
        return Mathf.Abs(velocity) < maxSpeed;
    }
    #endregion
    #endregion
}