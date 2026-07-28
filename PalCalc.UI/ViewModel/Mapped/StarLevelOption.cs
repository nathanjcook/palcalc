using System.Collections.Generic;

namespace PalCalc.UI.ViewModel.Mapped
{
    /// <summary>
    /// An entry in a breeding-bonus Pal's star-level picker (issue #208). Star levels are 0..4
    /// (0★ base .. 4★ fully condensed); <see cref="NotDeployed"/> means the Pal isn't active,
    /// which contributes no multiplier.
    /// </summary>
    public class StarLevelOption(string label, int value)
    {
        public const int NotDeployed = -1;

        public static readonly List<StarLevelOption> All = [
            new("Not deployed", NotDeployed),
            new("0★", 0),
            new("1★", 1),
            new("2★", 2),
            new("3★", 3),
            new("4★", 4),
        ];

        public string Label => label;
        public int Value => value;
    }
}
