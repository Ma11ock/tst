using Godot;
using System.IO;
using System;
using Tommy;

class Autoload : Node {
    public override void _Ready() {
        base._Ready();
        GD.Print("Initializing game...");

        // Get global config.
        if (OS.HasFeature("editor")) {
            DoConfigAt(ProjectSettings.GlobalizePath("res://").PlusFile("config.toml"));
        } else {
            DoConfigAt(OS.GetExecutablePath().GetBaseDir().PlusFile("config.toml"));
        }
        DoConfigAt(OS.GetUserDataDir().PlusFile("config.toml"));
        GD.Print("Done.");
    }

    public void SetConfig(TomlTable table) {
        string path = "";
        // TODO
        foreach(TomlNode node in table) {
            if (node is TomlTable t) {
                var c = t.Comment;
                GD.Print($"Comment is : {c}");
            }
            // ProjectSettings.SetSetting();
        }
    }

    private void DoConfigAt(string path) {
        GD.Print($"Reading config at {path}...");
        try {
            using (StreamReader reader = System.IO.File.OpenText(path)) {
                SetConfig(TOML.Parse(reader));
                GD.Print($"Done reading config at {path}.");
            }
        } catch (System.IO.FileNotFoundException) {
            GD.PrintErr($"No file at {path} !");
        } catch (TomlParseException e) {
            foreach(TomlSyntaxException syntaxEx in e.SyntaxErrors) {
                GD.PrintErr($"Error on {syntaxEx.Column}:{syntaxEx.Line}: {syntaxEx.Message}");
            }
        } catch (Exception e) {
            GD.PrintErr($"Could not open {path}: {e.ToString()}.");
        }
    }
}
