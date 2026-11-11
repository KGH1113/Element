using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Element.Application.Rendering;
using Element.Infrastructure.ElementNote.Ipc;
using Newtonsoft.Json.Linq;

namespace Element.Tests.Application.Rendering;

/// <summary>
/// What the key viewer render tests share: a target and an ElementNote channel
/// that answer like the real ones.
/// </summary>
internal static class RenderFixtures
{
  internal static readonly TimeSpan Quick = TimeSpan.FromMilliseconds(200);

  internal const string Ffmpeg = "/mods/OrbitRender/FFmpeg/ffmpeg";

  internal static JObject[] Frames(FakeChannel channel) =>
    channel
      .Snapshot()
      .Where(sent => sent.Name == KeyViewerRenderSession.FrameCommand)
      .Select(sent => sent.Payload)
      .ToArray();

  internal static object AnswerBegin(string name, JObject payload)
  {
    if (name != KeyViewerRenderSession.BeginCommand)
      return null;
    return new
    {
      ok = true,
      result = new
      {
        surfaces = payload["surfaces"]
          .Select(surface => new
          {
            viewerKind = (string)surface["viewerKind"],
            width = 400,
            height = 250,
            viewport = new
            {
              width = 400,
              height = 250,
              padding = 10,
            },
          })
          .ToArray(),
      },
    };
  }

  internal static object AnswerBeginAndFinish(string name, JObject payload) =>
    name == KeyViewerRenderSession.FinishCommand
      ? new
      {
        ok = true,
        result = new
        {
          videos = new[] { "hand", "foot" }
            .Select(kind => new
            {
              viewerKind = kind,
              path = "/render/s/" + kind + ".mov",
              width = 400,
              height = 250,
              frames = 2,
            })
            .ToArray(),
        },
      }
      : AnswerBegin(name, payload);

  internal sealed class FakeTarget : IReplayRenderTarget
  {
    public int RenderWidth { get; set; } = 1920;
    public int RenderHeight { get; set; } = 1080;
    public int Fps { get; set; } = 60;
    public bool TakesVideos { get; set; } = true;
    public string Ffmpeg { get; set; } = RenderFixtures.Ffmpeg;
    public int RenderFps => Fps;
    public bool RenderTakesOverlayVideos => TakesVideos;
    public string RenderFfmpegPath => Ffmpeg;
  }

  // Answers each command from another thread, like ElementNote's reply command.
  internal sealed class FakeChannel : IKeyViewerRenderChannel
  {
    private readonly object _gate = new();
    private int _sequence;

    public RenderReplies Replies { get; } = new();
    public List<(string Name, JObject Payload)> Sent { get; } = new();
    public bool Connected { get; set; } = true;
    public Func<string, JObject, object> Answer { get; set; } = (_, _) => null;

    public (string Name, JObject Payload)[] Snapshot()
    {
      lock (_gate)
        return Sent.ToArray();
    }

    public string Send(string name, object payload)
    {
      if (!Connected)
        return null;
      JObject json = JObject.FromObject(payload);
      string id;
      lock (_gate)
      {
        Sent.Add((name, json));
        id = "command-" + ++_sequence;
      }
      object answer = Answer(name, json);
      if (answer != null)
      {
        JObject reply = JObject.FromObject(answer);
        reply["requestId"] = id;
        var thread = new Thread(() => Replies.Post(KeyViewerRenderIpc.Parse(reply.ToString())));
        thread.Start();
      }
      return id;
    }
  }
}
