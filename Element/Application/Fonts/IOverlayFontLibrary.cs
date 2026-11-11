using System.Collections.Generic;
using Element.Domain.Elements;

namespace Element.Application.Fonts;

/// <summary>What the font picker lists, and adding font files to it.</summary>
internal interface IOverlayFontLibrary
{
  /// <summary>Fonts copied into the mod's Fonts folder.</summary>
  IReadOnlyList<OverlayFontFace> Imported { get; }

  /// <summary>Fonts installed on this PC; null while they are being listed.</summary>
  IReadOnlyList<OverlayFontFace> Installed { get; }

  /// <summary>Changes whenever either list changes.</summary>
  int Revision { get; }

  /// <summary>Starts listing installed fonts in the background, once.</summary>
  void RequestInstalled();

  /// <summary>Reads the Fonts folder again, for files added by hand.</summary>
  void RefreshImported();

  /// <summary>False where no file dialog exists; the folder opens instead.</summary>
  bool CanPickFiles { get; }

  /// <summary>Shows the system's file dialog and copies the chosen fonts into the Fonts folder.</summary>
  OverlayFontImport ImportFiles(string title);

  void OpenFolder();
}
