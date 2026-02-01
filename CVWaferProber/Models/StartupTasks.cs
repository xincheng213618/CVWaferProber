using CVWaferProber.Language;
using CVWaferProber.Services;
using CVWaferProber.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.Models
{
    public class MainStartupTask : StartupTask
    {
        public MainStartupTask() : base(LanguageManager.Instance.GetString("Task_Hardware"), "#6B7280")
        {
        }

        public override void Exec()
        {
            //MainService.Instance.Startup();
        }
    }
    public class CommStartupTask : StartupTask
    {
        public CommStartupTask(string description, string statusColor) : base(description, statusColor)
        {
        }

        public override void Exec()
        {
            Thread.Sleep(2000);
        }
    }
}
