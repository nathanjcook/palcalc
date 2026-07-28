namespace PalCalc.Model.Tests
{
    // Breeding-cake effects seeded from DA_BreedingItemEffectData (issue #208).
    [TestClass]
    public class CakeEffectTests
    {
        [TestMethod]
        public void LegendaryCake_Inherits4PassivesAndAllActiveSkills()
        {
            Assert.AreEqual(4, Cakes.Cake05.EffectivePassiveInheritCount);
            Assert.IsTrue(Cakes.Cake05.InheritAllActiveSkills);
        }

        [TestMethod]
        public void ZeroOverride_MeansNoOverride()
        {
            Assert.IsNull(Cakes.Cake02.EffectivePassiveInheritCount);
        }

        [TestMethod]
        public void MoreEggsCake_HasBreedCountTwo()
        {
            Assert.AreEqual(2, Cakes.Cake03.EffectiveBreedCount);
        }

        [TestMethod]
        public void DefaultBreedCount_IsOne()
        {
            Assert.AreEqual(1, new CakeEffect().EffectiveBreedCount);
        }

        [TestMethod]
        public void CakeNames_MatchGameData()
        {
            // Verified against paldb.cc / game data (Cake02..Cake05).
            Assert.AreEqual("Mushroom Cake", Cakes.Cake02.Name);
            Assert.AreEqual("Vegetable Cake", Cakes.Cake03.Name);
            Assert.AreEqual("Extravagant Vegetable Cake", Cakes.Cake04.Name);
            Assert.AreEqual("Special Cake", Cakes.Cake05.Name);
        }
    }
}
