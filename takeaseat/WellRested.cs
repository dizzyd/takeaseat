using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace takeaseat;

/// <summary>
/// Taking a seat once a day leaves a player well rested: they get hungry more slowly and heal
/// better for a few hours afterwards. Sitting for hours on end undoes it - the buff is lost,
/// nothing worse. All times are in-game hours, so the buff keeps the world's pace.
///
/// Server side only. What a player has earned lives in their entity's attributes, so it
/// survives logging out; the stat modifiers are re-applied from it on the next tick.
/// </summary>
public class WellRested
{
    /// <summary>How long a player must stay seated to earn the buff.</summary>
    public const double SitToEarnHours = 10 / 60.0;

    /// <summary>Earning again is possible this long after the last time - a little under a
    /// day, so a daily sit needn't land at exactly the same hour.</summary>
    public const double EarnEveryHours = 20;

    public const double LastsHours = 4;

    /// <summary>Still seated this long after sitting down, and the rested feeling is gone.</summary>
    public const double OverstayHours = 2;

    public const float HungerRate = -0.10f;
    public const float Healing = 0.10f;

    /// <summary>The code the stat modifiers are filed under, so they can be removed again.</summary>
    public const string StatCode = "takeaseat-wellrested";

    private const string EarnedAtKey = "takeaseat:restedAt";
    private const string UntilKey = "takeaseat:restedUntil";

    private readonly ICoreServerAPI sapi;
    private readonly Dictionary<long, double> seatedSince = new();

    public WellRested(ICoreServerAPI sapi)
    {
        this.sapi = sapi;
        sapi.Event.RegisterGameTickListener(_ =>
        {
            foreach (var player in sapi.World.AllOnlinePlayers)
            {
                if (player is IServerPlayer sp && sp.Entity != null) Update(sp);
            }
        }, 1000);
    }

    private double Now => sapi.World.Calendar.TotalHours;

    public void OnSat(EntityAgent entity) => seatedSince[entity.EntityId] = Now;

    public void OnStood(EntityAgent entity) => seatedSince.Remove(entity.EntityId);

    public bool IsRested(EntityPlayer entity) => entity.WatchedAttributes.GetDouble(UntilKey) > Now;

    /// <summary>Awards, revokes and applies the buff for one player. Safe to call at any time.</summary>
    public void Update(IServerPlayer player)
    {
        var entity = player.Entity;
        var attrs = entity.WatchedAttributes;
        double now = Now;

        if (entity.MountedOn is Seat && seatedSince.TryGetValue(entity.EntityId, out double since))
        {
            double seated = now - since;
            if (seated >= OverstayHours)
            {
                if (IsRested(entity))
                {
                    attrs.SetDouble(UntilKey, now);
                    Tell(player, "takeaseat:wellrested-lost");
                }
            }
            else if (seated >= SitToEarnHours && now - attrs.GetDouble(EarnedAtKey, double.MinValue) >= EarnEveryHours)
            {
                attrs.SetDouble(EarnedAtKey, now);
                attrs.SetDouble(UntilKey, now + LastsHours);
                Tell(player, "takeaseat:wellrested-gained");
            }
        }

        Apply(entity, IsRested(entity));
    }

    /// <summary>Sets or clears the modifiers, touching the stats only when they change: every
    /// change is written to the player's synced attributes.</summary>
    private static void Apply(EntityPlayer entity, bool rested)
    {
        bool applied = entity.Stats["hungerrate"]?.ValuesByKey.ContainsKey(StatCode) == true;
        if (rested == applied) return;

        if (rested)
        {
            entity.Stats.Set("hungerrate", StatCode, HungerRate);
            entity.Stats.Set("healingeffectivness", StatCode, Healing);
        }
        else
        {
            entity.Stats.Remove("hungerrate", StatCode);
            entity.Stats.Remove("healingeffectivness", StatCode);
        }
    }

    private static void Tell(IServerPlayer player, string langKey) =>
        player.SendMessage(GlobalConstants.GeneralChatGroup, Lang.GetL(player.LanguageCode, langKey), EnumChatType.Notification);
}
