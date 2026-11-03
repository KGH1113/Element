using System.Collections.Generic;
using Element.Domain.Elements;
using TMPro;

namespace Element.Presentation.Overlay.Shared;

internal enum OverlayFontLookup
{
  Found,

  // Installed fonts are still being listed; ask again when Revision changes.
  Pending,
  Missing,
}

/// <summary>Finds a saved font on this PC and loads it for TextMeshPro.</summary>
internal interface IOverlayFontSource
{
  /// <summary>Changes when fonts become findable: the installed list arrived or a file was imported.</summary>
  int Revision { get; }

  OverlayFontLookup Find(string key, out OverlayFontFace face);

  /// <summary>A dynamic font asset for the face, or null when it can't be read.</summary>
  TMP_FontAsset Create(OverlayFontFace face);

  /// <summary>Takes the installed list once it is ready. Main thread, once per frame at most.</summary>
  void Poll();
}

internal sealed class OverlayFontImport
{
  public readonly List<OverlayFontFace> Added = new();

  // File names that hold no font this PC can read.
  public readonly List<string> Unreadable = new();

  // File names that could not be copied into the Fonts folder.
  public readonly List<string> Failed = new();

  public bool Cancelled;
}

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
