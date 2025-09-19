using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace CVWaferProber.Core.ViewModels
{
    public class RangeEnabledObservableCollection<T> : ObservableCollection<T>
    {
        /// <summary>
        /// 批量添加元素，仅触发一次CollectionChanged事件
        /// </summary>
        /// <param name="items">待添加的元素集合</param>
        public void InsertRange(IEnumerable<T> items)
        {
            this.CheckReentrancy(); // 检查是否在批量操作中（防止递归调用）

            foreach (var item in items)
            {
                this.Items.Add(item); // 向底层集合添加元素（无通知）
            }

            FireCollectionChanged();
        }

        public void FireCollectionChanged()
        {
            // 触发一次重置通知（告知UI集合整体变更）
            this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

    }
}
