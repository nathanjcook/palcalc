namespace PalCalc.Model.Tests
{
    // Base-pal partner skills (Braloha = breeding-farm speed, Dynamoff = hatch speed) apply as
    // global, non-stacking time multipliers (<1 = faster). Registry that picks the best multiplier
    // per category is tested separately; here we pin that the multipliers actually scale the times.
    [TestClass]
    public class BasePalSpeedTests
    {
        [TestMethod]
        public void BreedingTimeMultiplier_ScalesAvgBreedingTime()
        {
            var settings = new GameSettings { BreedingTimeMultiplier = 0.5f };
            Assert.AreEqual(settings.BreedingTime * 2 * 0.5, settings.AvgBreedingTime);
        }

        [TestMethod]
        public void IncubationTimeMultiplier_ScalesHatchTime()
        {
            var normal = new GameSettings();
            var fast = new GameSettings { IncubationTimeMultiplier = 0.5f };
            Assert.AreEqual(EggSize.Large.IncubationTime(normal) * 0.5, EggSize.Large.IncubationTime(fast));
        }

        [TestMethod]
        public void Defaults_AreNeutral()
        {
            var settings = new GameSettings();
            Assert.AreEqual(1.0f, settings.BreedingTimeMultiplier);
            Assert.AreEqual(1.0f, settings.IncubationTimeMultiplier);
            Assert.AreEqual(settings.BreedingTime * 2, settings.AvgBreedingTime);
        }
    }
}
