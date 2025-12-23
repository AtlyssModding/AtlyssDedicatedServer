using System.Text.RegularExpressions;
using BepInEx.Logging;
using HarmonyLib;

namespace AtlyssDedicatedServer.HarmonyPatches;

[HarmonyPatch(typeof(ChatBehaviour), "New_ChatMessage")]
static class ChatMirrorPatch
{
    static void Postfix(string _message)
    {
        string cleanMessage = StripUnityRichText(_message);

        if (cleanMessage == HostConsoleFixPatch.LastServerMessage &&
            (DateTime.UtcNow - HostConsoleFixPatch.LastServerMessageTime).TotalMilliseconds < 100)
        {
            return;
        }

        Logger.LogInfo($"[Chat] {cleanMessage}");
    }

    static string StripUnityRichText(string input)
    {
        return Regex.Replace(input, "<.*?>", string.Empty);
    }
}