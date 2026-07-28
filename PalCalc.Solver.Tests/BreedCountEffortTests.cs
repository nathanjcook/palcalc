using PalCalc.Solver.PalReference;
using System;

namespace PalCalc.Solver.Tests
{
    // Breeding cakes (issue #208): BreedCount = eggs per breeding action (Vegetable Cake = 2).
    // It reduces the number of breeding *actions* (egg production), not incubation.
    [TestClass]
    public class BreedCountEffortTests
    {
        private static readonly TimeSpan PerBreed = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan Incubation = TimeSpan.FromHours(1);

        [TestMethod]
        public void BreedCount2_HalvesBreedingActions()
        {
            // 10 eggs needed: 1 egg/breed -> 10 actions; 2 eggs/breed -> 5 actions.
            // multipleIncubators effort = actions*PerBreed + one incubation.
            var effort1 = BredPalReference.ComputeSelfBreedingEffort(10, 1, PerBreed, Incubation, multipleIncubators: true);
            var effort2 = BredPalReference.ComputeSelfBreedingEffort(10, 2, PerBreed, Incubation, multipleIncubators: true);

            Assert.AreEqual(10 * PerBreed + Incubation, effort1);
            Assert.AreEqual(5 * PerBreed + Incubation, effort2);
        }

        [TestMethod]
        public void BreedCount_RoundsActionsUp()
        {
            // 9 eggs, 2 per breed -> ceil(9/2) = 5 actions.
            var effort = BredPalReference.ComputeSelfBreedingEffort(9, 2, PerBreed, Incubation, multipleIncubators: true);
            Assert.AreEqual(5 * PerBreed + Incubation, effort);
        }

        [TestMethod]
        public void BreedCount1_MatchesOriginalFormula()
        {
            var effort = BredPalReference.ComputeSelfBreedingEffort(7, 1, PerBreed, Incubation, multipleIncubators: true);
            Assert.AreEqual(7 * PerBreed + Incubation, effort);
        }

        [TestMethod]
        public void FractionalEggYield_ReducesActions()
        {
            // Grintale at 4★ -> expected 1.75 eggs per pickup. 10 eggs -> ceil(10/1.75) = 6 actions.
            var effort = BredPalReference.ComputeSelfBreedingEffort(10, 1.75f, PerBreed, Incubation, multipleIncubators: true);
            Assert.AreEqual(6 * PerBreed + Incubation, effort);
        }

        [TestMethod]
        public void IncubationDominated_BreedCountBarelyMoves()
        {
            // Without multiple incubators and incubation >> breeding, effort is incubation-bound and
            // the "all incubation" branch uses a single timePerBreed, so BreedCount doesn't change it.
            var bigIncubation = TimeSpan.FromHours(2);
            var effort1 = BredPalReference.ComputeSelfBreedingEffort(10, 1, PerBreed, bigIncubation, multipleIncubators: false);
            var effort2 = BredPalReference.ComputeSelfBreedingEffort(10, 2, PerBreed, bigIncubation, multipleIncubators: false);

            Assert.AreEqual(effort1, effort2);
        }
    }
}
