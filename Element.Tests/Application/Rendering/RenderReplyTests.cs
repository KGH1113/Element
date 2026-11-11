using System;
using System.Linq;
using System.Threading;
using AdofaiIpc.Contracts;
using Element.Application.Rendering;
using Element.Infrastructure.ElementNote.Ipc;
using Newtonsoft.Json;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Application.Rendering;

/// <summary>
/// ElementNote's replies reach their waiter in any order, are read whole, and only
/// protocol 2 renders.
/// </summary>
internal static class RenderReplyTests
{
  internal static void Run()
  {
    RepliesMayArriveBeforeTheWaiter();
    ParsesReplies();
    SpeaksProtocolTwoOnly();
  }

  private static void RepliesMayArriveBeforeTheWaiter()
  {
    var replies = new RenderReplies();
    replies.Post(new RenderReply { RequestId = "early", Ok = true });
    Check(replies.Wait("early", TimeSpan.Zero)?.Ok == true, "A reply that beat its waiter is kept.");
    Check(replies.Wait("early", TimeSpan.Zero) == null, "A reply is taken once.");
    var thread = new Thread(() =>
    {
      Thread.Sleep(30);
      replies.Post(new RenderReply { RequestId = "late", Ok = true });
    });
    thread.Start();
    Check(replies.Wait("late", TimeSpan.FromSeconds(5)) != null, "A waiter wakes when its reply arrives.");
    thread.Join();
    for (int i = 0; i < 20; i++)
      replies.Post(new RenderReply { RequestId = "stale-" + i });
    Check(replies.Wait("stale-0", TimeSpan.Zero) == null, "Unclaimed replies do not pile up.");
    var cleared = new Thread(() =>
    {
      Thread.Sleep(30);
      replies.Clear();
    });
    cleared.Start();
    DateTime start = DateTime.UtcNow;
    Check(replies.Wait("never", TimeSpan.FromSeconds(10)) == null, "Clear releases a waiter.");
    Check(DateTime.UtcNow - start < TimeSpan.FromSeconds(5), "The waiter does not sit out its timeout.");
    cleared.Join();
  }

  private static void ParsesReplies()
  {
    RenderReply begun = KeyViewerRenderIpc.Parse(
      JsonConvert.SerializeObject(
        new
        {
          requestId = "a",
          sessionId = "s",
          ok = true,
          result = new
          {
            surfaces = new[]
            {
              new
              {
                viewerKind = "foot",
                width = 300,
                height = 200,
                viewport = new
                {
                  width = 150,
                  height = 100,
                  padding = 12.5,
                },
              },
            },
          },
        }
      )
    );
    RenderSurface surface = begun?.Surfaces?.SingleOrDefault();
    Check(surface != null && surface.IsValid() && surface.Padding == 12.5f, "Surfaces keep their viewport.");
    RenderReply finished = KeyViewerRenderIpc.Parse(
      "{\"requestId\":\"b\",\"ok\":true,\"result\":{\"videos\":[{\"viewerKind\":\"hand\",\"path\":\"/v/hand.mov\",\"width\":645,\"height\":240,\"frames\":3600}]}}"
    );
    RenderVideo video = finished?.Videos?.Single();
    Check(
      video != null && video.IsValid() && video.Path == "/v/hand.mov" && video.Width == 645 && video.Frames == 3600,
      "Videos name their file, size and length."
    );
    RenderVideo pathless = KeyViewerRenderIpc
      .Parse(
        "{\"requestId\":\"c\",\"ok\":true,\"result\":{\"videos\":[{\"viewerKind\":\"hand\",\"width\":1,\"height\":1,\"frames\":1}]}}"
      )
      ?.Videos?.Single();
    Check(pathless != null && !pathless.IsValid(), "A video without a file is not laid over the render.");
    RenderReply failed = KeyViewerRenderIpc.Parse(
      "{\"requestId\":\"d\",\"ok\":false,\"error\":{\"code\":\"render-busy\",\"message\":\"m\"}}"
    );
    Check(failed?.Ok == false && failed.ErrorCode == "render-busy", "Failures keep their code.");
    Check(KeyViewerRenderIpc.Parse("{\"ok\":true}") == null, "A reply names its request.");
    Check(KeyViewerRenderIpc.Parse("{\"requestId\":\"e\"}") == null, "A reply says whether it worked.");
    Check(KeyViewerRenderIpc.Parse("not json") == null, "Garbage is not a reply.");
  }

  // An ElementNote that answers each frame with a file (protocol 1) can't make videos.
  private static void SpeaksProtocolTwoOnly()
  {
    var older = new KeyViewerRenderIpc();
    older.OnPeerEvent(Available("old", 1));
    Check(!older.TakesRecordedCounts, "Protocol 1 is not taken.");
    var current = new KeyViewerRenderIpc();
    current.OnPeerEvent(Available("new", 2));
    Check(current.TakesRecordedCounts, "Protocol 2 is.");
    Check(!current.Available, "Without the game's IPC channel nothing is sent.");
  }

  private static PeerEvent Available(string peer, int protocol) =>
    new(
      peer,
      "connection",
      KeyViewerRenderIpc.AvailableEvent,
      "id",
      null,
      JsonConvert.SerializeObject(new { protocolVersion = protocol, recordedCounters = true })
    );
}
