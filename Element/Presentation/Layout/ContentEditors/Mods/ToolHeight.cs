namespace Element.Presentation.Layout.ContentEditors.Mods;

/// <summary>How tall a tool of the column is.</summary>
internal enum ToolHeight
{
  /// <summary>As tall as it wants: a color picker, the nine points.</summary>
  Own,

  /// <summary>A list of rows, scrolling when taller than the column.</summary>
  Scroll,

  /// <summary>As tall as the column: the font list, which scrolls itself.</summary>
  Fill,
}
