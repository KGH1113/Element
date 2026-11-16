using System;
using System.Collections.Generic;
using ElementUI;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// What an editor's property column shows: a heading over one row per
/// property (font, size, color… for a text; fill, border… for a shape), or,
/// while one of them has a tool open in the column (a list, a picker), a
/// back row naming that tool. The rows go with the column.
/// </summary>
internal sealed class OverlayInspectorModel : ViewModel
{
  public OverlayInspectorModel(string heading, IReadOnlyList<OverlayBarButtonModel> rows, Action back)
  {
    foreach (OverlayBarButtonModel row in rows)
      Own(row);
    Heading = heading;
    Rows = rows;
    Back = Command(back);
  }

  /// <summary>Over the rows, such as "Text".</summary>
  public readonly string Heading;

  public readonly IReadOnlyList<OverlayBarButtonModel> Rows;

  /// <summary>The tool open in the column, as its back row names it; empty while the rows show.</summary>
  public readonly State<string> Open = new("");

  /// <summary>A tool is open in the column: the back row shows instead of the rows.</summary>
  public readonly State<bool> ToolOpen = new(false);

  /// <summary>Closes the open tool: the rows come back.</summary>
  public Command Back { get; }

  /// <summary>The rows, or the back row naming <paramref name="tool"/> (null for none).</summary>
  public void ShowOpen(string tool)
  {
    Open.Value = tool ?? "";
    ToolOpen.Value = !string.IsNullOrEmpty(tool);
  }

  /// <summary>Goes up each time a row's shown value changes.</summary>
  public int Revision
  {
    get
    {
      int revision = 0;
      for (int i = 0; i < Rows.Count; i++)
        revision += Rows[i].Revision;
      return revision;
    }
  }
}
