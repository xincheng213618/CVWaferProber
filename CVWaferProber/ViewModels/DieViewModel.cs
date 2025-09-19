using ChipMapping.ViewModels;
using CVWaferProber.Core.Models;
using CVWaferProber.Core.Models.Enums;
using CVWaferProber.Core.ViewModels;

namespace CVWaferProber.ViewModels
{
    public class DieViewModel : ViewModelBase
    {
        public uint? Id => chipViewModel?.Id;
        public int? ScreenX => (int)chipViewModel?.Position.X;
        public int? ScreenY => (int)chipViewModel?.Position.Y;
        public int? MapX => chipViewModel?.Column;
        public int? MapY => chipViewModel?.Row;
        public string? SerialNumber {  get; set; }

        public DieViewModel(ChipViewModel die)
        {
            this.chipViewModel = die;
        }

        public ChipStatus? Status  => chipViewModel?.Status;
        public string? DisplayStatus => ChipStatusTool.GetStatusDisplay((ChipStatus)Status);
        public DateTime? EndTestTime { get; set; }
        public DateTime? StartTestTime { get; set; }
        public string? TotalTime { get; set; }
        public ChipViewModel? chipViewModel { get; set; }
        public string? DataValue => string.Format("{0:F4}",chipViewModel?.DataValue);

        public void ChangeStatus(ChipStatus status, bool updateTime = false)
        {
            if (updateTime) EndTestTime = DateTime.Now;
            if (status == ChipStatus.TESTING)
            {
                StartTestTime = DateTime.Now;
            }
            else
            {
                var sp = EndTestTime - StartTestTime;
                TotalTime = sp.ToString();
            }
            chipViewModel?.SetStatus(status);
            if (chipViewModel != null) chipViewModel.IsSelected = true;
            OnPropertyChanged(nameof(DisplayStatus));
            OnPropertyChanged(nameof(StartTestTime));
            OnPropertyChanged(nameof(EndTestTime));
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(SerialNumber));
            OnPropertyChanged(nameof(TotalTime));
            OnPropertyChanged(nameof(DataValue));
        }

        public void UnSelected()
        {
            if (chipViewModel != null) chipViewModel.IsSelected = false;
        }
    }
}
