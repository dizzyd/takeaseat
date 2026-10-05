using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using VsTestkit.Testing;
using static VsTestkit.Testing.Vs;

namespace TakeASeat.Tests
{
    /// <summary>
    /// A picture, not a test: a player resting on a bench in front of a little cottage. Meant
    /// for a world with real terrain around the plot:
    ///
    ///     VSTK_PLAYSTYLE=vstestkit-standard bash scripts/run.sh ... --client \
    ///         --slot takeaseat-world --filter Scene
    ///
    /// Shots land in the game install directory as takeaseat-scene-*.png.
    /// </summary>
    public class Scene
    {
        // The cottage: 7 wide (x 4..10), 5 deep (z 1..5), its front wall facing south at z 5.
        const int X1 = 4, X2 = 10, Z1 = 1, Z2 = 5, DoorX = 7;

        const string Planks = "game:planks-oak-ud";
        const string Log = "game:log-placed-oak-ud";
        const string Floor = "game:cobblestone-granite";
        const string PaneAlongX = "game:glasspane-leaded-oak-ew";
        const string PaneAlongZ = "game:glasspane-leaded-oak-ns";
        const string RoofSouth = "game:clayshinglestairs-red-up-north-free";
        const string RoofNorth = "game:clayshinglestairs-red-up-south-free";

        static BlockPos Bench => P(DoorX - 1, 1, 8);

        [VsTest(TimeoutMs = 120000), RequiresClient]
        public async Task PlayerOnABenchByTheCottage()
        {
            await World.SetCalendarTo(500 * 24 + 10);

            BuildCottage();
            World.SetBlock("w4rd0sfurniture:roughbench-north", Bench);
            await Ticks(5);

            // Walk up from the cottage side so the sitter faces south, away from it.
            var me = Player.Me.Entity;
            await Player.Teleport(P(DoorX, 1, 7));
            await Ticks(5);
            await Interact.UseBlock(Bench);
            await Until(() => me.MountedOn is takeaseat.Seat, 60, "seated");
            await Ticks(60);

            await OnClient();
            if (Capi.World.Player.CameraMode != EnumCameraMode.ThirdPerson) await Input.Press(GlKeys.F5);
            ((Vintagestory.Client.NoObf.ClientMain)Capi.World).MainCamera.Tppcameradistance = 6f;
            await Input.Hotkey("togglehud");
            await OnServer();

            // Third person sits the camera behind where the player looks: looking north puts
            // it south of them, facing the bench with the cottage beyond.
            await Snap("front", At(DoorX + 0.5, 2.6, 3));
            await Snap("angle", At(DoorX - 3.5, 2.6, 2));

            await OnClient();
            await Input.Hotkey("togglehud");
            await OnServer();
            me.TryUnmount();
        }

        static void BuildCottage()
        {
            for (int x = X1; x <= X2; x++)
            for (int z = Z1; z <= Z2; z++)
            {
                World.SetBlock(Floor, P(x, 0, z));
                bool edgeX = x == X1 || x == X2, edgeZ = z == Z1 || z == Z2;
                for (int y = 1; y <= 3; y++)
                {
                    if (edgeX && edgeZ) World.SetBlock(Log, P(x, y, z));
                    else if (edgeX || edgeZ) World.SetBlock(Planks, P(x, y, z));
                }
            }

            // Windows either side of the door, and one in each gable-end wall.
            World.SetBlock(PaneAlongX, P(X1 + 1, 2, Z2));
            World.SetBlock(PaneAlongX, P(X2 - 1, 2, Z2));
            World.SetBlock(PaneAlongZ, P(X1, 2, 3));
            World.SetBlock(PaneAlongZ, P(X2, 2, 3));

            World.SetBlock("game:door-plank-south-down-closed-left", P(DoorX, 1, Z2));
            World.SetBlock("game:door-plank-south-up-closed-left", P(DoorX, 2, Z2));

            // Gable roof, ridge running east-west over z 3, overhanging a block all round.
            for (int x = X1 - 1; x <= X2 + 1; x++)
            {
                for (int step = 0; step < 3; step++)
                {
                    World.SetBlock(RoofNorth, P(x, 4 + step, Z1 - 1 + step));
                    World.SetBlock(RoofSouth, P(x, 4 + step, Z2 + 1 - step));
                }
                World.SetBlock(Planks, P(x, 7, 3));
            }

            // Fill the gable ends under the roof.
            foreach (int x in new[] { X1, X2 })
            {
                for (int z = Z1; z <= Z2; z++) World.SetBlock(Planks, P(x, 4, z));
                for (int z = Z1 + 1; z <= Z2 - 1; z++) World.SetBlock(Planks, P(x, 5, z));
                World.SetBlock(Planks, P(x, 6, 3));
            }
        }

        static Vec3d At(double x, double y, double z)
        {
            var o = P(0, 0, 0);
            return new Vec3d(o.X + x, o.Y + y, o.Z + z);
        }

        static async Task Snap(string view, Vec3d lookAt)
        {
            await Interact.LookAt(lookAt);
            await Frames.Wait(60);
            Log(await Shot.Take($"takeaseat-scene-{view}.png"));
        }
    }
}
