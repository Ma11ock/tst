using Godot;
using System;

public class Scene : Spatial {
    private Godot.CanvasLayer mDebugOverlay = null;
    private Godot.KinematicBody mPlayer = null;

    public float GetFPS() {
        return Godot.Engine.GetFramesPerSecond();
    }

    public float GetStaticMemoryUsageMB() {
        return ((float)Godot.OS.GetStaticMemoryUsage()) / 1_000_000F;
    }

    public override void _Ready() {
        base._Ready();

        mDebugOverlay = GetNode<Godot.CanvasLayer>("DebugOverlay");
        mPlayer = GetNode<Godot.KinematicBody>("Player");

        //mDebugOverlay.Call("AddStat", "Player position", mPlayer, "Position");
        mDebugOverlay.Call("AddStat", "FPS", this, "GetFPS", true);
        mDebugOverlay.Call("AddStat", "Memory Usage (MB)", this, "GetStaticMemoryUsageMB", true);
        mDebugOverlay.Call("AddStat", "Is on floor", mPlayer, "PlayerIsOnFloor", true);
        mDebugOverlay.Call("AddStat", "Velocity", mPlayer, "mVelocity", false);
        mDebugOverlay.Call("AddStat", "Wishdir", mPlayer, "mWishDir", false);
        mDebugOverlay.Call("AddStat", "Vertical Velocity", mPlayer, "mVerticalVelocity", false);
    }

    public override void _Input(InputEvent @event)
    {
        base._Input(@event);

        if(@event.IsActionPressed("menu")) {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }

        if(@event.IsActionPressed("debug_menu")) {
            mDebugOverlay.Visible = !mDebugOverlay.Visible;
        }

        if(@event is InputEventMouseButton mevent) {
            if(mevent.IsPressed()) {
                Input.MouseMode = Input.MouseModeEnum.Captured;
            }
        }
    }
}
