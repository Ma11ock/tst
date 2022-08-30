using Godot;

class Global : Node {
    [Signal]
    delegate void InstancePlayer(int id);

    [Signal]
    delegate void ToggleNetworkSetup(bool toggle);
}
