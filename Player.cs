using Godot;
using System;

// Notes on Quake's movement code: It's coordinate system is different to that
// of Godot's. In Quake Z is up/down, in Godot Z is forwards/backwards and Y is
// up/down.

public class Player : KinematicBody
{
    public float mouseSensitivity = 0.05F;

    public Godot.Spatial mHead = null;

    // Quake movement code.

    private bool mIsOnGround = false;
    private bool mWaterLevel = false;

    private float mFrameTime = 0F;

    private Vector3 mForward = new Vector3();
    private Vector3 mBackward = new Vector3();
    private Vector3 mUp = new Vector3();
    private Vector3 mWishDir = new Vector3();
    private Vector3 mVelocity = new Vector3();

    static public readonly Vector3 PLAYER_MINS = new Vector3(-16F, -16F, -24F);
    static public readonly Vector3 PLAYER_MAXS = new Vector3(16F, 16F, 32F);

    static public readonly float MAX_SPEED = 10;
    static public readonly float MAX_ACCEL = 10 * MAX_SPEED;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
	{
        base._Ready();
        mHead = GetNode<Godot.Spatial>("Head");

        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _Input(InputEvent @event)
    {
        base._Input(@event);

        // Move head.
        if(@event is InputEventMouseMotion mouseEvent) {
            RotateY(Mathf.Deg2Rad(-mouseEvent.Relative.x * mouseSensitivity));
            mHead.RotateX(Mathf.Deg2Rad(-mouseEvent.Relative.y * mouseSensitivity));
            mHead.Rotation = new Vector3(Mathf.Clamp(mHead.Rotation.x, Mathf.Deg2Rad(-89), Mathf.Deg2Rad(98)), 0F, 0F);
        }
    }

    public override void _PhysicsProcess(float delta)
    {
        base._PhysicsProcess(delta);

        mVelocity = mVelocity * 0.8F;

        mWishDir = new Vector3();

        if(Input.IsActionPressed("move_forward")) {
            mWishDir -= Transform.basis.z;
        }
        if(Input.IsActionPressed("move_backward")) {
            mWishDir += Transform.basis.z;
        }
        if(Input.IsActionPressed("move_left")) {
            mWishDir -= Transform.basis.x;
        }
        if(Input.IsActionPressed("move_right")) {
            mWishDir += Transform.basis.x;
        }

        mWishDir = mWishDir.Normalized();

        float currentSpeed = mVelocity.Dot(mWishDir);
        float addSpeed = Mathf.Clamp(MAX_SPEED - currentSpeed, 0F, MAX_ACCEL * delta);

        mVelocity = mVelocity + addSpeed * mWishDir;
        MoveAndSlide(mVelocity, Vector3.Up);
    }

    private int clipVeclocity(Vector3 inDir, Vector3 normal, Vector3 outDir, float overBound) {
        float backOff = 0F;
        float change = 0F;
        int blocked = 0;

        if(normal.y > 0) {

        }
        return 0;
    }
}
