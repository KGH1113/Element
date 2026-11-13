using System;
using Element.Domain.Overlay.Values;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Popovers;
using TMPro;
using UnityEngine;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// Where formulas are typed: a wide dock along the bottom of an editor, always
/// there, with no card behind it. One full-width line shows what is edited,
/// its formula with values as chips, and the result; below it every value is
/// laid out by category beside a large keypad. With nothing being edited the
/// line says what to do and the keypad rests. Editors keep their preview
/// above it. A panel on the editor's frame shows <see cref="OverlayInputDockView"/>;
/// this places it and answers where its parts are.
/// </summary>
internal sealed class OverlayInputDock
{
  private const float BottomGap = 16;

  private readonly ScreenPanel _panel;
  private readonly OverlayFormulaLine _line;
  private readonly float _width;

  /// <param name="press">A key typed: a digit, the point, an operator or '\b'.</param>
  /// <param name="clear">AC.</param>
  /// <param name="done">Done.</param>
  /// <param name="pick">A value clicked.</param>
  /// <param name="begin">A value starts being carried out of the dock, at a screen point.</param>
  /// <param name="move">The carried value moved.</param>
  /// <param name="end">The carried value was let go.</param>
  public OverlayInputDock(
    OverlayEditorFrame screen,
    Func<bool> reduceMotion,
    Action<char> press,
    Action clear,
    Action done,
    Action<OverlayValueDefinition> pick,
    Action<OverlayValueDefinition, Vector2> begin,
    Action<Vector2> move,
    Action<Vector2> end
  )
  {
    Model = new OverlayInputDockModel(press, clear, done, pick, begin, move, end);
    _width = Mathf.Min(1180, screen.Width - OverlayEditorFrame.Margin * 2);
    float height = OverlayFormulaLine.Height + OverlayInputDockView.LineGap + OverlayKeypadView.Height;
    Top = screen.Height - BottomGap - height;
    Left = (screen.Width - _width) * .5f;
    // The editor's notices show right above the dock instead of in its header.
    screen.Top.InDock = true;
    _line = new OverlayFormulaLine(Model);
    _panel = screen.Panel("Input dock", Color.clear);
    _panel.Mount(
      screen.NewBackend(),
      new OverlayInputDockView(Model, screen.Top, _line, _width, CategoryColumn(Model, screen.Measure), reduceMotion()),
      Model,
      _width
    );
    // The notice line sits above the formula line; Top stays the formula line's.
    Root.anchoredPosition = new Vector2(Left, -(Top - OverlayInputDockView.NoticeHeight));
  }

  /// <summary>What the dock shows and does.</summary>
  public OverlayInputDockModel Model { get; }

  public RectTransform Root => _panel.Root;

  /// <summary>Where the dock starts, in frame units; previews stay above it.</summary>
  public float Top { get; }

  private float Left { get; }

  /// <summary>The keypad's column, in frame units, for what opens right above it.</summary>
  public Rect KeypadArea =>
    new(
      Left + _width - OverlayKeypadView.Width,
      Top + OverlayFormulaLine.Height + OverlayInputDockView.LineGap,
      OverlayKeypadView.Width,
      OverlayKeypadView.Height
    );

  /// <summary>What the dock edits, such as "width" or "value:3"; null while nothing is.</summary>
  public string Owner => Model.Owner;

  /// <summary>A value is being carried out of the dock.</summary>
  public bool Dragging => Model.Dragging;

  public string FormulaText => Model.Formula.Value;
  public string ResultText => Model.Result.Value;
  public string HintText => Owner == null ? Model.Title.Value : "";

  /// <summary>What the dock edits from now on; null to rest.</summary>
  public void Edit(string owner) => Model.Edit(owner);

  /// <summary>
  /// Shows <paramref name="name"/> and the formula <paramref name="editing"/>
  /// types, with <paramref name="result"/> (already in its unit) on the right;
  /// with nothing being edited, <paramref name="hint"/> says what to do.
  /// </summary>
  public void Refresh(OverlayFormulaEditing editing, string name, string result, string hint) =>
    Model.Refresh(editing, name, result, hint);

  public void Blink(bool visible) => Model.Blink(visible);

  public bool Contains(Vector2 screen) => PopoverPlacement.Contains(Root, screen);

  /// <summary>The formula line, where a dropped value joins the formula.</summary>
  public bool FormulaContains(Vector2 screen) =>
    Owner != null && _line.Area != null && PopoverPlacement.Contains(_line.Area, screen);

  /// <summary>0 nothing, 1 a dragged value can join here, -1 it can't.</summary>
  public void SetDrop(int state) => Model.SetDrop(state);

  // The category names' column: as wide as the widest name, and a gap.
  private static float CategoryColumn(OverlayInputDockModel model, TextMeshProUGUI measure)
  {
    float column = 0;
    float size = measure.fontSize;
    measure.fontSize = 13;
    foreach (ValueSection section in model.Sections)
      column = Mathf.Max(column, Mathf.Ceil(measure.GetPreferredValues(section.Name).x));
    measure.fontSize = size;
    return column + 24;
  }
}
