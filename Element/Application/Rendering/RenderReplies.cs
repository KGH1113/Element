using System;
using System.Collections.Generic;
using System.Threading;

namespace Element.Application.Rendering;

/// <summary>
/// ElementNote's replies, posted on an IPC worker thread and taken by the game
/// thread that waits for them. A reply may arrive before its waiter starts.
/// </summary>
internal sealed class RenderReplies
{
  // Only one command waits at a time; older unclaimed replies are stale.
  private const int Capacity = 8;

  private readonly object _gate = new();
  private readonly Dictionary<string, RenderReply> _replies = new();
  private readonly Queue<string> _order = new();
  private int _generation;

  public void Post(RenderReply reply)
  {
    if (reply?.RequestId == null)
      return;
    lock (_gate)
    {
      if (!_replies.ContainsKey(reply.RequestId))
        _order.Enqueue(reply.RequestId);
      _replies[reply.RequestId] = reply;
      while (_order.Count > Capacity)
        _replies.Remove(_order.Dequeue());
      Monitor.PulseAll(_gate);
    }
  }

  /// <summary>The reply to <paramref name="requestId"/>, or null on timeout or <see cref="Clear"/>.</summary>
  public RenderReply Wait(string requestId, TimeSpan timeout)
  {
    if (requestId == null)
      return null;
    // TickCount, not DateTime or Stopwatch: during a replay render OrbitRender
    // may turn those into video time, which stands still while this waits.
    int start = Environment.TickCount;
    int limit = (int)Math.Max(0, Math.Min(int.MaxValue, timeout.TotalMilliseconds));
    lock (_gate)
    {
      int generation = _generation;
      while (true)
      {
        if (_replies.TryGetValue(requestId, out RenderReply reply))
        {
          _replies.Remove(requestId);
          return reply;
        }
        int remaining = limit - unchecked(Environment.TickCount - start);
        if (remaining <= 0 || generation != _generation)
          return null;
        Monitor.Wait(_gate, remaining);
      }
    }
  }

  /// <summary>Drops every reply and releases a waiter, such as when a session ends.</summary>
  public void Clear()
  {
    lock (_gate)
    {
      _replies.Clear();
      _order.Clear();
      _generation++;
      Monitor.PulseAll(_gate);
    }
  }
}
