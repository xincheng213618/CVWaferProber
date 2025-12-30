using ChipMapping.ViewModels;
using CVWaferProber.Core.Models;
using CVWaferProber.Core.Models.Enums;
using CVWaferProber.Core.ViewModels;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

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
        //private void BtnAssignToColumn_Click(object sender, RoutedEventArgs e)
        //{
        //    // 1. 验证输入非空
        //    var assignValue = (string)Application.Current.FindResource(BtnSearch).Text.Trim();
        //    if (string.IsNullOrWhiteSpace(assignValue))
        //    {
        //        MessageBox.Show("请输入要赋给整列的值！");
        //        return;
        //    }

        //    // 2. 遍历所有行，赋值给目标列的绑定属性（Department）
        //    foreach (var emp in _employeeList)
        //    {
        //        emp.Department = assignValue; // 赋值后自动刷新UI
        //    }

        //    // 3. 反馈结果
        //    MessageBox.Show($"已成功将「{assignValue}」赋给所有 {_employeeList.Count} 行的「部门」列！");
        //    txtColumnValue.Clear();
        //}
        //private bool _isSelected;
        ///// <summary>
        ///// 标记当前芯片是否被选中（用于表格高亮）
        ///// </summary>
        //public bool IsSelected
        //{
        //    get => _isSelected;
        //    set
        //    {
        //        if (_isSelected != value)
        //        {
        //            _isSelected = value;
        //            OnPropertyChanged(nameof(IsSelected)); // 确保继承了INotifyPropertyChanged
        //        }
        //    }
        //}
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
