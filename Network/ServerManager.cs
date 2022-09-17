using Godot;
using System.Collections.Generic;

using Snap = Godot.Collections.Dictionary;
class ServerManager : NetworkManager {
    public NetworkedMultiplayerENet mServer { get; private set; } = null;

    public int _mPort = 0;
    public int mPort { get => _mPort; set {
            if(_mPort == 0) {
                _mPort = value;
            }
        } }

    private Dictionary<int, ClientMaster> mClients = new Dictionary<int, ClientMaster>();

    public override void _Ready() {
        base._Ready();
        // For enet.
        Name = 1.ToString();

        if(mPort == 0) {
            mPort = NetworkManager.FreeUDPPort();
        }

        mServer = new NetworkedMultiplayerENet();
        mServer.CreateServer(mPort, MAX_CLIENTS);
        GetTree().NetworkPeer = mServer;

        GD.Print($"Started server on port {mPort}.");
    }

    public override void _ExitTree() {
        base._ExitTree();
        GD.Print("Ending server session");
    }

    public override void _Process(float delta) {
        base._Process(delta);
        //RpcUnreliableId(0, "RecvClientData", new Snap());
    }

    [Master]
    public void RecvUserInput(Snap inputs) {
    }

    public override void _NetworkPeerConnected(int id) {
        base._NetworkPeerConnected(id);
        ClientMaster cm = ClientMaster.MkClientMaster(id);
        GetTree().Root.AddChild(cm);
        mClients[id] = cm;
    }

    public override void _NetworkPeerDisconnected(int id) {
        base._NetworkPeerDisconnected(id);
        mClients[id].QueueFree();
    }
}
