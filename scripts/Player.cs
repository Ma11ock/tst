using Godot;

// Notes on Quake's movement code: It's coordinate system is different to that
// of Godot's. In Quake Z is up/down, in Godot Z is forwards/backwards and Y is
// up/down.

namespace Tst {
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

// Mutable player state. Compacted to make it easier to send over the network.
public class PlayerState : Godot.Object {
    public PlayerState() {
    }
}
}

public class Player : KinematicBody {
    // Children nodes.
    private Godot.Spatial mBody = null;
    private Godot.Spatial mHead = null;
    private Godot.Camera mCamera = null;
    private Godot.MeshInstance mModel = null;
    private Godot.Tween mMovementTween = null;

    public Tst.Input mInputs { get; private set; } = new Tst.Input();

    public bool mIsRealPlayer { get; private set; } = false;

    public void SetRealPlayer() {
        mIsRealPlayer = true;
    }

    private int _mNetworkId = 0;

    public int mNetworkId {
        get => _mNetworkId;
        set {
            if (_mNetworkId == 0) {
                _mNetworkId = value;
            }
        }
    }

    // Quake physics objects.
    static private float gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
    [Export]
    private float mMouseSensitivity = 0.05F;
    [Export]
    private float mMaxSpeed = 10F;
    [Export]
    private float mMaxAirSpeed = 0.6F;
    [Export]
    private float mAcceleration = 60F;
    [Export]
    private float mFriction = 6F;
    [Export]
    private float mJumpImpulse = 8F;
    private float mTerminalVelocity = gravity * -5F;

    // For stair snapping.
    public static readonly float STAIRS_FEELING_COEFFICIENT = 2.5F;
    public static readonly float WALL_MARGIN = 0.001F;
    public static readonly Vector3 STEP_HEIGHT_DEFAULT = new Vector3(0F, 0.6F, 0F);
    public static readonly float STEP_MAX_SLOPE_DEGREE = 0F;
    public static readonly int STEP_CHECK_COUNT = 2;
    private Vector3 mStepCheckHeight = STEP_HEIGHT_DEFAULT / STEP_CHECK_COUNT;
    private Vector3 mHeadOffset = Vector3.Zero;
    private float mBodyEulerY = 0F;
    public bool mIsStep { get; private set; } = false;

    // Camera interpolation on stairs.
    private float mCameraFeelingCoefficient = 2.5F;
    private Vector3 mCameraTargetPos = Vector3.Zero;
    private float mCameraCoefficient = 1.0F;
    private float mTimeInAir = 0F;

    public Vector3 mSnap {
        get; private set;
    } = Vector3.Zero;  // Needed for MoveAndSlideWithSnap(), which enables
                       // going down slopes without falling.

    public Vector3 mVelocity { get; private set; } = Vector3.Zero;
    public Vector3 mWishDir { get; private set; } = Vector3.Zero;
    public Vector3 mGravityVec { get; private set; } = Vector3.Zero;
    public float mVerticalVelocity { get; private set; } = 0F;  // Vertical component of velocity.
    public bool mWishJump {
        get; private set;
    } = false;  // If true, player has queued a jump : the jump key can be held
                // down before hitting the ground to jump.
    public bool mAutoJump {
        get; private set;
    } = false;  // If true, player has queued a jump : the jump key can be held
                // down before hitting the ground to jump.

    public string GetInputDescription() => mInputs.ToString();

    public Vector3 GetLookAt() => mHead.Transform.basis.z;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready() {
        base._Ready();

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

        Godot.CanvasLayer mDebugOverlay = GetParent().GetNode<Godot.CanvasLayer>("DebugOverlay");

        // tmp debug overlay system. not very robust :/.
        mDebugOverlay.Call("AddStat", "Is on floor", this, "PlayerIsOnFloor", true);
        mDebugOverlay.Call("AddStat", "Velocity", this, "mVelocity", false);
        mDebugOverlay.Call("AddStat", "Wishdir", this, "mWishDir", false);
        mDebugOverlay.Call("AddStat", "Vertical Velocity", this, "mVerticalVelocity", false);
        mDebugOverlay.Call("AddStat", "Auto jump", this, "mAutoJump", false);
        mDebugOverlay.Call("AddStat", "Wish jump", this, "mWishJump", false);
        mDebugOverlay.Call("AddStat", "Inputs", this, "GetInputDescription", true);
        mDebugOverlay.Call("AddStat", "Look at", this, "GetLookAt", true);
    }

    public override void _Input(InputEvent @event) {
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
        if (mIsRealPlayer) {
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
            RpcUnreliable("UpdatePlayer", GlobalTransform, mHead.GlobalTransform, mVelocity,
                          mWishDir, mWishJump, mGravityVec, mSnap, mVerticalVelocity);
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

    [Master]
    public void SendPlayerInput(float mouseDx, float mouseDy, float strafe, float forward,
                                bool jump, bool autoJump) {
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

    // TODO turn all of these into a class and send them as one.
    [Puppet]
    public void UpdatePlayer(Transform globalTransform, Transform headTransform, Vector3 velocity,
                             Vector3 wishDir, bool wishJump, Vector3 gravityVec, Vector3 snap,
                             float verticalVelocity) {
        // GD.Print($"Got back {globalTransform}, {velocity}");
        if (!NetworkSetup.IsServer) {
            GlobalTransform = globalTransform;
            mVelocity = velocity;
            mWishDir = wishDir;
            mWishJump = wishJump;
            mGravityVec = gravityVec;
            mVerticalVelocity = verticalVelocity;
            mSnap = snap;
        }
    }

    private void SendInputPacket() {
        RpcUnreliable("SendPlayerInput", mInputs.mouseX, mInputs.mouseY, mInputs.strafe,
                      mInputs.forwards, mWishJump, mAutoJump);
    }
}
