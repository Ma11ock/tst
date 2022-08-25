using Godot;
using System;
using System.Collections.Generic;

public class DebugOverlay : CanvasLayer {
    public struct debugObj {
        public string statName { get; private set; }
        public Godot.Object obj { get; private set; }
        public string fieldName { get; private set; }
        public bool fieldIsFunc { get; private set; }

        public debugObj(string statName, Godot.Object obj, string fieldName, bool fieldIsFunc) : this() {
            this.statName = statName;
            this.obj = obj;
            this.fieldName = fieldName;
            this.fieldIsFunc = fieldIsFunc;
        }
    }

    private Godot.Label mLabel = null;
    private List<debugObj> mStats = new List<debugObj>();

    public override void _Ready()
    {
        base._Ready();
        mLabel = GetNode<Godot.Label>("Label");
    }

    public override void _Process(float delta)
    {
        base._Process(delta);

        string labelText = "";

        foreach(debugObj obj in mStats) {
            System.Object value = null;
            var gobj = obj.obj;
            if(gobj != null && IsInstanceValid(gobj)) {
                if(obj.fieldIsFunc) {
                    value = gobj.Call(obj.fieldName);
                } else {
                    value = gobj.Get(obj.fieldName);
                }
            }

            value = (value == null) ? "null" : value;

            labelText += $"{obj.statName}: {value.ToString()}\n";
        }

        mLabel.Text = labelText;
    }

    public void AddStat(string statName, Godot.Object obj, string fieldName, bool fieldIsFunc) {
        mStats.Add(new debugObj(statName, obj, fieldName, fieldIsFunc));
    }

    public void AddStat(debugObj debugObj) {
        mStats.Add(debugObj);
    }
}
