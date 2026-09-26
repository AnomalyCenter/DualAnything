using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using ComputerysModdingUtilities;

[assembly: StraftatMod(isVanillaCompatible: false)]
[BepInPlugin("com.27n.dualanything", "DualAnything", "1.0.0")]

public class DualAnythingPlugin : BaseUnityPlugin
{
    public static ConfigEntry<KeyCode> HeavyAttackKey;

    private void Awake()
    {
        HeavyAttackKey = Config.Bind(
            "Controls",
            "HeavyAttackModifier",
            KeyCode.LeftAlt,
            "Hold to perform heavy attacks when dual wielding (intended for melee weapons, but kinda works for guns as well)"
        );

        var harmony = new Harmony("com.27n.dualanything");
        harmony.PatchAll();

        Logger.LogInfo("[27N] DualAnything loaded");
    }
}
