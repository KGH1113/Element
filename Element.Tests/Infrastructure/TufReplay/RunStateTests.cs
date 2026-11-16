using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using AdofaiIpc.Contracts;
using Element.Application.Attempts;
using Element.Application.Rendering;
using Element.Domain.Attempts;
using Element.Domain.Overlay.Values;
using Element.Infrastructure.ElementNote.Ipc;
using Element.Infrastructure.TufReplay;
using Element.Presentation.Overlay.Values;
using Element.Tests.Support;
using Newtonsoft.Json.Linq;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Infrastructure.TufReplay;

// TUFReplay saves Element's attempts and ElementNote's key counts with each run and hands
// them back through OnReplayState while it replays or renders that run.
internal static class RunStateTests
{
  [Suite("TUFReplay replays and renders show the attempts and key counts their run was played at")]
  public static void Run()
  {
    ReplaysShowTheRecordedAttempts();
    UnknownAttemptsShowAsDash();
    AttemptProviderCapturesAndRestores();
    ProvidersFollowTufReplaysContract();
    KeyCountsNeedElementNote();
    InGameReplaysCountFromTheRecordedCounts();
    ReadsCountReplies();
  }

  private static void ReplaysShowTheRecordedAttempts()
  {
    var replayed = new ReplayedAttempts(new FixedAttempts(new AttemptCounts(5, 9)));
    Same(replayed.Read(), 5, 9, "outside a replay the overlay shows the live counts");
    replayed.BeginReplay();
    Check(replayed.Read().IsUnknown, "a replay shows no live counts while its run's counts are on the way");
    replayed.ShowRecorded(new AttemptCounts(2, 3));
    Same(replayed.Read(), 2, 3, "a replay shows the counts its run was played at");
    replayed.ShowRecorded(null);
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

    provider.OnReplayBegin(render: false);
    Check(refreshes == 0, "the overlay redraws once the saved counts arrive, not before");
    provider.OnReplayState(new AttemptRunState { Attempt = 2, FullAttempt = 3 }, 0);
    Same(replayed.Read(), 2, 3, "the replay shows the saved counts");
    Check(refreshes == 1, "a shown overlay redraws when the saved counts arrive");
    // A timeline seek hands the start value over again.
    provider.OnReplayState(new AttemptRunState { Attempt = 2, FullAttempt = 3 }, 20_000_000);
    Same(replayed.Read(), 2, 3, "a seek keeps the saved counts");
    provider.OnReplayEnd();
    Same(replayed.Read(), 5, 9, "the live counts come back");
    Check(refreshes == 3, "a shown overlay redraws when the replay ends");

    provider.OnReplayState(new AttemptRunState { Attempt = 2, FullAttempt = 3 }, 0);
    Same(replayed.Read(), 5, 9, "a value outside a replay leaves the live counts");
    Check(refreshes == 3, "a value outside a replay redraws nothing");
    provider.OnReplayBegin(render: true);
    provider.OnReplayState(null, 0);
    Check(replayed.Read().IsUnknown, "a run saved without counts shows -");
    provider.OnReplayEnd();
    Same(replayed.Read(), 5, 9, "the live counts come back after a render");

    Check(
      new AttemptRunStateProvider(null, new ReplayedAttempts(null), null).CaptureRunState() == null,
      "without counting nothing is saved"
    );
  }

  // TUFReplay finds these methods by name and maps the state's public fields; a rename here
  // silently drops Element out of TUFReplay's runs. ReplayApi 2 refuses a provider whose
  // OnReplayBegin takes the state, or whose OnReplayEvent takes it without an OnReplayState.
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
    MethodInfo[] begins = provider.GetMethods(Public).Where(method => method.Name == "OnReplayBegin").ToArray();
    Check(
      begins.Length == 1 && Takes(begins[0], typeof(bool)),
      provider.Name + " has only OnReplayBegin(bool), a mode switch without the state"
    );
    MethodInfo replayState = provider.GetMethod("OnReplayState", Public);
    Check(
      replayState != null && replayState.ReturnType == typeof(void) && Takes(replayState, state, typeof(long)),
      provider.Name + " has OnReplayState(" + state.Name + ", long)"
    );
    Check(provider.GetMethod("OnReplayEvent", Public) == null, provider.Name + " adds no events during a run");
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
    provider.OnReplayBegin(render: true);
    Check(renderCounts.Counts == null, "a render has no counts until the run's arrive");
    provider.OnReplayState(recorded, 0);
    Check(renderCounts.Counts?["hand"]["A"] == 40, "a render starts from the run's key counts");
    provider.OnReplayEnd();
    Check(renderCounts.Counts == null, "the next render starts from the player's totals again");
    provider.OnReplayState(recorded, 0);
    Check(renderCounts.Counts == null, "a value outside a replay is dropped");
    provider.OnReplayBegin(render: false);
    provider.OnReplayState(recorded, 0);
    Check(renderCounts.Counts == null, "an in-game replay leaves render counts alone");
    provider.OnReplayEnd();
  }

  // ElementNote counts an in-game replay apart, from the counts the run was recorded at.
  private static void InGameReplaysCountFromTheRecordedCounts()
  {
    var counters = new KeyCounterIpc();
    var app = new FakeIpc();
    counters.Attach(app);
    counters.OnPeerEvent(
      new PeerEvent(FakeIpc.Peer, "connection", KeyCounterIpc.AvailableEvent, "1", null, "{\"protocolVersion\":1}")
    );
    var provider = new KeyCountRunStateProvider(counters, new RecordedKeyCounts());

    provider.OnReplayBegin(render: false);
    Check(app.Sent.Count == 0, "ElementNote hears of the replay together with the run's counts");
    provider.OnReplayState(Counts(40), 0);
    Check(app.Sent.Count == 1, "the run's counts start the replay in ElementNote once");
    Check(app.Counts(0)?["hand"]?.Value<int>("A") == 40, "the replay counts from the run's counts");
    // A timeline seek starts the run over with its start value.
    provider.OnReplayState(Counts(40), 20_000_000);
    Check(
      app.Sent.Count == 2 && app.Sent[1].Name == KeyCounterIpc.ReplayBeginCommand,
      "a seek starts ElementNote over"
    );
    Check(app.Counts(1)?["hand"]?.Value<int>("A") == 40, "ElementNote counts from the run's counts again, not the sum");
    provider.OnReplayEnd();
    Check(app.Sent.Count == 3 && app.Sent[2].Name == KeyCounterIpc.ReplayEndCommand, "the replay ends in ElementNote");

    provider.OnReplayBegin(render: false);
    provider.OnReplayState(null, 0);
    Check(
      app.Sent.Count == 4 && app.Sent[3].Name == KeyCounterIpc.ReplayBeginCommand && app.Counts(3) == null,
      "a run saved without counts counts on from the player's totals"
    );
    provider.OnReplayEnd();

    provider.OnReplayBegin(render: true);
    provider.OnReplayState(Counts(40), 0);
    provider.OnReplayEnd();
    Check(app.Sent.Count == 5, "a render starts no replay count in ElementNote");
  }

  private static void ReadsCountReplies()
  {
    Dictionary<string, Dictionary<string, int>> counts = KeyCounterIpc.ParseCounts(
      "{\"ok\":true,\"counters\":{\"hand-default\":{\"A\":3,\"NUMPAD 1\":12}}}"
    );
    Check(counts?["hand-default"]["NUMPAD 1"] == 12, "ElementNote's counts are read");
    Check(KeyCounterIpc.ParseCounts("{\"ok\":false,\"code\":\"x\"}") == null, "a failed read saves nothing");
    Check(
      KeyCounterIpc.ParseCounts("{\"ok\":true,\"counters\":{\"hand\":{\"A\":-1.5}}}") == null,
      "a bad count saves nothing"
    );
    Check(KeyCounterIpc.ParseCounts("not json") == null, "an unreadable reply saves nothing");
  }

  private static KeyCountRunState Counts(int a) =>
    new() { ByTab = new Dictionary<string, Dictionary<string, int>> { ["hand"] = new() { ["A"] = a } } };

  private static bool Takes(MethodInfo method, params Type[] parameters) =>
    method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(parameters);

  private static string Shown(OverlayValues values, OverlayValueId id) =>
    values.TryGet(id, out StringBuilder text) ? text.ToString() : null;

  private static void Same(AttemptCounts counts, int attempt, int full, string message) =>
    Check(!counts.IsUnknown && counts.Attempt == attempt && counts.FullAttempt == full, message);

  private sealed class FixedAttempts : IAttemptSource
  {
    private readonly AttemptCounts _counts;

    public FixedAttempts(AttemptCounts counts) => _counts = counts;

    public AttemptCounts Read() => _counts;
  }

  // ElementNote at the other end of the `element` channel; keeps the commands sent to it.
  private sealed class FakeIpc : IFeatureRegistration
  {
    public const string Peer = "element-note";

    public readonly List<(string Name, string Json)> Sent = new();

    public string Name => "element";

    public JToken Counts(int index) => JObject.Parse(Sent[index].Json)["counters"] is JObject counts ? counts : null;

    public string SendCommandToPeer(string peerId, string name, string json)
    {
      Check(peerId == Peer, "commands go to the ElementNote that offered its counts");
      Sent.Add((name, json));
      return "command-" + Sent.Count;
    }

    public void SendToPeer(string peerId, string name, string json, string correlationId = null) { }

    public void Publish(string name, string json) { }

    public void MarkInitializing() { }

    public void MarkReady() { }

    public void MarkError(string code, string message) { }

    public void Dispose() { }
  }
}
