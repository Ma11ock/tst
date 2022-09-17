using Godot;
using System.IO;
using System;
using Tommy;
using System.Runtime;

class Autoload : Node {
    private Global mGlobals = null;

    private enum SCSettings {
        None,  // None specified.
        Server,
        Client
    }

    private string GetNextArg(string[] args, ref int i) {
        try {
            return args[++i];
        } catch (IndexOutOfRangeException) {
            throw new InvalidDataException($"{args[i - i]} takes an argument");
        }
    }

    private int GetNextArgI(string[] args, ref int i) {
        try {
            return GetNextArg(args, ref i).ToInt();
        } catch (FormatException) {
            throw new InvalidDataException(
                $"{args[i - i]} takes an int as an argument, but got \"{args[i]}\" instead.");
        } catch (OverflowException) {
            throw new InvalidDataException(
                $"{args[i - i]} takes an int as an argument, but got \"{args[i]}\", which is out of range for an int [{int.MinValue}-{int.MaxValue}].");
        }
    }

    public override void _Ready() {
        base._Ready();
        GD.Print("Initializing game...");
        mGlobals = GetNode<Global>("/root/Global");
        ClientMaster.sGlobals = mGlobals;

        // Get global config.
        if (OS.HasFeature("editor")) {
            DoConfigAt(ProjectSettings.GlobalizePath("res://").PlusFile("config.toml"));
        } else {
            DoConfigAt(OS.GetExecutablePath().GetBaseDir().PlusFile("config.toml"));
        }
        DoConfigAt(OS.GetUserDataDir().PlusFile("config.toml"));
        GD.Print("Done.");

        // Parse cmd args.
        string[] args = OS.GetCmdlineArgs();
        string host = NetworkManager.DEFAULT_IP;
        int port = NetworkManager.DEFAULT_PORT;
        string nextArg = "";
        SCSettings settings = SCSettings.None;
        string errstr;

        for (int i = 0; i < args.Length; i++) {
            switch (args[i]) {
            case "--mk-server":
                settings = SCSettings.Server;
                port = GetNextArgI(args, ref i);
                break;
            case "--mk-client":
                settings = SCSettings.Client;
                // Try getting the port. If that fails, try getting a host and port.
                try {
                    nextArg = GetNextArg(args, ref i);
                    port = args[i].ToInt();
                } catch (OverflowException e) {
                    throw new InvalidDataException(
                        $"\"{args[i]}\" is not a valid int: {e.Message}.");
                } catch (InvalidDataException e) {
                    throw e;
                } catch (Exception) {
                    try {
                        if (settings == SCSettings.Client) {
                            // Maybe it's a url. Try to convert it.
                            Uri address = new Uri(args[i]);
                            port = address.Port;
                            host = address.Host;
                            break;
                        }
                    } catch (Exception e) {
                        errstr = $"\"{args[i]}\" is not a valid address: {e.Message}";
                        GD.PrintErr(errstr);
                        throw new InvalidDataException(errstr);
                    }
                }
                break;
            default:
                throw new InvalidDataException($"Unrecognized cmd argument: ${args[i]}.");
            }
        }

        ServerManager server = null;
        ClientManager client = null;
        switch (settings) {
        case SCSettings.None:
            // Turn this Godot instance into a server and make a client.
            GD.Print($"Starting server on port {port}...");
            server = mGlobals.MkInstance<ServerManager>("server_manager");
            GetTree().Root.CallDeferred("add_child", server);
            server.mPort = port;
            string[] clientArgs = new string[] { "--mk-client", port.ToString() };
            string exe = "Tst";
            if (OS.IsDebugBuild()) {
                exe = "godot-mono";
            }
            OS.Execute(exe, clientArgs, false);
            break;
        case SCSettings.Server:
            GD.Print($"Starting server on port {port}...");
            server = mGlobals.MkInstance<ServerManager>("server_manager");
            GetTree().Root.CallDeferred("add_child", server);
            server.mPort = port;
            break;
        case SCSettings.Client:
            client = mGlobals.MkInstance<ClientManager>("client_manager");
            GetTree().Root.CallDeferred("add_child", client);
            client.mPort = port;
            client.mAddress = host;
            break;
        }
    }

    public void SetConfig(TomlTable table) {
        string path = "";
        // TODO
        foreach(TomlNode node in table) {
            if (node is TomlTable t) {
                var c = t.Comment;
                // GD.Print($"Comment is : {c}");
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
