using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Sigwalk.Helpers;

/// <summary>批量添加/移除时只触发一次 Reset 通知，避免大量文件导入时 UI 卡顿。</summary>
public sealed class BulkObservableCollection<T> : ObservableCollection<T>
{
    public void AddRange(IEnumerable<T> items)
    {
        var list = items as IList<T> ?? items.ToList();
        if (list.Count == 0)
        {
            return;
        }

        CheckReentrancy();
        foreach (var item in list)
        {
            Items.Add(item);
        }

        NotifyReset();
    }

    public void RemoveRange(IEnumerable<T> items)
    {
        var set = new HashSet<T>(items);
        if (set.Count == 0)
        {
            return;
        }

        CheckReentrancy();
        for (var index = Items.Count - 1; index >= 0; index--)
        {
            if (set.Contains(Items[index]))
            {
                Items.RemoveAt(index);
            }
        }

        NotifyReset();
    }

    public void ReplaceAll(IEnumerable<T> items)
    {
        CheckReentrancy();
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        NotifyReset();
    }

    private void NotifyReset()
    {
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
