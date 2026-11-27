using System;
using System.Collections.Generic;
using Element.Presentation.OverlayEditor.Parts.Dock.Condition;
using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Popovers;
using ElementUI;
using TMPro;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.Parts.Dock;

/// <summary>
/// Where the input dock sits. An editor is two rows, one line across the
/// screen between them: above it what is edited (the preview, and the
/// property column right of it), below it how (the line and the values on
/// the left, <see cref="InputDockView"/>; the input method on the right,
/// <see cref="InputMethodView"/>, under the property column). The dock is
/// the lower row. It answers where its parts are, for what is laid out
/// around them. It holds nothing the dock shows: that is <see cref="InputDockViewModel"/>'s.
/// </summary>
internal sealed class InputDockPanel
{
  private const float BottomGap = 16;

  private readonly ScreenPanel _panel;
  private readonly ScreenPanel _method;
  private readonly InputDockViewModel _vm;
  private readonly float _left;
  private readonly float _right;
  private readonly List<UiVectorGraphic> _drawn = new();
  private bool _arriving;

  /// <param name="vm">What the dock shows; the panel lets it go with the frame.</param>
  public InputDockPanel(EditorFramePanel screen, InputDockViewModel vm, bool reduceMotion)
  {
    _vm = vm;
    _left = screen.ContentLeft;
    _right = screen.ContentLeft + screen.ContentWidth - KeypadView.Width;
    float width = screen.ContentWidth - KeypadView.Width - InputDockView.ColumnGap;
    Top = screen.Height - BottomGap - InputDockView.Height;
    // The line between the two rows, across the content.
    screen
      .Ui.Panel(
        "Row line",
        screen.Frame,
        Mathf.Round(_left),
        Mathf.Round(Top),
        Mathf.Round(screen.ContentWidth),
        1,
        new Color(1, 1, 1, .1f),
        false
      )
      .raycastTarget = false;
    _panel = screen.Panel("Input dock", Color.clear);
    _panel.Mount(
      screen.NewBackend(),
      new InputDockView(vm, width)
        .Environment(EditorEnvironment.Measure, Measurer(screen.Measure))
        .Environment(EditorEnvironment.ReduceMotion, reduceMotion),
      vm,
      width
    );
    // Under the row line.
    Root.anchoredPosition = new Vector2(Mathf.Round(_left), -Mathf.Round(Top + 1));
    _method = screen.Panel("Input method", Color.clear);
    _method.Mount(
      screen.NewBackend(),
      new InputMethodView(vm).Environment(EditorEnvironment.ReduceMotion, reduceMotion),
      // The dock's own panel lets the view model go; this one only draws it.
      null,
      KeypadView.Width
    );
    MethodRoot.anchoredPosition = new Vector2(Mathf.Round(_right), -Mathf.Round(MethodTop));
  }

  /// <summary>The left part: the line and the values.</summary>
  public RectTransform Root => _panel.Root;

  /// <summary>The right part: the input method.</summary>
  public RectTransform MethodRoot => _method.Root;

  /// <summary>Where the dock starts, at the line between the rows, in frame units; the preview and the property column stay above it.</summary>
  public float Top { get; }

  // The input method keeps a little room under the row line; the line on the left has its own.
  private float MethodTop => Top + InputDockView.MethodGap;

  /// <summary>The right of the lower row, in frame units: the input method and the row under it.</summary>
  public Rect KeypadArea => new(_right, MethodTop, KeypadView.Width, KeypadView.Height);

  /// <summary>
  /// Where an input method is drawn: a solid color's picker and the font
  /// list stand there. Inside a condition's slot, that condition's folded
  /// strip has the top of the place.
  /// </summary>
  public Rect MethodArea
  {
    get
    {
      float taken = _vm.Folded.Value ? ConditionStripView.Height + ConditionStripView.Gap : 0;
      return new(_right, MethodTop + taken, KeypadView.Width, KeypadView.MethodHeight - taken);
    }
  }

  /// <summary>Once a frame: the screens follow what changed.</summary>
  public void Tick()
  {
    _panel.Sync();
    _method.Sync();
    FollowArrival();
  }

  // Hand-drawn icons size their one-pixel edge for the scale they are drawn at. While the
  // input method grows into place they are drawn again as it grows, and once more when it
  // is there; drawn only once, small, they would keep an edge that wide and read as blurred.
  private void FollowArrival()
  {
    bool arriving = _vm.Arrival.Running;
    if (!arriving && !_arriving)
      return;
    _arriving = arriving;
    MethodRoot.GetComponentsInChildren(false, _drawn);
    foreach (UiVectorGraphic icon in _drawn)
      icon.FollowScale();
    _drawn.Clear();
  }

  public bool Contains(Vector2 screen) =>
    PopoverPlacement.Contains(Root, screen) || PopoverPlacement.Contains(MethodRoot, screen);

  // How wide words are at the dock's 13-unit size.
  private static Func<string, float> Measurer(TextMeshProUGUI measure) =>
    text =>
    {
      float size = measure.fontSize;
      measure.fontSize = 13;
      float width = Mathf.Ceil(measure.GetPreferredValues(text).x);
      measure.fontSize = size;
      return width;
    };
}
