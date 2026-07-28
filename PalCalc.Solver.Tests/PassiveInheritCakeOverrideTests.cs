using PalCalc.Model;
using System.Collections.Generic;

namespace PalCalc.Solver.Tests
{
    // Breeding-cake support (issue #208): PassiveInheritCountOverride forces a fixed number of
    // passives to be inherited directly from parents (e.g. legendary cake = 4).
    [TestClass]
    public class PassiveInheritCakeOverrideTests : PassivesProbabilitiesTestBase
    {
        [TestMethod]
        public void Override4_TwoDesiredFromTwoParents_GuaranteesDirectInheritance()
        {
            // With 4 forced direct inherits and 2 parent passives (both desired), both desired are
            // guaranteed inherited; only the (>= 2) random passives needed to reach 4 total remain.
            var actual = Probabilities.Passives.ProbabilityInheritedTargetPassives(
                parentPassives: [Runner, Swift],
                desiredParentPassives: [Runner, Swift],
                numFinalPassives: 4,
                passiveInheritCountOverride: 4
            );

            Assert.AreEqual(GameConstants.PassiveRandomAddedAtLeastN[2], actual, 0.0001f);
        }

        [TestMethod]
        public void Override4_BeatsDefaultDistribution()
        {
            var baseProb = Probabilities.Passives.ProbabilityInheritedTargetPassives([Runner, Swift], [Runner, Swift], 4);
            var cakeProb = Probabilities.Passives.ProbabilityInheritedTargetPassives([Runner, Swift], [Runner, Swift], 4, passiveInheritCountOverride: 4);

            Assert.IsTrue(cakeProb > baseProb, $"cake {cakeProb} should exceed base {baseProb}");
        }

        [TestMethod]
        public void OverrideBelowDesiredCount_IsImpossible()
        {
            // Can't inherit 3 specific passives if the cake only grants 2 direct inherits.
            var actual = Probabilities.Passives.ProbabilityInheritedTargetPassives(
                parentPassives: [Runner, Swift, Nimble],
                desiredParentPassives: [Runner, Swift, Nimble],
                numFinalPassives: 4,
                passiveInheritCountOverride: 2
            );

            Assert.AreEqual(0.0f, actual, 0.0001f);
        }

        [TestMethod]
        public void NullOverride_MatchesDefault()
        {
            var withoutParam = Probabilities.Passives.ProbabilityInheritedTargetPassives([Runner, Swift], [Runner], 4);
            var withNull = Probabilities.Passives.ProbabilityInheritedTargetPassives([Runner, Swift], [Runner], 4, passiveInheritCountOverride: null);

            Assert.AreEqual(withoutParam, withNull, 0.0001f);
        }
    }
}
