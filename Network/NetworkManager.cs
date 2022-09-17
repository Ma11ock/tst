using Godot;
using System.Net;
using System.Net.Sockets;

class NetworkManager : Godot.Node {
    public const int DEFAULT_PORT = 28960;

    public const int MAX_CLIENTS = 64;

    public const string DEFAULT_IP = "localhost";

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
        GetTree().Connect("network_peer_connected", this, "_NetworkPeerConnected");
        GetTree().Connect("network_peer_disconnected", this, "_NetworkPeerDisconnected");
    }

    public void ResetNetworkConnection() {
        if (GetTree().HasNetworkPeer()) {
            GetTree().NetworkPeer = null;
        }
    }

    public virtual void _NetworkPeerConnected(int id) {
        GD.Print($"Player joined with id {id}");
    }

    public virtual void _NetworkPeerDisconnected(int id) {
        GD.Print($"Player left with id {id}");
    }

    public bool IsServer() => GetTree().IsNetworkServer();
    public bool IsAClient() => !GetTree().IsNetworkServer();
}

