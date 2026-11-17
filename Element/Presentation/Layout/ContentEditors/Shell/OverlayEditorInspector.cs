using System;
using Element.Presentation.Layout.ContentEditors.Shell.Dock;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Popovers;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// The text and shape editors' property column: right of the preview, above
/// the input dock's keypad and as wide as it. One row per property
/// (<see cref="OverlayInspectorView"/>); a row that opens a tool (the color
/// picker, the font list, the grow point) opens it in this column, under a
/// back row that names it. Numbers a row holds are typed in the dock below.
/// </summary>
internal sealed class OverlayEditorInspector
{
  /// <summary>Between the column and what is above or below it.</summary>
  public const float Gap = 12;

  private readonly ScreenPanel _panel;
  private readonly OverlayInspectorModel _model;

  /// <param name="heading">Over the rows, such as "Text".</param>
  /// <param name="back">The back row: closes the open tool.</param>
  public OverlayEditorInspector(
    OverlayEditorFrame screen,
    InputDockPanel dock,
    string name,
    string heading,
    Action back,
    params OverlayBarButtonModel[] rows
  )
  {
    Rect column = dock.KeypadArea;
    Left = column.x;
    Width = column.width;
    Top = OverlayEditorFrame.TopBar + Gap;
    Bottom = dock.Top - Gap;
    _model = new OverlayInspectorModel(heading, rows, back);
    _panel = screen.Panel(name, Color.clear);
    _panel.Mount(screen.NewBackend(), new OverlayInspectorView(_model, Width), _model, Width, followsContent: true);
    Root.anchoredPosition = new Vector2(Mathf.Round(Left), -Mathf.Round(Top));
  }

  public RectTransform Root => _panel.Root;

  /// <summary>The column's left edge, in frame units; the preview keeps to the space before it.</summary>
  public float Left { get; }

  public float Width { get; }

  public float Right => Left + Width;

  /// <summary>Where the column starts, in frame units.</summary>
  public float Top { get; }

  /// <summary>Where the column ends, above the dock, in frame units.</summary>
  public float Bottom { get; }

  /// <summary>Where an open tool starts: under the back row.</summary>
  public float ToolTop => Top + OverlayInspectorView.BackRowHeight + 4;

  /// <summary>The tool open in the column, as its back row names it; null while the rows show.</summary>
  public string OpenTool => _model.ToolOpen.Value ? _model.Open.Value : null;

  /// <summary>
  /// The rows, or (with <paramref name="tool"/>) the back row naming the
  /// tool open below, lined up with the tool's left edge when
  /// <paramref name="toolRoot"/> reaches further left than the column (the
  /// two-column picker); fits the column to what its rows show now.
  /// </summary>
  public void Show(string tool, RectTransform toolRoot = null)
  {
    bool open = !string.IsNullOrEmpty(tool);
    _model.ShowOpen(tool);
    float x = open && toolRoot != null ? Mathf.Min(Left, toolRoot.anchoredPosition.x) : Left;
    if (!Mathf.Approximately(Root.anchoredPosition.x, Mathf.Round(x)))
      Root.anchoredPosition = new Vector2(Mathf.Round(x), Root.anchoredPosition.y);
  }

  public bool Contains(Vector2 screen) => PopoverPlacement.Contains(Root, screen);

  /// <summary>Whether a screen point (origin bottom left) is over <paramref name="row"/>.</summary>
  public bool Contains(OverlayBarButtonModel row, Vector2 screen) => PopoverPlacement.Contains(RectOf(row), screen);

  /// <summary>Where <paramref name="row"/> is drawn; null while it isn't (a tool is open).</summary>
  public RectTransform RectOf(OverlayBarButtonModel row)
  {
    Selectable control = _panel.Control<Selectable>(row.Name);
    return control != null && control.gameObject.activeInHierarchy ? (RectTransform)control.transform : null;
  }
}
