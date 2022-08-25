using Godot;
using System;

// Notes on Quake's movement code: It's coordinate system is different to that
// of Godot's. In Quake Z is up/down, in Godot Z is forwards/backwards and Y is
// up/down.

public class Player : KinematicBody {
    public float mouseSensitivity = 0.05F;

    public Godot.Spatial mHead = null;

    // Quake movement code.

    private bool mIsOnGround = false;
    private bool mWaterLevel = false;

    private float mFrameTime = 0F;

    private Vector3 mVelocity = new Vector3();

    static public readonly Vector3 PLAYER_MINS = new Vector3(-16F, -16F, -24F);
    static public readonly Vector3 PLAYER_MAXS = new Vector3(16F, 16F, 32F);

    static public readonly float MAX_SPEED = 10;
    static public readonly float MAX_ACCEL = 10 * MAX_SPEED;

    private float horzAcceleration = 10F;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready() {
        base._Ready();
        mHead = GetNode<Godot.Spatial>("Head");

        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _Input(InputEvent @event) {
        base._Input(@event);

        // Move head.
        // Maybe in physics process because it changes wishdir.
        if (@event is InputEventMouseMotion mouseEvent &&
            Input.MouseMode == Input.MouseModeEnum.Captured) {
            RotateY(Mathf.Deg2Rad(-mouseEvent.Relative.x * mouseSensitivity));
            mHead.RotateX(Mathf.Deg2Rad(-mouseEvent.Relative.y * mouseSensitivity));
            mHead.Rotation = new Vector3(
                Mathf.Clamp(mHead.Rotation.x, Mathf.Deg2Rad(-89), Mathf.Deg2Rad(98)), 0F, 0F);
        }
    }

    private Vector3 applyFriction(Vector3 velocity, float frameTime) {
        return velocity * 0.8F;
    }

    private Vector3 updateVelocityGround(Vector3 wishDir, Vector3 velocity, float frameTime) {
        velocity = applyFriction(velocity, frameTime);

        float currentSpeed = velocity.Dot(wishDir);
        float addSpeed = Mathf.Clamp(MAX_SPEED - currentSpeed, 0F, MAX_ACCEL * frameTime);
        return velocity + addSpeed * wishDir;
    }

    private Vector3 updateVelocityAir(Vector3 wishDir, Vector3 velocity, float frameTime) {
        float currentSpeed = velocity.Dot(wishDir);
        float addSpeed = Mathf.Clamp(MAX_SPEED - currentSpeed, 0F, MAX_ACCEL * frameTime);
        return velocity + addSpeed * wishDir;
    }

    public Vector3 applyGravity(Vector3 velocity, float frameTime) {
        Vector3 gravityVec = new Vector3();
        if (!IsOnFloor()) {
            gravityVec += Vector3.Down * 20 * frameTime;
        } else {
            gravityVec = -GetFloorNormal() * 20;
        }

        if (IsOnFloor() && Input.IsActionPressed("jump")) {
            gravityVec = Vector3.Up * 30;
        }

        return velocity * gravityVec;
    }

    public override void _PhysicsProcess(float delta) {
        base._PhysicsProcess(delta);
        Vector3 wishDir = new Vector3();

        if (Input.IsActionPressed("move_forward")) {
            wishDir -= Transform.basis.z;
        }
        if (Input.IsActionPressed("move_backward")) {
            wishDir += Transform.basis.z;
        }
        if (Input.IsActionPressed("move_left")) {
            wishDir -= Transform.basis.x;
        }
        if (Input.IsActionPressed("move_right")) {
            wishDir += Transform.basis.x;
        }

        if (Input.IsActionPressed("menu")) {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
        if (Input.IsMouseButtonPressed(Input.GetMouseButtonMask())) {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        // Do not go faster when moving diagonally.
        wishDir = wishDir.Normalized();

        mVelocity = applyGravity(mVelocity, delta);

        if (IsOnFloor()) {
            mVelocity = updateVelocityGround(wishDir, mVelocity, delta);
        } else {
            mVelocity = updateVelocityAir(wishDir, mVelocity, delta);
        }

        MoveAndSlide(mVelocity, Vector3.Up);
    }

    private int clipVeclocity(Vector3 inDir, Vector3 normal, Vector3 outDir, float overBound) {
        float backOff = 0F;
        float change = 0F;
        int blocked = 0;

        if (normal.y > 0) {
        }
        return 0;
    }
}
