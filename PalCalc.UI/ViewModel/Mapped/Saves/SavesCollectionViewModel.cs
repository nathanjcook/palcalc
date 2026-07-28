using CommunityToolkit.Mvvm.Input;
using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.Model.Service;
using PalCalc.UI.ViewModel.Mapped.Saves.Detection;
using PalCalc.UI.ViewModel.SaveSelection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Mapped.Saves
{
    public class SavesCollectionViewModel
    {
        public ISavesLocation SourceLocation { get; set; }
        public SaveType SaveType { get; set; }
        public ReadOnlyObservableCollection<SaveGameViewModel> AvailableSaves { get; set; }
        public ILocalizedText TypeLabel { get; set; }
        public ILocalizedText Title { get; set; }
        public IRelayCommand OpenFolderCommand { get; set; }
        public IRelayCommand AddSaveCommand { get; set; }
        public IRelayCommand<SaveGameViewModel> RemoveSaveCommand { get; set; }



        public static IEnumerable<SavesCollectionViewModel> DetectAll(AppSettings settings, ISavesService savesService)
        {
            // (only Steam saves are currently supported by `AllLocal`)
            var steamCollections = SteamSaves.CollectAll(DirectSavesLocation.AllLocal);
            if (steamCollections.Count == 0)
                steamCollections.Add(SteamSaves.MakePlaceholderCollection());

            var xboxCollections = XboxSaves.CollectAll(XboxSavesLocation.FindAll());
            if (xboxCollections.Count == 0)
                xboxCollections.Add(XboxSaves.MakePlaceholderCollection());


            var manualSaves = ManualSaves.CollectAll(settings, savesService);
            var remoteSaves = DedicatedServerSaves.CollectAll(settings, savesService);
            var dockerSaves = LocalDockerSaves.CollectAll(settings, savesService);
            var fakeSaves = VirtualSaves.CollectAll(settings, savesService);

            var collections = new List<SavesCollectionViewModel>();
            collections.AddRange(steamCollections);
            collections.AddRange(xboxCollections);
            collections.Add(manualSaves);
            collections.Add(remoteSaves);
            // Local Docker is pure auto-detect — only show the group when a save was actually found,
            // so non-Docker users don't see an empty section.
            if (dockerSaves.AvailableSaves.Count > 0) collections.Add(dockerSaves);
            collections.Add(fakeSaves);

            return collections;
        }
    }
}
