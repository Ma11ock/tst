using Godot;

namespace Tst {
public interface IQuakeMove {
    void MoveHead(float dx, float dy, float minXRotRad, float maxXRotRad);

    Vector3 MoveGround(Vector3 wishDir, Vector3 velocity, float verticalVelocity,
                       float accelerationFactor, float frictionFactor, float maxSpeed, float delta);

    Vector3 MoveAir(Vector3 wishDir, Vector3 velocity, float verticalVeclocity, float factor,
                    float maxSpeed, float delta);

    Vector3 Friction(Vector3 velocity, float factor, float maxSpeed, float delta);

    Vector3 Accelerate(Vector3 wishDir, Vector3 velocity, float acceleration, float maxSpeed,
                       float delta);

    void Simulate(float delta);

    bool QueueJump();
}

public abstract class QuakeMover : Godot.KinematicBody, IQuakeMove {
    protected Tst.CVarCollection mCvars = null;
    protected Vector3 mVelocity = Vector3.Zero;
    protected Vector3 mWishDir = Vector3.Zero;
    protected Vector3 mGravityVec = Vector3.Zero;
    protected float mVerticalVelocity = 0F;  // Vertical component of velocity.

    // Node references.
    /// <summary>
    /// Body reference.
    /// </summary>
    protected Godot.Spatial mBody = null;
    /// <summary>
    /// Head reference.
    /// </summary>
    protected Godot.Spatial mHead = null;
    /// <summary>
    /// Camera reference.
    /// </summary>
    protected Godot.Camera mCamera = null;
    // Quake physics objects.
    /// <summary>
    /// Gravity acceleration.
    /// </summary>
    static protected float gravity =
        (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
    /// <summary>
    /// Client's mouse sensitivity.
    /// </summary>
    [Export]
    protected float mMouseSensitivity = 0.2F;
    /// <summary>
    /// Max speed of the player in the air.
    /// </summary>
    [Export]
    protected float mMaxAirSpeed = 0.6F;
    /// <summary>
    /// Acceleration when moving.
    /// </summary>
    [Export]
    protected float mAcceleration = 60F;
    /// <summary>
    /// Ground friction.
    /// </summary>
    [Export]
    protected float mFriction = 6F;
    /// <summary>
    /// Y velocity when jumping.
    /// </summary>
    [Export]
    protected float mJumpImpulse = 8F;
    /// <summary>
    /// Terminal velocity, fastest a player is able to fall.
    /// </summary>
    protected float mTerminalVelocity = gravity * -5F;
    /// <summary>
    /// Snap vector, used for stairs and some collision physics. Needed for MoveAndSlideWithSnap(),
    /// which enables going down slopes without falling.
    /// </summary>
    protected Vector3 mSnap = Vector3.Zero;

    /// <summary>
    /// If true the player is currently jumping.
    /// </summary>
    protected bool mIsJump = false;
    /// <summary>
    /// If true, player has queued a jump : the jump key can be
    /// held down before hitting the ground to jump.
    /// </summary>
    protected bool mAutoJump = false;

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
    protected Vector3 mStepCheckHeight = STEP_HEIGHT_DEFAULT / STEP_CHECK_COUNT;
    /// <summary>
    ///
    /// </summary>
    protected Vector3 mHeadOffset = Vector3.Zero;
    /// <summary>
    /// Euler angle in Y direction for interpolation.
    /// </summary>
    protected float mBodyEulerY = 0F;

    // Camera interpolation on stairs.
    /// <summary>
    /// Camera target position for stairstep interpolation.
    /// </summary>
    protected Vector3 mCameraTargetPos = Vector3.Zero;
    /// <summary>
    /// TODO
    /// </summary>
    protected float mCameraCoefficient = 1.0F;
    /// <summary>
    /// Time spent in air for interpolation.
    /// </summary>
    protected float mTimeInAir = 0F;

    /// <summary>
    /// Max speed of the player on the ground.
    /// </summary>
    [Export]
    protected float mMaxSpeed = 10F;

    public Vector3 Accelerate(Vector3 wishDir, Vector3 velocity, float acceleration, float maxSpeed,
                              float delta) {
        // Current speed is calculated by projecting our velocity onto wishdir.
        // We can thus manipulate our wishdir to trick the engine into thinking we're going slower
        // than we actually are, allowing us to accelerate further.
        float currentSpeed = velocity.Dot(wishDir);

        // Next, we calculate the speed to be added for the next frame.
        // If our current speed is low enough, we will add the max acceleration.
        // If we're going too fast, our acceleration will be reduced (until it eventually hits 0,
        // where we don't add any more speed).
        float addSpeed = Mathf.Clamp(maxSpeed - currentSpeed, 0F, acceleration * delta);

        return velocity + wishDir * addSpeed;
    }

    public Vector3 Friction(Vector3 velocity, float factor, float maxSpeed, float delta) {
        float speed = velocity.Length();
        if (speed < 0.1F) {
            return new Vector3(0F, velocity.y, 0F);
        }

        float control = speed;  // speed < 5F ? 5F : speed;
        float drop = control * factor * delta;

        float newSpeed = speed - drop;
        if (newSpeed < 0F) {
            newSpeed = 0F;
        }

        newSpeed /= speed;

        Vector3 scaledVelocity = velocity * newSpeed;
        if (scaledVelocity.Length() < (maxSpeed / 100F)) {
            scaledVelocity = Vector3.Zero;
        }

        return scaledVelocity;
    }

    public Vector3 MoveAir(Vector3 wishDir, Vector3 velocity, float verticalVeclocity, float factor,
                           float maxSpeed, float delta) {
        // We first work on only on the horizontal components of our current velocity.
        Vector3 nextVelocity = Vector3.Zero;
        nextVelocity.x = velocity.x;
        nextVelocity.z = velocity.z;
        nextVelocity = Accelerate(wishDir, nextVelocity, factor, maxSpeed, delta);

        nextVelocity.y = verticalVeclocity;
        return nextVelocity;
    }

    public Vector3 MoveGround(Vector3 wishDir, Vector3 velocity, float verticalVelocity,
                              float accelerationFactor, float frictionFactor, float maxSpeed,
                              float delta) {
        // We first work on only on the horizontal components of our current velocity.
        Vector3 nextVelocity = Vector3.Zero;
        nextVelocity.x = velocity.x;
        nextVelocity.z = velocity.z;

        nextVelocity = Friction(nextVelocity, frictionFactor, maxSpeed, delta);
        nextVelocity = Accelerate(wishDir, nextVelocity, accelerationFactor, maxSpeed, delta);

        // Then get back our vertical component, and move the player.
        nextVelocity.y = verticalVelocity;
        return nextVelocity;
    }

    public abstract void MoveHead(float dx, float dy, float minXRotRad, float maxXRotRad);

    public void Simulate(float delta) {
        bool isStep = false;

        if (IsOnFloor()) {
            if (mIsJump) {
                // If we're on the ground but mIsJump is still true, this means we've just
                // landed.
                mSnap = Vector3.Zero;  // Set snapping to zero so we can get off the ground.
                mVerticalVelocity = mJumpImpulse;  // Jump.

                mVelocity =
                    MoveAir(mWishDir, mVelocity, mVerticalVelocity, mAcceleration, mMaxAirSpeed,
                            delta);  // Mimic Quake's way of treating first frame after
                // landing as still in the air.

                mIsJump = false;  // We have jumped, the player needs to press jump key again.
                mGravityVec += Vector3.Down * gravity * delta;
            } else {
                // Player is on the ground. Move normally, apply friction.
                mVerticalVelocity = 0F;
                mSnap = -GetFloorNormal();
                mVelocity = MoveGround(mWishDir, mVelocity, mVerticalVelocity, mAcceleration,
                                       mFriction, mMaxSpeed, delta);
                mGravityVec = Vector3.Zero;
            }
        } else {
            // We're in the air. Do not apply friction
            mSnap = Vector3.Down;
            mVerticalVelocity -= (mVerticalVelocity >= mTerminalVelocity) ? gravity * delta : 0F;
            mVelocity =
                MoveAir(mWishDir, mVelocity, mVerticalVelocity, mAcceleration, mMaxAirSpeed, delta);
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
    }

    public abstract bool QueueJump();
}
}
