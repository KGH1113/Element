using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Element.Application;
using Element.Application.Attempts;
using Element.Application.Rendering;
using Element.Domain.Attempts;
using Element.Domain.Elements;
using Element.Infrastructure.Ipc;
using Element.Infrastructure.Replays;
using Element.Presentation.Overlay.Attempt;
using Element.Presentation.Overlay.Elements;

namespace Element.Tests;

// TUFReplay saves Element's attempts and ElementNote's key counts with each run and hands
// them back while it replays or renders that run.
internal static class RunStateTests
{
  public static void Run()
  {
    ReplaysShowTheRecordedAttempts();
    UnknownAttemptsShowAsDash();
    AttemptProviderCapturesAndRestores();
    ProvidersFollowTufReplaysContract();
    KeyCountsNeedElementNote();
    ReadsCountReplies();
  }

  private static void ReplaysShowTheRecordedAttempts()
  {
    var replayed = new ReplayedAttempts(new FixedAttempts(new AttemptCounts(5, 9)));
    Same(replayed.Read(), 5, 9, "outside a replay the overlay shows the live counts");
    replayed.BeginReplay(new AttemptCounts(2, 3));
    Same(replayed.Read(), 2, 3, "a replay shows the counts its run was played at");
    replayed.BeginReplay(null);
    Check(replayed.Read().IsUnknown, "a run recorded without counts shows them as unknown");
    replayed.EndReplay();
    Same(replayed.Read(), 5, 9, "the live counts come back after the replay");
    Same(new ReplayedAttempts(null).Read(), 0, 0, "without counting the overlay shows zero, as before");
  }

  private static void UnknownAttemptsShowAsDash()
  {
    OverlayValues values = new();
    AttemptValues attempts = new(values);
    attempts.Render(AttemptCounts.Unknown);
    Check(Shown(values, OverlayValueId.Attempt) == "-", "an unknown attempt shows as -");
    Check(Shown(values, OverlayValueId.FullAttempt) == "-", "an unknown full attempt shows as -");
    Check(!values.TryGetNumber(OverlayValueId.Attempt, out _), "a formula with it shows nothing, not zero");
    attempts.Render(new AttemptCounts(4, 7));
    Check(Shown(values, OverlayValueId.Attempt) == "4", "the number comes back");
  }

  private static void AttemptProviderCapturesAndRestores()
  {
    var live = new FixedAttempts(new AttemptCounts(5, 9));
    var replayed = new ReplayedAttempts(live);
    int refreshes = 0;
    var provider = new AttemptRunStateProvider(live, replayed, () => refreshes++);

    AttemptRunState captured = provider.CaptureRunState();
    Check(captured.Attempt == 5 && captured.FullAttempt == 9, "a run saves the counts the overlay shows");

    provider.OnReplayBegin(new AttemptRunState { Attempt = 2, FullAttempt = 3 }, render: false);
    Same(replayed.Read(), 2, 3, "the replay shows the saved counts");
    Check(refreshes == 1, "a shown overlay redraws when the replay begins");
    provider.OnReplayBegin(null, render: true);
    Check(replayed.Read().IsUnknown, "a run saved without counts shows -");
    provider.OnReplayEnd();
    Same(replayed.Read(), 5, 9, "the live counts come back");
    Check(refreshes == 3, "a shown overlay redraws when the replay ends");

    Check(
      new AttemptRunStateProvider(null, new ReplayedAttempts(null), null).CaptureRunState() == null,
      "without counting nothing is saved"
    );
  }

  // TUFReplay finds these methods by name and maps the state's public fields; a rename here
  // silently drops Element out of TUFReplay's runs.
  private static void ProvidersFollowTufReplaysContract()
  {
    Contract(typeof(AttemptRunStateProvider), typeof(AttemptRunState), async: false);
    Contract(typeof(KeyCountRunStateProvider), typeof(KeyCountRunState), async: true);
    Check(AttemptRunStateProvider.Id == "Element.Attempts", "the attempt state id is saved with every run");
    Check(KeyCountRunStateProvider.Id == "Element.KeyCounts", "the key count state id is saved with every run");
  }

  private static void Contract(Type provider, Type state, bool async)
  {
    const BindingFlags Public = BindingFlags.Public | BindingFlags.Instance;
    MethodInfo capture = provider.GetMethod("CaptureRunState", Public);
    Check(capture != null && capture.GetParameters().Length == 0, provider.Name + " has CaptureRunState()");
    Type returned = async ? typeof(Task<>).MakeGenericType(state) : state;
    Check(capture.ReturnType == returned, provider.Name + ".CaptureRunState returns " + returned.Name);
    MethodInfo begin = provider.GetMethod("OnReplayBegin", Public);
    Check(
      begin != null
        && begin.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(new[] { state, typeof(bool) }),
      provider.Name + " has OnReplayBegin(" + state.Name + ", bool)"
    );
    MethodInfo end = provider.GetMethod("OnReplayEnd", Public);
    Check(end != null && end.GetParameters().Length == 0, provider.Name + " has OnReplayEnd()");
    Check(state.GetConstructor(Type.EmptyTypes) != null, state.Name + " can be created when TUFReplay reads a run");
    foreach (FieldInfo field in state.GetFields(Public))
      Check(
        field.FieldType == typeof(int) || field.FieldType == typeof(Dictionary<string, Dictionary<string, int>>),
        state.Name + "." + field.Name + " has a type TUFReplay stores"
      );
  }

  private static void KeyCountsNeedElementNote()
  {
    var counters = new KeyCounterIpc();
    var renderCounts = new RecordedKeyCounts();
    var provider = new KeyCountRunStateProvider(counters, renderCounts);
    Check(!counters.Available, "no ElementNote is connected");
    Task<KeyCountRunState> capture = provider.CaptureRunState();
    Check(capture.IsCompleted && capture.Result == null, "without ElementNote a run saves no key counts");

    var recorded = new KeyCountRunState
    {
      ByTab = new Dictionary<string, Dictionary<string, int>> { ["hand"] = new() { ["A"] = 40 } },
    };
    provider.OnReplayBegin(recorded, render: true);
    Check(renderCounts.Counts?["hand"]["A"] == 40, "a render starts from the run's key counts");
    provider.OnReplayEnd();
    Check(renderCounts.Counts == null, "the next render starts from the player's totals again");
    provider.OnReplayBegin(recorded, render: false);
    provider.OnReplayEnd();
    Check(renderCounts.Counts == null, "an in-game replay leaves render counts alone");
  }

  private static void ReadsCountReplies()
  {
    Dictionary<string, Dictionary<string, int>> counts = KeyCounterIpc.ParseCounts(
      "{\"ok\":true,\"counters\":{\"hand-default\":{\"A\":3,\"NUMPAD 1\":12}}}"
    );
    Check(counts?["hand-default"]["NUMPAD 1"] == 12, "ElementNote's counts are read");
    Check(KeyCounterIpc.ParseCounts("{\"ok\":false,\"code\":\"x\"}") == null, "a failed read saves nothing");
    Check(KeyCounterIpc.ParseCounts("{\"ok\":true,\"counters\":{\"hand\":{\"A\":-1.5}}}") == null, "a bad count saves nothing");
    Check(KeyCounterIpc.ParseCounts("not json") == null, "an unreadable reply saves nothing");
  }

  private static string Shown(OverlayValues values, OverlayValueId id) =>
    values.TryGet(id, out StringBuilder text) ? text.ToString() : null;

  private static void Same(AttemptCounts counts, int attempt, int full, string message) =>
    Check(!counts.IsUnknown && counts.Attempt == attempt && counts.FullAttempt == full, message);

  private static void Check(bool value, string message)
  {
    if (!value)
      throw new InvalidOperationException(message);
  }

  private sealed class FixedAttempts : IAttemptSource
  {
    private readonly AttemptCounts _counts;

    public FixedAttempts(AttemptCounts counts) => _counts = counts;

    public AttemptCounts Read() => _counts;
  }
}
