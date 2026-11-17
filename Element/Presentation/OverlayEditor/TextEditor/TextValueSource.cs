using System;
using System.Collections.Generic;
using Element.Domain.Overlay.Values;
using Element.Presentation.Overlay.Values;
using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.OverlayEditor.Parts.ValueSource;
using Element.Presentation.Ui.Popovers;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.TextEditor;

/// <summary>
/// Which value the text's color follows: a menu under the value row with the
/// numbers the text shows, then "Another value", after which the next value
/// clicked in the dock is the one (words are refused with a reason). The
/// owner (<see cref="TextValueColor"/>) makes the color follow the value
/// picked.
/// </summary>
internal sealed class TextValueSource
{
  private readonly IEditorActions _editor;
  private readonly TextDocumentEditing _text;
  private readonly OverlayValues _values;
  private readonly Action<OverlayValueDefinition> _picked;
  private readonly List<OverlayValueId> _inText = new();
  private readonly List<OverlayValueDefinition> _offered = new();
  private ValueSourceMenuPanel _menu;
  private Func<Rect> _row;
  private bool _armed;

  /// <param name="picked">A value to follow was picked.</param>
  public TextValueSource(
    IEditorActions editor,
    TextDocumentEditing text,
    OverlayValues values,
    Action<OverlayValueDefinition> picked
  )
  {
    _editor = editor;
    _text = text;
    _values = values;
    _picked = picked;
  }

  /// <summary>The menu of values; made by <see cref="Build"/>.</summary>
  public IPopover Menu => _menu;

  /// <summary>The menu is open.</summary>
  public bool Open => _menu.Visible;

  /// <summary>
  /// Waiting for the value to follow: the next value clicked in the dock is
  /// it (<see cref="PickFromDock"/>), not one added to the text. The editor
  /// sends the dock's clicks here while this is on.
  /// </summary>
  public bool Armed => _armed;

  /// <summary>The menu, opening under the value row <paramref name="row"/> says is where (frame units).</summary>
  public void Build(EditorFramePanel screen, Func<Rect> row)
  {
    _menu = new ValueSourceMenuPanel(screen);
    _row = row;
  }

  /// <summary>The value row: offers the values, <paramref name="followed"/> (a key) ticked; a second press puts them away.</summary>
  public void Toggle(string followed)
  {
    if (_menu.Visible || Armed)
    {
      _editor.CloseOpen();
      _editor.Render();
      return;
    }
    _editor.CloseOpen();
    _offered.Clear();
    _inText.Clear();
    _text.Document.ValuesIn(0, _text.Document.Count, _inText);
    foreach (OverlayValueId id in _inText)
    {
      OverlayValueDefinition value = OverlayValueCatalog.Get(id);
      if (value.IsNumeric && value.Available)
        _offered.Add(value);
    }
    Rect row = _row();
    _menu.Show(_offered, followed ?? "", Picked, Arm, row.xMax, row.yMax + 6);
    _editor.Render();
  }

  /// <summary>The dock goes back to adding values at the caret, and the menu closes.</summary>
  public void Close()
  {
    Disarm();
    _menu?.Hide();
  }

  /// <summary>The dock goes back to adding values at the caret.</summary>
  public void Disarm() => _armed = false;

  /// <summary>The first number the range shows; null when it shows none.</summary>
  public OverlayValueDefinition FirstInRange()
  {
    _text.Range(out int start, out int end);
    _inText.Clear();
    _text.Document.ValuesIn(start, end, _inText);
    foreach (OverlayValueId id in _inText)
      if (OverlayValueCatalog.Get(id).IsNumeric)
        return OverlayValueCatalog.Get(id);
    return null;
  }

  /// <summary>The value's number now, or (<paramref name="sample"/>) its example; NaN with neither.</summary>
  public double Number(OverlayValueDefinition value, out bool sample)
  {
    sample = false;
    if (_values.TryGetNumber(value.Id, out double number))
      return number;
    sample = true;
    return _values.WithSamples.TryGetNumber(value.Id, out number) ? number : double.NaN;
  }

  // The next value clicked in the dock is the one to follow.
  private void Arm()
  {
    _editor.CloseOpen();
    _armed = true;
    _editor.Notice(
      L("아래 값 목록에서 따라갈 값을 눌러 주세요.", "Click the value to follow in the values below."),
      false
    );
    _editor.Render();
  }

  /// <summary>The value clicked in the dock while <see cref="Armed"/>: a number is followed, words are refused with a reason.</summary>
  public void PickFromDock(OverlayValueDefinition value)
  {
    if (!value.IsNumeric)
    {
      _editor.Notice(
        L(
          ValueSectionNames.ValueName(value) + "은 글자라서 색을 정할 수 없어요. 숫자인 값을 골라 주세요.",
          ValueSectionNames.ValueName(value) + " is words, so it can't pick a color. Pick a value that is a number."
        )
      );
      _editor.Render();
      return;
    }
    Picked(value);
  }

  private void Picked(OverlayValueDefinition value)
  {
    Close();
    _picked(value);
  }
}
