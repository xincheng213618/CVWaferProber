using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber.Core.Events
{
    /// <summary>
    /// VAM测试完成后触发自动导出CSV的事件
    /// </summary>
    public class VAMAutoExportCsvEvent : BaseEvent
    {
        // 携带CVCIE文件路径（用于确保导出时数据已加载）
        public string CvcieFilePath { get; set; }
    }
}
