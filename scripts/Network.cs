using Godot;
using System.Net;
using System.Net.Sockets;

class Network : Godot.Node {
    public const short DEFAULT_PORT = 28960;
    public const int MAX_CLIENTS = 6;

    public string mIpAddress { get; set; } = "127.0.0.1";  // localhost

    public NetworkedMultiplayerENet mServer { get; private set; } = null;
    public NetworkedMultiplayerENet mClient { get; private set; } = null;

    private static readonly IPEndPoint DEFAULT_ENDPOINT =
        new IPEndPoint(IPAddress.Loopback, port: 0);
    /// <summary>
    /// Get a free UDP port from the OS. This function is a temporary solution until Godot 4.
    /// There could potentially be a race condition where another process uses our port before
    /// we can bind to it. However, this is unlikely since the OS will usually wait until all
    /// other free ports are allocated before wrapping around again.
    /// </summary>
    /// <returns>A (probably valid) free UDP port.</returns>
    public static int FreeUDPPort() {
        using (Socket socket =
                   new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)) {
            socket.Bind(DEFAULT_ENDPOINT);
            return ((IPEndPoint)socket.LocalEndPoint).Port;
        }
    }

    public override void _Ready() {
        base._Ready();
        // Connect networking signals.
        GetTree().Connect("connected_to_server", this, "_ConnectedToServer");
        GetTree().Connect("server_disconnected", this, "_ServerDisconnected");
        GetTree().Connect("connection_failed", this, "_ConnectionFailed");
        GetTree().Connect("network_peer_connected", this, "_NetworkPeerConnected");
    }

    public int CreateServer(int port = DEFAULT_PORT) {
        GD.Print("Creating server...");

        if(port == 0) {
            // Alloc port.
            port = FreeUDPPort();
        }

        mServer = new NetworkedMultiplayerENet();
        mServer.CreateServer(port, MAX_CLIENTS);
        GetTree().NetworkPeer = mServer;

        GD.Print("Done creating server.");

        return port;
    }

    public void JoinServer(int port = DEFAULT_PORT) {
        GD.Print("Joining server...");
        mClient = new NetworkedMultiplayerENet();
        mClient.CreateClient(mIpAddress, port);
        GetTree().NetworkPeer = mClient;
        GD.Print("Joined server.");
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

    public void ResetNetworkConnection() {
        if (GetTree().HasNetworkPeer()) {
            GetTree().NetworkPeer = null;
        }
    }

    public void _NetworkPeerConnected(int id) {
            GD.Print($"Player joined with id {id}");
    }
}
