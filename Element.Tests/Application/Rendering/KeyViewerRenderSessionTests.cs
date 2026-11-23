using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Element.Application.Rendering;
using Newtonsoft.Json.Linq;
using static Element.Tests.Application.Rendering.RenderFixtures;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Application.Rendering;

/// <summary>
/// A render session starts from the right counts, sends frames without waiting,
/// numbers them by video time, and finishes or fails with every page's video.
/// </summary>
internal static class KeyViewerRenderSessionTests
{
  internal static void Run()
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
    LetsACancelledRenderReleaseTheFinishWait();
    PulsesOncePerSecond();
    PulsesFromATimerThread();
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

  // The renderer waits for the videos off the game thread while the game shows its progress.
  // A render cancelled meanwhile stops the session there: the wait ends at once and leaves no
  // failure behind for the next render.
  private static void LetsACancelledRenderReleaseTheFinishWait()
  {
    var channel = new FakeChannel
    {
      Answer = (name, payload) => name == KeyViewerRenderSession.BeginCommand ? AnswerBegin(name, payload) : null,
    };
    var session = new KeyViewerRenderSession(channel, channel.Replies);
    session.Start(new[] { new RenderSurfaceRequest("hand", 1, 400, 250) }, 60, Ffmpeg);
    session.SendFrame(0, Quick);
    IReadOnlyList<RenderVideo> videos = Array.Empty<RenderVideo>();
    var waiter = new Thread(() => videos = session.Finish(TimeSpan.FromSeconds(30)));
    var watch = Stopwatch.StartNew();
    waiter.Start();
    Thread.Sleep(100);
    session.Stop();
    Check(waiter.Join(TimeSpan.FromSeconds(5)), "Stopping the session releases a finish waiting off the game thread.");
    Check(videos == null && watch.Elapsed < TimeSpan.FromSeconds(5), "A stopped session hands over no videos.");
    Check(session.Failure == null, "A cancelled render leaves no failure for the next one.");
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
}
