using Godot;
using System;
using System.Collections.Generic;

using Snap = Godot.Collections.Dictionary;

public class Scene : Spatial, Tst.Debuggable {
    public DebugOverlay mDebugOverlay { get; set; } = null;
    private Godot.PackedScene mPlayer = null;
    private Global mGlobal = null;

    /// <summary>
    /// State of all physics objects in the world that will be sent over the network. Only used on
    /// the server.
    /// </summary>
    private Snap mWorldState = null;

    /// <summary>
    /// Timestamp (ms) when the last world snapshot was recv'd. Only used on the client.
    /// </summary>
    private ulong mWorldTs = 0;

    /// <summary>
    /// Cache of previous player states. Only used by the server.
    /// </summary>
    private Snap mPlayerStates = null;

    /// <summary>
    /// Reference to preloaded assets.
    /// </summary>
    private Godot.Node mPreloads = null;

    /// <summary>
    /// Global debug console.
    /// </summary>
    private Console mDebugConsole = null;

    /// <summary>
    /// World State buffer (snapshots). Used only by the client.
    /// </summary>
    private List<Snap> mWorldCache = null;

    /// <summary>
    /// Amount of time to interpolate between frames. Only used by the client.
    /// </summary>
    private ulong mInterpolationConstant = 50;

    /// <summary>
    /// Timer used to determine when to send the world state to the client. Only used by the server.
    /// </summary>
    private float mPacketTimer = 0F;

    /// <summary>
    /// Interval of time to send a packet.
    /// </summary>
    private float _mPacketSendInterval = 1F / 20F;

    /// <summary>
    /// The rate that the server is sending data to the client. Only used on client.
    /// </summary>
    private int mPacketCounter = 0;

    /// <summary>
    /// The rate that the server is sending data to the client. Only used on client.
    /// </summary>
    private int mPacketUpdateRate = 0;

    /// <summary>
    /// Timer used to update debugging statistics.
    /// </summary>
    private ulong mStatUpadteTimer = 0;

    /// <summary>
    /// The last time (ms) that _PhysicsProcess was run. Used for calculating debug stats.
    /// </summary>
    private ulong mLastPhysTime = 0;

    public float mPacketSendInterval {
        get => _mPacketSendInterval;
        set {
            _mPacketSendInterval = Mathf.Clamp(value, 1F / 128F, float.MaxValue);
            // Reset the timer so something crazy doesn't happen.
            mPacketTimer = 0F;
            if (_mPacketSendInterval != value) {
                GD.PrintErr($"Attempt to set timer inverval to invalid value ({value}). " +
                            $"Set to {_mPacketSendInterval} instead");
            }
        }
    }

    /// <summary>
    /// Id for the debug overlay.
    /// </summary>
    private ulong mDebugId = 0;

    /// <summary>
    /// Name of the debug overlay, used so that children can add their information to the overlay.
    /// </summary>
    public const string DEBUG_OVERLAY_NAME = "_tst_debug_overlay";

    public void GetDebug(Control c) {
        if (c is Godot.Label label) {
            label.Text = $@"FPS: {Godot.Engine.GetFramesPerSecond()}
Memory: {GetStaticMemoryUsageMB():0.000}MB
Snapshots/second: {mPacketUpdateRate}";
        }
    }

    public ulong GetDebugId() => mDebugId;
    public void SetDebugId(ulong id) => mDebugId = id;

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

        mDebugConsole = MkInstance<Console>("console");
        mDebugConsole.Name = "_test_debug_console";
        mDebugConsole.Visible = false;
        AddChild(mDebugConsole);

        // Set up network connection stuff.
        GetTree().Connect("network_peer_connected", this, "_PlayerConnected");
        GetTree().Connect("network_peer_disconnected", this, "_PlayerDisconnected");

        mGlobal = GetNode<Global>("/root/Global");

        mGlobal.Connect("InstancePlayer", this, "_InstancePlayer");

        if (GetTree().NetworkPeer != null) {
            mGlobal.EmitSignal("ToggleNetworkSetup", false);
        }

        if (GetTree().IsNetworkServer()) {
            mPlayerStates = new Snap();
            mWorldState = new Snap();
        } else {
            // Client.
            mWorldCache = new List<Snap>();
        }
    }

    public override void _Process(float delta) {
        base._Process(delta);
    }

    private void PhysicsProcessClient(float delta) {
        ulong renderTime = OS.GetSystemTimeMsecs() - mInterpolationConstant;
        if (mWorldCache.Count > 1) {
            while (mWorldCache.Count > 2 &&
                   renderTime > Util.TryGetVOr(mWorldCache[1], "ts", ulong.MaxValue)) {
                // The world state is too old to be in the future, remove it.
                // This pushes mWorldCache[1] to the front, making it the old state.
                mWorldCache.RemoveAt(0);
            }
            ulong mostRecentTs = Util.TryGetVOr(mWorldCache[0], "ts", ulong.MaxValue);
            ulong futureTs = Util.TryGetVOr(mWorldCache[1], "ts", ulong.MaxValue);
            // Determine how much time has passed since the old state and the future state.
            float interpFactor =
                (float)(renderTime - mostRecentTs) / (float)(futureTs - mostRecentTs);
            // Ensure the factor is valid and wont mess up Lerp().
            const float MIN_FACTOR = 0.01F;
            interpFactor = Util.IsFinite(interpFactor) ? interpFactor : MIN_FACTOR;
            interpFactor = Mathf.Clamp(interpFactor, MIN_FACTOR, 1F);

            Snap oldPlayers = (Snap)mWorldCache[0]["plys"];
            Snap newPlayers = (Snap)mWorldCache[1]["plys"];

            // Lerp players.
            foreach(var key in newPlayers.Keys) {
                string player = "";
                if (key is string p) {
                    player = p;
                } else if (key is int pi) {
                    player = pi.ToString();
                } else {
                    GD.PrintErr(
                        $"Invalid key type for world update: got {key.GetType()}, expected int or string.");
                    continue;
                }

                if (!oldPlayers.Contains(player)) {
                    // Do not lerp the player if they don't exist in both snapshots.
                    continue;
                }

                Snap oldPlayerDat = null;
                Snap playerDat = null;
                try {
                    playerDat = (Snap)newPlayers[player];
                    oldPlayerDat = (Snap)oldPlayers[player];
                } catch (System.Collections.Generic.KeyNotFoundException) {
                    GD.PrintErr($"Player with id {player} was not found.");
                } catch (InvalidCastException e) {
                    GD.PrintErr($"Player data for {player} is not a Snapshot: {e}");
                } catch (Exception e) {
                    GD.PrintErr($"Error in receiving world state: {e}");
                }
                if (playerDat == null || oldPlayerDat == null) {
                    // Skip if error.
                    continue;
                }

                if (!HasNode(player)) {
                    // TODO dont error, spawn a player.
                    GD.PrintErr($"Player {player} does not exist in scene tree.");
                    continue;
                }

                Player pl = GetNodeOrNull<Player>(player);

                if (pl == null) {
                    GD.PrintErr($"Object at {player} is not a Player!");
                    continue;
                }

                // Calculate the position by lerping.
                try {
                    Transform oldPosition = (Transform)oldPlayerDat["gt"];
                    Transform futurePosition = (Transform)playerDat["gt"];
                    Transform newTransform =
                        oldPosition.InterpolateWith(futurePosition, interpFactor);
                    playerDat["gt"] = newTransform;
                    Transform oldHeadPosition = (Transform)oldPlayerDat["hgt"];
                    Transform futureHeadPosition = (Transform)playerDat["hgt"];
                    Transform newHeadTransform =
                        oldHeadPosition.InterpolateWith(futureHeadPosition, interpFactor);
                    Transform oldBodyPosition = (Transform)oldPlayerDat["bgt"];
                    Transform futureBodyPosition = (Transform)playerDat["bgt"];
                    Transform newBodyTransform =
                        oldBodyPosition.InterpolateWith(futureBodyPosition, interpFactor);
                    playerDat["gt"] = newTransform;
                    playerDat["hgt"] = newHeadTransform;
                    playerDat["bgt"] = newBodyTransform;
                    pl.UpdatePlayer(playerDat);
                    playerDat["gt"] = futurePosition;
                    playerDat["hgt"] = futureHeadPosition;
                    playerDat["bgt"] = futureBodyPosition;
                } catch (System.Collections.Generic.KeyNotFoundException) {
                    GD.PrintErr($"Player snapshot invalid (no transform). Not interpolating.");
                } catch (InvalidCastException e) {
                    GD.PrintErr(
                        $"Player snapshot invalid (global transform is not a valid type): {e}. Not interpolating.");
                } catch (Exception e) {
                    GD.PrintErr($"Error in reading trasnforms: {e}");
                }
            }
        }
    }

    private void PhysicsProcessServer(float delta) {
        if (mPlayerStates.Count > 0 && (mPacketTimer += delta) > mPacketSendInterval) {
            mPacketTimer -= mPacketSendInterval;
            mWorldState["ts"] = OS.GetSystemTimeMsecs().ToString();
            Snap ps = mPlayerStates.Duplicate(true);
            mWorldState["plys"] = ps;
            // Delete unnecessary fields when sending over the network.
            foreach(var player in mPlayerStates.Keys) {
                Snap playerDat = (Snap)ps[player];
                playerDat.Remove("ts");
            }

            SendWorldState();
        }
    }

    public override void _PhysicsProcess(float delta) {
        base._PhysicsProcess(delta);

        ulong totalTicks = OS.GetTicksMsec();

        mStatUpadteTimer += (totalTicks - mLastPhysTime);

        mLastPhysTime = totalTicks;

        if (mStatUpadteTimer > 1000) {
            mStatUpadteTimer = 0;
            mPacketUpdateRate = mPacketCounter;
            mPacketCounter = 0;
        }

        if (GetTree().IsNetworkServer()) {
            PhysicsProcessServer(delta);
        } else {
            PhysicsProcessClient(delta);
        }
    }

    public override void _ExitTree() {
        base._ExitTree();

        mDebugOverlay.Remove(this);
        RemoveChild(mDebugOverlay);
        mDebugOverlay.QueueFree();

        RemoveChild(mDebugConsole);
        mDebugConsole.QueueFree();
    }

    public void ToggleDebugOverlay() => mDebugOverlay.Visible = !mDebugOverlay.Visible;

    public void ToggleDebugOverlay(bool @on) => mDebugOverlay.Visible = @on;

    public bool IsDebugOverlayVisible() => mDebugOverlay.Visible;

    private T MkInstance<T>(string name)
        where T : Godot.Node {
        return (T)((PackedScene)mPreloads.Get(name)).Instance();
    }

    public override void _Input(InputEvent @event) {
        base._Input(@event);

        if (@event.IsActionPressed("menu")) {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }

        if (@event.IsActionPressed("toggle_console")) {
            Global.ToggleMouseSettings();
            mDebugConsole.ToggleVisible();
            GetTree().SetInputAsHandled();
            Global.InputCaptured = !Global.InputCaptured;
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
        mPlayerStates.Remove(id.ToString());
    }

    public void _InstancePlayer(int id) {
        if (HasNode(id.ToString())) {
            // Already have this player.
            return;
        }
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

    /// <summary>
    /// Send the input collected by the client to the server. Used only by the client.
    /// </summary>
    public void SendPlayerInput(Snap input) => RpcUnreliableId(1, "RecvPlayerInput", input);

    /// <summary>
    /// Cache the player state for sending to the clients. Used only by the server.
    /// </summary>
    public void SendPlayerState(int playerId,
                                Snap state) => mPlayerStates[playerId.ToString()] = state;

    /// <summary>
    /// Send world state to all the clients. Used only by the server.
    /// </summary>
    public void SendWorldState() => RpcUnreliableId(0, "RecvWorldState", mWorldState);

    /// <summary>
    /// Receive input from the player from the client. Used only by the server.
    /// </summary>
    [Master]
    public void RecvPlayerInput(Snap playerState) {
        string playerId = GetTree().GetRpcSenderId().ToString();
        if (!HasNode(playerId)) {
            GD.PrintErr($"Recv'd invalid player input: {GetTree().GetRpcSenderId()}.");
            return;
        }
        if (Util.TryGetVOr(playerState, "ts", ulong.MaxValue) <
            Util.TryGetVOr(mWorldState, "ts", ulong.MaxValue)) {
            return;
        }

        GetNode<Player>(playerId).PlayerInput(playerState);
    }

    /// <summary>
    /// Receive world state from the server. Used only by the client.
    /// </summary>
    [Puppet]
    public void RecvWorldState(Snap input) {
        // Do players first.
        mPacketCounter++;
        ulong ts = Util.TryGetVOr(input, "ts", ulong.MaxValue);
        if (ts > mWorldTs) {
            mWorldCache.Add(input);
            mWorldTs = ts;
        }
    }
}
