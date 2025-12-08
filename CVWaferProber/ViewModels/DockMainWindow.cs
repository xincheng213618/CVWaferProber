using CVWaferProber.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace CVWaferProber.ViewModels
{
    public class DockMainWindow: ViewModelBase
    {
        // 1. 面板显示状态属性（右上角相机面板默认隐藏）
        private bool _isMappingPanelVisible = true;
        public bool IsMappingPanelVisible
        {
            get => _isMappingPanelVisible;
            set { _isMappingPanelVisible = value; OnPropertyChanged(); }
        }

        private bool _isCameraPanelVisible = false; // 初始隐藏
        public bool IsCameraPanelVisible
        {
            get => _isCameraPanelVisible;
            set { _isCameraPanelVisible = value; OnPropertyChanged(); }
        }

        private bool _isSPPanelVisible = true;
        public bool IsSPPanelVisible
        {
            get => _isSPPanelVisible;
            set { _isSPPanelVisible = value; OnPropertyChanged(); }
        }

        private bool _isLogPanelVisible = true;
        public bool IsLogPanelVisible
        {
            get => _isLogPanelVisible;
            set { _isLogPanelVisible = value; OnPropertyChanged(); }
        }
        public ICommand ExitCommand { get; }
        public ICommand ResetLayoutCommand { get; }
        public ICommand OpenHelpCommand { get; }
        public ICommand OpenAboutCommand { get; }
        
        private void ExecuteExit() => Application.Current.Shutdown();
    }
}
