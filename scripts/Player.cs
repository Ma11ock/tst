using Godot;
using System;

// Notes on Quake's movement code: It's coordinate system is different to that
// of Godot's. In Quake Z is up/down, in Godot Z is forwards/backwards and Y is
// up/down.

public class Player : KinematicBody {
    // Children nodes.
    private Godot.Spatial mBody = null;
    private Godot.Spatial mHead = null;
    private Godot.Camera mCamera = null;
    private Godot.MeshInstance mModel = null;
    private Godot.Timer mNetworkTimer = null;
    private Godot.Tween mMovementTween = null;

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

    // Networking things.
    public Vector3 mPuppetVelocity = Vector3.Zero;
    public Vector3 mPuppetRotation = Vector3.Zero;
    public Vector3 mPuppetPosition = Vector3.Zero;

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

    // Called when the node enters the scene tree for the first time.
    public override void _Ready() {
        base._Ready();

        mBody = GetNode<Godot.Spatial>("Body");
        mHead = mBody.GetNode<Godot.Spatial>("Head");
        mCamera = mHead.GetNode<Godot.Camera>("Camera");
        mNetworkTimer = GetNode<Godot.Timer>("NetworkTickRate");
        mMovementTween = GetNode<Godot.Tween>("MovementTween");

        Input.MouseMode = Input.MouseModeEnum.Captured;
        mBodyEulerY = mBody.GlobalTransform.basis.GetEuler().y;

        mCameraTargetPos = mCamera.GlobalTransform.origin;
        mCamera.SetAsToplevel(true);
        mCamera.PhysicsInterpolationMode = Godot.Node.PhysicsInterpolationModeEnum.Off;

        mModel = GetNode<Godot.MeshInstance>("Model");

        GD.Print($"IsNM {IsNetworkMaster()}");
        mCamera.Current = IsNetworkMaster();
        mModel.Visible = !IsNetworkMaster();
    }

    public override void _Input(InputEvent @event) {
        base._Input(@event);

        if (!IsNetworkMaster()) {
            return;
        }
        // Move head.
        // Maybe in physics process because it changes wishdir.
        if (@event is InputEventMouseMotion mouseEvent &&
            Input.MouseMode == Input.MouseModeEnum.Captured) {
            mBody.RotateY(Mathf.Deg2Rad(-mouseEvent.Relative.x * mMouseSensitivity));
            mHead.RotateX(Mathf.Deg2Rad(-mouseEvent.Relative.y * mMouseSensitivity));

            float newRotX = Mathf.Clamp(mHead.Rotation.x, Mathf.Deg2Rad(-89), Mathf.Deg2Rad(89));

            mHead.Rotation = Util.ChangeX(mHead.Rotation, newRotX);
        }
    }

    /// Needed for debugging window.
    public bool PlayerIsOnFloor() {
        return IsOnFloor();
    }

    public override void _PhysicsProcess(float delta) {
        base._PhysicsProcess(delta);

        mIsStep = false;

        if (IsNetworkMaster()) {
            float forwardInput =
                Input.GetActionStrength("move_backward") - Input.GetActionStrength("move_forward");
            float strafeInput =
                Input.GetActionStrength("move_right") - Input.GetActionStrength("move_left");
            mWishDir = new Vector3(strafeInput, 0F, forwardInput)
                           .Rotated(Vector3.Up, mBody.GlobalTransform.basis.GetEuler().y)
                           .Normalized();
        } else {
            GlobalTransform = Util.ChangeTFormOrigin(GlobalTransform, mPuppetPosition);

            mVelocity = new Vector3(mPuppetVelocity.x, mVelocity.y, mPuppetVelocity.z);
            Rotation = Util.ChangeY(Rotation, mPuppetRotation.y);
            mHead.Rotation = Util.ChangeX(mHead.Rotation, mPuppetRotation.x);
        }
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
        // TODO clients can hack this
        if (!mMovementTween.IsActive()) {
            mVelocity = MoveAndSlideWithSnap(mVelocity, mSnap, Vector3.Up, false, 4,
                                             Mathf.Deg2Rad(46), false);
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

    [Puppet]
    public void UpdateState(Vector3 position, Vector3 velocity, Vector3 rotation) {
        mPuppetPosition = position;
        mPuppetVelocity = velocity;
        mPuppetRotation = rotation;

        // Interpolate playermovement for smooth.
        mMovementTween.InterpolateProperty(this, "global_transform", GlobalTransform,
                                           new Transform(GlobalTransform.basis, position), 0.1F);
        mMovementTween.Start();
    }

    public void _OnNetworkTickRateTimeout() {
        if (IsNetworkMaster()) {
            RpcUnreliable("UpdateState", GlobalTransform.origin, mVelocity,
                          new Vector2(mHead.Rotation.x, mHead.Rotation.y));
        } else {
            mNetworkTimer.Stop();
        }
    }
}
