using Element.Application.Gameplay;
using Element.Configuration;
using Element.Tests.Support;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Application.Gameplay;

/// <summary>When the overlay and the in-game key viewers show, and what applying settings redoes.</summary>
internal static class GameplaySessionTests
{
  [Suite("record mode, co-op and the layout editor decide what a play shows")]
  public static void Run()
  {
    PlayShowsOverlayAndViewers();
    ViewersWaitLoadedBetweenPlays();
    RecordModeAndCoopHideBoth();
    LayoutEditorShowsViewersOutsideAPlay();
    ApplyingRebuildsLayoutsOnlyWhenNeeded();
    ElementNoteOpensWhenAutoOpenTurnsOn();
  }

  private static void PlayShowsOverlayAndViewers()
  {
    var parts = new Parts();
    parts.Session.Show();
    Check(parts.Overlay.Shown && parts.Viewers.Shown, "a play shows the overlay and the key viewers");
    parts.Session.Hide();
    Check(!parts.Overlay.Shown && !parts.Viewers.Shown, "the end of a play hides both");
    parts.Viewers.Enabled = false;
    parts.Session.Show();
    Check(parts.Overlay.Shown && parts.Viewers.Off, "no viewer on: only the overlay shows, with no pages loaded");
  }

  private static void ViewersWaitLoadedBetweenPlays()
  {
    var parts = new Parts();
    parts.Session.ApplySettings();
    Check(parts.Viewers.Presence == KeyViewerPresence.Ready, "at startup the pages load hidden, before any play");
    parts.Session.Show();
    parts.Session.Hide();
    parts.Session.Show();
    Check(parts.Viewers.Shown, "a restart shows the key viewers again");
    Check(parts.Viewers.Unloads == 0, "ending and restarting a play never unloads the pages");
    parts.Session.Hide();
    Check(parts.Viewers.Presence == KeyViewerPresence.Ready, "after a play the pages stay loaded and hidden");
    parts.Viewers.Enabled = false;
    parts.Session.RefreshViewers();
    Check(parts.Viewers.Off && parts.Viewers.Unloads == 1, "removing the last viewer unloads the pages");
  }

  private static void RecordModeAndCoopHideBoth()
  {
    var parts = new Parts();
    parts.Recording.IsActive = true;
    parts.Session.Show();
    Check(!parts.Overlay.Shown && !parts.Viewers.Shown, "record mode hides the overlay and the key viewers");
    Check(parts.Viewers.Presence == KeyViewerPresence.Ready, "record mode keeps the pages loaded, only hidden");
    Check(parts.Recording.Reapplied == 1, "record mode hides the game's UI of the new play again");
    Check(!parts.Session.ShowsKeyViewersInPlay, "a rendered video of a recorded play shows no key viewers");

    parts = new Parts();
    parts.Play.IsCoop = true;
    parts.Session.Show();
    Check(!parts.Overlay.Shown && !parts.Viewers.Shown, "co-op keeps the overlay and key viewers hidden");
    Check(parts.Viewers.Presence == KeyViewerPresence.Ready, "co-op keeps the pages loaded for the next play");
  }

  private static void LayoutEditorShowsViewersOutsideAPlay()
  {
    var parts = new Parts();
    parts.Editing = true;
    parts.Play.IsCoop = true;
    parts.Session.RefreshViewers();
    Check(parts.Viewers.Shown, "the layout editor shows the key viewers outside a play, even in co-op");
    parts.Editing = false;
    parts.Session.RefreshViewers();
    Check(parts.Viewers.Presence == KeyViewerPresence.Ready, "closing the editor outside a play hides them again");
    parts.Recording.IsActive = true;
    parts.Editing = true;
    parts.Session.RefreshViewers();
    Check(parts.Viewers.Shown, "the layout editor shows them in record mode too");
    parts.Editing = false;
    parts.Session.RefreshViewers();
    Check(parts.Viewers.Presence == KeyViewerPresence.Ready, "closing it in record mode hides them, pages kept");
    parts.Recording.IsActive = false;
    parts.Viewers.Supported = false;
    parts.Editing = true;
    parts.Session.RefreshViewers();
    Check(parts.Viewers.Off, "a system without in-game viewers never loads them");
  }

  private static void ApplyingRebuildsLayoutsOnlyWhenNeeded()
  {
    var parts = new Parts();
    parts.Session.ApplySettings();
    parts.Session.ApplySettings();
    Check(parts.Looks == 2 && parts.Layouts == 1, "a color drag applies the look each sample and the layouts once");
    Check(!parts.Overlay.Shown && !parts.Viewers.Shown, "outside a play, applying shows nothing");
    parts.Settings.RecordMode = true;
    parts.Session.ApplySettings();
    Check(parts.Layouts == 2 && parts.Recording.IsActive, "switching record mode rebuilds the layouts and applies it");
    parts.Session.LayoutsReplaced();
    parts.Session.ApplySettings();
    Check(parts.Layouts == 3, "a profile's layouts are built on the next apply");
    parts.Settings.RecordMode = false;
    parts.Play.IsInGameWorld = true;
    parts.Session.ApplySettings();
    Check(parts.Overlay.Shown, "turning record mode off during a play shows the overlay again");
    Check(parts.Viewers.Shown, "turning record mode off during a play shows the key viewers again");
    parts.Settings.RecordMode = true;
    parts.Session.ApplySettings();
    Check(!parts.Viewers.Shown, "turning record mode on during a play hides the key viewers");
    parts.Settings.RecordMode = false;
    parts.Session.ApplySettings();
    Check(parts.Viewers.Shown && parts.Viewers.Unloads == 0, "switching record mode never unloads the pages");
  }

  private static void ElementNoteOpensWhenAutoOpenTurnsOn()
  {
    var parts = new Parts();
    parts.Session.ApplySettings();
    parts.Session.ApplySettings();
    Check(parts.App.Installs == 1 && parts.App.Opens == 0, "the bundled app is checked once at startup");
    parts.Settings.AutoOpenCloseDmNote = true;
    parts.Session.ApplySettings();
    parts.Session.ApplySettings();
    Check(parts.App.Opens == 1 && parts.App.Installs == 1, "turning auto open on opens it once");
  }

  private sealed class Parts
  {
    public readonly ElementSettings Settings = new() { AutoOpenCloseDmNote = false, RecordMode = false };
    public readonly FakePlay Play = new();
    public readonly FakeOverlay Overlay = new();
    public readonly FakeViewers Viewers = new();
    public readonly FakeRecording Recording = new();
    public readonly FakeApp App = new();
    public bool Editing;
    public int Looks;
    public int Layouts;
    public readonly GameplaySession Session;

    public Parts() =>
      Session = new GameplaySession(
        () => Settings,
        Play,
        Overlay,
        Viewers,
        Recording,
        App,
        () => Editing,
        () => Looks++,
        () => Layouts++
      );
  }

  private sealed class FakePlay : IPlayState
  {
    public bool IsCoop { get; set; }
    public bool IsInGameWorld { get; set; }
  }

  private sealed class FakeOverlay : IGameplayOverlay
  {
    public bool Shown;

    public void Show() => Shown = true;

    public void Hide() => Shown = false;
  }

  private sealed class FakeViewers : IInGameKeyViewers
  {
    public bool Supported { get; set; } = true;
    public bool Enabled = true;
    public KeyViewerPresence Presence;
    public int Unloads;
    public bool HasEnabledViewers => Enabled;
    public bool Shown => Presence == KeyViewerPresence.Shown;
    public bool Off => Presence == KeyViewerPresence.Off;

    public void SetPresence(KeyViewerPresence presence)
    {
      if (presence == KeyViewerPresence.Off && Presence != KeyViewerPresence.Off)
        Unloads++;
      Presence = presence;
    }
  }

  private sealed class FakeRecording : IRecordMode
  {
    public int Reapplied;
    public bool IsActive { get; set; }

    public void Apply(bool active) => IsActive = active;

    public void ReapplyCurrentScene() => Reapplied++;
  }

  private sealed class FakeApp : IElementNoteApp
  {
    public int Installs;
    public int Opens;

    public void InstallIfClosed() => Installs++;

    public void OpenIfClosed() => Opens++;
  }
}
