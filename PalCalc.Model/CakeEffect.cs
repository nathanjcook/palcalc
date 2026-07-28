using System.Collections.Generic;

namespace PalCalc.Model
{
    /// <summary>
    /// Breeding-item ("cake") effects, from the game's DA_BreedingItemEffectData asset (issue #208).
    /// Currently seeded from datamined values (see <see cref="Cakes"/>); a future PalCalc.GenDB reader
    /// (mirroring GameSettingReader) can parse these from game files and surface proper item names.
    /// </summary>
    public class CakeEffect
    {
        // Item key from the asset, e.g. "Cake02".."Cake05".
        public string ItemKey { get; set; }

        // English display name (from paldb.cc / game data). A future GenDB localization reader can
        // replace this with the properly-localized in-game name.
        public string Name { get; set; }

        // Flat bonus range applied to inherited IV values.
        public int TalentBonusMin { get; set; }
        public int TalentBonusMax { get; set; }

        public float MutationRateBonusPercent { get; set; }
        public int CombiRankBonus { get; set; }

        // Eggs produced per breeding action (default 1).
        public int BreedCount { get; set; } = 1;

        public bool InheritAllActiveSkills { get; set; }

        // Forces exactly N passives to be inherited directly from parents. 0 in the game data means
        // "no override" (use the normal weighted distribution).
        public int PassiveInheritCountOverride { get; set; }

        // 0 in the asset means "not overridden".
        public int? EffectivePassiveInheritCount =>
            PassiveInheritCountOverride > 0 ? PassiveInheritCountOverride : (int?)null;

        // Guard against nonsensical data; a breed always yields at least one egg.
        public int EffectiveBreedCount => BreedCount > 0 ? BreedCount : 1;
    }

    /// <summary>
    /// Datamined breeding-cake effects (DA_BreedingItemEffectData, issue #208). Seeded here until
    /// PalCalc.GenDB parses the asset directly.
    /// </summary>
    public static class Cakes
    {
        // Mushroom Cake — talents (IVs) grow slightly more easily.
        public static readonly CakeEffect Cake02 = new()
        { ItemKey = "Cake02", Name = "Mushroom Cake", TalentBonusMin = 1, TalentBonusMax = 5, BreedCount = 1 };

        // Vegetable Cake — pals lay 2 eggs per breed instead of 1.
        public static readonly CakeEffect Cake03 = new()
        { ItemKey = "Cake03", Name = "Vegetable Cake", BreedCount = 2 };

        // Extravagant Vegetable Cake — mutations more likely + talents grow more easily.
        public static readonly CakeEffect Cake04 = new()
        { ItemKey = "Cake04", Name = "Extravagant Vegetable Cake", TalentBonusMin = 1, TalentBonusMax = 5, MutationRateBonusPercent = 2.0f, BreedCount = 1 };

        // Special Cake (legendary rarity) — inherit 4 passives + all active skills.
        public static readonly CakeEffect Cake05 = new()
        { ItemKey = "Cake05", Name = "Special Cake", InheritAllActiveSkills = true, PassiveInheritCountOverride = 4, BreedCount = 1 };

        public static readonly IReadOnlyList<CakeEffect> All = [Cake02, Cake03, Cake04, Cake05];
    }
}
