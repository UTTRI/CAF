namespace ProcessOpenStreetMap;

internal sealed class MinHeap
{
    private readonly List<(int Destination, int Origin, float Cost)> _data
        = new();
    private HashSet<int> _contained = new();

    public int Count => _data.Count;

    public void Reset()
    {
        _contained.Clear();
        _data.Clear();
    }

    public (int Destination, int Origin, float Cost) PopMin()
    {
        var tailIndex = _data.Count - 1;
        if (tailIndex < 0)
        {
            return (-1, -1, -1);
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
                if (_data[childrenIndex].Cost < _data[current].Cost)
                {
                    _data[current] = _data[childrenIndex];
                    _data[childrenIndex] = last;
                    current = childrenIndex;
                    continue;
                }
                if (_data[childrenIndex + 1].Cost < _data[current].Cost)
                {
                    _data[current] = _data[childrenIndex + 1];
                    _data[childrenIndex + 1] = last;
                    current = childrenIndex + 1;
                    continue;
                }
            }
            else if (childrenIndex < _data.Count)
            {
                if (_data[childrenIndex].Cost < _data[current].Cost)
                {
                    _data[current] = _data[childrenIndex];
                    _data[childrenIndex] = last;
                    current = childrenIndex;
                    continue;
                }
            }
            break;
        }
        _contained.Remove(top.Destination);
        _data.RemoveAt(_data.Count - 1);
        return (top.Destination, top.Origin, top.Cost);
    }

    public void Push(int destination, int origin, float cost)
    {
        int current = _data.Count;
        if (_contained.Contains(destination))
        {
            for (current = 0; current < _data.Count; current++)
            {
                if (_data[current].Destination == destination)
                {
                    // if we found a better path to this node
                    if (_data[current].Cost > cost)
                    {
                        var temp = _data[current];
                        temp.Origin = origin;
                        temp.Cost = cost;
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
            _data.Add((destination, origin, cost));
            _contained.Add(destination);
        }
        // we don't need to check the root
        while (current >= 1)
        {
            var parentIndex = current >> 1;
            var parent = _data[parentIndex];
            if (parent.Cost <= _data[current].Cost)
            {
                break;
            }
            _data[parentIndex] = _data[current];
            _data[current] = parent;
            current = parentIndex;
        }
    }
}
