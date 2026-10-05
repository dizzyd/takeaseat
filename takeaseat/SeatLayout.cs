using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace takeaseat;

/// <summary>
/// Where the seats of one block variant are, worked out from its collision boxes so that a
/// furniture mod's chairs need no per-block numbers. The box that starts at the floor is
/// the seat and its top is the sitting height; a box standing on top of it is the
/// backrest, and the sitter faces away from it. A seat with no backrest leaves the facing
/// to the sitter, unless its properties say "facing": "variant". A seat with a back is lounged
/// in rather than perched on. A seat about two blocks long - a bench or couch - gets two
/// places. Every guess can be overridden from the behavior's properties.
/// </summary>
public sealed class SeatLayout
{
    /// <summary>Seat points relative to the block origin, centred on each place; Y is the sitting surface.</summary>
    public Vec3d[] Offsets { get; }

    /// <summary>Half the width (X) and depth (Z) of each place, for finding its edge in any facing.</summary>
    public Vec2d[] HalfSizes { get; }

    /// <summary>Yaw the sitter faces, or null when the seat has no back and the sitter chooses.</summary>
    public float? Yaw { get; }

    public float EyeHeight { get; }

    /// <summary>
    /// How far past the seat's front edge the sitter's position goes. The sitting pose is
    /// vanilla's ledge sit: its knees are at the entity origin and the shins hang straight
    /// down from there, with the hips about 0.36 behind. So the origin belongs just past the
    /// front edge, by the shins' half-thickness, or the legs hang through the seat.
    /// </summary>
    public float LegRoom { get; }

    /// <summary>
    /// How far behind the sitter's position their back reaches when lounging, or null for a
    /// seat without a backrest to lounge against. The lounge pose (tools/make-lounge.py) puts
    /// the upper back 0.67 behind the entity origin and the backside 0.575; the default lets
    /// the shoulders meet the backrest's face. It only ever moves the sitter forward of where
    /// the legs put them, so it matters on shallow seats and not deep ones.
    /// </summary>
    public float? LoungeDepth { get; }

    /// <summary>
    /// For a backless seat that is clearly longer one way than the other - a bench, a piano
    /// stool - the axis it runs along: true for X, false for Z. Null for a square stool, or
    /// for a seat whose facing is fixed anyway.
    /// </summary>
    public bool? LongAxisX { get; }

    private SeatLayout(Vec3d[] offsets, Vec2d[] halfSizes, float? yaw, float eyeHeight, float legRoom, float? loungeDepth, bool? longAxisX = null)
    {
        LongAxisX = longAxisX;
        Offsets = offsets;
        HalfSizes = halfSizes;
        Yaw = yaw;
        EyeHeight = eyeHeight;
        LegRoom = legRoom;
        LoungeDepth = loungeDepth;
    }

    /// <summary>Distance from a place's centre to its front edge, facing along <paramref name="yaw"/>.</summary>
    public static double ToFrontEdge(Vec2d halfSize, float yaw) =>
        Math.Abs(Math.Sin(yaw)) * halfSize.X + Math.Abs(Math.Cos(yaw)) * halfSize.Y;

    public static SeatLayout FromBlock(Block block, JsonObject? properties)
    {
        float eyeHeight = properties?["eyeHeight"].AsFloat(1f) ?? 1f;
        // 2/16 of the shin's 4/16 thickness, plus a pixel so it clears the edge.
        float legRoom = properties?["legRoom"].AsFloat(0.13f) ?? 0.13f;

        var boxes = block.CollisionBoxes is { Length: > 0 } ? block.CollisionBoxes : block.SelectionBoxes;
        if (boxes == null || boxes.Length == 0)
        {
            float height = properties?["seatHeight"].AsFloat(0.5f) ?? 0.5f;
            return new SeatLayout(new[] { new Vec3d(0.5, height, 0.5) }, new[] { new Vec2d(0.25, 0.25) }, null, eyeHeight, legRoom, null);
        }

        var seat = boxes.Where(b => b.Y1 <= 0.05f).OrderByDescending(b => b.XSize * b.ZSize).FirstOrDefault() ?? boxes[0];
        var back = boxes.FirstOrDefault(b => b != seat && b.Y1 >= seat.Y2 - 0.05f);

        // The free part of the seat, in front of the backrest.
        double x1 = seat.X1, x2 = seat.X2, z1 = seat.Z1, z2 = seat.Z2;
        int faceX = 0, faceZ = 0;
        if (back != null)
        {
            double dx = back.MidX - seat.MidX;
            double dz = back.MidZ - seat.MidZ;
            if (Math.Abs(dz) >= Math.Abs(dx))
            {
                faceZ = dz < 0 ? 1 : -1;
                if (faceZ > 0) z1 = Math.Max(z1, back.Z2); else z2 = Math.Min(z2, back.Z1);
            }
            else
            {
                faceX = dx < 0 ? 1 : -1;
                if (faceX > 0) x1 = Math.Max(x1, back.X2); else x2 = Math.Min(x2, back.X1);
            }
        }

        // Some chairs draw a backrest their collision box leaves out. Those opt in to facing
        // away from their placed side, the way the furniture's chairs with boxed backs do.
        if (back == null && properties?["facing"].AsString() == "variant" && block.Variant["side"] is string side)
        {
            var away = BlockFacing.FromCode(side).Opposite;
            faceX = away.Normali.X;
            faceZ = away.Normali.Z;
        }
        bool faced = faceX != 0 || faceZ != 0;

        // Seats sit side by side across the facing direction, or along the longer side when there is no back.
        bool alongX = faced ? faceZ != 0 : (x2 - x1) >= (z2 - z1);
        double length = alongX ? x2 - x1 : z2 - z1;
        int count = Math.Max(1, properties?["seats"].AsInt((int)Math.Round(length)) ?? (int)Math.Round(length));

        double y = properties?["seatHeight"].AsFloat(seat.Y2) ?? seat.Y2;
        var offsets = new Vec3d[count];
        var halfSize = alongX
            ? new Vec2d(length / count / 2, (z2 - z1) / 2)
            : new Vec2d((x2 - x1) / 2, length / count / 2);
        var halfSizes = Enumerable.Repeat(halfSize, count).ToArray();
        for (int i = 0; i < count; i++)
        {
            double along = (alongX ? x1 : z1) + (i + 0.5) * length / count;
            offsets[i] = alongX
                ? new Vec3d(along, y, (z1 + z2) / 2)
                : new Vec3d((x1 + x2) / 2, y, along);
        }

        // Anything with a back to lean on - boxed, or drawn and declared by "facing" - lounges.
        float? loungeDepth = faced && (properties?["lounge"].AsBool(true) ?? true)
            ? properties?["loungeDepth"].AsFloat(0.64f) ?? 0.64f
            : null;

        // Nobody sits along a bench: a long backless seat faces across itself.
        bool? longAxisX = null;
        if (!faced)
        {
            double sizeX = x2 - x1, sizeZ = z2 - z1;
            if (sizeX > sizeZ * 1.25) longAxisX = true;
            else if (sizeZ > sizeX * 1.25) longAxisX = false;
        }

        float? yaw = faced ? YawFacing(faceX, faceZ) : null;
        return new SeatLayout(offsets, halfSizes, yaw, eyeHeight, legRoom, loungeDepth, longAxisX);
    }

    /// <summary>
    /// The player yaw that faces along (dx, dz). A player's pitch rests near pi, which flips
    /// EntityPos.GetViewVector, so this is not the inverse of that for a plain yaw.
    /// </summary>
    public static float YawFacing(double dx, double dz) => (float)Math.Atan2(dx, dz);

    /// <summary>
    /// The way a sitter faces, given the way they were looking as they sat: the seat's own
    /// facing if it has one; across a long seat, whichever side is nearer the look; otherwise
    /// the nearest of the four directions.
    /// </summary>
    public float FacingFor(float lookYaw)
    {
        if (Yaw is float fixedYaw) return fixedYaw;
        return LongAxisX switch
        {
            true => YawFacing(0, Math.Cos(lookYaw) >= 0 ? 1 : -1),
            false => YawFacing(Math.Sin(lookYaw) >= 0 ? 1 : -1, 0),
            null => SnapYaw(lookYaw),
        };
    }

    /// <summary>Snaps a free yaw to the nearest of the four horizontal directions.</summary>
    public static float SnapYaw(float yaw) => (float)(Math.Round(yaw / GameMath.PIHALF) * GameMath.PIHALF);
}
