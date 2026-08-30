using SwitchPlease.Core.Threading;
using Xunit;

namespace SwitchPlease.Core.Tests;

/// <summary>
/// The queue between the hook callback and the worker.
///
/// It was untested for as long as it lived beside the Windows code, which is the wrong way
/// round: it is the one piece where a mistake is invisible. A lost keystroke does not throw,
/// it just makes one correction quietly wrong, and a torn index would do it under load and
/// never in a debugger.
/// </summary>
public class SpscRingBufferTests
{
    [Fact]
    public void ItemsComeBackInTheOrderTheyWentIn()
    {
        var queue = new SpscRingBuffer<int>(8);

        for (int i = 0; i < 8; i++)
        {
            Assert.True(queue.TryEnqueue(i));
        }

        for (int i = 0; i < 8; i++)
        {
            Assert.True(queue.TryDequeue(out int value));
            Assert.Equal(i, value);
        }
    }

    [Fact]
    public void AnEmptyQueueReportsNothing()
    {
        var queue = new SpscRingBuffer<int>(4);

        Assert.False(queue.TryDequeue(out int value));
        Assert.Equal(0, value);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 4)]
    [InlineData(8, 8)]
    [InlineData(9, 16)]
    [InlineData(1000, 1024)]
    public void CapacityRoundsUpToAPowerOfTwo(int requested, int expected)
    {
        // The masking that makes the indices free depends on it, so a capacity that is not a
        // power of two would silently corrupt every read past the wrap.
        var queue = new SpscRingBuffer<int>(requested);

        for (int i = 0; i < expected; i++)
        {
            Assert.True(queue.TryEnqueue(i), $"item {i} of {expected} was refused");
        }

        Assert.False(queue.TryEnqueue(expected));
    }

    [Fact]
    public void OverflowDropsTheNewestAndKeepsCount()
    {
        var queue = new SpscRingBuffer<int>(4);

        for (int i = 0; i < 4; i++)
        {
            queue.TryEnqueue(i);
        }

        Assert.False(queue.TryEnqueue(99));
        Assert.False(queue.TryEnqueue(100));
        Assert.Equal(2, queue.DroppedCount);

        // What was already in is untouched: dropping the newest is what keeps the callback
        // from ever having to wait for the worker.
        for (int i = 0; i < 4; i++)
        {
            Assert.True(queue.TryDequeue(out int value));
            Assert.Equal(i, value);
        }
    }

    [Fact]
    public void IndicesKeepWorkingPastTheWrap()
    {
        var queue = new SpscRingBuffer<int>(4);

        // Ten times round a four-slot buffer: if the mask or the wrap were wrong, this is
        // where the values would start coming back shuffled.
        for (int round = 0; round < 10; round++)
        {
            for (int i = 0; i < 4; i++)
            {
                Assert.True(queue.TryEnqueue((round * 4) + i));
            }

            for (int i = 0; i < 4; i++)
            {
                Assert.True(queue.TryDequeue(out int value));
                Assert.Equal((round * 4) + i, value);
            }
        }

        Assert.Equal(0, queue.DroppedCount);
    }

    [Fact]
    public void OneProducerAndOneConsumerLoseNothing()
    {
        // The arrangement the class is actually used in. Capacity is deliberately far
        // smaller than the run, so the consumer really does have to catch up.
        const int Total = 200_000;

        var queue = new SpscRingBuffer<int>(64);
        var received = new List<int>(Total);

        var producer = new Thread(() =>
        {
            for (int i = 0; i < Total; i++)
            {
                while (!queue.TryEnqueue(i))
                {
                    Thread.SpinWait(1);
                }
            }
        });

        producer.Start();

        while (received.Count < Total)
        {
            if (queue.TryDequeue(out int value))
            {
                received.Add(value);
            }
            else
            {
                Thread.SpinWait(1);
            }
        }

        producer.Join();

        Assert.Equal(Total, received.Count);

        for (int i = 0; i < Total; i++)
        {
            Assert.Equal(i, received[i]);
        }
    }
}
