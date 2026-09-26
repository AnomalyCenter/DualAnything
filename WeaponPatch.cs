using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

static class DualAnythingUtil
{
    public static bool IsCochin(GameObject obj)
    {
        return obj != null && obj.GetComponentInParent<PigItem>() != null;
    }
}


// ============================================================
// Allow switching weapons even when vanilla considers the
// weapon two-handed.
// ============================================================

[HarmonyPatch(typeof(PlayerPickup), "SwitchWeapons")]
class AllowTwoHandSwapPatch
{
    static bool Prefix(PlayerPickup __instance)
    {
        var type = typeof(PlayerPickup);

        var objInHand =
        AccessTools.Field(type, "objInHand")?.GetValue(__instance) as GameObject;

        // Pigs are intentionally left completely vanilla.
        if (DualAnythingUtil.IsCochin(objInHand))
        {
            return false;
        }

        return true;
    }

    static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(Weapon), "requireBothHands");

        foreach (var instr in instructions)
        {
            if (instr.opcode == OpCodes.Ldfld &&
                instr.operand as FieldInfo == field)
            {
                // Remove the Weapon instance that ldfld would consume.
                yield return new CodeInstruction(OpCodes.Pop);

                // Pretend requireBothHands is always false.
                yield return new CodeInstruction(OpCodes.Ldc_I4_0);
            }
            else
            {
                yield return instr;
            }
        }
    }
}


[HarmonyPatch(typeof(PlayerPickup), "HandleInteraction")]
class HandleDualWieldPickupPatch
{
    static bool Prefix(PlayerPickup __instance)
    {
        var type = typeof(PlayerPickup);

        var currentInteractable =
        AccessTools.Field(type, "currentInteractable")
        ?.GetValue(__instance) as Component;

        // No pickup target - let vanilla handle dropping/switching/etc.
        if (currentInteractable == null)
            return true;

        if (DualAnythingUtil.IsCochin(currentInteractable.gameObject))
            return true;

        var incomingWeapon =
        currentInteractable.GetComponent<Weapon>();

        // Non-weapons use vanilla interaction.
        if (incomingWeapon == null)
            return true;

        var hasRight =
        (bool)AccessTools.Field(type, "hasObjectInHand")
        .GetValue(__instance);

        // If the right hand is empty, this is the first weapon.
        // Let vanilla put it in the right hand.
        if (!hasRight)
            return true;

        var leftHandPickup =
        AccessTools.Method(type, "LeftHandPickup");

        if (leftHandPickup == null)
        {
            Debug.LogError(
                "[DualAnything] Could not find PlayerPickup.LeftHandPickup(). " +
                "Falling back to vanilla interaction."
            );

            return true;
        }

        leftHandPickup.Invoke(__instance, null);

        return false;
    }
}


// ============================================================
// When DualAnything puts a weapon into the left hand, some
// weapons don't have a valid left-hand camera position.
//
// Use the existing "couperet" position as the fallback.
// ============================================================

[HarmonyPatch(typeof(Weapon), "Awake")]
class FixWeaponIndexes
{
    static void Postfix(Weapon __instance)
    {
        var item = __instance.GetComponent<ItemBehaviour>();

        if (item == null)
            return;

        if (item.camChildIndex > 10)
        {
            item.camChildIndex = 9;
            item.camChildIndexLeftHand = 9;
        }
    }
}


// ============================================================
// Some melee weapons don't have a left-hand animation name.
// Reuse their normal attack animation in that case.
// ============================================================

[HarmonyPatch(typeof(MeleeWeapon), "Fire")]
class FixLeftHandAnimName
{
    static void Prefix(MeleeWeapon __instance)
    {
        if (!__instance.inLeftHand)
            return;

        var leftAnimField =
        AccessTools.Field(typeof(MeleeWeapon), "baseAttackLeftAnim");

        var baseAnimField =
        AccessTools.Field(typeof(MeleeWeapon), "baseAttackAnim");

        var leftAnim =
        leftAnimField?.GetValue(__instance) as string;

        if (string.IsNullOrEmpty(leftAnim) || leftAnim == "None")
        {
            leftAnimField?.SetValue(
                __instance,
                baseAnimField?.GetValue(__instance)
            );
        }
    }
}


// ============================================================
// When a weapon occupies the left hand, RMB normally enters
// the game's aiming state.
//
// DualAnything instead uses the configured heavy-attack
// modifier while the left hand is occupied.
// ============================================================

[HarmonyPatch(typeof(FirstPersonController), "Update")]
class OverrideAimingLogic
{
    static void Postfix(FirstPersonController __instance)
    {
        var type = typeof(FirstPersonController);

        var pickup =
        AccessTools.Field(type, "playerPickupScript")
        ?.GetValue(__instance);

        if (pickup == null)
            return;

        var pickupType = pickup.GetType();

        var hasLeftField =
        AccessTools.Field(pickupType, "hasObjectInLeftHand");

        if (hasLeftField == null)
            return;

        bool hasLeft =
        (bool)hasLeftField.GetValue(pickup);

        if (!hasLeft)
            return;

        var isAimingField =
        AccessTools.Field(type, "isAiming");

        if (isAimingField == null)
            return;

        bool modifier =
        Input.GetKey(DualAnythingPlugin.HeavyAttackKey.Value);

        isAimingField.SetValue(__instance, modifier);
    }
}
