using Godot;

using Snap = Godot.Collections.Dictionary;

class ClientManager : NetworkManager {
    public NetworkedMultiplayerENet mClient { get; private set; } = null;
    // TODO will need to make this more robust when we have the ability to move servers.
    public int mPort { get; set; } = 0;
    public string mAddress { get; set; } = "";

    public Scene mCurScene = null;

    public override void _Ready() {
        base._Ready();
        GD.Print($"Starting client on {mAddress}:{mPort}...");
        GetTree().Connect("connected_to_server", this, "_ConnectedToServer");
        GetTree().Connect("server_disconnected", this, "_ServerDisconnected");
        GetTree().Connect("connection_failed", this, "_ConnectionFailed");
        mClient = new NetworkedMultiplayerENet();
        mClient.CreateClient(mAddress, mPort);
        GetTree().NetworkPeer = mClient;

        GD.Print($"Created client on {mAddress}:{mPort}.");
        // For enet recving.
        Name = GetTree().GetNetworkUniqueId().ToString();
    }

    public override void _ExitTree() {
        base._ExitTree();
        GD.Print("Ending client session");
    }

    public void _ConnectedToServer() {
        GD.Print("Successfully connected to server.");
    }

    public void _ServerDisconnected() {
        GD.Print("Server disconnected.");
        ResetNetworkConnection();
    }

    public void _ConnectionFailed() {
        GD.Print("Connection to server failed.");
        ResetNetworkConnection();
    }

    [Puppet]
    public void RecvClientData(Snap snapshot) {
        if(mCurScene == null) {
            return;
        }

        mCurScene.RecvWorldState(snapshot);
    }

    public override void _Process(float delta) {
        base._Process(delta);

        RpcUnreliableId(1, "RecvInput", new Snap());
    }
}
