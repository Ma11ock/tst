using Godot;
using System;

public class NetworkSetup : Control {
    private Network mNetwork = null;
    private Global mGlobal = null;

    public override void _Ready() {
        mNetwork = GetNode<Network>("/root/Network");
        mGlobal = GetNode<Global>("/root/Global");

        mGlobal.Connect("ToggleNetworkSetup", this, "_ToggleNetworkSetup");
    }

    public void _OnIpAddressTextChanged(string newText) {
        mNetwork.mIpAddress = newText;
    }

    public void _OnHostPressed() {
        mNetwork.CreateServer();
        Hide();
    }

    public void _OnJoinPressed() {
        mNetwork.JoinServer();
        Hide();

        mGlobal.EmitSignal("InstancePlayer", GetTree().GetNetworkUniqueId());
    }

    public void _ToggleNetworkSetup(bool visible) {
        Visible = visible;
    }
}
