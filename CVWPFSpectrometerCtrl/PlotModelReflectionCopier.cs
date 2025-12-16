using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CVWPFSpectrometerCtrl
{
    public static class PlotModelReflectionCopier
    {
        // 标记需要深度复制的OxyPlot核心类型
        private static readonly Type[] DeepCopyTypes = new[]
        {
            typeof(PlotModel), typeof(Axis), typeof(Series), typeof(Annotation),
            typeof(LineSeries), typeof(LinearAxis), typeof(TextAnnotation),
            typeof(DataPoint), typeof(ScatterPoint)
        };

        /// <summary>
        /// 反射深度复制PlotModel（自动复制所有属性和子对象）
        /// </summary>
        /// <param name="source">源PlotModel</param>
        /// <returns>全新的复制后的PlotModel</returns>
        public static PlotModel DeepCopy(this PlotModel source)
        {
            if (source == null) return null;

            // 创建空的目标PlotModel
            var target = new PlotModel();

            // 反射复制所有可写属性
            CopyObjectProperties(source, target);

            return target;
        }

        /// <summary>
        /// 反射复制对象的所有属性（递归处理子对象）
        /// </summary>
        /// <param name="source">源对象</param>
        /// <param name="target">目标对象</param>
        private static void CopyObjectProperties(object source, object target)
        {
            if (source == null || target == null) return;

            var sourceType = source.GetType();
            var targetType = target.GetType();

            // 获取所有可写的公共属性（排除索引器）
            var properties = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && !p.GetIndexParameters().Any());

            foreach (var prop in properties)
            {
                try
                {
                    var sourceValue = prop.GetValue(source);
                    if (sourceValue == null) continue;

                    // 根据属性类型处理复制逻辑
                    object targetValue = GetCopiedValue(sourceValue);

                    // 设置目标对象属性值
                    var targetProp = targetType.GetProperty(prop.Name, BindingFlags.Public | BindingFlags.Instance);
                    targetProp?.SetValue(target, targetValue);
                }
                catch (Exception ex)
                {
                    // 忽略无法复制的属性（如只读/内部属性）
                    Console.WriteLine($"复制属性 {prop.Name} 失败：{ex.Message}");
                }
            }
        }

        /// <summary>
        /// 根据值类型获取复制后的值（深度复制引用类型）
        /// </summary>
        /// <param name="sourceValue">源值</param>
        /// <returns>复制后的值</returns>
        private static object GetCopiedValue(object sourceValue)
        {
            var valueType = sourceValue.GetType();

            // 1. 值类型/字符串：直接返回（值类型自动复制）
            if (valueType.IsValueType || valueType == typeof(string))
            {
                return sourceValue;
            }

            // 2. 集合类型：递归复制每个元素
            if (sourceValue is IList list)
            {
                var listType = valueType;
                var itemType = listType.IsGenericType ? listType.GetGenericArguments()[0] : typeof(object);

                // 创建新集合
                var newList = (IList)Activator.CreateInstance(listType);

                foreach (var item in list)
                {
                    if (item == null) continue;
                    // 递归复制集合中的每个元素
                    var newItem = GetCopiedValue(item);
                    newList.Add(newItem);
                }
                return newList;
            }

            // 3. OxyPlot核心类型：深度复制
            if (DeepCopyTypes.Any(t => t.IsAssignableFrom(valueType)))
            {
                // 创建同类型的新对象
                var newInstance = Activator.CreateInstance(valueType);
                // 递归复制该对象的所有属性
                CopyObjectProperties(sourceValue, newInstance);
                return newInstance;
            }

            // 4. 其他引用类型：浅复制（如需深度复制可扩展）
            return sourceValue;
        }

        /// <summary>
        /// 简化版：将源PlotModel复制到已有目标PlotModel
        /// </summary>
        public static void CopyTo(this PlotModel source, PlotModel target)
        {
            if (source == null || target == null) return;

            // 清空目标原有内容（避免叠加）
            target.Series.Clear();
            target.Axes.Clear();
            target.Annotations.Clear();
            target.Legends.Clear();

            // 反射复制所有属性
            CopyObjectProperties(source, target);

            // 强制刷新PlotModel
            target.InvalidatePlot(true);
        }
    }
}
