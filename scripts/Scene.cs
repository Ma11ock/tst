using Godot;
using System;

public class Scene : Spatial, Tst.Debuggable {
    public DebugOverlay mDebugOverlay { get; set; } = null;
    private Godot.PackedScene mPlayer = null;
    private Global mGlobal = null;

    private Godot.Node mPreloads = null;

    private Console mDebugConsole = null;

    private ulong mDebugId = 0;

    public const string DEBUG_OVERLAY_NAME = "_tst_debug_overlay";

    public void GetDebug(Control c) {
        if(c is Godot.Label label) {
            label.Text = $"FPS: {Godot.Engine.GetFramesPerSecond()}\nMemory: {GetStaticMemoryUsageMB():0.000}MB";
        }

    }

    public ulong GetDebugId() => mDebugId;
    public void SetDebugId(ulong id) => mDebugId = id;

    public float GetFPS() {
        return Godot.Engine.GetFramesPerSecond();
    }

    public float GetStaticMemoryUsageMB() {
        return ((float)Godot.OS.GetStaticMemoryUsage()) / 1_000_000F;
    }

    public override void _Ready() {
        base._Ready();

        mPreloads = (Godot.Node)((GDScript)GD.Load("res://scripts/Preloads.gd")).New();
        mPlayer = (PackedScene)mPreloads.Get("player");
        mDebugOverlay = MkInstance<DebugOverlay>("debug_overlay");
        mDebugOverlay.Name = DEBUG_OVERLAY_NAME;
        AddChild(mDebugOverlay);

        mDebugOverlay.Add<Godot.Label>(this);

        // Set up network connection stuff.
        GetTree().Connect("network_peer_connected", this, "_PlayerConnected");
        GetTree().Connect("network_peer_disconnected", this, "_PlayerDisconnected");

        mGlobal = GetNode<Global>("/root/Global");

        mGlobal.Connect("InstancePlayer", this, "_InstancePlayer");

        if (GetTree().NetworkPeer != null) {
            mGlobal.EmitSignal("ToggleNetworkSetup", false);
        }
    }

    private T MkInstance<T>(string name)
        where T : Godot.Node {
        return (T)((PackedScene)mPreloads.Get(name)).Instance();
    }

    public override void _Process(float delta) {
        base._Process(delta);
    }

    public override void _ExitTree()
    {
        base._ExitTree();

        mDebugOverlay.Remove(this);
        RemoveChild(mDebugOverlay);
        mDebugOverlay.QueueFree();
    }

    public override void _Input(InputEvent @event) {
        base._Input(@event);

        if (@event.IsActionPressed("menu")) {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }

        if (@event.IsActionPressed("debug_menu")) {
            mDebugOverlay.Visible = !mDebugOverlay.Visible;
        }

        if (@event.IsActionPressed("toggle_console")) {
            if (mDebugConsole == null) {
                mDebugConsole = MkInstance<Console>("console");
                mDebugConsole.Name = "_test_debug_console";
                mDebugConsole.Visible = true;
                AddChild(mDebugConsole);
                Input.MouseMode = Input.MouseModeEnum.Visible;
            } else {
                mDebugConsole.Visible = false;
                mDebugConsole.QueueFree();
                mDebugConsole = null;
                Input.MouseMode = Input.MouseModeEnum.Captured;
            }
            // GetTree().SetInputAsHandled();
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
        // playerInstance.SetNetworkMaster(id);
        if (id == 1) {
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
