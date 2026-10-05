using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HarmonyLib;
using takeaseat;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using VsTestkit.Testing;
using static VsTestkit.Testing.Vs;

namespace TakeASeat.Tests
{
    /// <summary>
    /// Sitting in W4RD0's Furniture. Needs that mod and its library loaded:
    ///
    ///     run.sh ../takeaseat/tests --mod ../takeaseat/takeaseat \
    ///         --mods ../takeaseat/tests/fixtures/Mods --client
    /// </summary>
    public class Seating
    {
        const string Chair = "w4rd0sfurniture:roughchair-north";
        const string Couch = "w4rd0sfurniture:roughcouch-north";
        const string Stool = "w4rd0sfurniture:roughstool-north";

        // On the ground, not in it - see olla's note on aiming at flush blocks.
        static BlockPos Seat => P(8, 1, 8);

        static readonly Regex SeatCodes = new("chair|bench|couch|stool|toilet");

        static Block Block(string code) => Sapi.World.GetBlock(new AssetLocation(code))
            ?? throw new AssertionException($"{code} is not loaded - are the fixture mods in?");

        static EntityPlayer Me => Player.Me.Entity;

        [BeforeEach]
        public async Task StandUpFirst()
        {
            if (Me.MountedOn != null) Me.TryUnmount();
            await Ticks(2);
        }

        [VsTest]
        public Task EverySeatInTheFurnitureModCanBeSatIn()
        {
            var seats = Sapi.World.Blocks
                .Where(b => b.Code?.Domain == "w4rd0sfurniture" && SeatCodes.IsMatch(b.Code.Path))
                .ToList();

            Assert.Greater(seats.Count, 0, "furniture seat blocks loaded");
            var missing = seats.Where(b => b.GetBehavior<BlockBehaviorSeat>() == null).Select(b => b.Code.ToShortString()).ToList();
            Assert.Equal(0, missing.Count, "seat blocks without the behavior: " + string.Join(", ", missing));
            Log($"{seats.Count} seat block variants patched");
            return Task.CompletedTask;
        }

        [VsTest]
        public Task AChairHasOneSeatFacingAwayFromItsBack()
        {
            // roughchair-north: seat box z 0.16..0.84, no back box - so check a chair with one.
            var layout = Block("w4rd0sfurniture:rusticchair-north").GetBehavior<BlockBehaviorSeat>().Layout;
            Assert.Equal(1, layout.Offsets.Length, "seats");
            Assert.Close(layout.Offsets[0].Y, 0.5, 0.01, "sitting height is the seat box top");
            Assert.NotNull(layout.Yaw, "a chair with a back fixes the facing");

            // Back is at low z (z 0.19..0.34), so the sitter faces +z: south.
            Assert.Close(Math.Cos(layout.Yaw.Value), 1, 0.01, "faces south, away from the backrest");
            Assert.Greater(layout.Offsets[0].Z, 0.342, "seat point is in front of the backrest");
            return Task.CompletedTask;
        }

        [VsTest]
        public Task ACouchSeatsTwo()
        {
            var layout = Block(Couch).GetBehavior<BlockBehaviorSeat>().Layout;
            Assert.Equal(2, layout.Offsets.Length, "seats");
            Assert.Close(layout.Offsets[0].X, 0.52, 0.05, "first seat");
            Assert.Close(layout.Offsets[1].X, 1.48, 0.05, "second seat");
            return Task.CompletedTask;
        }

        [VsTest]
        public Task AChairWhoseBackIsOnlyInTheModelFacesAwayFromItsSide()
        {
            // roughchair's collision box stops at the seat; its backrest is drawn but not boxed.
            var layout = Block("w4rd0sfurniture:roughchair-east").GetBehavior<BlockBehaviorSeat>().Layout;
            Assert.NotNull(layout.Yaw, "facing fixed by the variant");
            Assert.Close(Math.Sin(layout.Yaw.Value), -1, 0.01, "an -east chair faces west");
            return Task.CompletedTask;
        }

        /// <summary>
        /// The game lerps a keyframe's offsets, rotations and stretches as groups of three, and
        /// a group with any part missing crashes the client the moment the animation starts.
        /// </summary>
        [VsTest]
        public Task TheLoungeAnimationHasCompleteKeyframes()
        {
            var shape = Sapi.Assets.Get(new AssetLocation("game:shapes/entity/humanoid/seraph-faceless.json")).ToObject<Shape>();
            var lounge = shape.Animations.FirstOrDefault(a => a.Code == "takeaseat-lounge");
            Assert.NotNull(lounge, "lounge animation patched into the player shape");

            foreach (var frame in lounge.KeyFrames)
            foreach (var (name, e) in frame.Elements)
            {
                void Group(string what, params double?[] parts)
                {
                    if (parts.Any(p => p != null) && parts.Any(p => p == null))
                        Assert.Fail($"frame {frame.Frame} {name}: {what} given only in part");
                }
                Group("offset", e.OffsetX, e.OffsetY, e.OffsetZ);
                Group("rotation", e.RotationX, e.RotationY, e.RotationZ);
                Group("stretch", e.StretchX, e.StretchY, e.StretchZ);
            }
            return Task.CompletedTask;
        }

        [VsTest]
        public async Task AChairWithABackIsLoungedInAndAStoolIsNot()
        {
            var system = Sapi.ModLoader.GetModSystem<TakeASeatModSystem>();

            // rusticchair-north: backrest face at z 0.342, front edge 0.844. The lounging back
            // reaches 0.64 behind the sitter's position, just ahead of where the legs put them
            // (0.844 + 0.13), so the backside ends up against the backrest, not on the edge.
            World.SetBlock("w4rd0sfurniture:rusticchair-north", Seat);
            await Ticks(2);
            var chair = system.GetOrCreate(Sapi.World, Seat).Seats[0];
            Assert.Equal("takeaseat-lounge", chair.SuggestedAnimation.Code, "chair pose");
            Assert.Close(chair.SeatPosition.Z - Seat.Z, 0.342 + 0.64, 0.01, "back against the backrest");
            Assert.Less(chair.SeatPosition.Z - Seat.Z - 0.575, 0.342 + 0.08, "backside at the back of the seat");

            World.SetBlock(Stool, Seat);
            await Ticks(2);
            var stool = system.GetOrCreate(Sapi.World, Seat).Seats[0];
            Assert.Equal("takeaseat-sit", stool.SuggestedAnimation.Code, "stool pose");
        }

        /// <summary>
        /// Found photographing a bench approached diagonally: snapping the look to the nearest
        /// side sat the player sideways along it.
        /// </summary>
        [VsTest]
        public Task ABenchSeatsYouAcrossItWhicheverWayYouApproach()
        {
            var layout = Block("w4rd0sfurniture:roughbench-north").GetBehavior<BlockBehaviorSeat>().Layout;
            Assert.Equal(true, layout.LongAxisX, "a north-placed bench runs along X");

            float south = SeatLayout.YawFacing(0, 1), north = SeatLayout.YawFacing(0, -1);
            float southWest = SeatLayout.YawFacing(-1, 1.05), west = SeatLayout.YawFacing(-1, 0.05);
            float northEast = SeatLayout.YawFacing(1, -1.05);
            Assert.Close(layout.FacingFor(southWest), south, 0.001, "approached from the north-east, looking south-west");
            Assert.Close(layout.FacingFor(west), south, 0.001, "looking along the bench, a little south");
            Assert.Close(layout.FacingFor(northEast), north, 0.001, "approached from the south-west");

            var stool = Block(Stool).GetBehavior<BlockBehaviorSeat>().Layout;
            Assert.Null(stool.LongAxisX, "a square stool can face any side");
            Assert.Close(stool.FacingFor(SeatLayout.YawFacing(-1, 0.05)), SeatLayout.YawFacing(-1, 0), 0.001, "stool faces west");
            return Task.CompletedTask;
        }

        [VsTest]
        public Task AStoolLetsTheSitterChooseTheirFacing()
        {
            var layout = Block(Stool).GetBehavior<BlockBehaviorSeat>().Layout;
            Assert.Equal(1, layout.Offsets.Length, "seats");
            Assert.Null(layout.Yaw, "no backrest, no fixed facing");
            return Task.CompletedTask;
        }

        [VsTest]
        public async Task ASeatSurvivesBeingSavedAndRestored()
        {
            World.SetBlock(Couch, Seat);
            await Ticks(2);

            var seats = Sapi.ModLoader.GetModSystem<TakeASeatModSystem>().GetOrCreate(Sapi.World, Seat);
            Assert.True(Me.TryMount(seats.Seats[1]), "mounted the second couch seat");

            var tree = new TreeAttribute();
            seats.Seats[1].MountableToTreeAttributes(tree);
            var restored = Sapi.World.ClassRegistry.GetMountable(tree);
            Assert.True(ReferenceEquals(seats.Seats[1], restored), "the tree resolves back to the occupied seat");
        }

        [VsTest]
        public async Task BreakingTheChairPutsTheSitterBackOnTheirFeet()
        {
            World.SetBlock(Chair, Seat);
            await Ticks(2);

            var seats = Sapi.ModLoader.GetModSystem<TakeASeatModSystem>().GetOrCreate(Sapi.World, Seat);
            Assert.True(Me.TryMount(seats.Seats[0]), "mounted");

            World.SetBlock("game:air", Seat);
            await Ticks(2);

            Assert.Null(Me.MountedOn, "still seated in a chair that is gone");
            Assert.Null(Sapi.ModLoader.GetModSystem<TakeASeatModSystem>().Get(Seat), "registry entry left behind");
        }

        [VsTest(TimeoutMs = 60000), RequiresClient]
        public async Task RightClickSitsAndSneakStandsUp()
        {
            World.SetBlock(Chair, Seat);
            await Player.StandNear(Seat, 2);
            await Ticks(5);

            await Interact.UseBlock(Seat);
            await Until(() => Me.MountedOn is takeaseat.Seat, 60, "server sees the player seated");

            await OnClient();
            await Until(() => Capi.World.Player.Entity.MountedOn is takeaseat.Seat, 60, "client sees the player seated");
            await Until(() => Capi.World.Player.Entity.AnimManager.IsAnimationActive("takeaseat-lounge"), 60, "lounging animation plays");

            // The server only hears a player's position from their client, so read it here.
            var pos = Capi.World.Player.Entity.Pos;
            await Until(() => Math.Abs(pos.Y - (Seat.Y + 0.5)) < 0.05, 60, "sat at seat height");
            Assert.Close(pos.X, Seat.X + 0.5, 0.05, "sat centred across the chair");
            // The pose hangs the shins straight down from the origin, so it belongs just past
            // the seat's front edge (z 0.84375 on a north-placed rough chair), not over the seat.
            Assert.Close(pos.Z, Seat.Z + 0.84375 + 0.13, 0.02, "sat with the legs clear of the seat's front edge");
            await OnServer();

            // Vanilla reads the sneak key only while the mouse is grabbed, and a test client's
            // window never has focus to grab it. Immersive mouse mode is the other way through.
            bool immersive = Capi.Settings.Bool["immersiveMouseMode"];
            Capi.Settings.Bool["immersiveMouseMode"] = true;
            try
            {
                await Input.Press(GlKeys.ShiftLeft, 3);
                await Until(() => Me.MountedOn == null, 60, "sneak stands up");
            }
            finally
            {
                Capi.Settings.Bool["immersiveMouseMode"] = immersive;
            }
            await Ticks(5);

            bool stuck = Sapi.World.CollisionTester.IsColliding(Sapi.World.BlockAccessor, Me.SelectionBox, Me.Pos.XYZ, false);
            Assert.False(stuck, "stood up inside the furniture");
            Assert.Close(Me.Pos.Y, Seat.Y, 0.1, "stood up on the floor beside the chair");
        }

        [VsTest(TimeoutMs = 60000), RequiresClient]
        public async Task SneakRightClickDoesNotSit()
        {
            World.SetBlock(Chair, Seat);
            await Player.StandNear(Seat, 2);
            await Ticks(5);

            await Input.KeyDown(GlKeys.ShiftLeft);
            try
            {
                await Interact.UseBlock(Seat);
                await Ticks(10);
            }
            finally
            {
                await Input.KeyUp(GlKeys.ShiftLeft);
            }

            Assert.Null(Me.MountedOn, "sat down while sneaking");
        }

        [VsTest, RequiresClient]
        public Task TheEdgeSitPatchIsRegisteredOnce()
        {
            var method = AccessTools.Method(typeof(EntityPlayer), "onAnimControls");
            Assert.NotNull(method, "EntityPlayer.onAnimControls exists");
            var info = Harmony.GetPatchInfo(method);
            Assert.Equal(1, info?.Prefixes.Count(p => p.owner == TakeASeatModSystem.HarmonyId) ?? 0, "prefixes");
            return Task.CompletedTask;
        }

        /// <summary>Not an assertion - a picture to judge height, facing and pose by eye.</summary>
        [VsTest(TimeoutMs = 90000), RequiresClient]
        public async Task Photograph()
        {
            await World.SetCalendarTo(500 * 24 + 12);
            World.SetBlock("w4rd0sfurniture:rusticchair-north", Seat);
            World.SetBlock("game:planks-oak-ud", P(8, 1, 10));
            await Player.StandNear(Seat, 2);
            await Ticks(5);

            await Interact.UseBlock(Seat);
            await Until(() => Me.MountedOn is takeaseat.Seat, 60, "seated");

            await Input.Press(GlKeys.F5);
            await Frames.Wait(30);
            Log(await Shot.Take("takeaseat-seated-thirdperson.png"));
            await Interact.LookAt(new Vec3d(Seat.X + 4.5, Seat.Y + 0.5, Seat.Z + 0.5));
            await Frames.Wait(30);
            Log(await Shot.Take("takeaseat-seated-side.png"));
            await Input.Press(GlKeys.F5);
            await Frames.Wait(30);
            Log(await Shot.Take("takeaseat-seated-second.png"));
            await Input.Press(GlKeys.F5);
            await Frames.Wait(30);
            Log(await Shot.Take("takeaseat-seated-firstperson.png"));
        }
    }
}
