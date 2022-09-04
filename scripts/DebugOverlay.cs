using Godot;
using System;
using System.Collections.Generic;

namespace Tst {
public interface Debuggable {
    void GetDebug(Control c);

    ulong GetDebugId();
    void SetDebugId(ulong id);
}
}

public class DebugOverlay : Control {
    private Godot.VBoxContainer mVbox = null;

    private ulong mDebugIds = 0;

    private Dictionary<ulong, Tst.Debuggable> mDebuggables =
        new Dictionary<ulong, Tst.Debuggable>();

    public override void _Ready() {
        base._Ready();
        mVbox = GetNode<Godot.VBoxContainer>("VBox");
    }

    public override void _Process(float delta) {
        base._Process(delta);

        if (!Visible) {
            return;
        }

        foreach(var kv in mDebuggables) {
            ulong id = kv.Key;
            Tst.Debuggable debuggable = kv.Value;
            Control c = mVbox.GetNodeOrNull<Control>(id.ToString());
            if (c == null) {
                GD.PrintErr($"Debuggable with id {id} does not have a control");
                continue;
            }

            debuggable.GetDebug(c);
        }
    }

    public void Add<T>(Tst.Debuggable debuggable)
        where T : Control, new() {
        if (debuggable == null || debuggable.GetDebugId() != 0) {
            return;
        }
        ulong id = ++mDebugIds;
        debuggable.SetDebugId(id);
        mDebuggables[id] = debuggable;

        T item = new T();
        item.Name = id.ToString();
        mVbox.AddChild(item);
    }

    public void Remove(Tst.Debuggable debuggable) {
        ulong id = 0;
        if (debuggable == null || (id = debuggable.GetDebugId()) == 0) {
            return;
        }
        mDebuggables.Remove(id);

        Control c = mVbox.GetNodeOrNull<Control>(id.ToString());
        if (c != null) {
            mVbox.RemoveChild(c);
            c.QueueFree();
        }
    }
}
