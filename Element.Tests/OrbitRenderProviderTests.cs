using System;
using System.Collections.Generic;
using System.Reflection;
using Element.Infrastructure.Rendering;
using UnityEngine;

namespace Element.Tests;

// OrbitRender finds the provider's methods by reflection, so these look them up
// the way OrbitRender's OverlayApi does (OrbitRender/OverlayApi.cs, Entry).
internal static class OrbitRenderProviderTests
{
  private const BindingFlags Public = BindingFlags.Public | BindingFlags.Instance;

  public static void Run()
  {
    var overlay = new FakeOverlay();
    var provider = new OrbitRenderProvider(overlay);

    // API 2 binds a typed delegate to this exact signature.
    MethodInfo typed = typeof(OrbitRenderProvider).GetMethod(
      "OnReplayKey",
      Public,
      null,
      new[] { typeof(KeyCode), typeof(int?), typeof(bool), typeof(long) },
      null
    );
    Check(typed != null, "API 2 finds the typed key callback");
    var replayKey =
      (Action<KeyCode, int?, bool, long>)
        Delegate.CreateDelegate(typeof(Action<KeyCode, int?, bool, long>), provider, typed);

    replayKey(KeyCode.RightAlt, 21, true, 100);
    replayKey(KeyCode.RightAlt, 21, false, 200);
    Check(
      overlay.Keys.Count == 2
        && overlay.Keys[0] == ("RightAlt", true, 100L)
        && overlay.Keys[1] == ("RightAlt", false, 200L),
      "a key reaches ElementNote by its Unity name, as with API 1"
    );

    replayKey(KeyCode.None, 21, true, 300);
    Check(overlay.Keys.Count == 2, "a key with only a native code is skipped");

    // API 1 finds the string callback by name and passes KeyCode.ToString().
    MethodInfo legacy = typeof(OrbitRenderProvider).GetMethod("OnReplayInput", Public);
    Check(legacy != null, "API 1 still finds its callback");
    legacy.Invoke(provider, new object[] { "Space", true, 400L });
    Check(overlay.Keys.Count == 3 && overlay.Keys[2] == ("Space", true, 400L), "API 1 keys still play");
  }

  private sealed class FakeOverlay : IRenderOverlay
  {
    public readonly List<(string Key, bool Down, long Ticks)> Keys = new();

    public IEnumerable<Canvas> Canvases() => Array.Empty<Canvas>();

    public void Begin(bool replay) { }

    public void End() { }

    public void Clock(long ticks) { }

    public void Input(string key, bool down, long ticks) => Keys.Add((key, down, ticks));

    public void BeforeCapture() { }
  }

  private static void Check(bool value, string message)
  {
    if (!value)
      throw new InvalidOperationException(message);
  }
}
