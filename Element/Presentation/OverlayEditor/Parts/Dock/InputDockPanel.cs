using System;
using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Popovers;
using ElementUI;
using TMPro;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.Parts.Dock;

/// <summary>
/// Where the input dock sits: along the bottom of an editor, as wide as its
/// content. It puts <see cref="InputDockView"/> on a panel of the editor's
/// frame and answers where the dock's parts are, for what opens above the
/// keypad and for a carried value. It holds nothing the dock shows: that is
/// <see cref="InputDockViewModel"/>'s.
/// </summary>
internal sealed class InputDockPanel
{
  private const float BottomGap = 16;

  private readonly ScreenPanel _panel;
  private readonly InputDockViewModel _vm;
  private readonly InputDockView _view;
  private readonly float _width;

  /// <param name="vm">What the dock shows; the panel lets it go with the frame.</param>
  public InputDockPanel(EditorFramePanel screen, InputDockViewModel vm, bool reduceMotion)
  {
    _vm = vm;
    _width = screen.ContentWidth;
    float height = FormulaLineView.Height + InputDockView.LineGap + KeypadView.Height;
    Top = screen.Height - BottomGap - height;
    Left = screen.ContentLeft;
    _panel = screen.Panel("Input dock", Color.clear);
    _view = new InputDockView(vm, _width);
    _panel.Mount(
      screen.NewBackend(),
      _view
        .Environment(EditorEnvironment.Measure, Measurer(screen.Measure))
        .Environment(EditorEnvironment.ReduceMotion, reduceMotion),
      vm,
      _width
    );
    // The notice line sits above the formula line; Top stays the formula line's.
    Root.anchoredPosition = new Vector2(Left, -(Top - InputDockView.NoticeHeight));
  }

  public RectTransform Root => _panel.Root;

  /// <summary>Where the dock starts, in frame units; previews stay above it.</summary>
  public float Top { get; }

  private float Left { get; }

  /// <summary>The keypad's column, in frame units, for what opens right above it.</summary>
  public Rect KeypadArea =>
    new(
      Left + _width - KeypadView.Width,
      Top + FormulaLineView.Height + InputDockView.LineGap,
      KeypadView.Width,
      KeypadView.Height
    );

  /// <summary>Once a frame: a part of the values shown for the first time gets its pointer.</summary>
  public void Tick()
  {
    _vm.Values.FollowMade();
    _panel.Sync();
  }

  public bool Contains(Vector2 screen) => PopoverPlacement.Contains(Root, screen);

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
