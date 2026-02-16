using CVWaferProber.Language;
using CVWaferProber.Services;
using System.ComponentModel;

namespace CVWaferProber.Models
{
    // 任务状态枚举
    public enum TaskStatus
    {
        Pending,    // 等待中
        Running,    // 执行中
        Completed,  // 已完成
        Failed      // 失败
    }
    // 启动任务类
    public abstract class StartupTask : INotifyPropertyChanged
    {
        private string _description;
        private string _statusColor;
        private bool _isActive;
        private bool _isCompleted;
        private string _statusIcon;
        private TaskStatus _status;

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(nameof(Description)); }
        }

        public string StatusColor
        {
            get => _statusColor;
            set { _statusColor = value; OnPropertyChanged(nameof(StatusColor)); }
        }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = value;
                OnPropertyChanged(nameof(IsActive));

                // 当状态变为非活动时，确保IsCompleted保持正确
                if (!value && !_isCompleted)
                {
                    StatusColor = "#6B7280";
                }
            }
        }

        public TaskStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));

                // 根据状态更新外观
                switch (value)
                {
                    case TaskStatus.Pending:
                        StatusColor = "#6B7280"; // 灰色
                        StatusIcon = "";
                        break;
                    case TaskStatus.Running:
                        StatusColor = "#60A5FA"; // 蓝色
                        StatusIcon = "⟳";
                        break;
                    case TaskStatus.Completed:
                        StatusColor = "#10B981"; // 绿色
                        StatusIcon = "✓";
                        IsCompleted = true;
                        IsActive = false;
                        break;
                    case TaskStatus.Failed:
                        StatusColor = "#EF4444"; // 红色
                        StatusIcon = "✗";
                        IsCompleted = false;
                        IsActive = false;
                        break;
                }
            }
        }

        //public bool IsSuccess
        //{
        //    get => _isSuccess;
        //    set
        //    {
        //        _isSuccess = value;
        //        OnPropertyChanged(nameof(IsSuccess));
        //    }
        //}

        public bool IsCompleted
        {
            get => _isCompleted;
            set
            {
                _isCompleted = value;
                OnPropertyChanged(nameof(IsCompleted));

                // 当任务完成时，自动设置为非活动
                if (value)
                {
                    IsActive = false;
                    StatusColor = "#10B981";
                    StatusIcon = "✓";
                }
                else
                {
                    IsActive = false;
                    StatusColor = "#EF4444";  // 失败颜色（红色）
                    StatusIcon = "✗";        // 失败图标（叉号）
                }
            }
        }

        public string StatusIcon
        {
            get => _statusIcon;
            set { _statusIcon = value; OnPropertyChanged(nameof(StatusIcon)); }
        }

        public StartupTask(string description, string statusColor)
        {
            Description = description;
            StatusColor = statusColor;
            IsActive = false;
            IsCompleted = false;
            StatusIcon = "";
            Status = TaskStatus.Pending;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public abstract void Exec();
    }

    public class MainStartupTask : StartupTask
    {
        public MainStartupTask() : base(LanguageManager.Instance.GetString("Task_Hardware"), "#6B7280")
        {
        }

        public override void Exec()
        {
            this.Status = TaskStatus.Running;
            var task = MainService.Instance.TryRegistAsync();
            task.Wait();
            bool bR = task.Result;
            if (bR) this.Status = TaskStatus.Completed;
            else this.Status = TaskStatus.Failed;
        }
    } 
    public class MotionStartupTask : StartupTask
    {
        public MotionStartupTask() : base(LanguageManager.Instance.GetString("Task_Motion"), "#6B7280")
        {
        }

        public override void Exec()
        {
            this.Status = TaskStatus.Running;
            var task = MainService.Instance.TryConnectAsync();
            task.Wait();
            bool bR = task.Result;
            if (bR) this.Status = TaskStatus.Completed;
            else this.Status = TaskStatus.Failed;
        }
    }
    public class CommStartupTask : StartupTask
    {
        public CommStartupTask(string description, string statusColor) : base(description, statusColor)
        {
        }

        public override void Exec()
        {
            this.Status = TaskStatus.Running;
            MainService.Instance.Startup();
            this.Status = TaskStatus.Completed;
        }
    }
}
