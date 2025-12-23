using System.Reflection.Emit;
using HarmonyLib;

namespace AtlyssDedicatedServer.HarmonyPatches;

[HarmonyPatch(typeof(AtlyssNetworkManager), nameof(AtlyssNetworkManager.Clear_ConsoleInit))]
static class ClearPatch
{
    static bool Prefix() => false;
}