using ChipMapping.Models;
using ChipMapping.Models.Enums;
using ChipMapping.ViewModels;

namespace CVWaferProber.ViewModels
{
    public class DieViewModel : ViewModelBase
    {
        public uint? Id => chipViewModel?.Id;
        public int? ScreenX => (int)chipViewModel?.Position.X;
        public int? ScreenY => (int)chipViewModel?.Position.Y;
        public int? MapX => chipViewModel?.Column;
        public int? MapY => chipViewModel?.Row;

        public DieViewModel(ChipViewModel die)
        {
            this.chipViewModel = die;
        }

        public ChipStatus? Status  => chipViewModel?.Status;
        public string? DisplayStatus  => ChipStatusTool.GetStatusDisplay((ChipStatus)Status);
        public DateTime? TestTime { get; set; }

        public ChipViewModel? chipViewModel { get; set; }

        public void ChangeStatus(ChipStatus status,bool updateTime = false)
        {
            if(updateTime)TestTime = DateTime.Now;
            chipViewModel?.SetStatus(status);
            chipViewModel.IsSelected = true;
            OnPropertyChanged(nameof(DisplayStatus));
            OnPropertyChanged(nameof(TestTime));
            OnPropertyChanged(nameof(Status));
        }

        public void UnSelected()
        {
            chipViewModel.IsSelected = false;
        }
    }
}
