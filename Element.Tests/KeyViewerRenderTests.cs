using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Element.Application.Rendering;
using Element.Configuration;
using Element.Domain.DmNote;
using Element.Domain.Layout;
using Element.Infrastructure.Ipc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Element.Tests;

internal static class KeyViewerRenderTests
{
  private static readonly TimeSpan Quick = TimeSpan.FromMilliseconds(200);

  public static void Run()
  {
    StartsWithLiveCountsAndExplicitSurfaces();
    StartsFromARunsRecordedCounts();
    WaitsForTheAppToDrawEachFrame();
    KeepsTimesMonotonicAndAfterThePageStarts();
    StopsOnTimeoutAndEndsTheSession();
    ReportsTheAppsFailure();
    FailsWithoutAnApp();
    RejectsSurfacesWithoutAViewport();
    PulsesOncePerSecond();
    RepliesMayArriveBeforeTheWaiter();
    ParsesReplies();
    PlacesViewersInTheVideo();
    CropsThePagePadding();
  }

  private static void StartsWithLiveCountsAndExplicitSurfaces()
  {
    var channel = new FakeChannel();
    var session = new KeyViewerRenderSession(channel, channel.Replies);
    Check(
      session.Start(
        new[] { new RenderSurfaceRequest("hand", 1.5f, 400, 250), new RenderSurfaceRequest("foot", 2, 210, 270) }
      ),
      "A connected app takes the session."
    );
    JObject begin = channel.Sent.Single(sent => sent.Name == KeyViewerRenderSession.BeginCommand).Payload;
    Check(
      System.Text.RegularExpressions.Regex.IsMatch((string)begin["sessionId"], "^[a-f0-9]{32}$"),
      "The id is 32 hex digits."
    );
    Check((string)begin["counterBaseline"] == "live", "The video continues the player's key counts.");
    Check((string)begin["frameFormat"] == "rgba", "Raw frames skip PNG encoding where the app can.");
    Check((long)begin["initialOutputTimeUs"] == 0, "The page starts at zero.");
    JArray surfaces = (JArray)begin["surfaces"];
    Check(surfaces.Count == 2 && (string)surfaces[1]["viewerKind"] == "foot", "Each viewer is its own surface.");
    Check(
      (float)surfaces[0]["scale"] == 1.5f && (float)surfaces[0]["surfaceWidth"] == 400,
      "Scale and viewport go along."
    );
    Check(begin["automaticPlacement"] == null, "The game places viewers itself; no desktop window is read.");
  }

  private static void StartsFromARunsRecordedCounts()
  {
    var channel = new FakeChannel();
    var session = new KeyViewerRenderSession(channel, channel.Replies);
    var recorded = new Dictionary<string, Dictionary<string, int>> { ["hand"] = new() { ["A"] = 40 } };
    session.Start(new[] { new RenderSurfaceRequest("hand", 1, 400, 250) }, recorded);
    JToken baseline = channel.Sent.Single(sent => sent.Name == KeyViewerRenderSession.BeginCommand).Payload[
      "counterBaseline"
    ];
    Check(
      (string)baseline["mode"] == "recorded" && (int)baseline["counters"]["hand"]["A"] == 40,
      "TUFReplay's render of a run counts on from the key counts the run was recorded at."
    );
  }

  private static void WaitsForTheAppToDrawEachFrame()
  {
    var channel = new FakeChannel { Answer = AnswerFrames };
    var session = new KeyViewerRenderSession(channel, channel.Replies);
    session.Start(new[] { new RenderSurfaceRequest("hand", 1, 400, 250) });
    IReadOnlyList<RenderFrame> first = session.Capture(0, Quick, Quick);
    Check(
      first?.Count == 1 && first[0].FramePath == "/frames/hand/0",
      "The first frame waits for begin, then its frame."
    );
    Check(session.Surfaces?.Count == 1 && session.Surfaces[0].Padding == 10, "The page's own viewport is kept.");
    IReadOnlyList<RenderFrame> second = session.Capture(166_666, Quick, Quick);
    Check(second?[0].FramePath == "/frames/hand/1", "Frames count up.");
    long[] indexes = channel
      .Sent.Where(sent => sent.Name == KeyViewerRenderSession.FrameCommand)
      .Select(sent => (long)sent.Payload["frameIndex"])
      .ToArray();
    Check(indexes.SequenceEqual(new long[] { 0, 1 }), "ElementNote requires frames in order from zero.");
    Check(session.Failure == null && session.Running, "A healthy render keeps running.");
  }

  private static void KeepsTimesMonotonicAndAfterThePageStarts()
  {
    var channel = new FakeChannel { Answer = AnswerFrames };
    var session = new KeyViewerRenderSession(channel, channel.Replies);
    session.Start(new[] { new RenderSurfaceRequest("hand", 1, 400, 250) });
    session.Input("Z", true, 5_000_000);
    session.Input("X", true, 2_000_000);
    session.Input("Z", false, 9_000_000);
    session.Capture(10_000_000, Quick, Quick);
    session.Input("X", false, 1_000);
    session.Capture(5_000_000, Quick, Quick);
    JObject[] frames = channel
      .Sent.Where(sent => sent.Name == KeyViewerRenderSession.FrameCommand)
      .Select(sent => sent.Payload)
      .ToArray();
    long firstTime = (long)frames[0]["outputTimeUs"];
    Check(firstTime == KeyViewerRenderSession.TimeOffsetUs + 1_000_000, "Ticks are 100 ns; times start one second in.");
    long[] eventTimes = frames[0]["events"].Select(e => (long)e["outputTimeUs"]).ToArray();
    Check(
      eventTimes.SequenceEqual(new[] { 1_500_000L, 1_500_000, 1_900_000 }),
      "Keys never go back in time: " + string.Join(",", eventTimes)
    );
    Check(
      frames[0]["events"].Select(e => (int)e["sequence"]).SequenceEqual(new[] { 0, 1, 2 }),
      "Keys keep their order."
    );
    Check((long)frames[1]["outputTimeUs"] == firstTime, "A frame never precedes the last one.");
    Check((long)frames[1]["events"][0]["outputTimeUs"] == firstTime, "A late key lands on the frame, not before it.");
    Check(!frames[1]["events"].Any(e => (string)e["key"] == "Z"), "Sent keys are not sent again.");
  }

  private static void StopsOnTimeoutAndEndsTheSession()
  {
    var channel = new FakeChannel { Answer = (_, _) => null };
    var session = new KeyViewerRenderSession(channel, channel.Replies);
    session.Start(new[] { new RenderSurfaceRequest("hand", 1, 400, 250) });
    Check(session.Capture(0, Quick, Quick) == null, "No answer, no frame.");
    Check(
      session.Failure?.Code == RenderFailure.Timeout && !session.Failure.AfterFirstFrame,
      "A silent app times out."
    );
    Check(!session.Running, "The session closes.");
    Check(channel.Sent.Last().Name == KeyViewerRenderSession.EndCommand, "ElementNote is told to give input back.");
    int sent = channel.Sent.Count;
    Check(
      session.Capture(0, Quick, Quick) == null && channel.Sent.Count == sent,
      "Later frames neither wait nor send."
    );
  }

  private static void ReportsTheAppsFailure()
  {
    var channel = new FakeChannel
    {
      Answer = (name, payload) =>
        name == KeyViewerRenderSession.BeginCommand
          ? AnswerFrames(name, payload)
          : new { ok = false, error = new { code = "render-frame-timeout", message = "late" } },
    };
    var session = new KeyViewerRenderSession(channel, channel.Replies);
    session.Start(new[] { new RenderSurfaceRequest("hand", 1, 400, 250) });
    Check(session.Capture(0, Quick, Quick) == null, "A failed frame returns nothing.");
    Check(session.Failure?.Code == "render-frame-timeout", "The app's code is kept for the notice.");
  }

  private static void FailsWithoutAnApp()
  {
    var channel = new FakeChannel { Connected = false };
    var session = new KeyViewerRenderSession(channel, channel.Replies);
    Check(!session.Start(new[] { new RenderSurfaceRequest("hand", 1, 400, 250) }), "No app, no session.");
    Check(session.Failure?.Code == RenderFailure.AppUnavailable, "The notice says to open ElementNote.");
    Check(
      !session.Start(Array.Empty<RenderSurfaceRequest>()) && session.Failure == null,
      "No viewers is not a failure."
    );
  }

  private static void RejectsSurfacesWithoutAViewport()
  {
    var channel = new FakeChannel
    {
      Answer = (name, _) =>
        name == KeyViewerRenderSession.BeginCommand
          ? new
          {
            ok = true,
            result = new
            {
              surfaces = new[]
              {
                new
                {
                  viewerKind = "hand",
                  width = 400,
                  height = 250,
                  frameFormat = "rgba",
                },
              },
            },
          }
          : null,
    };
    var session = new KeyViewerRenderSession(channel, channel.Replies);
    session.Start(new[] { new RenderSurfaceRequest("hand", 1, 400, 250) });
    Check(session.Capture(0, Quick, Quick) == null, "An older app's surfaces cannot be cropped or placed.");
    Check(session.Failure?.Code == RenderFailure.Incompatible, "The notice says to update ElementNote.");
  }

  private static void PulsesOncePerSecond()
  {
    var channel = new FakeChannel();
    var session = new KeyViewerRenderSession(channel, channel.Replies);
    DateTime now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    session.Pulse(now);
    Check(channel.Sent.Count == 0, "No session, no pulse.");
    session.Start(new[] { new RenderSurfaceRequest("hand", 1, 400, 250) });
    for (int frame = 0; frame < 120; frame++)
      session.Pulse(now.AddMilliseconds(frame * 16.7));
    Check(
      channel.Sent.Count(sent => sent.Name == KeyViewerRenderSession.PulseCommand) == 2,
      "One pulse a second, not one a frame."
    );
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
                frameFormat = "png",
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
    Check(
      surface != null && surface.IsValid() && surface.Padding == 12.5f && surface.FrameFormat == "png",
      "Surfaces keep their viewport."
    );
    RenderReply frame = KeyViewerRenderIpc.Parse(
      "{\"requestId\":\"b\",\"ok\":true,\"result\":{\"frames\":[{\"viewerKind\":\"hand\",\"framePath\":\"/f\"}]}}"
    );
    Check(frame?.Frames?.Single().FramePath == "/f", "Frames name their file.");
    RenderReply failed = KeyViewerRenderIpc.Parse(
      "{\"requestId\":\"c\",\"ok\":false,\"error\":{\"code\":\"render-busy\",\"message\":\"m\"}}"
    );
    Check(failed?.Ok == false && failed.ErrorCode == "render-busy", "Failures keep their code.");
    Check(KeyViewerRenderIpc.Parse("{\"ok\":true}") == null, "A reply names its request.");
    Check(KeyViewerRenderIpc.Parse("{\"requestId\":\"d\"}") == null, "A reply says whether it worked.");
    Check(KeyViewerRenderIpc.Parse("not json") == null, "Garbage is not a reply.");
  }

  private static void PlacesViewersInTheVideo()
  {
    Near(2, RenderViewerPlacement.VideoScale(960, 540, 1920, 1080), "A video twice the game view doubles everything.");
    // A 200x100 viewer 16 px from the bottom left of a 960x540 game view.
    var screen = new LayoutBounds(16, 540 - 100 - 16, 200, 100);
    OverlayLayoutSettings anchor = NativeViewerGrowth.Capture(screen, 960, 540, 1, Anchor9.BottomLeft);
    RenderViewerPlacement placement = RenderViewerPlacement.Create(
      "hand",
      screen,
      960,
      540,
      2,
      anchor,
      120,
      70,
      1920,
      1080
    );
    Near(4, placement.Scale, "Video pixels per CSS pixel: game pixels per CSS pixel times the video scale.");
    LayoutBounds placed = placement.Place(100, 50);
    Near(400f / 1920, placed.Width, "Content width in CSS pixels times the scale.");
    Near(32f / 1920, placed.X, "The anchored left margin doubles with the video.");
    Near(1 - 32f / 1080, placed.Y + placed.Height, "The bottom margin doubles too.");
    // A wider video keeps the corner; margins and size grow by the same factor.
    float k = RenderViewerPlacement.VideoScale(960, 540, 2560, 1080);
    RenderViewerPlacement wide = RenderViewerPlacement.Create("hand", screen, 960, 540, 2, anchor, 120, 70, 2560, 1080);
    LayoutBounds wider = wide.Place(100, 50);
    Near(16 * k / 2560, wider.X, "The left margin scales with the video.");
    Near(1 - 16 * k / 1080, wider.Y + wider.Height, "The bottom margin scales the same way.");
    Near(100 * 2 * k / 2560, wider.Width, "So does the viewer.");
    RenderViewerPlacement centered = RenderViewerPlacement.Create(
      "foot",
      screen,
      960,
      540,
      2,
      null,
      120,
      70,
      1920,
      1080
    );
    LayoutBounds middle = centered.Place(100, 50);
    Near((16 + 100f) / 960, middle.X + middle.Width / 2, "Without an anchor, the center stays.");
    Check(
      RenderViewerPlacement.Create("hand", screen, 0, 540, 2, anchor, 120, 70, 1920, 1080) == null,
      "No game view, no placement."
    );
  }

  private static void CropsThePagePadding()
  {
    (float x, float y, float width, float height) = RenderViewerPlacement.ContentUv(200, 100, 10, false);
    Near(.05f, x, "Left padding.");
    Near(.1f, y, "Bottom padding.");
    Near(.9f, width, "Content width.");
    Near(.8f, height, "Content height.");
    (float _, float flippedY, float _, float flippedHeight) = RenderViewerPlacement.ContentUv(200, 100, 10, true);
    Near(.9f, flippedY, "Raw rows start at the top, so the image samples from the far edge.");
    Near(-.8f, flippedHeight, "Flipped content has negative height.");
  }

  private static object AnswerFrames(string name, JObject payload)
  {
    if (name == KeyViewerRenderSession.BeginCommand)
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
              frameFormat = "rgba",
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
    if (name == KeyViewerRenderSession.FrameCommand)
      return new
      {
        ok = true,
        result = new
        {
          frames = new[] { new { viewerKind = "hand", framePath = "/frames/hand/" + (long)payload["frameIndex"] } },
        },
      };
    return null;
  }

  private static void Near(float expected, float actual, string message) =>
    Check(Math.Abs(expected - actual) < 1e-4f, message + " Expected " + expected + ", got " + actual + ".");

  private static void Check(bool value, string message)
  {
    if (!value)
      throw new InvalidOperationException(message);
  }

  // Answers each command from another thread, like ElementNote's reply command.
  private sealed class FakeChannel : IKeyViewerRenderChannel
  {
    private int _sequence;

    public RenderReplies Replies { get; } = new();
    public List<(string Name, JObject Payload)> Sent { get; } = new();
    public bool Connected { get; set; } = true;
    public Func<string, JObject, object> Answer { get; set; } = (_, _) => null;

    public string Send(string name, object payload)
    {
      if (!Connected)
        return null;
      JObject json = JObject.FromObject(payload);
      Sent.Add((name, json));
      string id = "command-" + ++_sequence;
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
