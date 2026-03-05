using ChipMapping.ViewModels;
using CVWaferProber.Core.Config;

namespace CVWaferProber.Services
{
    public class MappingService
    {
        public event EventHandler<ChipViewModel> ChipSelected;
        public ChipMappingControlViewModel CustomVM { get; private set; }

        public MappingService(ChipMappingControlViewModel customVM)
        {
            CustomVM = customVM;
            CustomVM.ChipSelected += OnChipSelected;

            CustomVM.OutsiderRing = ConfigManager.Config.MapSettings.OutsiderRing;
        }

        private void OnChipSelected(object? sender, ChipViewModel _selectedChip)
        {
            ChipSelected?.Invoke(this, _selectedChip);
        }
    }
}
