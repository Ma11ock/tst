using Godot;

// Notes on Quake's movement code: It's coordinate system is different to that
// of Godot's. In Quake Z is up/down, in Godot Z is forwards/backwards and Y is
// up/down.

using Snap = Godot.Collections.Dictionary<string, object>;

namespace Tst {

/// <summary>
/// </summary>
public struct Input {
    public float mouseX;
    public float mouseY;
    public float strafe;
    public float forwards;

    public Input(float mouseX, float mouseY, float strafe, float forwards) {
        this.mouseX = mouseX;
        this.mouseY = mouseY;
        this.strafe = strafe;
        this.forwards = forwards;
    }

    public Input DeltaMouse(float dx, float dy) {
        return new Input(mouseX + dx, mouseY + dy, strafe, forwards);
    }

    public Input ResetMouse() {
        return new Input(0F, 0F, strafe, forwards);
    }

    public override string ToString() {
        return $"mouseX = {mouseX}, mouseY = {mouseY}, strafe = {strafe}, forwards = {forwards}";
    }
}
}

/// <summary>
/// Player class.
/// </summary>
public class Player : KinematicBody, Tst.Debuggable, Tst.Snappable {
    // Children nodes.
    /// <summary>
    /// Body reference.
    /// </summary>
    private Godot.Spatial mBody = null;
    /// <summary>
    /// Head reference.
    /// </summary>
    private Godot.Spatial mHead = null;
    /// <summary>
    /// Camera reference.
    /// </summary>
    private Godot.Camera mCamera = null;
    /// <summary>
    /// Model reference.
    /// </summary>
    private Godot.MeshInstance mModel = null;
    /// <summary>
    /// Movement tween reference for softer movement.
    /// </summary>
    private Godot.Tween mMovementTween = null;

    /// <summary>
    /// Input struct for the current frame.
    /// </summary>
    private Tst.Input mInputs = new Tst.Input();
    /// <summary>
    /// True if this player instance is client's player character.
    /// </summary>
    private bool mIsRealPlayer = false;

    /// <summary>
    /// Mark this player as being the client's player.
    /// </summary>
    public void SetRealPlayer() {
        mIsRealPlayer = true;
    }

    /// <summary>
    /// Real id of the network.
    /// </summary>
    private int _mNetworkId = 0;

    /// <summary>
    /// Id of the network. Only settable once.
    /// </summary>
    public int mNetworkId {
        get => _mNetworkId;
        set {
            if (_mNetworkId == 0) {
                _mNetworkId = value;
            }
        }
    }

    /// <summary>
    /// Id of the network. Only settable once.
    /// </summary>
    private Tst.SnapAction mSnapshotType = Tst.SnapAction.None;

    private ulong mSnappableId = 0;

    public ulong GetId() => mSnappableId;

    public void SetId(ulong id) => mSnappableId = id;

    public Tst.Snappable Clone() {
        return new Player();
    }

    public Tst.SnapAction GetSnapAction() {
        return mSnapshotType;
    }

    /// <summary>
    /// Reference to the Scene's debug overlay.
    /// </summary>
    private DebugOverlay mDebugOverlay = null;

    // Quake physics objects.
    /// <summary>
    /// Gravity acceleration.
    /// </summary>
    static private float gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
    /// <summary>
    /// Client's mouse sensitivity.
    /// </summary>
    [Export]
    private float mMouseSensitivity = 0.05F;
    /// <summary>
    /// Max speed of the player on the ground.
    /// </summary>
    [Export]
    private float mMaxSpeed = 10F;
    /// <summary>
    /// Max speed of the player in the air.
    /// </summary>
    [Export]
    private float mMaxAirSpeed = 0.6F;
    /// <summary>
    /// Acceleration when moving.
    /// </summary>
    [Export]
    private float mAcceleration = 60F;
    /// <summary>
    /// Ground friction.
    /// </summary>
    [Export]
    private float mFriction = 6F;
    /// <summary>
    /// Y velocity when jumping.
    /// </summary>
    [Export]
    private float mJumpImpulse = 8F;
    /// <summary>
    /// Terminal velocity, fastest a player is able to fall.
    /// </summary>
    private float mTerminalVelocity = gravity * -5F;
    /// <summary>
    /// Snap vector, used for stairs and some collision physics.
    /// </summary>
    private Vector3 mSnap = Vector3.Zero;  // Needed for MoveAndSlideWithSnap(), which enables
                                           // going down slopes without falling.

    private Vector3 mVelocity = Vector3.Zero;
    private Vector3 mWishDir = Vector3.Zero;
    private Vector3 mGravityVec = Vector3.Zero;
    private float mVerticalVelocity = 0F;  // Vertical component of velocity.
    private bool mWishJump = false;  // If true, player has queued a jump : the jump key can be
                                     // held down before hitting the ground to jump.
    private bool mAutoJump = false;  // If true, player has queued a jump : the jump key can be
                                     // held down before hitting the ground to jump.

    // For stair snapping.
    public const float STAIRS_FEELING_COEFFICIENT = 2.5F;
    public const float WALL_MARGIN = 0.001F;
    public static readonly Vector3 STEP_HEIGHT_DEFAULT = new Vector3(0F, 0.6F, 0F);
    public const float STEP_MAX_SLOPE_DEGREE = 0F;
    public const int STEP_CHECK_COUNT = 2;
    private Vector3 mStepCheckHeight = STEP_HEIGHT_DEFAULT / STEP_CHECK_COUNT;
    private Vector3 mHeadOffset = Vector3.Zero;
    private float mBodyEulerY = 0F;
    private bool mIsStep = false;

    // Camera interpolation on stairs.
    private float mCameraFeelingCoefficient = 2.5F;
    private Vector3 mCameraTargetPos = Vector3.Zero;
    private float mCameraCoefficient = 1.0F;
    private float mTimeInAir = 0F;

    private ulong mDebugId = 0;

    public ulong GetDebugId() => mDebugId;

    public void SetDebugId(ulong id) => mDebugId = id;

    public void GetDebug(Control c) {
        if (c is Godot.Label label) {
            label.Text = $@"Velocity: {mVelocity}
Wishdir: {mWishDir}
Wish jump: {mWishJump}
Is on floor: {IsOnFloor()}
Vertical velocity: {mVerticalVelocity}";
        }
    }

    public string GetInputDescription() => mInputs.ToString();

    public Vector3 GetLookAt() => mHead.Transform.basis.z;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready() {
        base._Ready();

        mDebugOverlay = GetParent().GetNodeOrNull<DebugOverlay>(Scene.DEBUG_OVERLAY_NAME);
        if (mDebugOverlay != null) {
            mDebugOverlay.Add<Label>(this);
        }
        mBody = GetNode<Godot.Spatial>("Body");
        mHead = mBody.GetNode<Godot.Spatial>("Head");
        mCamera = mHead.GetNode<Godot.Camera>("Camera");
        mMovementTween = GetNode<Godot.Tween>("MovementTween");

        Input.MouseMode = Input.MouseModeEnum.Captured;
        mBodyEulerY = mBody.GlobalTransform.basis.GetEuler().y;

        mCameraTargetPos = mCamera.GlobalTransform.origin;
        mCamera.SetAsToplevel(true);
        mCamera.PhysicsInterpolationMode = Godot.Node.PhysicsInterpolationModeEnum.Off;

        mModel = GetNode<Godot.MeshInstance>("Model");

        mCamera.Current = mIsRealPlayer;
        mModel.Visible = !mIsRealPlayer;
    }

    public override void _UnhandledInput(InputEvent @event) {
        base._Input(@event);

        if (!mIsRealPlayer) {
            return;
        }
        if (@event is InputEventMouseMotion mouseEvent &&
            Input.MouseMode == Input.MouseModeEnum.Captured) {
            float dx = -mouseEvent.Relative.x * mMouseSensitivity;
            float dy = -mouseEvent.Relative.y * mMouseSensitivity;
            mInputs = mInputs.DeltaMouse(dx, dy);
        }
    }

    public override void _Process(float delta) {
        base._Process(delta);
        //  Find the current interpolated transform of the target.
        Transform tr = mHead.GetGlobalTransformInterpolated();

        // Provide some delayed smoothed lerping towards the target position.
        mCameraTargetPos = Util.Lerp(mCameraTargetPos, tr.origin,
                                     delta * mVelocity.Length() * STAIRS_FEELING_COEFFICIENT);

        mCamera.Translation = Util.ChangeX(mCamera.Translation, tr.origin.x);

        if (IsOnFloor()) {
            mTimeInAir = 0F;
            mCameraCoefficient = 1.0F;
            mCamera.Translation = Util.ChangeY(mCamera.Translation, mCameraTargetPos.y);
        } else {
            mTimeInAir += delta;
            if (mTimeInAir > 1.0F) {
                mCameraCoefficient += delta;
                mCameraCoefficient = Mathf.Clamp(mCameraCoefficient, 2.0F, 4.0F);
            } else {
                mCameraCoefficient = 2.0F;
            }

            mCamera.Translation = Util.ChangeY(mCamera.Translation, mCameraTargetPos.y);
        }

        mCamera.Translation = Util.ChangeZ(mCamera.Translation, tr.origin.z);
        mCamera.Rotation =
            Util.ChangeXY(mCamera.Rotation, mHead.Rotation.x, mBody.Rotation.y + mBodyEulerY);
    }

    /// Needed for debugging window.
    public bool PlayerIsOnFloor() {
        return IsOnFloor();
    }

    public override void _PhysicsProcess(float delta) {
        base._PhysicsProcess(delta);

        mIsStep = false;
        // int inputNum = 1;  // For sending inputs over the network.

        float forwardInput = mInputs.forwards;
        float strafeInput = mInputs.strafe;
        // TODO better way to ignore captured input.
        if (mIsRealPlayer && !Global.InputCaptured) {
            forwardInput =
                Input.GetActionStrength("move_backward") - Input.GetActionStrength("move_forward");
            strafeInput =
                Input.GetActionStrength("move_right") - Input.GetActionStrength("move_left");

            mInputs = new Tst.Input(mInputs.mouseX, mInputs.mouseY, strafeInput, forwardInput);
            SendInputPacket();
        }
        MoveHead(mInputs.mouseX, mInputs.mouseY);
        mInputs = mInputs.ResetMouse();

        mWishDir = new Vector3(strafeInput, 0F, forwardInput)
                       .Rotated(Vector3.Up, mBody.GlobalTransform.basis.GetEuler().y)
                       .Normalized();

        // Move.
        if (IsOnFloor()) {
            if (mWishJump) {
                // # If we're on the ground but wish_jump is still true, this means we've just
                // landed.
                mSnap = Vector3.Zero;  // Set snapping to zero so we can get off the ground.
                mVerticalVelocity = mJumpImpulse;  // Jump.

                MoveAir(mVelocity, delta);  // Mimic Quake's way of treating first frame after
                // landing as still in the air.

                mWishJump = false;  // We have jumped, the player needs to press jump key again.
                mGravityVec += Vector3.Down * gravity * delta;
            } else {
                // Player is on the ground. Move normally, apply friction.
                mVerticalVelocity = 0F;
                mSnap = -GetFloorNormal();
                MoveGround(mVelocity, delta);
                mGravityVec = Vector3.Zero;
            }
        } else {
            // We're in the air. Do not apply friction
            mSnap = Vector3.Down;
            mVerticalVelocity -= (mVerticalVelocity >= mTerminalVelocity) ? gravity * delta : 0F;
            MoveAir(mVelocity, delta);
            mGravityVec += Vector3.Down * gravity * delta;
        }

        if (mVelocity != Vector3.Zero || mVerticalVelocity != 0F) {
            mSnapshotType = Tst.SnapAction.Delta;
        } else {
            mSnapshotType = Tst.SnapAction.None;
        }

        if (IsOnCeiling()) {
            mVerticalVelocity = 0F;
        }

        QueueJump();

        if (mGravityVec.y >= 0) {
            for (int i = 0; i < STEP_CHECK_COUNT; i++) {
                PhysicsTestMotionResult testMotionResult = new PhysicsTestMotionResult();

                Vector3 stepHeight = STEP_HEIGHT_DEFAULT - i * mStepCheckHeight;
                Transform transform3d = GlobalTransform;
                Vector3 motion = stepHeight;
                bool isPlayerCollided = PhysicsServer.BodyTestMotion(GetRid(), transform3d, motion,
                                                                     false, testMotionResult);

                if (testMotionResult.CollisionNormal.y < 0) {
                    continue;
                }

                if (!isPlayerCollided) {
                    transform3d.origin += stepHeight;
                    motion = mVelocity * delta;
                    isPlayerCollided = PhysicsServer.BodyTestMotion(GetRid(), transform3d, motion,
                                                                    false, testMotionResult);

                    if (!isPlayerCollided) {
                        transform3d.origin += motion;
                        motion = -stepHeight;
                        isPlayerCollided = PhysicsServer.BodyTestMotion(
                            GetRid(), transform3d, motion, false, testMotionResult);
                        if (isPlayerCollided) {
                            if (testMotionResult.CollisionNormal.AngleTo(Vector3.Up) <=
                                Mathf.Deg2Rad(STEP_MAX_SLOPE_DEGREE)) {
                                mHeadOffset = -testMotionResult.MotionRemainder;
                                mIsStep = true;
                                GlobalTransform = Util.ChangeTFormOrigin(
                                    GlobalTransform,
                                    GlobalTransform.origin + -testMotionResult.MotionRemainder);
                                break;
                            }
                        }
                    } else {
                        Vector3 wallCollisionNormal = testMotionResult.CollisionNormal;

                        transform3d.origin += testMotionResult.CollisionNormal * WALL_MARGIN;
                        motion = (mVelocity * delta).Slide(wallCollisionNormal);
                        isPlayerCollided = PhysicsServer.BodyTestMotion(
                            GetRid(), transform3d, motion, false, testMotionResult);

                        if (!isPlayerCollided) {
                            transform3d.origin += motion;
                            motion = -stepHeight;

                            isPlayerCollided = PhysicsServer.BodyTestMotion(
                                GetRid(), transform3d, motion, false, testMotionResult);

                            if (isPlayerCollided &&
                                testMotionResult.CollisionNormal.AngleTo(Vector3.Up) <=
                                    Mathf.Deg2Rad(STEP_MAX_SLOPE_DEGREE)) {
                                mHeadOffset = -testMotionResult.MotionRemainder;
                                mIsStep = true;
                                GlobalTransform = Util.ChangeTFormOrigin(
                                    GlobalTransform,
                                    GlobalTransform.origin + -testMotionResult.MotionRemainder);
                                break;
                            }
                        }
                    }
                } else {
                    Vector3 wallCollisionNormal = testMotionResult.CollisionNormal;
                    transform3d.origin += testMotionResult.CollisionNormal * WALL_MARGIN;
                    motion = stepHeight;
                    isPlayerCollided = PhysicsServer.BodyTestMotion(GetRid(), transform3d, motion,
                                                                    false, testMotionResult);

                    if (!isPlayerCollided) {
                        transform3d.origin += stepHeight;
                        motion = (mVelocity * delta).Slide(wallCollisionNormal);
                        isPlayerCollided = PhysicsServer.BodyTestMotion(
                            GetRid(), transform3d, motion, false, testMotionResult);

                        if (!isPlayerCollided) {
                            transform3d.origin += motion;
                            motion = -stepHeight;
                            isPlayerCollided = PhysicsServer.BodyTestMotion(
                                GetRid(), transform3d, motion, false, testMotionResult);

                            if (isPlayerCollided) {
                                if (testMotionResult.CollisionNormal.AngleTo(Vector3.Up) <=
                                    Mathf.Deg2Rad(STEP_MAX_SLOPE_DEGREE)) {
                                    mHeadOffset = -testMotionResult.MotionRemainder;
                                    mIsStep = true;
                                    GlobalTransform = Util.ChangeTFormOrigin(
                                        GlobalTransform,
                                        GlobalTransform.origin + -testMotionResult.MotionRemainder);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        bool isFalling = false;

        if (!mIsStep && IsOnFloor()) {
            PhysicsTestMotionResult testMotionResult = new PhysicsTestMotionResult();

            Vector3 stepHeight = STEP_HEIGHT_DEFAULT;
            Transform transform3d = GlobalTransform;

            Vector3 motion = mVelocity * delta;
            bool isPlayerCollided = PhysicsServer.BodyTestMotion(GetRid(), transform3d, motion,
                                                                 false, testMotionResult);

            if (!isPlayerCollided) {
                transform3d.origin += motion;
                motion = -stepHeight;
                isPlayerCollided = PhysicsServer.BodyTestMotion(GetRid(), transform3d, motion,
                                                                false, testMotionResult);
                if (isPlayerCollided) {
                    if (testMotionResult.CollisionNormal.AngleTo(Vector3.Up) <=
                        Mathf.Deg2Rad(STEP_MAX_SLOPE_DEGREE)) {
                        mHeadOffset = testMotionResult.Motion;
                        mIsStep = true;
                        GlobalTransform = Util.ChangeTFormOrigin(
                            GlobalTransform,
                            GlobalTransform.origin + testMotionResult.MotionRemainder);
                    }
                } else {
                    isFalling = true;
                }

            } else {
                if (testMotionResult.CollisionNormal.y == 0) {
                    Vector3 wallCollisionNormal = testMotionResult.CollisionNormal;
                    transform3d.origin += testMotionResult.CollisionNormal * WALL_MARGIN;
                    motion = (mVelocity * delta).Slide(wallCollisionNormal);
                    isPlayerCollided = PhysicsServer.BodyTestMotion(GetRid(), transform3d, motion,
                                                                    false, testMotionResult);
                    if (!isPlayerCollided) {
                        transform3d.origin += motion;
                        motion = -stepHeight;
                        isPlayerCollided = PhysicsServer.BodyTestMotion(
                            GetRid(), transform3d, motion, false, testMotionResult);
                        if (isPlayerCollided) {
                            if (testMotionResult.CollisionNormal.AngleTo(Vector3.Up) <=
                                Mathf.Deg2Rad(STEP_MAX_SLOPE_DEGREE)) {
                                mHeadOffset = testMotionResult.Motion;
                                mIsStep = true;
                                GlobalTransform = Util.ChangeTFormOrigin(
                                    GlobalTransform,
                                    GlobalTransform.origin + testMotionResult.MotionRemainder);
                            }
                        } else {
                            isFalling = true;
                        }
                    }
                }
            }
        }

        if (!mIsStep) {
            mHeadOffset = mHeadOffset.LinearInterpolate(
                Vector3.Zero, delta * mVelocity.Length() * STAIRS_FEELING_COEFFICIENT);
        }

        if (isFalling) {
            mSnap = Vector3.Zero;
        }
        mVelocity =
            MoveAndSlideWithSnap(mVelocity, mSnap, Vector3.Up, false, 4, Mathf.Deg2Rad(46), false);

        if (mIsRealPlayer) {
            // Interpolate playermovement for smooth.
            mMovementTween.InterpolateProperty(
                this, "global_transform", GlobalTransform,
                new Transform(GlobalTransform.basis, GlobalTransform.origin), 0.1F);
            mMovementTween.Start();
        } else if (GetTree().IsNetworkServer()) {
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
    private Vector3 Friction(Vector3 inputVelocity, float delta) {
        float speed = inputVelocity.Length();
        if (speed < 0.1F) {
            return new Vector3(0F, inputVelocity.y, 0F);
        }

        float control = speed;  // speed < 5F ? 5F : speed;
        float drop = control * mFriction * delta;

        float newSpeed = speed - drop;
        if (newSpeed < 0F) {
            newSpeed = 0F;
        }

        newSpeed /= speed;

        Vector3 scaledVelocity = inputVelocity * newSpeed;
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

        nextVelocity = Friction(nextVelocity, delta);
        nextVelocity = Accelerate(mWishDir, nextVelocity, mAcceleration, mMaxSpeed, delta);

        // Then get back our vertical component, and move the player.
        nextVelocity.y = mVerticalVelocity;
        mVelocity = nextVelocity;
    }

    // Accelerate without applying friction (with a lower allowed max_speed).
    private void MoveAir(Vector3 velocity, float delta) {
        // We first work on only on the horizontal components of our current velocity.
        Vector3 nextVelocity = Vector3.Zero;
        nextVelocity.x = velocity.x;
        nextVelocity.z = velocity.z;
        nextVelocity = Accelerate(mWishDir, nextVelocity, mAcceleration, mMaxAirSpeed, delta);

        nextVelocity.y = mVerticalVelocity;
        mVelocity = nextVelocity;
    }

    private void MoveHead(float dx, float dy) {
        mBody.RotateY(Mathf.Deg2Rad(dx));
        mHead.RotateX(Mathf.Deg2Rad(dy));
        float newRotX = Mathf.Clamp(mHead.Rotation.x, Mathf.Deg2Rad(-89), Mathf.Deg2Rad(89));
        mHead.Rotation = Util.ChangeX(mHead.Rotation, newRotX);
    }

    T TryGetVOr<T>(Snap dat, string key, T or)
        where T : unmanaged {
        T? r = TryGetV<T>(dat, key);
        return (r == null) ? or : r.Value;
    }

    T? TryGetV<T>(Snap dat, string key)
        where T : unmanaged {
        object obj = null;
        dat.TryGetValue(key, out obj);

        if (obj == null || !(obj is T)) {
            return null;
        }

        return (T)obj;
    }

    T TryGetR<T>(Snap dat, string key)

        where T : class {
        object obj = null;
        dat.TryGetValue(key, out obj);

        if (obj == null || !(obj is T)) {
            return null;
        }

        return (T)obj;
    }

    [Master]
    public void SendPlayerInput(Snap dat) {
        float mouseDx = TryGetVOr<float>(dat, "mDx", 0F);
        float mouseDy = TryGetVOr<float>(dat, "mDy", 0F);
        float strafe = TryGetVOr<float>(dat, "str", 0F);
        float forward = TryGetVOr<float>(dat, "for", 0F);
        bool jump = TryGetVOr<bool>(dat, "jmp", false);
        bool autoJump = TryGetVOr<bool>(dat, "ajp", false);

        // Ensure all floats are valid values. Reject the input if so.
        if (!Util.IsFinite(mouseDx)) {
            GD.Print($"Mouse X value is not finite");
            return;
        } else if (!Util.IsFinite(mouseDy)) {
            GD.Print($"Mouse Y value is not finite");
            return;
        } else if (!Util.IsFinite(strafe)) {
            GD.Print($"Strafe value is not finite");
            return;
        } else if (!Util.IsFinite(forward)) {
            GD.Print($"Forward movement value is not finite");
            return;
        }

        mInputs = new Tst.Input(mouseDx, mouseDy, strafe, forward);
        mWishJump = jump;
        mAutoJump = autoJump;
    }

    [Puppet]
    public void UpdatePlayer(Snap recv) {
        // TODO instead of current values, should be the cache from the last recv'd snapshot.
        Transform globalTransform = TryGetVOr<Transform>(recv, "gt", GlobalTransform);
        Vector3 velocity = TryGetVOr<Vector3>(recv, "vel", mVelocity);
        bool wishJump = TryGetVOr<bool>(recv, "jmp", false);
        Vector3 gravityVec = TryGetVOr<Vector3>(recv, "grav", mGravityVec);
        Vector3 wishDir = TryGetVOr<Vector3>(recv, "wdir", mWishDir);
        float verticalVelocity = TryGetVOr<float>(recv, "vvel", mVerticalVelocity);
        Vector3 snap = TryGetVOr<Vector3>(recv, "snap", mSnap);
        Transform headTransform = TryGetVOr<Transform>(recv, "hgt", mHead.GlobalTransform);
        Transform bodyTransform = TryGetVOr<Transform>(recv, "bgt", mBody.GlobalTransform);

        // GD.Print($"Got back {globalTransform}, {velocity}");
        if (!NetworkSetup.IsServer) {
            GlobalTransform = globalTransform;
            mVelocity = velocity;
            mWishDir = wishDir;
            mWishJump = wishJump;
            mGravityVec = gravityVec;
            mVerticalVelocity = verticalVelocity;
            mSnap = snap;
            mHead.GlobalTransform = headTransform;
            mBody.GlobalTransform = bodyTransform;
        }
    }

    public override void _ExitTree() {
        base._ExitTree();

        if (mDebugOverlay != null) {
            mDebugOverlay.Remove(this);
        }
    }

    private void SendInputPacket() {
        Snap send = new Snap() {
            {"mDx",   mInputs.mouseX},
            {"mDy",   mInputs.mouseY},
            {"str",   mInputs.strafe},
            {"for", mInputs.forwards},
            {"jmp",        mWishJump},
            {"ajp",        mAutoJump}
        };
        RpcUnreliable("SendPlayerInput", send);
    }

    private void SendPlayerState() {
        Snap send = new Snap() {
            {  "gt",       GlobalTransform},
            { "hgt", mHead.GlobalTransform},
            { "bgt", mBody.GlobalTransform},
            { "vel",             mVelocity},
            { "jmp",             mWishJump},
            { "ajp",             mAutoJump},
            {"grav",           mGravityVec},
            {"snap",                 mSnap},
            {"vvel",     mVerticalVelocity},
            {"wdir",              mWishDir}
        };
        RpcUnreliableId(0, "UpdatePlayer", send);
    }
}
