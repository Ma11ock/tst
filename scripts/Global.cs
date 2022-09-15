using Godot;

using System.Collections.Generic;

class Global : Node {
    [Signal]
    delegate void InstancePlayer(int id);

    [Signal]
    delegate void ToggleNetworkSetup(bool toggle);

    /// <summary>
    /// True if something should be capturing input (player should not move).
    /// </summary>
    public static bool InputCaptured = false;

    public static void ToggleMouseSettings() => Input.MouseMode =
        (Input.MouseMode == Input.MouseModeEnum.Visible) ? Input.MouseModeEnum.Captured :
                                                           Input.MouseModeEnum.Visible;

    public Tst.CVarCollection mCollection {
        get; private set;
    } = new Tst.CVarCollection(new Dictionary<string, Tst.CVar>() {
        { "fps_max", new Tst.CIVar(0) },
        { "cl_interp_ratio", new Tst.CIVar(1) },
    });
}
