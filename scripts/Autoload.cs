using Godot;
using System.IO;
using System;
using Tommy;
using System.Runtime;

class Autoload : Node {
    private Network mNetwork = null;

    private enum SCSettings {
        None,  // None specified.
        Server,
        Client
    }

    private string GetNextArg(string[] args, ref int i) {
        if (args.Length < ++i) {
            throw new InvalidDataException($"{args[i - i]} needs an argument");
        }
        return args[i];
    }

    public override void _Ready() {
        base._Ready();
        mNetwork = GetNode<Network>("/root/Network");
        GD.Print("Initializing game...");

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
        string host = mNetwork.mIpAddress;
        int port = Network.DEFAULT_PORT;
        string nextArg = "";
        SCSettings settings = SCSettings.None;
        string errstr;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--mk-server":
                    settings = SCSettings.Server;
                    goto case "--mk-client";
                case "--mk-client":
                    // Check for cmd arg misconfig.
                    switch (settings)
                    {
                        case SCSettings.Server:
                            errstr = "mk-client and mk-server both specified";
                            GD.PrintErr(errstr);
                            throw new InvalidDataException(errstr);
                        case SCSettings.Client:
                            errstr = "mk-client specified twice";
                            GD.PrintErr(errstr);
                            throw new InvalidDataException(errstr);
                    }
                    // Setup client data.
                    settings = SCSettings.Client;
                    try
                    {
                        nextArg = GetNextArg(args, ref i);
                        port = args[i].ToInt();
                    }
                    catch (OverflowException e)
                    {
                        throw new InvalidDataException(
                            $"\"{args[i]}\" is not a valid int: {e.Message}.");
                    }
                    catch (InvalidDataException e)
                    {
                        throw e;
                    }
                    catch (Exception)
                    {
                        try
                        {
                            if (settings == SCSettings.Client)
                            {
                                // Maybe it's a url. Try to convert it.
                                Uri address = new Uri(args[i]);
                                port = address.Port;
                                host = address.Host;
                                break;
                            }
                        }
                        catch (Exception e)
                        {
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

        switch (settings) {
        case SCSettings.None:
            // Turn this Godot instance into a server and make a client.
            port = mNetwork.CreateServer(0);
            GD.Print($"Started server on port {port}.");
            string[] clientArgs = new string[] { "--mk-client", port.ToString() };
            string exe = "Tst";
            if (OS.IsDebugBuild()) {
                exe = "godot-mono";
            }
            OS.Execute(exe, clientArgs, false);
            break;
        case SCSettings.Server:
            break;
        case SCSettings.Client:
            GD.Print($"Starting client on {host}:{port}.");
            mNetwork.JoinServer(port);
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
