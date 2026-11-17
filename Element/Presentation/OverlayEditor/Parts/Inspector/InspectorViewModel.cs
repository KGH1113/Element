using System;
using System.Collections.Generic;
using Element.Presentation.OverlayEditor.Parts.Property;
using ElementUI;

namespace Element.Presentation.OverlayEditor.Parts.Inspector;

/// <summary>
/// What an editor's property column shows: a heading over one row per
/// property (font, size, color… for a text; fill, border… for a shape), or,
/// while one of them has a tool open in the column (a list, a picker), a
/// back row naming that tool. The rows go with the column.
/// </summary>
internal sealed class InspectorViewModel : ViewModel
{
  public InspectorViewModel(string heading, IReadOnlyList<PropertyButtonViewModel> rows, Action back)
  {
    foreach (PropertyButtonViewModel row in rows)
      Own(row);
    Heading = heading;
    Rows = rows;
    Back = Command(back);
    ToolOpen = Computed(() => Open.Value.Length > 0);
  }

  /// <summary>Over the rows, such as "Text".</summary>
  public readonly string Heading;

  public readonly IReadOnlyList<PropertyButtonViewModel> Rows;

  /// <summary>The tool open in the column, as its back row names it; empty while the rows show.</summary>
  public readonly State<string> Open = new("");

  /// <summary>A tool is open in the column: the back row shows instead of the rows.</summary>
  public readonly Computed<bool> ToolOpen;

  /// <summary>The tool open in the column, as its back row names it; null while the rows show.</summary>
  public string OpenTool => ToolOpen.Value ? Open.Value : null;

  /// <summary>Closes the open tool: the rows come back.</summary>
  public Command Back { get; }

  /// <summary>The rows, or the back row naming <paramref name="tool"/> (null for none).</summary>
  public void ShowOpen(string tool) => Open.Value = tool ?? "";
}
