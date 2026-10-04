using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace takeaseat;

/// <summary>The seats of one placed block - one for a chair, two for a bench or couch.</summary>
public sealed class BlockSeats : IMountable
{
    private readonly TakeASeatModSystem system;

    internal TakeASeatModSystem System => system;
    public BlockPos Pos { get; }
    public Block Block { get; }
    public Seat[] Seats { get; }

    public BlockSeats(TakeASeatModSystem system, BlockPos pos, Block block, SeatLayout layout)
    {
        this.system = system;
        Pos = pos;
        Block = block;
        Seats = layout.Offsets.Select((offset, i) => new Seat(this, i, offset, layout.HalfSizes[i], layout.Yaw ?? 0, layout.EyeHeight, layout.LegRoom, layout.LoungeDepth)).ToArray();
    }

    IMountableSeat[] IMountable.Seats => Seats;
    public EntityPos Position => Seats[0].SeatPosition;
    public double StepPitch => 0;
    public Entity? Controller => null;
    public Entity? OnEntity => null;
    public EntityControls? ControllingControls => null;

    public bool AnyMounted() => Seats.Any(s => s.Passenger != null);

    /// <summary>The block was replaced - rotated, broken, swapped - since these seats were made.</summary>
    public bool IsStillValid(IWorldAccessor world) => world.BlockAccessor.GetBlock(Pos).Id == Block.Id;

    public Seat? NearestFreeSeat(EntityAgent forEntity, Vec3d? hitPosition)
    {
        var hit = hitPosition ?? new Vec3d(0.5, 0.5, 0.5);
        return Seats
            .Where(s => s.CanMount(forEntity))
            .OrderBy(s => s.Offset.HorizontalSquareDistanceTo(hit.X, hit.Z))
            .FirstOrDefault();
    }

    public void UnmountAll()
    {
        foreach (var seat in Seats)
        {
            seat.DoTeleportOnUnmount = false;
            seat.Passenger?.TryUnmount();
        }
        system.Release(this);
    }

    internal void SeatEmptied()
    {
        if (!AnyMounted()) system.Release(this);
    }
}

public sealed class Seat : IMountableSeat
{
    private static readonly AnimationMetaData SitAnimation = new AnimationMetaData
    {
        Code = "takeaseat-sit",
        Animation = "sitidle",
    }.Init();

    /// <summary>Sitting back against a backrest. The animation is patched into the player shape.</summary>
    private static readonly AnimationMetaData LoungeAnimation = new AnimationMetaData
    {
        Code = "takeaseat-lounge",
        Animation = "takeaseat-lounge",
    }.Init();

    private readonly BlockSeats owner;
    private readonly int index;
    private readonly EntityControls controls = new();
    private readonly EntityPos seatPos = new();
    private readonly Vec2d halfSize;
    private readonly float legRoom;
    private readonly float? loungeDepth;
    private long passengerEntityId;

    public Seat(BlockSeats owner, int index, Vec3d offset, Vec2d halfSize, float yaw, float eyeHeight, float legRoom, float? loungeDepth)
    {
        this.owner = owner;
        this.index = index;
        this.halfSize = halfSize;
        this.legRoom = legRoom;
        this.loungeDepth = loungeDepth;
        Offset = offset;
        Yaw = yaw;
        LocalEyePos = new Vec3f(0, eyeHeight, 0);
        controls.OnAction = OnControls;
    }

    public Vec3d Offset { get; }
    public float Yaw { get; set; }
    public EntityAgent? Passenger { get; private set; }

    public EntityPos SeatPosition
    {
        get
        {
            // Worked out per call rather than once: a stool's facing is chosen as it is sat on.
            // The legs must clear the front edge; a lounger's back must also reach the backrest,
            // which is as far behind the centre as the front edge is ahead of it.
            double edge = SeatLayout.ToFrontEdge(halfSize, Yaw);
            double forward = edge + legRoom;
            if (loungeDepth is float depth) forward = Math.Max(forward, depth - edge);
            seatPos.SetPos(owner.Pos);
            seatPos.Yaw = Yaw;
            return seatPos.Add(Offset.X + Math.Sin(Yaw) * forward, Offset.Y, Offset.Z + Math.Cos(Yaw) * forward);
        }
    }

    Entity? IMountableSeat.Passenger => Passenger;
    public IMountable MountSupplier => owner;
    public Entity? Entity => null;
    public bool CanControl => false;
    public EnumMountAngleMode AngleMode => EnumMountAngleMode.FixateYaw;
    public AnimationMetaData SuggestedAnimation => loungeDepth != null ? LoungeAnimation : SitAnimation;
    public bool SkipIdleAnimation => true;
    public float FpHandPitchFollow => 1;
    public Vec3f LocalEyePos { get; }
    public Matrixf? RenderTransform => null;
    public EntityControls Controls => controls;
    public bool DoTeleportOnUnmount { get; set; } = true;

    public string SeatId { get => "takeaseat-" + index; set { } }
    public SeatConfig? Config { get => null; set { } }
    public long PassengerEntityIdForInit { get => passengerEntityId; set => passengerEntityId = value; }

    /// <summary>A passenger whose entity has gone - a player who logged out seated - no longer holds the seat.</summary>
    public bool CanMount(EntityAgent entityAgent) => Passenger == null || Passenger == entityAgent || Passenger.State == EnumEntityState.Despawned;

    public bool CanUnmount(EntityAgent entityAgent) => true;

    public void DidMount(EntityAgent entityAgent)
    {
        if (Passenger == entityAgent) return;
        if (!CanMount(entityAgent))
        {
            // Someone else took the seat while this entity was away.
            entityAgent.TryUnmount();
            return;
        }

        Passenger = entityAgent;
        passengerEntityId = entityAgent.EntityId;
        owner.System.WellRested?.OnSat(entityAgent);
    }

    public void DidUnmount(EntityAgent entityAgent)
    {
        if (Passenger != entityAgent) return;
        Passenger = null;
        passengerEntityId = 0;
        owner.System.WellRested?.OnStood(entityAgent);

        if (DoTeleportOnUnmount) StandUp(entityAgent);
        DoTeleportOnUnmount = true;
        owner.SeatEmptied();
    }

    public void MountableToTreeAttributes(TreeAttribute tree)
    {
        tree.SetString("className", TakeASeatModSystem.MountableClass);
        tree.SetInt("posx", owner.Pos.X);
        tree.SetInt("posy", owner.Pos.InternalY);
        tree.SetInt("posz", owner.Pos.Z);
        tree.SetInt("seat", index);
        tree.SetFloat("yaw", Yaw);
    }

    private void OnControls(EnumEntityAction action, bool on, ref EnumHandling handled)
    {
        if (action == EnumEntityAction.Sneak && on)
        {
            Passenger?.TryUnmount();
            controls.StopAllMovement();
            handled = EnumHandling.PassThrough;
        }
    }

    /// <summary>
    /// Puts the sitter back on their feet clear of the furniture: in front of the seat if
    /// there is room, else beside it, else on top of it.
    /// </summary>
    private void StandUp(EntityAgent entity)
    {
        var world = entity.World;
        var seat = SeatPosition;
        double floor = owner.Pos.Y + 0.001;

        double frontX = Math.Sin(Yaw), frontZ = Math.Cos(Yaw);
        var candidates = new[] { new Vec3d(seat.X + frontX * 0.8, floor, seat.Z + frontZ * 0.8) }
            .Concat(BlockFacing.HORIZONTALS.Select(f => owner.Pos.ToVec3d().Add(0.5, 0.001, 0.5).Add(f.Normalf.X, 0, f.Normalf.Z)))
            .Append(owner.Pos.ToVec3d().Add(0.5, 1.001, 0.5));

        foreach (var pos in candidates)
        {
            if (!world.CollisionTester.IsColliding(world.BlockAccessor, entity.SelectionBox, pos, false))
            {
                entity.TeleportTo(pos);
                return;
            }
        }
    }
}
