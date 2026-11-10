using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Element.Infrastructure.Rendering;
using UnityEngine;

namespace Element.Tests;

// TUFReplay's ReplayApi finds the provider's methods by name and signature
// (TUFReplay/ModApi/Providers/ReplayProvider.cs), so these look them up the same way.
internal static class ReplayRenderProviderTests
{
  private const BindingFlags Public = BindingFlags.Public | BindingFlags.Instance;

  public static void Run()
  {
    FollowsTheContract();
    LeavesReplaysInTheGameAlone();
    DeliversRenderedKeys();
    HandsOverVideosOnlyWhileRendering();
  }

  private static void FollowsTheContract()
  {
    Type type = typeof(ReplayRenderProvider);
    Has(type, "OnReplayBegin", typeof(void), typeof(bool));
    Has(type, "OnReplayEnd", typeof(void));
    Has(type, "OnReplayClock", typeof(void), typeof(long));
    Has(type, "OnReplayKey", typeof(void), typeof(KeyCode), typeof(int?), typeof(bool), typeof(long));
    Has(type, "GetReplayCanvases", typeof(IEnumerable<Canvas>));
    Has(type, "OnBeforeFrameCapture", typeof(void));
    Has(type, "AddReplayOverlayVideos", typeof(void), typeof(Action<string, RectInt, RectInt>));
    Check(type.GetMethod("CaptureRunState", Public) == null, "the render provider saves no state with runs");
    Check(ReplayRenderProvider.Id == "Element.Render", "the render provider keeps its id");
  }

  private static void LeavesReplaysInTheGameAlone()
  {
    var overlay = new FakeOverlay();
    var provider = new ReplayRenderProvider(overlay);
    provider.OnReplayBegin(false);
    provider.OnReplayClock(100);
    provider.OnReplayKey(KeyCode.Space, 49, true, 100);
    provider.OnReplayEnd();
    Check(overlay.Calls.Count == 0, "a replay in the game reaches no render code");
  }

  private static void DeliversRenderedKeys()
  {
    var overlay = new FakeOverlay();
    var provider = new ReplayRenderProvider(overlay);
    provider.OnReplayBegin(true);
    provider.OnReplayClock(50);
    provider.OnReplayKey(KeyCode.RightAlt, 21, true, 100);
    provider.OnReplayKey(KeyCode.None, 21, true, 150);
    provider.OnReplayKey(KeyCode.RightAlt, null, false, 200);
    // A new replay ends the one TUFReplay did not end.
    provider.OnReplayBegin(true);
    provider.OnReplayEnd();
    provider.OnReplayEnd();
    Check(
      overlay.Calls.SequenceEqual(
        new[] { "begin", "clock 50", "RightAlt down 100", "RightAlt up 200", "end", "begin", "end" }
      ),
      "a rendered replay begins, keys arrive by their Unity name, keys without one are skipped, and it ends once: "
        + string.Join(", ", overlay.Calls)
    );
  }

  private static void HandsOverVideosOnlyWhileRendering()
  {
    var overlay = new FakeOverlay();
    var provider = new ReplayRenderProvider(overlay);
    var added = new List<string>();
    Action<string, RectInt, RectInt> add = (path, source, target) =>
      added.Add(
        path
          + " "
          + source.x
          + ","
          + source.y
          + " -> "
          + target.x
          + ","
          + target.y
          + " "
          + target.width
          + "x"
          + target.height
      );
    provider.AddReplayOverlayVideos(add);
    Check(added.Count == 0, "no render, no videos");
    provider.OnReplayBegin(true);
    provider.AddReplayOverlayVideos(add);
    provider.AddReplayOverlayVideos(null);
    provider.OnReplayEnd();
    provider.AddReplayOverlayVideos(add);
    Check(
      added.SequenceEqual(new[] { "/v/hand.mov 22,22 -> 40,860 601x196" }),
      "a render's videos go to TUFReplay with their source and target: " + string.Join(", ", added)
    );
  }

  private static void Has(Type type, string name, Type returns, params Type[] parameters)
  {
    MethodInfo method = type.GetMethod(name, Public, null, parameters, null);
    Check(method != null && method.ReturnType == returns, type.Name + " has " + name + " as TUFReplay binds it");
  }

  private sealed class FakeOverlay : IRenderOverlay
  {
    public readonly List<string> Calls = new();

    public IEnumerable<Canvas> Canvases() => Array.Empty<Canvas>();

    public void Begin() => Calls.Add("begin");

    public void End() => Calls.Add("end");

    public void Clock(long ticks) => Calls.Add("clock " + ticks);

    public void Input(string key, bool down, long ticks) => Calls.Add(key + (down ? " down " : " up ") + ticks);

    public void BeforeCapture() => Calls.Add("capture");

    public void AddVideos(Action<string, RectInt, RectInt> add) =>
      add("/v/hand.mov", new RectInt(22, 22, 601, 196), new RectInt(40, 860, 601, 196));
  }

  private static void Check(bool value, string message)
  {
    if (!value)
      throw new InvalidOperationException(message);
  }
}
