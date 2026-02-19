using System.Collections.Generic;

namespace Application.Logic;

internal class StreamBuffer<T>
{
    public int NextExpected { get; set; } = 1;
    public SortedDictionary<int, List<T>> Pending { get; } = new();
    public int? LastOrder { get; set; }
}
