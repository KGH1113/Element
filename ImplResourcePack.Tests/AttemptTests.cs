using System;
using System.Collections.Generic;
using System.IO;
using ImplResourcePack.Application.Attempts;
using ImplResourcePack.Domain.Attempts;
using ImplResourcePack.Infrastructure.Attempts;

namespace ImplResourcePack.Tests;

/// <summary>Attempt counting per start and speed, the play lifecycle, and Attempts.dat.</summary>
internal static class AttemptTests
{
  private static readonly MapId MapA = new(1, 2);
  private static readonly MapId MapB = new(3, 4);

  public static void Run()
  {
    Keys();
    Book();
    Tracker();
    SkipRules();
    PublicApi();
    Codec();
    FileStore();
  }

  private static void Keys()
  {
    Check(new AttemptKey(5, 1.1f * 1d).Equals(new AttemptKey(5, 1.1d)), "float noise in pitch × speed keeps one key");
    Check(!new AttemptKey(5, 1d).Equals(new AttemptKey(6, 1d)), "another start tile is another key");
    Check(!new AttemptKey(5, 1d).Equals(new AttemptKey(5, 1.2d)), "another speed is another key");
    Check(
      new AttemptKey(0, double.NaN).Equals(new AttemptKey(0, double.NaN)),
      "an unreadable speed still finds its count"
    );
    Check(MapId.FromDigest(new byte[16]).Equals(new MapId(0, 0)), "a 16-byte digest names a map");
  }

  private static void Book()
  {
    AttemptBook book = new();
    AttemptKey start = new(0, 1);
    AttemptKey checkpoint = new(120, 1);
    Same(book.Add(MapA, start), 1, 1, "first attempt");
    Same(book.Add(MapA, start), 2, 2, "second attempt from the same start");
    Same(book.Add(MapA, checkpoint), 1, 3, "a checkpoint start counts apart and adds to the full count");
    Same(book.Read(MapA, start), 2, 3, "reading does not change counts");
    Same(book.Read(MapB, start), 0, 0, "another map starts at zero");
    Same(book.Remove(MapA, checkpoint), 0, 2, "taking back an attempt lowers both counts");
    Same(book.Remove(MapA, checkpoint), 0, 2, "counts never go below zero");
    book.Remove(MapA, start);
    book.Remove(MapA, start);
    Check(book.Maps.Count == 0, "a map with no attempts left is dropped");
  }

  private static void Tracker()
  {
    MemoryStore store = new();
    FakeRuns runs = new() { Next = new AttemptRun(MapA, new AttemptKey(0, 1), true) };
    AttemptTracker tracker = new(runs, store, new AttemptSkipRules(), null);

    tracker.Begin(0);
    Same(tracker.Read(), 1, 1, "a play counts when it starts");
    Check(store.Saves == 0, "starting a play does not write the file");
    tracker.MarkPlayed();
    tracker.End();
    Check(store.Saves == 1, "a played attempt is saved when the play ends");
    tracker.End();
    Check(store.Saves == 1, "ending twice saves once");

    tracker.Begin(0);
    Same(tracker.Read(), 2, 2, "a retry shows the next attempt");
    tracker.End();
    Same(tracker.Read(), 1, 1, "leaving before clearing a tile takes the attempt back");
    Check(store.Saves == 1, "a taken-back attempt writes nothing");

    tracker.Begin(0);
    tracker.Begin(0);
    Same(tracker.Read(), 2, 2, "a second start without an end settles the first one");

    runs.Next = new AttemptRun(MapA, new AttemptKey(0, 1), false);
    tracker.Begin(0);
    Same(tracker.Read(), 1, 1, "autoplay shows counts without adding one");
    tracker.MarkPlayed();
    tracker.End();
    Same(tracker.Read(), 1, 1, "autoplay never changes counts");

    runs.Next = new AttemptRun(MapA, new AttemptKey(40, 1.1d), true);
    tracker.Begin(40);
    Same(tracker.Read(), 1, 2, "a new start counts on its own and in the full count");
    tracker.MarkPlayed();
    tracker.Dispose();
    Check(store.Flushes == 1 && store.Saves == 2, "stopping the mod saves the play in progress and waits for it");

    runs.Readable = false;
    AttemptTracker blind = new(runs, new MemoryStore(), null, null);
    blind.Begin(0);
    Same(blind.Read(), 0, 0, "a play the game cannot describe shows zero");
    blind.MarkPlayed();
    blind.End();
  }

  private static void SkipRules()
  {
    AttemptSkipRules rules = new();
    List<string> errors = new();
    FakeRuns runs = new() { Next = new AttemptRun(MapA, new AttemptKey(0, 1), true) };
    AttemptTracker tracker = new(runs, new MemoryStore(), rules, (context, _) => errors.Add(context));
    Played(tracker);
    Same(tracker.Read(), 1, 1, "plays count with no rules");

    bool replaying = true;
    rules.Set("Replay", () => replaying);
    Played(tracker);
    Same(tracker.Read(), 1, 1, "a rule keeps a play out of the count and still shows the counts");
    replaying = false;
    Played(tracker);
    Same(tracker.Read(), 2, 2, "the next play counts once the rule says so, with nothing to switch back");

    rules.Set("Replay", () => true);
    rules.Set("Replay", () => false);
    Check(rules.Count == 1, "setting a rule again replaces it");
    Played(tracker);
    Same(tracker.Read(), 3, 3, "only the latest rule from a mod applies");

    rules.Set("Other", () => true);
    Played(tracker);
    Same(tracker.Read(), 3, 3, "any mod's rule can keep a play out");
    rules.Remove("Other");
    rules.Remove("Missing");
    Played(tracker);
    Same(tracker.Read(), 4, 4, "a removed rule no longer applies");

    rules.Set("Broken", () => throw new InvalidOperationException("mod unloaded"));
    Played(tracker);
    Same(tracker.Read(), 5, 5, "a rule that throws does not stop counting");
    Check(rules.Count == 1 && errors.Count == 1 && errors[0].Contains("Broken"), "a failing rule is removed and named");

    int asked = 0;
    rules.Set(
      "Counter",
      () =>
      {
        asked++;
        return false;
      }
    );
    runs.Next = new AttemptRun(MapA, new AttemptKey(0, 1), false);
    Played(tracker);
    Check(asked == 0, "autoplay does not ask the rules");

    Throws<ArgumentException>(() => rules.Set(" ", () => true), "a rule needs an owner");
    Throws<ArgumentNullException>(() => rules.Set("Replay", null), "a rule needs a body");
  }

  private static void PublicApi()
  {
    Check(AttemptCounter.ApiVersion == 1, "the public API starts at version 1");
    int before = AttemptCounter.Rules.Count;
    AttemptCounter.SetSkipRule("ImplResourcePack.Tests", () => true);
    Check(AttemptCounter.Rules.Count == before + 1, "another mod can set a rule");
    AttemptCounter.RemoveSkipRule("ImplResourcePack.Tests");
    Check(AttemptCounter.Rules.Count == before, "and remove it");

    Type api = typeof(AttemptCounter).Assembly.GetType("ImplResourcePack.AttemptCounter");
    Check(
      api?.GetField("ApiVersion")?.GetValue(null) is 1
        && api.GetMethod("SetSkipRule", new[] { typeof(string), typeof(Func<bool>) }) != null
        && api.GetMethod("RemoveSkipRule", new[] { typeof(string) }) != null,
      "reflection finds the documented API"
    );
  }

  private static void Played(AttemptTracker tracker)
  {
    tracker.Begin(0);
    tracker.MarkPlayed();
    tracker.End();
  }

  private static void Codec()
  {
    AttemptBook book = new();
    book.Add(MapA, new AttemptKey(0, 1));
    book.Add(MapA, new AttemptKey(0, 1));
    book.Add(MapA, new AttemptKey(12, 1.1d));
    book.Add(MapB, new AttemptKey(3, 0.9d));
    byte[] data = AttemptBookCodec.Encode(book);
    AttemptBook read = AttemptBookCodec.Decode(data);
    Same(read.Read(MapA, new AttemptKey(0, 1)), 2, 3, "counts survive a save");
    Same(read.Read(MapA, new AttemptKey(12, 1.1d)), 1, 3, "speeds survive a save");
    Same(read.Read(MapB, new AttemptKey(3, 0.9d)), 1, 1, "every map survives a save");

    Throws(() => AttemptBookCodec.Decode(new byte[] { 1, 2, 3, 4, 1 }), "a file of another kind is rejected");
    byte[] cut = new byte[data.Length - 3];
    Array.Copy(data, cut, cut.Length);
    Throws(() => AttemptBookCodec.Decode(cut), "a cut-off file is rejected");
  }

  private static void FileStore()
  {
    string root = Path.Combine(Path.GetTempPath(), "impl-attempts-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
      List<string> warnings = new();
      List<string> errors = new();
      AttemptFileStore store = new(root, (context, _) => errors.Add(context), warnings.Add);
      Check(store.Load().Maps.Count == 0, "no file means no attempts yet");

      AttemptBook book = new();
      book.Add(MapA, new AttemptKey(0, 1));
      store.Save(book);
      book.Add(MapA, new AttemptKey(0, 1));
      store.Save(book);
      store.Flush();
      Same(store.Load().Read(MapA, new AttemptKey(0, 1)), 2, 2, "the latest save is on disk after a flush");
      Check(errors.Count == 0, "saving reports no errors");

      string path = Path.Combine(root, AttemptFileStore.FileName);
      File.WriteAllBytes(path, new byte[] { 9, 9 });
      Check(store.Load().Maps.Count == 0, "an unreadable file starts from zero");
      Check(
        File.Exists(path + ".broken") && errors.Count == 1 && errors[0].Contains(".broken"),
        "the unreadable file is kept aside and the log says where"
      );
      Check(warnings.Count == 0, "a finished save leaves nothing to warn about");
    }
    finally
    {
      Directory.Delete(root, true);
    }
  }

  private static void Same(AttemptCounts counts, int attempt, int full, string message) =>
    Check(
      counts.Attempt == attempt && counts.FullAttempt == full,
      message + " (got " + counts.Attempt + "/" + counts.FullAttempt + ", expected " + attempt + "/" + full + ")"
    );

  private static void Throws<T>(Action action, string message)
    where T : Exception
  {
    try
    {
      action();
    }
    catch (T)
    {
      return;
    }
    throw new InvalidOperationException(message);
  }

  private static void Throws(Action action, string message)
  {
    try
    {
      action();
    }
    catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException)
    {
      return;
    }
    throw new InvalidOperationException(message);
  }

  private static void Check(bool value, string message)
  {
    if (!value)
      throw new InvalidOperationException(message);
  }

  private sealed class FakeRuns : IAttemptRunSource
  {
    public AttemptRun Next;
    public bool Readable = true;

    public bool TryRead(int startFloor, out AttemptRun run)
    {
      run = Next;
      return Readable;
    }
  }

  private sealed class MemoryStore : IAttemptStore
  {
    public int Saves;
    public int Flushes;

    public AttemptBook Load() => new();

    public void Save(AttemptBook book) => Saves++;

    public void Flush() => Flushes++;
  }
}
