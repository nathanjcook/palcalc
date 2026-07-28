using System.Linq;

namespace PalCalc.Model.Tests
{
    // Base/party Pal partner skills that speed up breeding. Values pinned to paldb.cc per-star tables.
    [TestClass]
    public class BasePalEffectTests
    {
        [TestMethod]
        public void PerStarBonusPercents_MatchGameData()
        {
            CollectionAssert.AreEqual(new[] { 20f, 26f, 32f, 38f, 50f }, BasePalEffects.Braloha.BonusPercentByStar.ToArray());
            CollectionAssert.AreEqual(new[] { 20f, 22f, 26f, 32f, 40f }, BasePalEffects.Dynamoff.BonusPercentByStar.ToArray());
            CollectionAssert.AreEqual(new[] { 50f, 55f, 60f, 65f, 75f }, BasePalEffects.Grintale.BonusPercentByStar.ToArray());
        }

        [TestMethod]
        public void Braloha_IsBreedingSpeedIncrease()
        {
            Assert.AreEqual(BasePalEffectTarget.BreedingTime, BasePalEffects.Braloha.Target);
            // "+X% speed" -> time x 1/(1+X). 0★ = +20%, 4★ = +50%.
            Assert.AreEqual(1f / 1.20f, BasePalEffects.Braloha.MultiplierAt(0), 1e-6f);
            Assert.AreEqual(1f / 1.50f, BasePalEffects.Braloha.MultiplierAt(4), 1e-6f);
        }

        [TestMethod]
        public void Dynamoff_IsHatchTimeReduction()
        {
            Assert.AreEqual(BasePalEffectTarget.IncubationTime, BasePalEffects.Dynamoff.Target);
            // "-X% time" -> time x (1-X). 0★ = -20% -> 0.80, 4★ = -40% -> 0.60.
            Assert.AreEqual(0.80f, BasePalEffects.Dynamoff.MultiplierAt(0), 1e-6f);
            Assert.AreEqual(0.60f, BasePalEffects.Dynamoff.MultiplierAt(4), 1e-6f);
        }

        [TestMethod]
        public void Grintale_IsExpectedEggYield()
        {
            Assert.AreEqual(BasePalEffectTarget.EggYield, BasePalEffects.Grintale.Target);
            // "X% chance +1 egg" -> expected yield (1+X). 0★ = 50% -> 1.50, 4★ = 75% -> 1.75.
            Assert.AreEqual(1.50f, BasePalEffects.Grintale.MultiplierAt(0), 1e-6f);
            Assert.AreEqual(1.75f, BasePalEffects.Grintale.MultiplierAt(4), 1e-6f);
        }

        [TestMethod]
        public void BestTimeMultiplier_EmptySelections_IsNeutral()
        {
            Assert.AreEqual(1.0f, BasePalEffects.BestTimeMultiplier([], BasePalEffectTarget.BreedingTime));
            Assert.AreEqual(1.0f, BasePalEffects.BestEggYieldMultiplier([]));
        }

        [TestMethod]
        public void Resolvers_RouteByTarget()
        {
            var sels = new[] { new BasePalSelection(BasePalEffects.Grintale, 4) };
            // Grintale is EggYield only -> breeding/incubation stay neutral.
            Assert.AreEqual(1.0f, BasePalEffects.BestTimeMultiplier(sels, BasePalEffectTarget.BreedingTime));
            Assert.AreEqual(1.0f, BasePalEffects.BestTimeMultiplier(sels, BasePalEffectTarget.IncubationTime));
            Assert.AreEqual(1.75f, BasePalEffects.BestEggYieldMultiplier(sels), 1e-6f);
        }

        [TestMethod]
        public void BestTimeMultiplier_NonStacking_PicksFastest()
        {
            var sels = new[]
            {
                new BasePalSelection(BasePalEffects.Braloha, 0), // +20% -> 0.833
                new BasePalSelection(BasePalEffects.Braloha, 4), // +50% -> 0.667
            };
            Assert.AreEqual(1f / 1.50f, BasePalEffects.BestTimeMultiplier(sels, BasePalEffectTarget.BreedingTime), 1e-6f);
        }

        [TestMethod]
        public void ApplyTo_SetsAllThreeMultipliers()
        {
            var settings = new GameSettings();
            BasePalEffects.ApplyTo(settings, new[]
            {
                new BasePalSelection(BasePalEffects.Braloha, 4),
                new BasePalSelection(BasePalEffects.Dynamoff, 0),
                new BasePalSelection(BasePalEffects.Grintale, 4),
            });

            Assert.AreEqual(1f / 1.50f, settings.BreedingTimeMultiplier, 1e-6f);
            Assert.AreEqual(0.80f, settings.IncubationTimeMultiplier, 1e-6f);
            Assert.AreEqual(1.75f, settings.EggYieldMultiplier, 1e-6f);
        }
    }
}
