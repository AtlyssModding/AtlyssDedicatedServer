using HarmonyLib;
using UnityEngine;

namespace AtlyssDedicatedServer.HarmonyPatches;

[HarmonyPatch(typeof(AudioListener), nameof(AudioListener.volume), MethodType.Setter)]
static class PreventAudioChanges
{
    internal static bool LockAudio { get; set; }
    
    static bool Prefix() => !LockAudio;
}