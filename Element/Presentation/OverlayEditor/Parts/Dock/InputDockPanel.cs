using System;
using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Popovers;
using ElementUI;
using TMPro;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.Parts.Dock;

/// <summary>
/// Where the input dock sits in an editor of two columns: the line and the
/// values at the bottom of the left one (<see cref="InputDockView"/>), the
/// input method at the bottom of the right one (<see cref="InputMethodView"/>),
/// a hairline between the columns. Neither reaches into the other. It
/// answers where its parts are, for what is laid out around them. It holds
/// nothing the dock shows: that is <see cref="InputDockViewModel"/>'s.
/// </summary>
internal sealed class InputDockPanel
{
  private const float BottomGap = 16;

  private readonly ScreenPanel _panel;
  private readonly ScreenPanel _method;
  private readonly InputDockViewModel _vm;
  private readonly float _left;
  private readonly float _right;

  /// <param name="vm">What the dock shows; the panel lets it go with the frame.</param>
  public InputDockPanel(EditorFramePanel screen, InputDockViewModel vm, bool reduceMotion)
  {
    _vm = vm;
    _left = screen.ContentLeft;
    _right = screen.ContentLeft + screen.ContentWidth - KeypadView.Width;
    float width = screen.ContentWidth - KeypadView.Width - InputDockView.ColumnGap;
    Top = screen.Height - BottomGap - InputDockView.Height;
    // The line between the columns, from under the header to the bottom.
    float line = Mathf.Round(_right - InputDockView.ColumnGap * .5f);
    screen
      .Ui.Panel(
        "Column line",
        screen.Frame,
        line,
        EditorFramePanel.TopBar + 8,
        1,
        screen.Height - BottomGap - EditorFramePanel.TopBar - 8,
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
    Root.anchoredPosition = new Vector2(Mathf.Round(_left), -Mathf.Round(Top));
    _method = screen.Panel("Input method", Color.clear);
    _method.Mount(
      screen.NewBackend(),
      new InputMethodView(vm).Environment(EditorEnvironment.ReduceMotion, reduceMotion),
      // The dock's own panel lets the view model go; this one only draws it.
      null,
      KeypadView.Width
    );
    MethodRoot.anchoredPosition = new Vector2(Mathf.Round(_right), -Mathf.Round(Top));
  }

  /// <summary>The left part: the line and the values.</summary>
  public RectTransform Root => _panel.Root;

  /// <summary>The right part: the input method over Remove and Apply.</summary>
  public RectTransform MethodRoot => _method.Root;

  /// <summary>Where the dock starts (its hairline), in frame units; the preview stays above it, the fields above the input method.</summary>
  public float Top { get; }

  /// <summary>The right column's lower part, in frame units: the input method and the row under it.</summary>
  public Rect KeypadArea => new(_right, Top, KeypadView.Width, KeypadView.Height);

  /// <summary>Where an input method is drawn: a solid color's picker and the font list stand there.</summary>
  public Rect MethodArea => new(_right, Top, KeypadView.Width, KeypadView.MethodHeight);

  /// <summary>Once a frame: the screens follow what changed.</summary>
  public void Tick()
  {
    _panel.Sync();
    _method.Sync();
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
