using Godot;
using System.Collections.Generic;

// Notes on Quake's movement code: It's coordinate system is different to that
// of Godot's. In Quake Z is up/down, in Godot Z is forwards/backwards and Y is
// up/down.

using Snap = Godot.Collections.Dictionary;

namespace Tst {

/// <summary>
/// Player input struct.
/// </summary>
public struct Input {
    /// <summary>
    /// Strafe values. Should range from [-1,1].
    /// </summary>
    public float strafe { get; private set; }
    /// <summary>
    /// Forward/backwards values. Should range from [-1,1].
    /// </summary>
    public float forwards { get; private set; }
    /// <summary>
    /// Change in mouse's X direction.
    /// </summary>
    public float dx { get; private set; }
    /// <summary>
    /// Change in mouse's Y direction.
    /// </summary>
    public float dy { get; private set; }
    /// <summary>
    /// If true, player has queued a jump : the jump key can be held down before hitting the ground
    /// to jump.
    /// </summary>
    public bool jump { get; private set; }

    public ulong id { get; private set; }

    /// <summary>
    /// Create a new input with strafe and forward set, everything else set to 0.
    /// </summary>
    /// <param name="strafe"> Strafe value. </param>
    /// <param name="strafe"> Forwards value. </param>
    public Input(float strafe, float forwards) {
        this.strafe = strafe;
        this.forwards = forwards;
        this.dx = 0F;
        this.dy = 0F;
        this.jump = false;
        this.id = 0;
    }

    /// <summary>
    /// Create a new input. Full constructor, sets all members.
    /// <param name="strafe"> Strafe value. </param>
    /// <param name="strafe"> Forwards value. </param>
    /// <param name="dx"> Mouse x value. </param>
    /// <param name="dy"> Mouse y value. </param>
    /// <param name="jump"> This frame's jump value. </param>
    /// </summary>
    public Input(float strafe, float forwards, float dx, float dy, bool jump) {
        this.strafe = strafe;
        this.forwards = forwards;
        this.dx = dx;
        this.dy = dy;
        this.jump = jump;
        this.id = 0;
    }

    /// <summary>
    /// Create a new input. Full constructor, sets all members.
    /// </summary>
    /// <returns> Create a string representation of all of <c>Input</c>'s members. </returns>
    public override string ToString() =>
        $"strafe = {strafe}, forwards = {forwards}, dx = {dx}, dy = {dy}, jump = {jump}";

    /// <summary>
    /// Create a new input. Sets the mouse values, all other values are unchanged.
    /// </summary>
    /// <param name="dx"> Mouse x value. </param>
    /// <param name="dy"> Mouse y value. </param>
    public Input SetMouse(float dx, float dy) => new Input(this.strafe, this.forwards, dx, dy,
                                                           this.jump);
    /// <summary>
    /// Create a new input. Sets the mouse values to <see cref="dx"> + dx, and <see cref="dy"> + dy.
    /// Keep all remaining values the same.
    /// </summary>
    /// <param name="dx"> Mouse x value. </param>
    /// <param name="dy"> Mouse y value. </param>
    public Input DeltaMouse(float dx, float dy) => new Input(this.strafe, this.forwards,
                                                             this.dx + dx, this.dy + dy, this.jump);

    /// <summary>
    /// Create a new input. Sets the mouse values to <see cref="dx"> + dx, and <see cref="dy"> + dy.
    /// Keep all remaining values the same.
    /// </summary>
    /// <param name="dx"> Mouse x value. </param>
    /// <param name="dy"> Mouse y value. </param>
    public Input ResetMouse() => SetMouse(0F, 0F);

    /// <summary>
    /// Create a new input. Sets the direction to strafe and forwards. All other values remain the
    /// same.
    /// </summary>
    /// <param name="strafe"> Strafe value. </param>
    /// <param name="strafe"> Forwards value. </param>
    public Input SetDirs(float strafe, float forwards) => new Input(strafe, forwards, this.dx,
                                                                    this.dy, this.jump);

    /// <summary>
    /// Create a new input. Sets <see cref="jump"> to jump. All other values remain the same.
    /// </summary>
    /// <param name="jump"> Jump value. </param>
    public Input SetJump(bool wishJump = true) => new Input(this.strafe, this.forwards, this.dx,
                                                            this.dy, wishJump);

    public Input SetId(ulong id) {
        this.id = id;
        return this;
    }
}
}

/// <summary>
/// Player class. When run on a client, the Player can be:
/// <list type="bullet">
/// <item><term>Client's player</term><description>The player for this client's session. Interprets
/// inputs, does client-side prediction, etc.</description>
/// </item>
/// <item>
/// <term>Client puppet player</term>
/// <description>Player object of someone else's client. The player is a dummy object
/// mastered by the server. It acts as any other physics object in the
/// simulation.</description>
/// </item>
//// <item>
/// <term>Server player</term>
/// <description>Remote player /represented by a client. Interprets user
/// inputs.</description>
/// </item>
/// </list>
/// </summary>
public class Player : KinematicBody, Tst.Debuggable {
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
    /// Queue of player inputs. Used only by the server.
    /// </summary>
    private Queue<Snap> mPlayerInputQueue = null;

    /// <summary>
    /// Last state predicted by the client. Only used by the client's real player.
    /// </summary>
    private Snap mLastPredictedState = null;

    /// <summary>
    /// Input counter used to track which inputs the server has processed. Used only by the client's
    /// player.
    /// </summary>
    private ulong mInputIdCounter = 0;

    /// <summary>
    /// List of player inputs. Used only by the client for rollback and reconciliation.
    /// </summary>
    private List<Tst.Input> mPlayerInputList = null;

    /// <summary>
    /// Input struct for the current frame.
    /// </summary>
    private Tst.Input mInputs = new Tst.Input(0F, 0F, 0F, 0F, false);
    /// <summary>
    /// True if this player instance is client's player character.
    /// </summary>
    public bool mIsRealPlayer { get; private set; } = false;

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
    /// Reference to the Scene's debug overlay.
    /// </summary>
    private DebugOverlay mDebugOverlay = null;

    /// <summary>
    /// Reference to the scene manager.
    /// </summary>
    private Scene mSceneRef = null;

    // Quake physics objects.
    /// <summary>
    /// Gravity acceleration.
    /// </summary>
    static private float gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
    /// <summary>
    /// Client's mouse sensitivity.
    /// </summary>
    [Export]
    private float mMouseSensitivity = 0.09F;
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
    /// Snap vector, used for stairs and some collision physics. Needed for MoveAndSlideWithSnap(),
    /// which enables going down slopes without falling.
    /// </summary>
    private Vector3 mSnap = Vector3.Zero;

    /// <summary>
    /// The n'th tick the game server is on. Used only by the server and the client's player.
    /// </summary>
    public ulong mCurTick = 0;

    private Vector3 mVelocity = Vector3.Zero;
    private Vector3 mWishDir = Vector3.Zero;
    private Vector3 mGravityVec = Vector3.Zero;
    private float mVerticalVelocity = 0F;  // Vertical component of velocity.

    /// <summary>
    /// If true the player is currently jumping.
    /// </summary>
    private bool mIsJump = false;
    /// <summary>
    /// If true, player has queued a jump : the jump key can be
    /// held down before hitting the ground to jump.
    /// </summary>
    private bool mAutoJump = false;

    // For stair snapping.
    /// <summary>
    /// Linear interpolation constant for stair stepping.
    /// </summary>
    public const float STAIRS_FEELING_COEFFICIENT = 2.5F;
    /// <summary>
    /// Constant for wall collisions in stairs code.
    /// </summary>
    public const float WALL_MARGIN = 0.001F;
    /// <summary>
    /// Constant for checking step height.
    /// </summary>
    public static readonly Vector3 STEP_HEIGHT_DEFAULT = new Vector3(0F, 0.6F, 0F);
    /// <summary>
    /// Constant for checking step angles.
    /// </summary>
    public const float STEP_MAX_SLOPE_DEGREE = 0F;
    /// <summary>
    /// Times to check for steps.
    /// </summary>
    public const int STEP_CHECK_COUNT = 2;
    /// <summary>
    /// ?
    /// </summary>
    private Vector3 mStepCheckHeight = STEP_HEIGHT_DEFAULT / STEP_CHECK_COUNT;
    /// <summary>
    ///
    /// </summary>
    private Vector3 mHeadOffset = Vector3.Zero;
    /// <summary>
    /// Euler angle in Y direction for interpolation.
    /// </summary>
    private float mBodyEulerY = 0F;

    // Camera interpolation on stairs.
    /// <summary>
    /// Camera target position for stairstep interpolation.
    /// </summary>
    private Vector3 mCameraTargetPos = Vector3.Zero;
    /// <summary>
    /// TODO
    /// </summary>
    private float mCameraCoefficient = 1.0F;
    /// <summary>
    /// Time spent in air for interpolation.
    /// </summary>
    private float mTimeInAir = 0F;

    /// <summary>
    /// ID for debugging system.
    /// </summary>
    private ulong mDebugId = 0;

    /// <summary>
    /// Get the debugger id.
    /// </summary>
    public ulong GetDebugId() => mDebugId;

    /// <summary>
    /// Set the debugger ID.
    /// </summary>
    public void SetDebugId(ulong id) => mDebugId = id;

    public void GetDebug(Control c) {
        if (c is Godot.Label label) {
            label.Text = $@"Velocity: {mVelocity}
Wishdir: {mWishDir}
GlobalT: {GlobalTransform}
Is jump: {mIsJump}
Is on floor: {IsOnFloor()}
Vertical velocity: {mVerticalVelocity}";
        }
    }

    public string GetInputDescription() => mInputs.ToString();

    public Vector3 GetLookAt() => mHead.Transform.basis.z;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready() {
        base._Ready();

        mSceneRef = (Scene)GetParent();
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

        // Setup specific to server/client.
        if (GetTree().IsNetworkServer()) {
            mPlayerInputQueue = new Queue<Snap>();
        } else {
            mPlayerInputList = new List<Tst.Input>();
            mLastPredictedState = SnapshotState();
        }
    }

    public override void _UnhandledInput(InputEvent @event) {
        base._UnhandledInput(@event);

        if (!mIsRealPlayer) {
            return;
        }
        if (@event is InputEventMouseMotion mouseEvent &&
            Input.MouseMode == Input.MouseModeEnum.Captured) {
            float dx = -mouseEvent.Relative.x * mMouseSensitivity;
            float dy = -mouseEvent.Relative.y * mMouseSensitivity;
            mInputs = mInputs.DeltaMouse(dx, dy);
            MoveHead(dx, dy);
        }
    }

    public override void _Process(float delta) {
        base._Process(delta);
        if (!mIsRealPlayer) {
            return;
        }
        //  Find the current interpolated transform of the target.
        Transform tr = mHead.GetGlobalTransformInterpolated();

        // Provide some delayed smoothed lerping towards the target position.
        mCameraTargetPos = Util.Lerp(mCameraTargetPos, tr.origin,
                                     delta * mVelocity.Length() * STAIRS_FEELING_COEFFICIENT);

        mCamera.Translation = Util.ChangeX(mCamera.Translation, tr.origin.x);

        // TODO fix mCameraCoefficient vs CAMERA_COEFFICIENT
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

    private void SimulatePhysics(float delta, bool dummyInput = false) {
        bool isStep = false;

        float forwardInput = mInputs.forwards;
        float strafeInput = mInputs.strafe;
        if (mIsRealPlayer && !Global.InputCaptured && !dummyInput) {
            forwardInput =
                Input.GetActionStrength("move_backward") - Input.GetActionStrength("move_forward");
            strafeInput =
                Input.GetActionStrength("move_right") - Input.GetActionStrength("move_left");

            mInputs = mInputs.SetDirs(strafeInput, forwardInput).SetJump(QueueJump());
            SendInputPacket();
        } else if (GetTree().IsNetworkServer()) {
            // Get inputs.
            NextInput();
        }
        mWishDir = new Vector3(strafeInput, 0F, forwardInput)
                       .Rotated(Vector3.Up, mBody.GlobalTransform.basis.GetEuler().y)
                       .Normalized();

        if (IsOnFloor()) {
            if (mIsJump) {
                // If we're on the ground but mIsJump is still true, this means we've just
                // landed.
                mSnap = Vector3.Zero;  // Set snapping to zero so we can get off the ground.
                mVerticalVelocity = mJumpImpulse;  // Jump.

                MoveAir(mVelocity, delta);  // Mimic Quake's way of treating first frame after
                // landing as still in the air.

                mIsJump = false;  // We have jumped, the player needs to press jump key again.
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

        if (IsOnCeiling()) {
            mVerticalVelocity = 0F;
        }

        // Stair stepping.
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
                                isStep = true;
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
                                isStep = true;
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
                                    isStep = true;
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

        if (!isStep && IsOnFloor()) {
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
                        isStep = true;
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
                                isStep = true;
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

        if (!isStep) {
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
            SendPlayerState();
        }

        if (mInputs.jump) {
            mIsJump = true;
        }
    }

    private Snap SnapshotState() => new Snap() {
        {  "gt",                    GlobalTransform},
        { "hgt",              mHead.GlobalTransform},
        { "bgt",              mBody.GlobalTransform},
        { "vel",                          mVelocity},
        { "ajp",                          mAutoJump},
        {"grav",                        mGravityVec},
        {"snap",                              mSnap},
        {"vvel",                  mVerticalVelocity},
        {"wdir",                           mWishDir},
        {  "ts", OS.GetSystemTimeMsecs().ToString()},
        {"tick",                mCurTick.ToString()}
    };

    public override void _PhysicsProcess(float delta) {
        base._PhysicsProcess(delta);
        ++mCurTick;
        SimulatePhysics(delta);
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
        // If we're going too fast, our acceleration will be reduced (until it eventually hits 0,
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
    private bool QueueJump() {
        if (!mIsRealPlayer) {
            return mInputs.jump;
        }
        if (!IsOnFloor()) {
            return false;
        }
        // If auto_jump is true, the player keeps jumping as long as the key is kept down.
        if (mAutoJump) {
            return Input.IsActionPressed("jump");
        }

        if (Input.IsActionJustPressed("jump")) {
            return true;
        }
        return false;
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

        mBody.Transform = mBody.Transform.Orthonormalized();
        mHead.Transform = mHead.Transform.Orthonormalized();
        Transform = Transform.Orthonormalized();
    }

    ulong macks = 0;

    private void NextInput() {
        if (mPlayerInputQueue.Count < 1 || !GetTree().IsNetworkServer()) {
            return;
        }
        Snap dat = mPlayerInputQueue.Dequeue();
        macks = Util.TryGetVOr(dat, "tick", ulong.MaxValue);
        float dx = Util.TryGetVOr<float>(dat, "dx", 0F);
        float dy = Util.TryGetVOr<float>(dat, "dy", 0F);
        float strafe = Util.TryGetVOr<float>(dat, "str", 0F);
        float forward = Util.TryGetVOr<float>(dat, "for", 0F);
        bool jump = Util.TryGetVOr<bool>(dat, "jmp", false);
        bool autoJump = Util.TryGetVOr<bool>(dat, "ajp", false);

        // Ensure all floats are valid values. Reject the input if so.
        if (!Util.IsFinite(0)) {
            GD.PrintErr($"Head rotation is not finite");
            return;
        } else if (!Util.IsFinite(strafe)) {
            GD.PrintErr($"Strafe value is not finite");
            return;
        } else if (!Util.IsFinite(forward)) {
            GD.PrintErr($"Forward movement value is not finite");
            return;
        }
        // TODO make this tst from the snap in a better way and validate (probably as a
        // constructor).

        // Move.
        mInputs = new Tst.Input(strafe, forward, dx, dy, jump);
        mAutoJump = autoJump;
        MoveHead(dx, dy);
    }

    public void PlayerInput(Snap dat) {
        if (mPlayerInputQueue.Count > 32) {
            GD.PrintErr($"Err: {mPlayerInputQueue.Count} is at max!");
        }
        mPlayerInputQueue.Enqueue(dat);
    }

    public Snap ClientPredict(Snap recv, float factor, bool fromServer) {
        if (!mIsRealPlayer) {
            return null;
        }
        if (!fromServer) {
            return recv;
        }
        mLastPredictedState = SnapshotState();
        // TODO instead of current values, should be the cache from the last recv'd snapshot.
        ulong acks = Util.TryGetVOr(recv, "ack", ulong.MaxValue);
        if (acks >= mCurTick) {
            GD.PrintErr($"Server tick is < current tick ! {acks}<{mCurTick}");
            return null;
        }
        UpdatePlayer(recv);
        // Rollback code. We reinterpret all unacknowledged inputs with the new state received by
        // the server.
        var tmp = mInputs;
        int nRm = 0;
        foreach(var input in mPlayerInputList) {
            if (input.id <= acks) {
                nRm++;
                // GD.Print($"Reconciling {input}");
                mInputs = input;
                SimulatePhysics((1F / 128F), true);
            }
        }
        mPlayerInputList.RemoveRange(0, nRm);
        mInputs = tmp;
        // Inform the scene of our predicted state.
        Snap result = SnapshotState();
        // Lerp(GlobalTransform, mHead.GlobalTransform, mBody.GlobalTransform, factor);
        return result;
    }

    public void Lerp(Transform wholeTo, Transform headTo, Transform bodyTo, float factor) {
        Snap oldPlayerDat = mLastPredictedState;
        Transform oldPosition = (Transform)oldPlayerDat["gt"];
        GlobalTransform = oldPosition.InterpolateWith(wholeTo, factor);

        Transform oldHeadPosition = (Transform)oldPlayerDat["hgt"];
        mHead.GlobalTransform = oldHeadPosition.InterpolateWith(headTo, factor);

        Transform oldBodyPosition = (Transform)oldPlayerDat["bgt"];
        mHead.GlobalTransform = oldBodyPosition.InterpolateWith(bodyTo, factor);
    }

    public void ExtrapolateTo(Transform wholeTo, Transform headTo, Transform bodyTo) {
        if (mIsRealPlayer) {
            return;
        }
    }

    public void UpdatePlayer(Snap recv) {
        GlobalTransform = Util.TryGetVOr<Transform>(recv, "gt", GlobalTransform);
        mVelocity = Util.TryGetVOr<Vector3>(recv, "vel", mVelocity);
        mGravityVec = Util.TryGetVOr<Vector3>(recv, "grav", mGravityVec);
        mWishDir = Util.TryGetVOr<Vector3>(recv, "wdir", mWishDir);
        mVerticalVelocity = Util.TryGetVOr<float>(recv, "vvel", mVerticalVelocity);
        mSnap = Util.TryGetVOr<Vector3>(recv, "snap", mSnap);
        mHead.GlobalTransform = Util.TryGetVOr<Transform>(recv, "hgt", mHead.GlobalTransform);
        mBody.GlobalTransform = Util.TryGetVOr<Transform>(recv, "bgt", mBody.GlobalTransform);
    }

    public override void _ExitTree() {
        base._ExitTree();

        if (mDebugOverlay != null) {
            mDebugOverlay.Remove(this);
        }
    }

    private void SendInputPacket() {
        Snap send = new Snap() {
            {  "dx",                         mInputs.dx},
            {  "dy",                         mInputs.dy},
            { "str",                     mInputs.strafe},
            { "for",                   mInputs.forwards},
            { "jmp",                       mInputs.jump},
            { "ajp",                          mAutoJump},
            {  "ts", OS.GetSystemTimeMsecs().ToString()},
            {"tick",                mCurTick.ToString()}
        };
        mPlayerInputList.Add(mInputs.SetId(mInputIdCounter++));
        mInputs = mInputs.ResetMouse();
        mSceneRef.SendPlayerInput(send);
    }

    private void SendPlayerState() {
        Snap state = SnapshotState();
        state["ack"] = mInputs.id;
        mSceneRef.SendPlayerState(mNetworkId, state);
    }
}
