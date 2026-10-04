using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace takeaseat;

/// <summary>
/// Makes a block something to sit on: right-click to sit, sneak to stand. Attach it to any
/// block by JSON patch; <see cref="SeatLayout"/> works out the rest from the block's shape.
/// </summary>
public class BlockBehaviorSeat : BlockBehavior
{
    private JsonObject? properties;
    private SeatLayout? layout;

    public BlockBehaviorSeat(Block block) : base(block) { }

    public Block Block => block;

    /// <summary>Computed on first use, when the variant's collision boxes are certain to be resolved.</summary>
    public SeatLayout Layout => layout ??= SeatLayout.FromBlock(block, properties);

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        this.properties = properties;
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
    {
        // Sneaking keeps the vanilla right-click, so blocks can still be placed against a chair.
        if (byPlayer.Entity.Controls.ShiftKey) return false;
        if (byPlayer.Entity.MountedOn != null) return false;

        if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
        {
            handling = EnumHandling.PreventDefault;
            return false;
        }

        var blockSeats = world.Api.ModLoader.GetModSystem<TakeASeatModSystem>().GetOrCreate(world, blockSel.Position);
        var seat = blockSeats?.NearestFreeSeat(byPlayer.Entity, blockSel.HitPosition);
        if (seat == null) return false;

        if (Layout.Yaw == null) seat.Yaw = SeatLayout.SnapYaw(byPlayer.Entity.Pos.Yaw);

        handling = EnumHandling.PreventSubsequent;
        return byPlayer.Entity.TryMount(seat);
    }

    public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos, ref EnumHandling handling)
    {
        world.Api.ModLoader.GetModSystem<TakeASeatModSystem>().Get(pos)?.UnmountAll();
        base.OnBlockRemoved(world, pos, ref handling);
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer, ref EnumHandling handling)
    {
        var sit = new WorldInteraction
        {
            ActionLangCode = "takeaseat:blockhelp-sit",
            MouseButton = EnumMouseButton.Right,
        };
        return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer, ref handling).Append(sit).ToArray();
    }
}
