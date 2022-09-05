using Godot;
using System;
using System.Collections.Generic;

using Cmd = System.Func<string[], Console, int>;

public class Console : Control {
    private Godot.LineEdit mInput = null;
    private Godot.TextEdit mOutput = null;

    public const int DEFAULT_CONSOLE_LIMIT_MAX = 4096;

    private static int _MaxOutputChars = DEFAULT_CONSOLE_LIMIT_MAX;

    public static int MaxOutputChars {
        get => _MaxOutputChars;
        set {
            if (value < DEFAULT_CONSOLE_LIMIT_MAX) {
                GD.PrintErr(
                    $"Attempt to set console limit to below min value ({value})... skipping.");
                return;
            }

            _MaxOutputChars = value;
        }
    }

    public static List<(string, Cmd)> Commands {
        get; private set;
    } = new List<(string, Cmd)> { ("fps_max", FpsMax),
                                  ("net_graph", NetGraph),
                                  ("god", God),
                                  ("?", GetLastResult),
                                  ("console_max_chars", ConsoleMaxChars),
                                  ("clear", Clear),
                                  ("say", Say) };

    public static void AddCmds(string name, Cmd command) => AddCmds((name, command));

    public static void AddCmds((string, Cmd)command) => Commands.Add(command);

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

    public static int Say(string[] argv, Console c) {
        c.mOutput.Text += "say: ";
        for (int i = 1; i < argv.Length; i++) {
            c.mOutput.Text += argv[i];
        }
        return 0;
    }

    public static int Clear(string[] argv, Console c) {
        c.mOutput.Text = "";
        return 0;
    }

    public static int ConsoleMaxChars(string[] argv, Console c) {
        int argc = argv.Length;

        if (argv.Length <= 1) {
            c.mOutput.Text +=
                "Error: console_max_chars takes 1 argument (an integer). Was given none.";
            return 1;
        }

        try {
            int max = argv[1].ToInt();
            MaxOutputChars = max;
        } catch (FormatException) {
            c.mOutput.Text +=
                $"Error: console_max_chars takes 1 argument (an integer). Was given {argv[1]}.";
            return 1;
        } catch (OverflowException) {
            c.mOutput.Text +=
                $"Error: console_max_chars takes 1 argument (an integer). Was given {argv[1]}.";
            return 1;
        }

        return 0;
    }

    public static int FpsMax(string[] argv, Console c) {
        int argc = argv.Length;

        if (argv.Length <= 1) {
            c.mOutput.Text += "Error: fps_max takes 1 argument (an integer). Was given none.";
            return 1;
        }

        try {
            int fps = argv[1].ToInt();
            Engine.TargetFps = fps;
        } catch (FormatException) {
            c.mOutput.Text += $"Error: fps_max takes 1 argument (an integer). Was given {argv[1]}.";
            return 1;
        } catch (OverflowException) {
            c.mOutput.Text += $"Error: fps_max takes 1 argument (an integer). Was given {argv[1]}.";
            return 1;
        }

        return 0;
    }

    public static int NetGraph(string[] argv, Console c) {
        int argc = argv.Length;

        Scene s = (c.GetParent() as Scene);

        if (s == null) {
            c.mOutput.Text += "Parent is not a `Scene`, cannot show performance graphs!";
            return 1;
        }

        bool shouldVisible = false;

        if (argv.Length > 1) {
            // Find out what the user wants.
            int maybeInt = 0;
            if (!Int32.TryParse(argv[1], out maybeInt)) {
                if (!Boolean.TryParse(argv[1], out shouldVisible)) {
                    c.mOutput.Text +=
                        $"net_graph takes a boolean as an argument, got {argv[1]} instead.";
                    return 1;
                }
            } else {
                shouldVisible = maybeInt != 0;
            }
        }

        s.ToggleDebugOverlay(shouldVisible);

        return 0;
    }

    public static int God(string[] argv, Console c) {
        // TOdo
        return -1;
    }

    public static int LastResult { get; private set; } = 0;

    public static int GetLastResult(string[] argv, Console c) {
        c.mOutput.Text += LastResult.ToString();
        return LastResult;
    }

    private void RunCmd(string input) {
        // Chop up input into a function call and an argument vector.
        char[] delims = {
            '\r',
            ' ',
            '\n',
        };
        string[] args = input.Split(delims, StringSplitOptions.RemoveEmptyEntries);

        if (args.Length == 0) {
            return;
        }
        string call = args[0];

        foreach((string, Cmd)command in Commands) {
            if (command.Item1 == call) {
                LastResult = command.Item2(args, this);
                // Append newline if the command did not.
                if (!mOutput.Text.EndsWith("\n")) {
                    mOutput.Text += '\n';
                }

                goto print_return_value;
            }
        }

        LastResult = int.MinValue;
        mOutput.Text += $"Error: \"{call}\" is not a recognized command.\n";
print_return_value:
        char terminalChar = '$';  // TODO make '#' if client has admin privs.
        mInput.PlaceholderText = $"{LastResult} {terminalChar}";
    }

    public void _OnInputTextEntered(string input) {
        mInput.Clear();
        RunCmd(input);
    }

    public void _OnOutputTextChanged() {
        // Limit the number of chars that can be in the text box.
        if (mOutput.Text.Length > MaxOutputChars) {
            mOutput.Text = mOutput.Text.Substring(mOutput.Text.Length - MaxOutputChars);
        }
    }
}
