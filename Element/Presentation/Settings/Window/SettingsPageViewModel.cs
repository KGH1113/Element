using Element.Configuration;
using ElementUI;

namespace Element.Presentation.Settings.Window;

/// <summary>
/// One page of the settings window: what it shows and what it can do. The
/// window shows one page at a time; Back goes to <see cref="Parent"/>.
/// </summary>
internal abstract class SettingsPageViewModel : ViewModel
{
  protected SettingsPageViewModel(SettingsWindowViewModel window, SettingsPageViewModel parent, string title)
  {
    Window = window;
    Parent = parent;
    Settings = window.Settings;
    Title = title;
  }

  public readonly SettingsWindowViewModel Window;

  /// <summary>The page Back returns to; null for the first page.</summary>
  public readonly SettingsPageViewModel Parent;

  public readonly ElementSettings Settings;

  /// <summary>The window's title while the page shows. A page may change it, as the preset editor does for a new preset.</summary>
  public string Title;

  /// <summary>The window opened.</summary>
  public virtual void Opened() { }

  /// <summary>The page became the one shown.</summary>
  public virtual void Shown() { }

  /// <summary>Every frame while the page is shown: polling that can't be an event, such as planet previews.</summary>
  public virtual void Tick() { }

  /// <summary>Every frame while the window is open. While the page records a key it takes it and returns true.</summary>
  public virtual bool TickCapture() => false;

  /// <summary>Stops recording a key.</summary>
  public virtual void CancelCapture() { }

  /// <summary>Escape closes something on the page; false leaves Escape to Back.</summary>
  public virtual bool HandleEscape() => false;

  /// <summary>Another profile's values replaced the settings in place.</summary>
  public virtual void SettingsReplaced() { }

  /// <summary>The window closed.</summary>
  public virtual void Closed() { }
}
