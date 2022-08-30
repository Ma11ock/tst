using Godot;

class Network : Godot.Node {
    public static readonly short DEFAULT_PORT = 28960;
    public static readonly int MAX_CLIENTS = 6;

    public string mIpAddress { get; set; } = "127.0.0.1";

    public NetworkedMultiplayerENet mServer { get; private set; } = null;
    public NetworkedMultiplayerENet mClient { get; private set; } = null;

    public override void _Ready() {
        base._Ready();
        // Connect networking signals.
        GetTree().Connect("connected_to_server", this, "_ConnectedToServer");
        GetTree().Connect("server_disconnected", this, "_ServerDisconnected");
        GetTree().Connect("connection_failed", this, "_ConnectionFailed");
        GetTree().Connect("network_peer_connected", this, "_NetworkPeerConnected");
    }

    public void CreateServer() {
        GD.Print("Creating server...");

        mServer = new NetworkedMultiplayerENet();
        mServer.CreateServer(DEFAULT_PORT, MAX_CLIENTS);
        GetTree().NetworkPeer = mServer;

        GD.Print("Done creating server.");
    }

    public void JoinServer() {
        GD.Print("Joining server...");

        mClient = new NetworkedMultiplayerENet();
        mClient.CreateClient(mIpAddress, DEFAULT_PORT);
        GetTree().NetworkPeer = mClient;
    }

    public void _ConnectedToServer() {
        GD.Print("Successfully connected to server.");
    }

    public void _ServerDisconnected() {
        GD.Print("Server disconnected.");
        ResetNetworkConnection();
    }

    public void _ConnectionFailed() {
        GD.Print("Connection to server. failed.");
        ResetNetworkConnection();
    }

    public void ResetNetworkConnection() {
        if (GetTree().HasNetworkPeer()) {
            GetTree().NetworkPeer = null;
        }
    }

    public void _NetworkPeerConnected(int id) {
        GD.Print($"Player joined with id {id}");
    }
}
