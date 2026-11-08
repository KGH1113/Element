using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using AdofaiIpc.Contracts;
using Element.Application.Rendering;
using Element.Configuration;
using Element.Domain.DmNote;
using Element.Domain.Layout;
using Element.Infrastructure.Ipc;
using Element.Presentation.Rendering;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Element.Tests;

internal static class KeyViewerRenderTests
{
  private static readonly TimeSpan Quick = TimeSpan.FromMilliseconds(200);
  private const string Ffmpeg = "/mods/OrbitRender/FFmpeg/ffmpeg";

  public static void Run()
  {
    StartsWithLiveCountsAndExplicitSurfaces();
    StartsFromARunsRecordedCounts();
    SendsFramesWithoutWaitingForThem();
    NumbersFramesByVideoTime();
    KeepsTimesMonotonicAndAfterThePageStarts();
    StopsWhenBeginTimesOut();
    ReportsTheAppsFailure();
    FailsWithoutAnApp();
    RejectsSurfacesWithoutAViewport();
    FinishesWithAVideoPerPage();
    RejectsFinishWithoutEveryVideo();
    AllowsSlowFinishes();
    PulsesOncePerSecond();
    PulsesFromATimerThread();
    RepliesMayArriveBeforeTheWaiter();
    ParsesReplies();
    SpeaksProtocolTwoOnly();
    PlacesViewersInTheVideo();
    CropsThePagePadding();
    MapsAFrameTexelForPixel();
    OverlaysEveryViewerOnItsPage();
    NeedsARendererThatTakesVideos();
    ExplainsWhyKeyViewersAreMissing();
  }

  private static void StartsWithLiveCountsAndExplicitSurfaces()
  {
    var channel = new FakeChannel();
    var session = new KeyViewerRenderSession(channel, channel.Replies);
    Check(
      session.Start(
        new[] { new RenderSurfaceRequest("hand", 1.5f, 400, 250), new RenderSurfaceRequest("foot", 2, 210, 270) },
        60,
        Ffmpeg
      ),
      "A connected app takes the session."
    );
    JObject begin = channel.Sent.Single(sent => sent.Name == KeyViewerRenderSession.BeginCommand).Payload;
    Check(
      System.Text.RegularExpressions.Regex.IsMatch((string)begin["sessionId"], "^[a-f0-9]{32}$"),
      "The id is 32 hex digits."
    );
    Check((string)begin["counterBaseline"] == "live", "The video continues the player's key counts.");
    Check((int)begin["fps"] == 60, "The videos match the render's frame rate.");
    Check((string)begin["ffmpegPath"] == Ffmpeg, "ElementNote encodes with the FFmpeg TUFReplay shares.");
    Check(begin["frameFormat"] == null, "ElementNote picks its capture format itself.");
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
    session.Start(new[] { new RenderSurfaceRequest("hand", 1, 400, 250) }, 60, Ffmpeg, recorded);
    JToken baseline = channel.Sent.Single(sent => sent.Name == KeyViewerRenderSession.BeginCommand).Payload[
      "counterBaseline"
    ];
    Check(
      (string)baseline["mode"] == "recorded" && (int)baseline["counters"]["hand"]["A"] == 40,
      "TUFReplay's render of a run counts on from the key counts the run was recorded at."
    );
  }

  // Frames get no answer; a session that waited for one would sit out its timeout.
  private static void SendsFramesWithoutWaitingForThem()
  {
    var channel = new FakeChannel { Answer = AnswerBegin };
    var session = new KeyViewerRenderSession(channel, channel.Replies);
    session.Start(new[] { new RenderSurfaceRequest("hand", 1, 400, 250) }, 60, Ffmpeg);
    Check(session.SendFrame(0, Quick), "The first frame waits for begin, then goes.");
    Check(session.Surfaces?.Count == 1 && session.Surfaces[0].Padding == 10, "The page's own viewport is kept.");
    var watch = Stopwatch.StartNew();
    for (int frame = 1; frame <= 50; frame++)
      Check(session.SendFrame(frame * 166_667L, TimeSpan.FromSeconds(10)), "Later frames go out too.");
    Check(watch.Elapsed < TimeSpan.FromSeconds(1), "Frames never wait for ElementNote to draw them.");
    Check(
      channel.Sent.Count(sent => sent.Name == KeyViewerRenderSession.FrameCommand) == 51,
      "Every video frame is sent."
    );
    Check(session.Failure == null && session.Running, "A healthy render keeps running.");
    Check(session.FramesSpanned == 51, "The video spans every frame sent.");
  }

  private static void NumbersFramesByVideoTime()
  {
    Check(KeyViewerRenderSession.FrameIndex(0, 60) == 0, "The video's first frame is frame zero.");
    Check(KeyViewerRenderSession.FrameIndex(166_667, 60) == 1, "One sixtieth of a second is frame one.");
    Check(KeyViewerRenderSession.FrameIndex(10_000_000, 60) == 60, "One second is sixty frames.");
    Check(KeyViewerRenderSession.FrameIndex(10_000_000, 24) == 24, "The rate is the video's.");
    // 144 Hz simulation sampled for 60 fps video: frame 7 at 7/60 s comes from 17/144 s.
    Check(
      KeyViewerRenderSession.FrameIndex(17 * 10_000_000L / 144, 60) == 7,
      "A nearby game frame rounds to its video frame."
    );
    Check(KeyViewerRenderSession.FrameIndex(-5, 60) == 0, "Time before the video is its first frame.");

    var channel = new FakeChannel { Answer = AnswerBegin };
    var session = new KeyViewerRenderSession(channel, channel.Replies);
    session.Start(new[] { new RenderSurfaceRequest("hand", 1, 400, 250) }, 60, Ffmpeg);
    session.SendFrame(0, Quick);
    session.Input("Z", true, 100_000);
    // Still output frame 0: nothing is sent and the key waits for the next frame.
    session.SendFrame(10_000, Quick);
    session.SendFrame(3 * 166_667L, Quick);
    JObject[] frames = Frames(channel);
    Check(
      frames.Select(frame => (long)frame["frameIndex"]).SequenceEqual(new long[] { 0, 3 }),
      "Indices follow the video and may skip."
    );
    Check(frames[1]["events"].Single()["key"].Value<string>() == "Z", "A key held back goes with the next frame.");
    Check(session.FramesSpanned == 4, "Skipped frames still count toward the video's length.");
  }

  private static void KeepsTimesMonotonicAndAfterThePageStarts()
  {
    var channel = new FakeChannel { Answer = AnswerBegin };
    var session = new KeyViewerRenderSession(channel, channel.Replies);
    session.Start(new[] { new RenderSurfaceRequest("hand", 1, 400, 250) }, 60, Ffmpeg);
    session.Input("Z", true, 5_000_000);
    session.Input("X", true, 2_000_000);
    session.Input("Z", false, 9_000_000);
    session.SendFrame(10_000_000, Quick);
    session.Input("X", false, 1_000);
    // Time that went back still makes a later video frame, never an earlier time.
    session.SendFrame(10_200_000, Quick);
    JObject[] frames = Frames(channel);
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
    Check(
      (long)frames[1]["events"][0]["outputTimeUs"] == firstTime,
      "A late key lands on the last frame, not before it."
    );
    Check(!frames[1]["events"].Any(e => (string)e["key"] == "Z"), "Sent keys are not sent again.");
  }

  private static void StopsWhenBeginTimesOut()
  {
    var channel = new FakeChannel { Answer = (_, _) => null };
    var session = new KeyViewerRenderSession(channel, channel.Replies);
    session.Start(new[] { new RenderSurfaceRequest("hand", 1, 400, 250) }, 60, Ffmpeg);
    Check(!session.SendFrame(0, Quick), "No begin, no frame.");
    Check(session.Failure?.Code == RenderFailure.Timeout, "A silent app times out.");
    Check(!session.Running, "The session closes.");
    Check(channel.Sent.Last().Name == KeyViewerRenderSession.EndCommand, "ElementNote is told to give input back.");
    int sent = channel.Sent.Count;
    Check(
      !session.SendFrame(0, Quick) && session.Finish(Quick) == null && channel.Sent.Count == sent,
      "Later frames and finish neither wait nor send."
    );
  }

  private static void ReportsTheAppsFailure()
  {
    var channel = new FakeChannel
    {
      Answer = (name, payload) =>
        name == KeyViewerRenderSession.BeginCommand
          ? AnswerBegin(name, payload)
          : new { ok = false, error = new { code = "render-ffmpeg-failed", message = "exit 1" } },
    };
    var session = new KeyViewerRenderSession(channel, channel.Replies);
    session.Start(new[] { new RenderSurfaceRequest("hand", 1, 400, 250) }, 60, Ffmpeg);
    session.SendFrame(0, Quick);
    Check(session.Finish(Quick) == null, "A failed finish has no videos.");
    Check(session.Failure?.Code == "render-ffmpeg-failed", "The app's code is kept for the notice.");
    Check(channel.Sent.Last().Name == KeyViewerRenderSession.EndCommand, "The session ends.");
  }

  private static void FailsWithoutAnApp()
  {
    var channel = new FakeChannel { Connected = false };
    var session = new KeyViewerRenderSession(channel, channel.Replies);
    Check(!session.Start(new[] { new RenderSurfaceRequest("hand", 1, 400, 250) }, 60, Ffmpeg), "No app, no session.");
    Check(session.Failure?.Code == RenderFailure.AppUnavailable, "The notice says to open ElementNote.");
    Check(
      !session.Start(Array.Empty<RenderSurfaceRequest>(), 60, Ffmpeg) && session.Failure == null,
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
                },
              },
            },
          }
          : null,
    };
    var session = new KeyViewerRenderSession(channel, channel.Replies);
    session.Start(new[] { new RenderSurfaceRequest("hand", 1, 400, 250) }, 60, Ffmpeg);
    Check(!session.SendFrame(0, Quick), "An older app's surfaces cannot be cropped or placed.");
    Check(session.Failure?.Code == RenderFailure.Incompatible, "The notice says to update ElementNote.");
  }

  private static void FinishesWithAVideoPerPage()
  {
    var channel = new FakeChannel { Answer = AnswerBeginAndFinish };
    var session = new KeyViewerRenderSession(channel, channel.Replies);
    session.Start(
      new[] { new RenderSurfaceRequest("hand", 1, 400, 250), new RenderSurfaceRequest("foot", 1, 300, 200) },
      60,
      Ffmpeg
    );
    Check(
      session.Finish(Quick) == null && session.Failure == null && session.Running,
      "Nothing sent, nothing to finish."
    );
    session.SendFrame(0, Quick);
    session.SendFrame(166_667, Quick);
    IReadOnlyList<RenderVideo> videos = session.Finish(Quick);
    Check(videos?.Count == 2, "One video per page.");
    Check(videos.Single(video => video.ViewerKind == "foot").Path == "/render/s/foot.mov", "Each names its file.");
    JObject finish = channel.Sent.Single(sent => sent.Name == KeyViewerRenderSession.FinishCommand).Payload;
    Check((string)finish["sessionId"] == (string)channel.Sent[0].Payload["sessionId"], "Finish names the session.");
    Check(session.Running, "The session stays open so the renderer can read the videos.");
    session.Stop();
    Check(channel.Sent.Last().Name == KeyViewerRenderSession.EndCommand, "Stop lets ElementNote delete them.");
  }

  private static void RejectsFinishWithoutEveryVideo()
  {
    var channel = new FakeChannel
    {
      Answer = (name, payload) =>
        name == KeyViewerRenderSession.FinishCommand
          ? new
          {
            ok = true,
            result = new
            {
              videos = new[]
              {
                new
                {
                  viewerKind = "hand",
                  path = "/render/s/hand.mov",
                  width = 400,
                  height = 250,
                  frames = 1,
                },
              },
            },
          }
          : AnswerBegin(name, payload),
    };
    var session = new KeyViewerRenderSession(channel, channel.Replies);
    session.Start(
      new[] { new RenderSurfaceRequest("hand", 1, 400, 250), new RenderSurfaceRequest("foot", 1, 300, 200) },
      60,
      Ffmpeg
    );
    session.SendFrame(0, Quick);
    Check(session.Finish(Quick) == null, "A missing page video drops the key viewers.");
    Check(session.Failure?.Code == RenderFailure.Incompatible, "The notice says to update ElementNote.");
  }

  private static void AllowsSlowFinishes()
  {
    Check(KeyViewerRenderSession.FinishTimeout(0) == TimeSpan.FromMinutes(2), "A short video waits two minutes.");
    Check(
      KeyViewerRenderSession.FinishTimeout(14_400) == TimeSpan.FromMinutes(2) + TimeSpan.FromMinutes(120),
      "A four-minute 60 fps video allows ElementNote two frames a second."
    );
    Check(KeyViewerRenderSession.FinishTimeout(long.MaxValue / 2) == TimeSpan.FromHours(3), "The wait has a ceiling.");
  }

  private static void PulsesOncePerSecond()
  {
    var channel = new FakeChannel();
    var session = new KeyViewerRenderSession(channel, channel.Replies);
    DateTime now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    session.Pulse(now);
    Check(channel.Sent.Count == 0, "No session, no pulse.");
    session.Start(new[] { new RenderSurfaceRequest("hand", 1, 400, 250) }, 60, Ffmpeg);
    for (int frame = 0; frame < 120; frame++)
      session.Pulse(now.AddMilliseconds(frame * 16.7));
    Check(
      channel.Sent.Count(sent => sent.Name == KeyViewerRenderSession.PulseCommand) == 2,
      "One pulse a second, not one a frame."
    );
  }

  // The lease is kept from a timer while the game thread waits for finish.
  private static void PulsesFromATimerThread()
  {
    var channel = new FakeChannel { Answer = AnswerBegin };
    var session = new KeyViewerRenderSession(channel, channel.Replies);
    session.Start(new[] { new RenderSurfaceRequest("hand", 1, 400, 250) }, 60, Ffmpeg);
    string id = (string)channel.Sent[0].Payload["sessionId"];
    DateTime now = DateTime.UtcNow;
    var pulses = new Thread(() =>
    {
      for (int i = 0; i < 200; i++)
        session.Pulse(now.AddSeconds(i));
    });
    pulses.Start();
    for (int frame = 0; frame < 200; frame++)
      session.SendFrame(frame * 166_667L, Quick);
    pulses.Join();
    session.Stop();
    (string Name, JObject Payload)[] sent = channel.Snapshot();
    Check(
      sent.Where(message => message.Name == KeyViewerRenderSession.PulseCommand)
        .All(message => (string)message.Payload["sessionId"] == id),
      "Pulses from another thread name the session."
    );
    int pulsesBefore = sent.Count(message => message.Name == KeyViewerRenderSession.PulseCommand);
    session.Pulse(now.AddHours(1));
    Check(
      channel.Snapshot().Count(message => message.Name == KeyViewerRenderSession.PulseCommand) == pulsesBefore,
      "No pulse after the session ends."
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
    RenderFrameCrop crop = RenderFrameCrop.Of(200, 100, 200, 100, 10);
    Check(crop.X == 10 && crop.Y == 10 && crop.Width == 180 && crop.Height == 80, "The padding goes on every side.");
    // 30 CSS px of padding at 0.75 px per CSS px is 22.5 texels.
    RenderFrameCrop half = RenderFrameCrop.Of(645, 240, 860, 320, 30);
    Check(half.X == 22 && half.Width == 601, "A crop between texels moves to a whole texel.");
    Check(half.Y == 22 && half.Height == 196, "So does the vertical crop.");
    Near(601 / .75f, half.CssWidth, "The shown texels in CSS pixels.");
    RenderFrameCrop tiny = RenderFrameCrop.Of(3, 3, 3, 3, 1.4f);
    Check(tiny.Width == 1 && tiny.Height == 1, "At least one texel stays.");
  }

  // A 5K Retina game view (2 screen pixels per CSS pixel) rendered to 1080p draws the
  // viewer at 0.75 video pixels per CSS pixel. ElementNote draws the 860x320 page into
  // 645x240 pixels; the video must show those pixels one to one, or they blur.
  private static void MapsAFrameTexelForPixel()
  {
    var screen = new LayoutBounds(17.3f, 2880 - 600 - 41.7f, 1600, 600);
    OverlayLayoutSettings anchor = NativeViewerGrowth.Capture(screen, 5120, 2880, 1, Anchor9.BottomLeft);
    RenderViewerPlacement placement = RenderViewerPlacement.Create(
      "hand",
      screen,
      5120,
      2880,
      2,
      anchor,
      860,
      320,
      1920,
      1080
    );
    Near(.75f, placement.Scale, "Video pixels per CSS pixel.");
    RenderFrameCrop crop = RenderFrameCrop.Of(645, 240, 860, 320, 30);
    LayoutBounds placed = placement.Place(crop.CssWidth, crop.CssHeight);
    Pixels(crop.Width, placed.Width * 1920, "One video pixel per texel across.");
    Pixels(crop.Height, placed.Height * 1080, "And down.");
    Whole(placed.X * 1920, "The left edge sits on a pixel boundary.");
    Whole(placed.Y * 1080, "So does the top edge.");
    LayoutBounds centered = RenderViewerPlacement
      .Create("foot", screen, 5120, 2880, 2, null, 860, 320, 1920, 1080)
      .Place(crop.CssWidth, crop.CssHeight);
    Whole(centered.X * 1920, "A centered viewer snaps too.");
    Whole(centered.Y * 1080, "Vertically as well.");

    RenderOverlayRects rects = RenderOverlayRects.Of(placement, 645, 240, 860, 320, 30);
    Check(
      rects.Source.X == 22 && rects.Source.Y == 22 && rects.Source.Width == 601 && rects.Source.Height == 196,
      "The video shows the page minus its padding: " + rects.Source
    );
    Check(
      rects.Target.Width == rects.Source.Width && rects.Target.Height == rects.Source.Height,
      "And lays it one pixel per pixel: " + rects.Target
    );
    Check(
      rects.Target.X == (int)Math.Round(placed.X * 1920) && rects.Target.Y == (int)Math.Round(placed.Y * 1080),
      "At the viewer's place: " + rects.Target
    );
    Check(rects.Target.Y + rects.Target.Height <= 1080 && rects.Target.X >= 0, "Inside the video.");
  }

  // Two hand viewers share one page, drawn at the larger one's scale.
  private static void OverlaysEveryViewerOnItsPage()
  {
    var big = new LayoutBounds(20, 2880 - 600 - 40, 1600, 600);
    var small = new LayoutBounds(3000, 100, 800, 300);
    RenderViewerPlacement large = RenderViewerPlacement.Create(
      "hand",
      big,
      5120,
      2880,
      2,
      NativeViewerGrowth.Capture(big, 5120, 2880, 1, Anchor9.BottomLeft),
      860,
      320,
      1920,
      1080
    );
    RenderViewerPlacement smaller = RenderViewerPlacement.Create(
      "hand",
      small,
      5120,
      2880,
      1,
      NativeViewerGrowth.Capture(small, 5120, 2880, 1, Anchor9.TopRight),
      860,
      320,
      1920,
      1080
    );
    RenderViewerPlacement foot = RenderViewerPlacement.Create("foot", big, 5120, 2880, 2, null, 400, 300, 1920, 1080);
    var surfaces = new[]
    {
      new RenderSurface
      {
        ViewerKind = "hand",
        Width = 645,
        Height = 240,
        ViewportWidth = 860,
        ViewportHeight = 320,
        Padding = 30,
      },
      new RenderSurface
      {
        ViewerKind = "foot",
        Width = 300,
        Height = 225,
        ViewportWidth = 400,
        ViewportHeight = 300,
        Padding = 0,
      },
    };
    var videos = new[]
    {
      new RenderVideo
      {
        ViewerKind = "hand",
        Path = "/v/hand.mov",
        Width = 645,
        Height = 240,
        Frames = 10,
      },
      new RenderVideo
      {
        ViewerKind = "foot",
        Path = "/v/foot.mov",
        Width = 300,
        Height = 225,
        Frames = 10,
      },
    };
    IReadOnlyList<KeyViewerOverlay> overlays = KeyViewerRenderPlan.Overlays(
      new[] { large, smaller, foot },
      surfaces,
      videos
    );
    Check(overlays.Count == 3, "Every placed viewer goes over the video.");
    Check(
      overlays[0].Path == "/v/hand.mov" && overlays[1].Path == "/v/hand.mov" && overlays[2].Path == "/v/foot.mov",
      "Viewers of a kind share their page's video."
    );
    Check(overlays[0].Source.Equals(overlays[1].Source), "Both show the same part of the page.");
    Check(
      overlays[1].Target.Width < overlays[0].Target.Width && overlays[1].Target.Height < overlays[0].Target.Height,
      "The smaller viewer gets a smaller target: " + overlays[0].Target + " vs " + overlays[1].Target
    );
    Check(
      Math.Abs(overlays[1].Target.Width - overlays[0].Source.Width / 2) <= 1,
      "Half the scale, half the size: " + overlays[1].Target
    );
    Check(overlays[1].Target.X > 960, "Each viewer keeps its own place.");
    Check(
      KeyViewerRenderPlan.Overlays(new[] { large }, surfaces, Array.Empty<RenderVideo>()).Count == 0,
      "No video, nothing to lay over."
    );
  }

  private static void NeedsARendererThatTakesVideos()
  {
    var ready = new FakeTarget();
    Check(KeyViewerRenderPlan.Blocker(ready, true, true) == null, "Everything is in place.");
    Check(
      KeyViewerRenderPlan.Blocker(new FakeTarget { TakesVideos = false }, true, true)?.Code
        == RenderFailure.RendererOutdated,
      "OrbitRender that can't lay videos over the render needs an update."
    );
    Check(
      KeyViewerRenderPlan.Blocker(new FakeTarget { Ffmpeg = null }, true, true)?.Code == RenderFailure.RendererOutdated,
      "So does a TUFReplay that shares no FFmpeg."
    );
    Check(
      KeyViewerRenderPlan.Blocker(new FakeTarget { Fps = 0 }, true, true)?.Code == RenderFailure.RendererOutdated,
      "Or reports no frame rate."
    );
    Check(
      KeyViewerRenderPlan.Blocker(new FakeTarget { TakesVideos = false }, false, false)?.Code
        == RenderFailure.RendererOutdated,
      "The renderer comes first: opening ElementNote wouldn't help."
    );
    Check(
      KeyViewerRenderPlan.Blocker(ready, false, true)?.Code == RenderFailure.AppOutdated,
      "A connected ElementNote without protocol 2 needs an update."
    );
    Check(
      KeyViewerRenderPlan.Blocker(ready, false, false)?.Code == RenderFailure.AppUnavailable,
      "No ElementNote at all needs opening."
    );
  }

  private static void ExplainsWhyKeyViewersAreMissing()
  {
    (string title, string detail) = RenderNotices.For(RenderFailure.RendererOutdated, true);
    Check(title == "키뷰어 없이 렌더링했어요", "The title says what happened.");
    Check(detail == "TUFReplay와 OrbitRender를 업데이트한 뒤 다시 렌더링해주세요.", "The detail says what to do.");
    Check(
      RenderNotices.For(RenderFailure.RendererOutdated, false).Detail
        == "Update TUFReplay and OrbitRender, then render again.",
      "And in English."
    );
    Check(
      RenderNotices.For(RenderFailure.AppOutdated, false).Detail == "Update ElementNote, then render again.",
      "An old ElementNote is updated."
    );
    Check(
      RenderNotices.For(RenderFailure.AppUnavailable, false).Detail.StartsWith("Open ElementNote"),
      "A closed one is opened."
    );
    Check(
      RenderNotices
        .For("render-ffmpeg-failed", false)
        .Detail.StartsWith("ElementNote couldn't make the key viewer video"),
      "An encoding failure says the video wasn't made."
    );
  }

  private static JObject[] Frames(FakeChannel channel) =>
    channel
      .Snapshot()
      .Where(sent => sent.Name == KeyViewerRenderSession.FrameCommand)
      .Select(sent => sent.Payload)
      .ToArray();

  private static PeerEvent Available(string peer, int protocol) =>
    new(
      peer,
      "connection",
      KeyViewerRenderIpc.AvailableEvent,
      "id",
      null,
      JsonConvert.SerializeObject(new { protocolVersion = protocol, recordedCounters = true })
    );

  private static void Pixels(float expected, float actual, string message) =>
    Check(Math.Abs(expected - actual) < 1e-3, message + " Expected " + expected + ", got " + actual + ".");

  private static void Whole(float pixels, string message) => Pixels((float)Math.Round(pixels), pixels, message);

  private static object AnswerBegin(string name, JObject payload)
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

  private static object AnswerBeginAndFinish(string name, JObject payload) =>
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

  private static void Near(float expected, float actual, string message) =>
    Check(Math.Abs(expected - actual) < 1e-4f, message + " Expected " + expected + ", got " + actual + ".");

  private static void Check(bool value, string message)
  {
    if (!value)
      throw new InvalidOperationException(message);
  }

  private sealed class FakeTarget : IReplayRenderTarget
  {
    public int RenderWidth { get; set; } = 1920;
    public int RenderHeight { get; set; } = 1080;
    public int Fps { get; set; } = 60;
    public bool TakesVideos { get; set; } = true;
    public string Ffmpeg { get; set; } = KeyViewerRenderTests.Ffmpeg;
    public int RenderFps => Fps;
    public bool RenderTakesOverlayVideos => TakesVideos;
    public string RenderFfmpegPath => Ffmpeg;
  }

  // Answers each command from another thread, like ElementNote's reply command.
  private sealed class FakeChannel : IKeyViewerRenderChannel
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
