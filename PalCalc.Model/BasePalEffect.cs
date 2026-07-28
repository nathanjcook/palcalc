using System;
using System.Collections.Generic;
using System.Linq;

namespace PalCalc.Model
{
    // Which breeding quantity a base/party Pal's partner skill affects.
    public enum BasePalEffectTarget
    {
        BreedingTime,   // egg-production time at the Breeding Farm (Braloha)
        IncubationTime, // egg hatch time at the Incubator (Dynamoff)
        EggYield,       // expected eggs per pickup (Grintale)
    }

    /// <summary>
    /// A Pal partner skill that speeds up breeding while the Pal is active (in base or party).
    /// Per-star values are pinned to paldb.cc tables. Non-stacking: only the single best per target
    /// applies (matching the "Does not stack" wording on all of these skills).
    /// </summary>
    public class BasePalEffect
    {
        public string PalName { get; set; }
        public string PartnerSkillName { get; set; }

        // Display-only: "In base" or "In party".
        public string Condition { get; set; }

        public BasePalEffectTarget Target { get; set; }

        // Bonus percent per star level, index 0..4 (0★ base .. 4★ fully condensed).
        public IReadOnlyList<float> BonusPercentByStar { get; set; }

        /// <summary>
        /// The multiplier this effect applies to its target quantity at the given star level.
        /// BreedingTime/IncubationTime give a time multiplier (&lt;1 = faster); EggYield gives an
        /// eggs-per-action multiplier (&gt;1 = fewer breeding actions). The per-target formula
        /// reflects each Pal's exact in-game wording (each target currently maps to one Pal).
        /// </summary>
        public float MultiplierAt(int starLevel)
        {
            var x = BonusPercentByStar[starLevel] / 100f;
            return Target switch
            {
                // Braloha: "increases egg production speed by X%" -> time / (1 + X)
                BasePalEffectTarget.BreedingTime => 1f / (1f + x),
                // Dynamoff: "shortens incubation time by X%" -> time * (1 - X)
                BasePalEffectTarget.IncubationTime => 1f - x,
                // Grintale: "X% chance of one extra egg on pickup" -> expected yield (1 + X)
                BasePalEffectTarget.EggYield => 1f + x,
                _ => throw new NotImplementedException(),
            };
        }
    }

    // A chosen Pal at a chosen star level (0..4).
    public readonly record struct BasePalSelection(BasePalEffect Effect, int StarLevel);

    /// <summary>
    /// Base/party Pal partner skills that affect breeding, seeded from paldb.cc. Resolves a set of
    /// selected Pals into the (default-neutral) breeding multipliers on <see cref="GameSettings"/>.
    /// </summary>
    public static class BasePalEffects
    {
        // Braloha "Balmy Weather" — in base, faster egg production at the Breeding Farm.
        public static readonly BasePalEffect Braloha = new()
        {
            PalName = "Braloha",
            PartnerSkillName = "Balmy Weather",
            Condition = "In base",
            Target = BasePalEffectTarget.BreedingTime,
            BonusPercentByStar = [20f, 26f, 32f, 38f, 50f],
        };

        // Dynamoff "Electro-Massage Incubation" — in base, shorter egg incubation time.
        public static readonly BasePalEffect Dynamoff = new()
        {
            PalName = "Dynamoff",
            PartnerSkillName = "Electro-Massage Incubation",
            Condition = "In base",
            Target = BasePalEffectTarget.IncubationTime,
            BonusPercentByStar = [20f, 22f, 26f, 32f, 40f],
        };

        // Grintale "Glaring Cat's Eye" — in party, chance of a bonus egg when picking one up.
        public static readonly BasePalEffect Grintale = new()
        {
            PalName = "Grintale",
            PartnerSkillName = "Glaring Cat's Eye",
            Condition = "In party",
            Target = BasePalEffectTarget.EggYield,
            BonusPercentByStar = [50f, 55f, 60f, 65f, 75f],
        };

        public static readonly IReadOnlyList<BasePalEffect> All = [Braloha, Dynamoff, Grintale];

        // Best (fastest) time multiplier among selected effects for a time target; 1.0 if none.
        // Non-stacking -> the smallest (most beneficial) multiplier wins.
        public static float BestTimeMultiplier(IEnumerable<BasePalSelection> selections, BasePalEffectTarget target)
        {
            var best = 1.0f;
            foreach (var s in selections)
                if (s.Effect.Target == target)
                    best = Math.Min(best, s.Effect.MultiplierAt(s.StarLevel));
            return best;
        }

        // Best (highest) egg-yield multiplier among selected EggYield effects; 1.0 if none.
        public static float BestEggYieldMultiplier(IEnumerable<BasePalSelection> selections)
        {
            var best = 1.0f;
            foreach (var s in selections)
                if (s.Effect.Target == BasePalEffectTarget.EggYield)
                    best = Math.Max(best, s.Effect.MultiplierAt(s.StarLevel));
            return best;
        }

        // Writes the selected Pals' best-per-target multipliers onto the game settings.
        public static void ApplyTo(GameSettings settings, IEnumerable<BasePalSelection> selections)
        {
            var sels = selections as IReadOnlyCollection<BasePalSelection> ?? selections.ToList();
            settings.BreedingTimeMultiplier = BestTimeMultiplier(sels, BasePalEffectTarget.BreedingTime);
            settings.IncubationTimeMultiplier = BestTimeMultiplier(sels, BasePalEffectTarget.IncubationTime);
            settings.EggYieldMultiplier = BestEggYieldMultiplier(sels);
        }
    }
}
