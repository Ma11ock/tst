using Godot;

class Global : Node {
    [Signal]
    delegate void InstancePlayer(int id);

    [Signal]
    delegate void ToggleNetworkSetup(bool toggle);

    /// <summary>
    /// True if something should be capturing input (player should not move).
    /// </summary>
    public static bool InputCaptured = false;

    public static void ToggleMouseSettings() =>
        Input.MouseMode = (Input.MouseMode == Input.MouseModeEnum.Visible) ?
                              Input.MouseModeEnum.Captured :
                              Input.MouseModeEnum.Visible;
}
