namespace ProcessOpenStreetMap;

internal sealed class MinHeap
{
    private readonly List<((int origin, int destination) link,
        (int parentOrigin, int parentDestination) parentLink, float cost)> _data
        = new();
    private HashSet<(int origin, int destination)> _contained = new();

    public int Count => _data.Count;

    public void Reset()
    {
        _contained.Clear();
        _data.Clear();
    }

    public ((int origin, int destination) link,
        (int parentOrigin, int parentDestination) parentLink, float cost) PopMin()
    {
        var tailIndex = _data.Count - 1;
        if (tailIndex < 0)
        {
            return ((-1, -1), (-1, -1), -1);
        }
        var top = _data[0];
        var last = _data[tailIndex];
        _data[0] = last;
        var current = 0;
        while (current < _data.Count)
        {
            var childrenIndex = (current << 1) + 1;
            if (childrenIndex + 1 < _data.Count)
            {
                if (_data[childrenIndex].cost < _data[current].cost)
                {
                    _data[current] = _data[childrenIndex];
                    _data[childrenIndex] = last;
                    current = childrenIndex;
                    continue;
                }
                if (_data[childrenIndex + 1].cost < _data[current].cost)
                {
                    _data[current] = _data[childrenIndex + 1];
                    _data[childrenIndex + 1] = last;
                    current = childrenIndex + 1;
                    continue;
                }
            }
            else if (childrenIndex < _data.Count)
            {
                if (_data[childrenIndex].cost < _data[current].cost)
                {
                    _data[current] = _data[childrenIndex];
                    _data[childrenIndex] = last;
                    current = childrenIndex;
                    continue;
                }
            }
            break;
        }
        _contained.Remove(top.link);
        _data.RemoveAt(_data.Count - 1);
        return (top.link, top.parentLink, top.cost);
    }

    public void Push((int origin, int destination) link, (int parentOrigin, int parentDestination) parentLink, float cost)
    {
        int current = _data.Count;
        if (_contained.Contains(link))
        {
            for (current = 0; current < _data.Count; current++)
            {
                if (_data[current].link == link)
                {
                    // if we found a better path to this node
                    if (_data[current].cost > cost)
                    {
                        var temp = _data[current];
                        temp.parentLink = parentLink;
                        temp.cost = cost;
                        _data[current] = temp;
                        break;
                    }
                    else
                    {
                        // if the contained child is already better ignore the request
                        return;
                    }
                }
            }
        }
        if (current == _data.Count)
        {
            // if it is not already contained
            _data.Add((link, parentLink, cost));
            _contained.Add(link);
        }
        // we don't need to check the root
        while (current >= 1)
        {
            var parentIndex = current >> 1;
            var parent = _data[parentIndex];
            if (parent.cost <= _data[current].cost)
            {
                break;
            }
            _data[parentIndex] = _data[current];
            _data[current] = parent;
            current = parentIndex;
        }
    }
}
