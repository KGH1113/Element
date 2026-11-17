using ElementUI;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.Canvas;

/// <summary>
/// The toolbar's bottom line: the hovered control's hint, or for a few
/// seconds an error (warm) or a note on what a press did (white). Anything in
/// the editor may write it; the toolbar shows <see cref="Text"/> in <see cref="Ink"/>.
/// </summary>
internal sealed class CanvasStatusLine
{
  private string _hint = "";
  private float _until;

  /// <summary>What the line says now.</summary>
  public readonly State<string> Text = new("");

  /// <summary>Its color: muted for a hint, white for a note, warm for an error.</summary>
  public readonly State<UiColor> Ink = new(Colors.Muted);

  /// <summary>Whether an error or note is showing.</summary>
  public bool Showing => Time.unscaledTime < _until;

  public void ShowError(string message) => Show(message, Colors.Danger, 5);

  /// <summary>A plain note, not an error: what a press did when it had nothing to change.</summary>
  public void ShowNote(string message) => Show(message, Colors.Foreground, 4);

  /// <summary>Ends the error or note now.</summary>
  public void Clear()
  {
    _until = 0;
    ShowHint();
  }

  /// <summary>The hovered control's hint, shown while no error or note is; null when the pointer left it.</summary>
  public void Hint(string hint)
  {
    _hint = hint ?? "";
    if (!Showing)
      ShowHint();
  }

  /// <summary>Once a frame: the hint comes back once an error or note is over.</summary>
  public void Tick()
  {
    if (_until > 0 && !Showing)
      Clear();
  }

  private void Show(string message, UiColor ink, float seconds)
  {
    Text.Value = message;
    Ink.Value = ink;
    _until = Time.unscaledTime + seconds;
  }

  private void ShowHint()
  {
    Text.Value = _hint;
    Ink.Value = Colors.Muted;
  }
}
