using CVWaferProber.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using static Microsoft.WindowsAPICodePack.Shell.PropertySystem.SystemProperties.System;

namespace CVWaferProber.ViewModels
{
    public class MappingDataViewModel : ViewModelBase
    {

        private string timestamp;
        public string Timestamp
        {
            get => timestamp;
            set
            {
                timestamp = value;
                OnPropertyChanged(); // 通知 UI 属性变更
            }
        }
        private string serialNumber;
        public string SerialNumber
        {
            get => serialNumber;
            set
            {
                serialNumber = value;
                OnPropertyChanged(); // 通知 UI 属性变更
            }
        }
        
        public ICommand SearchCommand { get; set; }
        public MappingDataViewModel()
        {
            SearchCommand = new RelayCommand(OnSearch);

        }

        private void OnSearch(object? obj)
        {



        }
    }
}
