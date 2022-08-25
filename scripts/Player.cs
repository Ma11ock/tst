using Godot;
using System;

// Notes on Quake's movement code: It's coordinate system is different to that
// of Godot's. In Quake Z is up/down, in Godot Z is forwards/backwards and Y is
// up/down.

public class Player : KinematicBody {
    // Children.
    private Godot.Spatial mHead = null;
    private Godot.Camera mCamera = null;

    // Quake physics objects.
    static private float gravity = 15F;
    [Export]
    private float mMouseSensitivity = 0.05F;
    [Export]
    private float mMaxSpeed = 6F;
    [Export]
    private float mMaxAirSpeed = 0.6F;
    [Export]
    private float mAcceleration = 60F;
    [Export]
    private float mFriction = 0.9F;
    [Export]
    private float mJumpImpulse = 4.8F;
    private float mTerminalVelocity = gravity * -5F;

    private Vector3 mSnap = Vector3.Zero;  // Needed for MoveAndSlideWithSnap(), which enables
                                           // going down slopes without falling.

    public Vector3 mVelocity { get; private set; } = Vector3.Zero;
    public Vector3 mWishDir { get; private set; } = Vector3.Zero;

    public float mVerticalVelocity { get; private set; } = 0F;  // Vertical component of velocity.

    private bool mWishJump = false;  // If true, player has queued a jump : the jump key can be held
                                     // down before hitting the ground to jump.
    private bool mAutoJump = false;  // If true, player has queued a jump : the jump key can be held
                                     // down before hitting the ground to jump.

    // Called when the node enters the scene tree for the first time.
    public override void _Ready() {
        base._Ready();

        mHead = GetNode<Godot.Spatial>("Head");
        mCamera = mHead.GetNode<Godot.Camera>("Camera");

        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _Input(InputEvent @event) {
        base._Input(@event);

        // Move head.
        // Maybe in physics process because it changes wishdir.
        if (@event is InputEventMouseMotion mouseEvent &&
            Input.MouseMode == Input.MouseModeEnum.Captured) {
            mHead.RotateX(Mathf.Deg2Rad(-mouseEvent.Relative.y * mMouseSensitivity));
            RotateY(Mathf.Deg2Rad(-mouseEvent.Relative.x * mMouseSensitivity));

            Vector3 cameraRot = mHead.RotationDegrees;
            cameraRot.x = Mathf.Clamp(cameraRot.x, -90, 90);
            mHead.RotationDegrees = cameraRot;
        }
    }

    /// Needed for debugging window.
    public bool PlayerIsOnFloor() {
        return IsOnFloor();
    }

    public override void _PhysicsProcess(float delta) {
        base._PhysicsProcess(delta);

        float forwardInput =
            Input.GetActionStrength("move_backward") - Input.GetActionStrength("move_forward");
        float strafeInput =
            Input.GetActionStrength("move_right") - Input.GetActionStrength("move_left");
        mWishDir = new Vector3(strafeInput, 0F, forwardInput)
                       .Rotated(Vector3.Up, GlobalTransform.basis.GetEuler().y)
                       .Normalized();
        QueueJump();

        if (IsOnFloor()) {
            if (mWishJump) {
                // # If we're on the ground but wish_jump is still true, this means we've just
                // landed.
                mSnap = Vector3.Zero;  // Set snapping to zero so we can get off the ground.
                mVerticalVelocity = mJumpImpulse;  // Jump.

                MoveAir(mVelocity, delta);  // Mimic Quake's way of treating first frame after
                                            // landing as still in the air.

                mWishJump = false;  // We have jumped, the player needs to press jump key again.
            } else {
                // Player is on the ground. Move normally, apply friction.
                mVerticalVelocity = 0F;
                mSnap = -GetFloorNormal();
                MoveGround(mVelocity, delta);
            }
        } else {
            // We're in the air. Do not apply friction
            mSnap = Vector3.Down;
            mVerticalVelocity = Mathf.Clamp(gravity * delta, 0F, mTerminalVelocity);
            //mVerticalVelocity -= (mVerticalVelocity < mTerminalVelocity) ? gravity * delta : 0F;
            MoveAir(mVelocity, delta);
        }

        if (IsOnCeiling()) {
            mVerticalVelocity = 0F;
        }
    }

    // This is were we calculate the speed to add to current velocity
    private Vector3 Accelerate(Vector3 wishDir, Vector3 inputVelocity, float acceleration,
                               float maxSpeed, float delta) {
        // Current speed is calculated by projecting our velocity onto wishdir.
        // We can thus manipulate our wishdir to trick the engine into thinking we're going slower
        // than we actually are, allowing us to accelerate further.
        float currentSpeed = inputVelocity.Dot(wishDir);

        // Next, we calculate the speed to be added for the next frame.
        // If our current speed is low enough, we will add the max acceleration.
        // If we're going too fast, our acceleration will be reduced (until it evenutually hits 0,
        // where we don't add any more speed).
        float addSpeed = Mathf.Clamp(maxSpeed - currentSpeed, 0F, acceleration * delta);

        return inputVelocity + wishDir * addSpeed;
    }

    // Scale down horizontal velocity.
    // For now, we're simply substracting 10% from our current velocity. This is not how it works in
    // engines like idTech or Source !
    private Vector3 Friction(Vector3 inputVelocity) {
        float speed = inputVelocity.Length();
        Vector3 scaledVelocity = inputVelocity * mFriction;

        if (scaledVelocity.Length() < (mMaxSpeed / 100F)) {
            scaledVelocity = Vector3.Zero;
        }

        return scaledVelocity;
    }

    // Set wish_jump depending on player input.
    private void QueueJump() {
        // If auto_jump is true, the player keeps jumping as long as the key is kept down.
        if (mAutoJump) {
            mWishJump = Input.IsActionPressed("jump");
            return;
        }

        if (Input.IsActionJustPressed("jump")) {
            mWishJump = !mWishJump;
        }
    }

    // Apply friction, then accelerate.
    private void MoveGround(Vector3 velocity, float delta) {
        // We first work on only on the horizontal components of our current velocity.
        Vector3 nextVelocity = Vector3.Zero;
        nextVelocity.x = velocity.x;
        nextVelocity.z = velocity.z;

        nextVelocity = Friction(nextVelocity);
        nextVelocity = Accelerate(mWishDir, nextVelocity, mAcceleration, mMaxSpeed, delta);

        // Then get back our vertical component, and move the player.
        nextVelocity.y = mVerticalVelocity;
        mVelocity = MoveAndSlideWithSnap(nextVelocity, mSnap, Vector3.Up);
    }

    // Accelerate without applying friction (with a lower allowed max_speed).
    private void MoveAir(Vector3 velocity, float delta) {
        // We first work on only on the horizontal components of our current velocity.
        Vector3 nextVelocity = Vector3.Zero;
        nextVelocity.x = velocity.x;
        nextVelocity.z = velocity.z;
        nextVelocity = Accelerate(mWishDir, nextVelocity, mAcceleration, mMaxAirSpeed, delta);

        nextVelocity.y = mVerticalVelocity;
        mVelocity = MoveAndSlideWithSnap(nextVelocity, mSnap, Vector3.Up);
    }
}
