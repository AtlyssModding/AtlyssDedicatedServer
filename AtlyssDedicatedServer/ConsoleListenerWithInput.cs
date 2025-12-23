using System.Text;
using BepInEx;
using BepInEx.Logging;

namespace AtlyssDedicatedServer;

public class ConsoleListenerWithInput(ConsoleLogListener original) : ILogListener
{
    public readonly StringBuilder Input = new StringBuilder();
    private static readonly object _consoleLock = new object();
    
    public void Dispose()
    {
        original.Dispose();
    }

    public void LogEvent(object sender, LogEventArgs eventArgs)
    {
        ClearInputLine();
        original.LogEvent(sender, eventArgs);
        DrawInputPrompt();
    }

    private static string WindowWidthString = "";

    private void ClearInputLine()
    {
        int width = Math.Max(Console.WindowWidth - 1, 0);

        if (WindowWidthString.Length != width)
            WindowWidthString = new string(' ', width);
        
        ConsoleManager.StandardOutStream.Write('\r');
        ConsoleManager.StandardOutStream.Write(WindowWidthString);
        ConsoleManager.StandardOutStream.Write('\r');
    }
    
    private void DrawInputPrompt()
    {
        lock (_consoleLock)
        {
            ConsoleManager.StandardOutStream.Write("> ");
            ConsoleManager.StandardOutStream.Write(Input.ToString());
            ConsoleManager.StandardOutStream.Flush();
        }
    }

    public void ProcessInput()
    {
        if (!Console.KeyAvailable)
            return;

        ConsoleKeyInfo key = Console.ReadKey(intercept: true);
        lock (_consoleLock)
        {
            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    string command = Input.ToString();
                    Input.Clear();

                    ConsoleManager.StandardOutStream.WriteLine();
                    
                    HostConsole._current.Send_ServerMessage(command);
                    ClearInputLine();
                    DrawInputPrompt();
                    break;

                case ConsoleKey.Backspace:
                    if (Input.Length > 0)
                    {
                        Input.Length--;
                        ClearInputLine();
                        DrawInputPrompt();
                    }

                    break;
                
                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        Input.Append(key.KeyChar);
                        ConsoleManager.StandardOutStream.Write(key.KeyChar);
                    }

                    break;
            }
        }
    }
}