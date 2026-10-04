using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace takeaseat;

/// <summary>
/// Owns the live seats for one side. Seat blocks carry no block entity, so a seat exists
/// here only while someone is sitting in it (or is about to) and is dropped on unmount.
/// In singleplayer both sides load this assembly, so the registry must stay per-instance:
/// a static one would hand the server's seat to the client.
/// </summary>
public class TakeASeatModSystem : ModSystem
{
    public const string MountableClass = "takeaseat";

    private readonly Dictionary<BlockPos, BlockSeats> seats = new();

    public const string HarmonyId = "com.dizzyd.takeaseat";

    private Harmony? harmony;

    /// <summary>The daily sitting buff; server side only, so null on the client.</summary>
    public WellRested? WellRested { get; private set; }

    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        api.RegisterBlockBehaviorClass("takeaseat.Seat", typeof(BlockBehaviorSeat));
        api.RegisterMountable(MountableClass, GetMountable);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);
        WellRested = new WellRested(api);
    }

    /// <summary>
    /// The edge-sit animation logic the patch guards against runs on the client only. Patching
    /// here rather than in Start also keeps singleplayer, where both sides share this assembly,
    /// from registering the prefix twice.
    /// </summary>
    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);
        harmony = new Harmony(HarmonyId);
        harmony.PatchAll(typeof(TakeASeatModSystem).Assembly);
    }

    public override void Dispose()
    {
        harmony?.UnpatchAll(HarmonyId);
        base.Dispose();
    }

    public BlockSeats? GetOrCreate(IWorldAccessor world, BlockPos pos)
    {
        if (seats.TryGetValue(pos, out var existing) && existing.IsStillValid(world)) return existing;

        var behavior = world.BlockAccessor.GetBlock(pos).GetBehavior<BlockBehaviorSeat>();
        if (behavior == null)
        {
            seats.Remove(pos);
            return null;
        }

        var created = new BlockSeats(this, pos.Copy(), behavior.Block, behavior.Layout);
        seats[created.Pos] = created;
        return created;
    }

    public BlockSeats? Get(BlockPos pos) => seats.GetValueOrDefault(pos);

    internal void Release(BlockSeats blockSeats)
    {
        if (seats.TryGetValue(blockSeats.Pos, out var current) && current == blockSeats) seats.Remove(blockSeats.Pos);
    }

    /// <summary>
    /// Rebuilds a seat from what <see cref="Seat.MountableToTreeAttributes"/> wrote, when a
    /// seated player is loaded (rejoin, chunk load, or the client learning of a mount).
    /// </summary>
    private static IMountableSeat? GetMountable(IWorldAccessor world, TreeAttribute tree)
    {
        var system = world.Api.ModLoader.GetModSystem<TakeASeatModSystem>();
        var pos = new BlockPos(tree.GetInt("posx"), tree.GetInt("posy"), tree.GetInt("posz"));
        var blockSeats = system.GetOrCreate(world, pos);
        if (blockSeats == null) return null;

        int index = tree.GetInt("seat");
        if (index < 0 || index >= blockSeats.Seats.Length) return null;

        var seat = blockSeats.Seats[index];
        if (tree.HasAttribute("yaw")) seat.Yaw = tree.GetFloat("yaw");
        return seat;
    }
}
