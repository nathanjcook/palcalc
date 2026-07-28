using PalCalc.Model;
using System.Collections.Generic;
using System.Linq;

namespace PalCalc.UI.ViewModel.Mapped
{
    /// <summary>
    /// An entry in the breeding-cake picker (issue #208). <see cref="Value"/> is null for the plain
    /// Cake - breeding always consumes a cake, and the basic one has no special effects, so it's the
    /// default and leaves solver behaviour unchanged.
    /// </summary>
    public class CakeViewModel(CakeEffect value)
    {
        public static readonly CakeViewModel Default = new(null);

        public static readonly List<CakeViewModel> Options = [Default, .. Cakes.All.Select(c => new CakeViewModel(c))];

        public CakeEffect Value => value;

        // Cake names come from the datamined registry and aren't localized yet; a future GenDB
        // localization reader can replace them with the in-game names.
        public string Label => value?.Name ?? "Cake";

        public static CakeViewModel FromItemKey(string itemKey) =>
            Options.FirstOrDefault(o => o.Value?.ItemKey == itemKey) ?? Default;
    }
}
