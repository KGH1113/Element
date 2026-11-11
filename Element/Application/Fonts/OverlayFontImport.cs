using System.Collections.Generic;
using Element.Domain.Elements;

namespace Element.Application.Fonts;

/// <summary>What importing font files added, and what it could not.</summary>
internal sealed class OverlayFontImport
{
  public readonly List<OverlayFontFace> Added = new();

  // File names that hold no font this PC can read.
  public readonly List<string> Unreadable = new();

  // File names that could not be copied into the Fonts folder.
  public readonly List<string> Failed = new();

  public bool Cancelled;
}
