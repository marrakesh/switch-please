using System.Runtime.CompilerServices;

namespace SwitchPlease.Core.Threading;

/// <summary>
/// Single-producer / single-consumer queue with no allocation and no locking.
///
/// This is the handoff between the hook callback and the worker thread, and it is the
/// reason the callback can finish in well under a microsecond: it writes one struct into a
/// pre-allocated array and advances an index. Nothing here can block, so the callback can
/// never be the thing that trips the LowLevelHooksTimeout.
///
/// Overflow drops the newest item rather than blocking. Losing a keystroke record only
/// costs the accuracy of one correction; stalling the callback would cost the whole hook.
///
/// Lives here rather than beside the hook that uses it because there is nothing about it
/// that is Windows, and the invariant it rests on -- that exactly one thread writes and
/// exactly one reads -- is the kind that is worth having tests for.
/// </summary>
public sealed class SpscRingBuffer<T>
    where T : struct
{
    private readonly T[] _items;
    private readonly int _mask;

    private long _writeIndex;
    private long _readIndex;

    /// <param name="capacity">Rounded up to the next power of two.</param>
    public SpscRingBuffer(int capacity)
    {
        int size = 1;
        while (size < capacity)
        {
            size <<= 1;
        }

        _items = new T[size];
        _mask = size - 1;
    }

    public long DroppedCount { get; private set; }

    /// <summary>
    /// How many items are waiting. Read from anywhere, so both indices are read through
    /// Volatile; the answer is a snapshot and may be stale the moment it is returned, which
    /// is all a diagnostic needs it to be.
    /// </summary>
    public int Count => (int)Math.Max(0, Volatile.Read(ref _writeIndex) - Volatile.Read(ref _readIndex));

    /// <summary>Producer side. Must only be called from the hook thread.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryEnqueue(in T item)
    {
        long write = _writeIndex;
        long read = Volatile.Read(ref _readIndex);

        if (write - read > _mask)
        {
            DroppedCount++;
            return false;
        }

        _items[write & _mask] = item;
        Volatile.Write(ref _writeIndex, write + 1);
        return true;
    }

    /// <summary>Consumer side. Must only be called from the worker thread.</summary>
    public bool TryDequeue(out T item)
    {
        long read = _readIndex;

        if (read >= Volatile.Read(ref _writeIndex))
        {
            item = default;
            return false;
        }

        item = _items[read & _mask];
        Volatile.Write(ref _readIndex, read + 1);
        return true;
    }
}
