namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>How a place that can take a carried value looks while one is carried.</summary>
internal enum ValueDrop
{
  /// <summary>The value isn't over it.</summary>
  None,

  /// <summary>The value is over it, and letting go there would use it.</summary>
  Accepts,

  /// <summary>The value is over it, but it can't take it (words where a number goes).</summary>
  Refuses,
}
