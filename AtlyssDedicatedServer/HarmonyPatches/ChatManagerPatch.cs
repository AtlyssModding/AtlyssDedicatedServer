using AtlyssDedicatedServer;
using HarmonyLib;
using UnityEngine;

namespace AtlyssDedicatedServer.HarmonyPatches;

[HarmonyPatch(typeof(HostConsole), nameof(HostConsole.Send_ServerMessage))]
[HarmonyPriority(Priority.First)]
static class ChatManagerPatch
{
    static bool Prefix(ref string _message)
    {
        if (string.IsNullOrWhiteSpace(_message))
            return false;
        
        string[] array = _message.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (_message.Contains('<') || _message.Contains('>'))
        {
            Plugin.Logger.LogInfo("'<' and '>' are not allowed in server messages.");
            return false;
        }

        if (_message[0] != '/')
        {
            HostConsole._current.Init_ServerMessage(_message);
            return false;
        }
        
        if (array[0].Trim() == "/restart")
        {
            if (!(array.Length >= 2 && Utils.TryParseInterval(array[1], out var seconds) && seconds >= 1))
                seconds = 20;
            
            Plugin.Handler.ScheduleRestart(seconds);
            return false;
        }
        
        if (array[0].Trim() == "/shutdown")
        {
            if (!(array.Length >= 2 && Utils.TryParseInterval(array[1], out var seconds) && seconds >= 1))
                seconds = 20;

            Plugin.Handler.ScheduleShutdown(seconds);
            return false;
        }
        
        if (_message.Trim() == "/cancelsd" || _message.Trim() == "/cancelrt")
        {
            Plugin.Handler.CancelShutdownOrRestart();
            return false;
        }
        
        HostConsole._current._cmdManager.Init_ConsoleCommand(array[0].TrimStart('/'), array.Length > 1 ? array[1] : string.Empty);

        return false;
    }
}