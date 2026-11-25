using Element.Domain.Overlay.Variables;

namespace Element.Presentation.OverlayEditor.Parts.Formula;

/// <summary>
/// Where the values the player makes are kept, shared by every element: the
/// dock makes one, renames it and writes its variable here, and every element
/// that uses it follows.
/// </summary>
internal interface ICustomValueBook
{
  /// <summary>A new value that gives <paramref name="type"/>, with nothing in it yet; its key.</summary>
  string Make(OverlayValueType type);

  /// <summary>The value <paramref name="key"/> now holds <paramref name="variable"/>.</summary>
  void Write(string key, OverlayVariable variable);

  void Rename(string key, string name);
}
