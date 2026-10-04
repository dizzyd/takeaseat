using HarmonyLib;
using Vintagestory.API.Common;

namespace takeaseat;

/// <summary>
/// Vanilla edge-sitting (sit-on-floor at a ledge) plays the same "sitidle" animation a
/// chair needs, and owns it outright: every animation tick that the floor-sit control is
/// not held, EntityPlayer.onAnimControls sees "sitidle" running, takes it for a stale
/// edge-sit and stops it. Seated in a chair, that would stand the player up again the
/// moment they sat down. While on one of our seats, leave the floor-sit trigger alone.
/// </summary>
[HarmonyPatch(typeof(EntityPlayer), "onAnimControls")]
public static class EdgeSitPatch
{
    public static bool Prefix(EntityPlayer __instance, AnimationMetaData anim, ref bool __result)
    {
        if (__instance.MountedOn is not Seat || anim.Animation != "sitflooridle") return true;

        __result = true;
        return false;
    }
}
