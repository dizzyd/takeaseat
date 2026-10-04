using System.Threading.Tasks;
using takeaseat;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using VsTestkit.Testing;
using static VsTestkit.Testing.Vs;

namespace TakeASeat.Tests
{
    /// <summary>
    /// The daily buff for sitting down. Time is moved with Hours(), which shifts the calendar
    /// instantly, and the buff is updated by calling WellRested.Update directly rather than
    /// waiting on its once-a-second tick.
    /// </summary>
    public class WellRestedTests
    {
        static BlockPos Chair => P(8, 1, 8);

        static IServerPlayer Me => (IServerPlayer)Player.Me;
        static WellRested Buff => Sapi.ModLoader.GetModSystem<TakeASeatModSystem>().WellRested;

        static float HungerRate => Me.Entity.Stats.GetBlended("hungerrate");
        static float Healing => Me.Entity.Stats.GetBlended("healingeffectivness");

        [BeforeEach]
        public async Task StartUnrested()
        {
            if (Me.Entity.MountedOn != null) Me.Entity.TryUnmount();
            Me.Entity.WatchedAttributes.RemoveAttribute("takeaseat:restedAt");
            Me.Entity.WatchedAttributes.RemoveAttribute("takeaseat:restedUntil");
            Buff.Update(Me);
            World.SetBlock("w4rd0sfurniture:rusticchair-north", Chair);
            await Ticks(2);
        }

        static void SitDown()
        {
            var seat = Sapi.ModLoader.GetModSystem<TakeASeatModSystem>().GetOrCreate(Sapi.World, Chair).Seats[0];
            Assert.True(Me.Entity.TryMount(seat), "sat down");
        }

        static async Task SitFor(double hours)
        {
            SitDown();
            await Hours(hours);
            Buff.Update(Me);
        }

        [VsTest]
        public async Task ABriefSitDoesNothing()
        {
            await SitFor(0.1);
            Assert.False(Buff.IsRested(Me.Entity), "rested after six minutes");
            Assert.Close(HungerRate, 1, 0.001, "hunger rate");
        }

        [VsTest]
        public async Task TenMinutesSeatedLeavesYouWellRested()
        {
            await SitFor(0.2);
            Assert.True(Buff.IsRested(Me.Entity), "rested");
            Assert.Close(HungerRate, 0.9, 0.001, "hunger rate");
            Assert.Close(Healing, 1.1, 0.001, "healing");
        }

        [VsTest]
        public async Task ItCanOnlyBeEarnedOnceADay()
        {
            await SitFor(0.2);
            double earned = Me.Entity.WatchedAttributes.GetDouble("takeaseat:restedAt");

            Me.Entity.TryUnmount();
            await Hours(5);
            await SitFor(0.2);
            Assert.Equal(earned, Me.Entity.WatchedAttributes.GetDouble("takeaseat:restedAt"), "earned again after 5 hours");

            Me.Entity.TryUnmount();
            await Hours(15);
            await SitFor(0.2);
            Assert.Greater(Me.Entity.WatchedAttributes.GetDouble("takeaseat:restedAt"), earned, "earned again after 20 hours");
        }

        [VsTest]
        public async Task SittingTooLongWearsItOffWithoutAPenalty()
        {
            await SitFor(0.2);
            Assert.True(Buff.IsRested(Me.Entity), "rested");

            await Hours(1.7);
            Buff.Update(Me);
            Assert.True(Buff.IsRested(Me.Entity), "lost it before two hours seated");

            await Hours(0.2);
            Buff.Update(Me);
            Assert.False(Buff.IsRested(Me.Entity), "still rested after two hours seated");
            Assert.Close(HungerRate, 1, 0.001, "hunger rate back to normal, not worse");
            Assert.Close(Healing, 1, 0.001, "healing back to normal, not worse");

            // Losing it doesn't reset the clock: staying put or sitting again today earns nothing.
            Me.Entity.TryUnmount();
            await SitFor(0.2);
            Assert.False(Buff.IsRested(Me.Entity), "earned back the same day");
        }

        [VsTest]
        public async Task ItWearsOffAfterAFewHours()
        {
            await SitFor(0.2);
            Me.Entity.TryUnmount();

            await Hours(3.9);
            Buff.Update(Me);
            Assert.True(Buff.IsRested(Me.Entity), "still rested before four hours are up");

            await Hours(0.2);
            Buff.Update(Me);
            Assert.False(Buff.IsRested(Me.Entity), "rested after four hours");
            Assert.Close(HungerRate, 1, 0.001, "hunger rate");
        }

        [VsTest]
        public async Task TheBuffIsRestoredFromWhatWasSaved()
        {
            await SitFor(0.2);
            Me.Entity.TryUnmount();

            // As after a reload: the earned time is saved, but the modifiers may not be.
            Me.Entity.Stats.Remove("hungerrate", WellRested.StatCode);
            Me.Entity.Stats.Remove("healingeffectivness", WellRested.StatCode);
            Buff.Update(Me);
            Assert.Close(HungerRate, 0.9, 0.001, "hunger rate restored");
        }
    }
}
