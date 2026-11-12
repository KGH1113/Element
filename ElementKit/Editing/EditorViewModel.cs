using ElementUI;

namespace ElementKit;

/// <summary>
/// What an editing screen written with ElementUI reads and changes: your
/// element's <see cref="Settings"/>. Bind controls straight to its fields,
/// <c>Bind(() =&gt; VM.Settings.Width)</c>; Element records each change for
/// undo, shows it on the preview and saves it when the player is done.
/// </summary>
/// <remarks>
/// Undo puts back an earlier copy of the settings, so read them through
/// <see cref="Settings"/> every time rather than keeping the object.
/// For a screen with logic of its own (a button that resets several fields),
/// write a view model of your own that takes this one and changes
/// <c>Settings</c> in its commands.
/// </remarks>
/// <typeparam name="TSettings">The element's settings class.</typeparam>
public sealed class EditorViewModel<TSettings> : ViewModel
  where TSettings : class, new()
{
  private readonly OverlayElement<TSettings> _element;

  internal EditorViewModel(OverlayElement<TSettings> element, bool korean)
  {
    _element = element;
    Korean = korean;
  }

  /// <summary>The settings as they are now: the preview's, which Element saves.</summary>
  public TSettings Settings
  {
    [Pure]
    get => _element.Settings;
  }

  /// <summary>Whether players see Element in Korean.</summary>
  public bool Korean { get; }

  /// <summary><paramref name="korean"/> or <paramref name="english"/>, as players see Element.</summary>
  [Pure]
  public string L(string korean, string english) => Korean ? korean : english;
}
