using System;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using TMPro;
using UnityEngine;

namespace Element.Presentation.Layout.Canvas;

/// <summary>
/// The toolbar's bottom line: the hovered control's hint, or for a few
/// seconds an error (warm) or a note on what a press did (white).
/// </summary>
internal sealed class LayoutStatusLine
{
  private TextMeshProUGUI _text;
  private float _until;
  private bool _note;
  private string _hint = "";

  public void Build(UiFactory ui, RectTransform toolbar)
  {
    _text = ui.Text("", toolbar, 12, 102, LayoutSession.ToolbarWidth - 24, 14, 11, UiPalette.Muted);
    _text.alignment = TextAlignmentOptions.Midline;
  }

  /// <summary>Whether an error or note is showing.</summary>
  public bool Showing => Time.unscaledTime < _until;

  public void ShowError(string message)
  {
    _note = false;
    _text.text = message;
    _until = Time.unscaledTime + 5;
  }

  /// <summary>A plain note, not an error: what a press did when it had nothing to change.</summary>
  public void ShowNote(string message)
  {
    _text.text = message;
    _until = Time.unscaledTime + 4;
    _note = true;
  }

  /// <summary>Ends the error or note now.</summary>
  public void Clear() => _until = 0;

  /// <summary>Shows <paramref name="hint"/> while its control is hovered or focused.</summary>
  public Action<bool> HintFor(string hint) => visible => _hint = visible ? hint : "";

  /// <summary>Once a frame: the message's color, and the hint once it is over.</summary>
  public void Refresh()
  {
    bool message = Showing;
    _text.color =
      !message ? UiPalette.Muted
      : _note ? UiPalette.Foreground
      : UiPalette.Error;
    if (!message)
      _note = false;
    if (Time.unscaledTime >= _until)
      _text.text = _hint;
  }
}
