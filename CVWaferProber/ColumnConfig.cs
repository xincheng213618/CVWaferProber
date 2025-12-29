using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVWaferProber
{
    // 列配置模型（适配DieViewModel的属性）
    public class ColumnConfig
    {
        /// <summary>
        /// 列头显示文本
        /// </summary>
        public string ColumnHeader { get; set; }

        /// <summary>
        /// 绑定的DieViewModel属性名
        /// </summary>
        public string ColumnBindingPath { get; set; }

        /// <summary>
        /// 是否选中（显示/导出）
        /// </summary>
        public bool IsSelected { get; set; }

        /// <summary>
        /// 是否为可选列（默认列不可取消）
        /// </summary>
        public bool IsOptional { get; set; }

        /// <summary>
        /// 列类型（Text/CheckBox）
        /// </summary>
        public ColumnType ColumnType { get; set; }
        public ColumnKey ColumnKey { get; set; }
    }

    /// <summary>
    /// 定义列类型枚举
    /// </summary>
    public enum ColumnKey
    {
        AOI,
        IVL,
        EQE,
        VAM,
        Other
    }
    /// <summary>
    /// 列类型枚举
    /// </summary>
    public enum ColumnType
    {
        Text,
        CheckBox
    }
}
