using Godot;

using Snap = Godot.Collections.Dictionary;
class ClientMaster : Godot.Node {

    private int _mRemoteId = 0;
    public int mRemoteId
    {
        get => _mRemoteId;
        set
        {
            _mRemoteId = value;
            Name = value.ToString();
        }
    }

    // Set by autoload.
    public static Global sGlobals = null;

    public override void _Ready() {
        base._Ready();
        Name = mRemoteId.ToString();
    }

    public override void _Process(float delta) {
        base._Process(delta);
        RpcUnreliable("RecvClientData", new Snap());
    }

    [Master]
    public void RecvInput(Snap snap) {
    }

    public static ClientMaster MkClientMaster(int id) {
        ClientMaster result = sGlobals.MkInstance<ClientMaster>("client_master");
        result.mRemoteId = id;
        return result;
    }
}
