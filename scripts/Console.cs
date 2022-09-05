using Godot;
using System;
using System.Collections.Generic;

using Cmd = System.Func<string[], (string, int)>;

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
    } = new List<(string, Cmd)> { ("fps_max", FpsMax), ("net_graph", NetGraph), ("god", God),
                                  ("?", GetLastResult), ("console_max_chars", ConsoleMaxChars) };

    public static void AddCmds(string name, Cmd command) => AddCmds((name, command));

    public static void AddCmds((string, Cmd)command) {
        Commands.Add(command);
    }

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

    public static (string, int) ConsoleMaxChars(string[] argv) {
        int argc = argv.Length;

        return ("", 0);
    }

    public static (string, int) FpsMax(string[] argv) {
        int argc = argv.Length;

        if (argv.Length <= 1) {
            return ("Error: fps_max takes 1 argument (an integer). Was given none.", 1);
        }

        try {
            int fps = argv[1].ToInt();
        } catch (FormatException e) {
            return ($"Error: fps_max takes 1 argument (an integer). Was given {argv[1]}.", 1);
        } catch (OverflowException e) {
            return ($"Error: fps_max takes 1 argument (an integer). Was given {argv[1]}.", 1);
        }

        return ("", 0);
    }

    public static (string, int) NetGraph(string[] argv) {
        int argc = argv.Length;

        return ("", 0);
    }

    public static (string, int) God(string[] argv) {
        return ("", 0);
    }

    public static int LastResult { get; private set; } = 0;

    public static (string, int) GetLastResult(string[] argv) => ("", LastResult);

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
                (string @out, int result) = command.Item2(args);
                LastResult = result;
                mOutput.Text += @out + '\n';

                // Limit the number of chars that can be in the text box.
                if (mOutput.Text.Length > MaxOutputChars) {
                    mOutput.Text = mOutput.Text.Substring(mOutput.Text.Length - MaxOutputChars);
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
}
