using ChipMapping.ViewModels;

namespace CVWaferProber.Services
{
    public class MappingService
    {
        public event EventHandler<ChipViewModel> ChipSelected;
        public ChipMappingControlViewModel CustomVM { get; private set; }
        public MappingService() : this(new ChipMappingControlViewModel())
        {
        }
        public MappingService(ChipMappingControlViewModel customVM)
        {
            CustomVM = customVM;
            CustomVM.ChipSelected += OnChipSelected;
        }

        private void OnChipSelected(object? sender, ChipViewModel _selectedChip)
        {
            ChipSelected?.Invoke(this, _selectedChip);
        }
    }
}
