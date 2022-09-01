using Godot;
using System;

public class Scene : Spatial {
    public Godot.CanvasLayer mDebugOverlay { get; set; } = null;
    private Godot.PackedScene mPlayer = null;
    private Global mGlobal = null;

    private Godot.Node mPreloads = null;

    public float GetFPS() {
        return Godot.Engine.GetFramesPerSecond();
    }

    public float GetStaticMemoryUsageMB() {
        return ((float)Godot.OS.GetStaticMemoryUsage()) / 1_000_000F;
    }

    public override void _Ready() {
        base._Ready();

        mDebugOverlay = GetNode<Godot.CanvasLayer>("DebugOverlay");
        // mPlayer = GetNode<Godot.KinematicBody>("Player");

        mPreloads = (Godot.Node)((GDScript)GD.Load("res://scripts/Preloads.gd")).New();
        mPlayer = (PackedScene)mPreloads.Get("player");

        // mDebugOverlay.Call("AddStat", "Player position", mPlayer, "Position");
        mDebugOverlay.Call("AddStat", "FPS", this, "GetFPS", true);
        mDebugOverlay.Call("AddStat", "Memory Usage (MB)", this, "GetStaticMemoryUsageMB", true);

        // Set up network connection stuff.
        GetTree().Connect("network_peer_connected", this, "_PlayerConnected");
        GetTree().Connect("network_peer_disconnected", this, "_PlayerDisconnected");

        mGlobal = GetNode<Global>("/root/Global");

        mGlobal.Connect("InstancePlayer", this, "_InstancePlayer");

        if (GetTree().NetworkPeer != null) {
            mGlobal.EmitSignal("ToggleNetworkSetup", false);
        }
    }

    public override void _Input(InputEvent @event) {
        base._Input(@event);

        if (@event.IsActionPressed("menu")) {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }

        if (@event.IsActionPressed("debug_menu")) {
            mDebugOverlay.Visible = !mDebugOverlay.Visible;
        }

        if (@event is InputEventMouseButton mevent) {
            if (mevent.IsPressed()) {
                Input.MouseMode = Input.MouseModeEnum.Captured;
            }
        }
    }

    public void _PlayerConnected(int id) {
        GD.Print($"Player {id} has connected");
        _InstancePlayer(id);
    }

    public void _PlayerDisconnected(int id) {
        GD.Print($"Player {id} disconnected.");
        // Destroy player.
        if (HasNode(id.ToString())) {
            GetNode(id.ToString()).QueueFree();
        }
    }

    public void _InstancePlayer(int id) {
        // Make a new player and add it to the scene.
        Player playerInstance = (Player)mPlayer.Instance();
        //playerInstance.SetNetworkMaster(id);
        if(id == 1) {
            // We are a client. Connected to the server.
            // We're creating this client's player object.
            playerInstance.SetRealPlayer();
            playerInstance.mNetworkId = GetTree().GetNetworkUniqueId();
            playerInstance.Name = GetTree().GetNetworkUniqueId().ToString();
            GD.Print($"Your player id is {playerInstance.Name}");
        } else {
            // We are the server. Connected to a client.
            // Set the ID to the one we were given by RPC.
            playerInstance.mNetworkId = id;
            playerInstance.Name = id.ToString();
            playerInstance.mNetworkId = id;
            GD.Print($"Created player for client {playerInstance.Name}");
        }
        AddChild(playerInstance);

        playerInstance.GlobalTransform =
            Util.ChangeTFormOrigin(playerInstance.GlobalTransform, new Vector3(0F, 15F, 0F));
    }
}
