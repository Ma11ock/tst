using Godot;
using System;

public class Console : Control {
    private Godot.LineEdit mInput = null;
    private Godot.TextEdit mOutput = null;

    public override void _Ready() {
        mInput = GetNode<Godot.LineEdit>("input");
        mOutput = GetNode<Godot.TextEdit>("output");
    }

    public void ToggleVisible() {
        Visible = !Visible;
        if (Visible) {
            mInput.GrabFocus();
        }
    }

    private void RunCmd(string input) {
        mOutput.Text += $"{input}\n";

        switch (input) {
        case "fps_max":

            break;
        }
    }

    public void _OnInputTextEntered(string input) {
        mInput.Clear();
        RunCmd(input);
    }
}
