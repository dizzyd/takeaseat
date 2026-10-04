using System;
using System.Linq;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using VsTestkit.Testing;
using static VsTestkit.Testing.Vs;

namespace TakeASeat.Tests
{
    /// <summary>
    /// Pictures, not assertions: sit in a selection of the furniture and photograph each from
    /// the side and from the front, for judging the pose by eye. Run with --filter Gallery.
    /// Shots land in the game install directory as takeaseat-gallery-*.png.
    /// </summary>
    public class Gallery
    {
        static BlockPos Seat => P(8, 1, 8);

        [VsTest(TimeoutMs = 60000), RequiresClient] public Task RoughChair() => Shoot("roughchair");
        [VsTest(TimeoutMs = 60000), RequiresClient] public Task RoughDiningChair() => Shoot("roughchairdining");
        [VsTest(TimeoutMs = 60000), RequiresClient] public Task RoughArmchair() => Shoot("rougharmchair");
        [VsTest(TimeoutMs = 60000), RequiresClient] public Task RoughStool() => Shoot("roughstool");
        [VsTest(TimeoutMs = 60000), RequiresClient] public Task RoughBench() => Shoot("roughbench");
        [VsTest(TimeoutMs = 60000), RequiresClient] public Task RoughCouch() => Shoot("roughcouch");
        [VsTest(TimeoutMs = 60000), RequiresClient] public Task RusticChair() => Shoot("rusticchair");
        [VsTest(TimeoutMs = 60000), RequiresClient] public Task RusticArmchair() => Shoot("rusticarmchairpadded");
        [VsTest(TimeoutMs = 60000), RequiresClient] public Task RusticToilet() => Shoot("rustictoilet");
        [VsTest(TimeoutMs = 60000), RequiresClient] public Task PianoStool() => Shoot("pianostool");

        static async Task Shoot(string name)
        {
            var me = Player.Me.Entity;
            if (me.MountedOn != null) me.TryUnmount();

            await World.SetCalendarTo(500 * 24 + 12);
            World.SetBlock($"w4rd0sfurniture:{name}-north", Seat);
            // From the north, so a backless seat - which faces the way the sitter looks - faces south too.
            await Player.Teleport(P(8, 1, 6));
            await Ticks(5);

            await Interact.UseBlock(Seat);
            await Until(() => me.MountedOn is takeaseat.Seat, 60, "seated");
            // Let the right-click that sat us down finish its arm swing before photographing.
            await Ticks(60);

            await OnClient();
            var cam = Capi.World.Player.CameraMode;
            if (cam != EnumCameraMode.ThirdPerson) await Input.Press(GlKeys.F5);
            // Closer than the default 3 blocks, so the legs are big enough to judge.
            ((Vintagestory.Client.NoObf.ClientMain)Capi.World).MainCamera.Tppcameradistance = 2f;
            await OnServer();

            // The sitter faces south (+z). Third person puts the camera behind the look
            // direction, so looking east shows the left side, looking north shows the front.
            var c = new Vec3d(Seat.X + 0.5, Seat.Y + 0.6, Seat.Z + 0.5);
            await Snap(name, "side", c.AddCopy(5, -0.3, 0));
            await Snap(name, "front", c.AddCopy(2.5, -1.2, -4.5));

            me.TryUnmount();
            await Ticks(2);
        }

        static async Task Snap(string name, string view, Vec3d lookAt)
        {
            await Interact.LookAt(lookAt);
            await Frames.Wait(40);
            Log(await Shot.Take($"takeaseat-gallery-{name}-{view}.png"));
        }
    }
}
