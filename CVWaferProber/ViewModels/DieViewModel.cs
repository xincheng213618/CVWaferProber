using ChipMapping.ViewModels;
using CVWaferProber.Core.Models;
using CVWaferProber.Core.Models.Enums;
using CVWaferProber.Core.ViewModels;
using CVWaferProber.Utils;
using System.Windows;
using WaferComm.StateMachine;

namespace CVWaferProber.ViewModels
{
    public class DieViewModel : ViewModelBase
    {
        public uint? Id => chipViewModel?.Id;
        public int? ScreenX => (int?)chipViewModel?.Position.X;
        public int? ScreenY => (int?)chipViewModel?.Position.Y;
        public int? MapX => chipViewModel?.Column;
        public int? MapY => chipViewModel?.Row;
        // 核心修改：SerialNumber 属性添加变更通知
        private string? _serialNumber;
        public string? SerialNumber
        {
            get => _serialNumber;
            set
            {
                if (_serialNumber != value)
                {
                    _serialNumber = value;
                    OnPropertyChanged(nameof(SerialNumber));
                    // 通知MainViewModel更新良率
                    MainViewModel.Instance?.CalculateYieldBySerialNumber();
                }
            }
        }

        public bool IsIVLCameraEnabled {  get; set; }
        public bool IsChinese {  get; set; }

        // AOI复选框绑定属性
        private bool _isAOIEnabled;
        public bool IsAOIEnabled
        {
            get => _isAOIEnabled;
            set { _isAOIEnabled = value; OnPropertyChanged(); }
        }

        // IVL复选框绑定属性
        private bool _isIVLEnabled;
        public bool IsIVLEnabled
        {
            get => _isIVLEnabled;
            set { _isIVLEnabled = value; OnPropertyChanged(); }
        }
        private bool _isEQEEnabled;
        public bool IsEQEEnabled
        {
            get => _isEQEEnabled;
            set { _isEQEEnabled = value; OnPropertyChanged(); }
        }

        // IVL复选框绑定属性
        private bool _isVAMEnabled;
        public bool IsVAMEnabled
        {
            get => _isVAMEnabled;
            set { _isVAMEnabled = value; OnPropertyChanged(); }
        }
        public DieViewModel(ChipViewModel die)
        {
            this.chipViewModel = die;
            this.IsIVLCameraEnabled = false;
            this.IsChinese = GetCurrentLanguage() == "Chinese";
           
        }

        public ChipStatus? Status => chipViewModel?.Status;
        //public ChipStatus? Status => ChipStatus.IVL_COMPLETED;
        public string? DisplayStatus => Status.HasValue ? ChipStatusTool.GetStatusDisplay(Status.Value, IsChinese) : "Unknown";
        public DateTime? EndTestTime { get; set; }
        public DateTime? StartTestTime { get; set; }
        public MotionStatus MStatus { get; set; }
        public string? TotalTime { get; set; }
        public ChipViewModel? chipViewModel { get; set; }
        public string? DataValue => string.Format("{0:F4}",chipViewModel?.DataValue);

        public static string GetCurrentLanguage()
        {
            var app = Application.Current;
            if (app?.Resources?.MergedDictionaries?.FirstOrDefault() is ResourceDictionary resourceDict)
            {
                var source = resourceDict.Source?.ToString();
                if (source != null)
                {
                    if (source.Contains("Chinese.xaml"))
                        return "Chinese";
                    else if (source.Contains("English.xaml"))
                        return "English";
                }
            }
            return "Unknown";
        }
        public void ChangeStatus(ChipStatus status, bool updateTime = false)
        {
            if (updateTime) EndTestTime = DateTime.Now;
            if (status == ChipStatus.TESTING || status == ChipStatus.IVL_TESTING)
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
            FirePropertyChanged();
        }
        public void ChangeStatusOnly(ChipStatus status)
        {
            chipViewModel?.SetStatus(status);
            FirePropertyChanged();
        }

        public void TestingReady(string proberId,string timestamp)
        {
            this.EndTestTime = null;
            this.SerialNumber = SNBuilder.Build(proberId, timestamp, this);
            this.StartTestTime = null;
            this.TotalTime = null;
            this.chipViewModel?.SetStatus(ChipStatus.WAITING);
            FirePropertyChanged();
        }
        public void UnSelected()
        {
            if (chipViewModel != null) chipViewModel.IsSelected = false;
        }

        public void ResetStatus()
        {
            UnSelected();
            chipViewModel?.SetStatus(ChipStatus.WAITING);
            StartTestTime = null;
            EndTestTime = null;
            TotalTime = null;
            SerialNumber = null;
            if (chipViewModel != null && chipViewModel.ChipData != null) chipViewModel.ChipData.DataValue = null;
            FirePropertyChanged();
        }

        private void FirePropertyChanged()
        {
            OnPropertyChanged(nameof(DisplayStatus));
            OnPropertyChanged(nameof(StartTestTime));
            OnPropertyChanged(nameof(EndTestTime));
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(SerialNumber));
            OnPropertyChanged(nameof(TotalTime));
            OnPropertyChanged(nameof(DataValue));
        }

        public (string x, string y) ToMapAxis()
        {
            string x = string.Format("{0}{1:D3}", this.MapX >= 0 ? "+" : "", this.MapX);
            string y = string.Format("{0}{1:D3}", this.MapY >= 0 ? "+" : "", this.MapY);
            return (x, y);
        }

        #region 动态属性
        public string LightOnStatus { get; set; } = "na";
        public string RegisterPixels { get; set; } = "na";
        public string FinalClass { get; set; } = "na"; // 最终等级
        public string AOIGradeLevel { get; set; } = "na";
        public string BlackPattern { get; set; } = "na";
        public string Temperature { get; set; } = "na";
        public string PixelLogic { get; set; } = "na";
        //public string MeasurePin { get; set; } = "na";
        public string Pressure { get; set; } = "na";
        public int TouchDownCounts { get; set; } = 0;
        public string ProbingCardSN { get; set; } = "na";
       


        #endregion
    }
}
