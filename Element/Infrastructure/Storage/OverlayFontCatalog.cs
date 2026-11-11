using System;
using System.Collections.Generic;
using System.IO;
using Element.Application.Fonts;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Infrastructure.Platform;
using TMPro;
using UnityEngine.TextCore.LowLevel;

namespace Element.Infrastructure.Storage;

/// <summary>
/// The fonts the overlay can use: files copied into the mod's Fonts folder,
/// which survives updates, and the fonts installed on this PC. The installed
/// list is read once per session on a worker thread; the folder is read on
/// demand on the main thread, as FontEngine requires.
/// </summary>
internal sealed class OverlayFontCatalog : IOverlayFontSource, IOverlayFontLibrary
{
  // Installed fonts known only by name are loaded through the OS at this size;
  // TMP pads them by 9, which keeps the bundled (padding + 1) / size ratio.
  private const int NamedFontPointSize = 120;

  // Labels such as "Progress" are made of these.
  private const string LatinSample = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

  private readonly Action<string, Exception> _error;
  private readonly OverlayFontFolder _folder;
  private readonly InstalledFontList _installed;

  public OverlayFontCatalog(string folder, IFontFilePicker picker, Action<string> warn, Action<string, Exception> error)
  {
    _error = error;
    _folder = new OverlayFontFolder(folder, picker, warn, error);
    _installed = new InstalledFontList(warn, error);
  }

  /// <summary>Goes up when either list changes.</summary>
  public int Revision => _folder.Revision + _installed.Revision;

  public IReadOnlyList<OverlayFontFace> Imported => _folder.Faces;

  public IReadOnlyList<OverlayFontFace> Installed
  {
    get
    {
      Poll();
      return _installed.Faces;
    }
  }

  public bool CanPickFiles => _folder.CanPickFiles;

  public OverlayFontLookup Find(string key, out OverlayFontFace face)
  {
    face = null;
    if (!OverlayFontKey.TryParse(key, out string family, out string style))
      return OverlayFontLookup.Missing;
    face = OverlayFontFaces.Match(_folder.Faces, family, style);
    if (face != null)
      return OverlayFontLookup.Found;
    Poll();
    if (_installed.Faces == null)
    {
      RequestInstalled();
      return OverlayFontLookup.Pending;
    }
    face = OverlayFontFaces.Match(_installed.Faces, family, style);
    return face != null ? OverlayFontLookup.Found : OverlayFontLookup.Missing;
  }

  public TMP_FontAsset Create(OverlayFontFace face)
  {
    if (face == null)
      return null;
    try
    {
      if (face.Path == null)
        return TMP_FontAsset.CreateFontAsset(face.Family, face.Style, NamedFontPointSize);
      if (!File.Exists(face.Path))
        return null;
      return TMP_FontAsset.CreateFontAsset(
        face.Path,
        face.FaceIndex,
        OverlayFontSampling.PointSize,
        OverlayFontSampling.Padding,
        GlyphRenderMode.SDFAA,
        OverlayFontSampling.AtlasSize,
        OverlayFontSampling.AtlasSize
      );
    }
    catch (Exception exception)
    {
      _error("Load the font " + face.Key, exception);
      return null;
    }
  }

  public bool LacksLatin(OverlayFontFace face)
  {
    if (face == null)
      return false;
    try
    {
      FontEngineError loaded =
        face.Path == null
          ? FontEngine.LoadFontFace(face.Family, face.Style)
          : FontEngine.LoadFontFace(face.Path, OverlayFontSampling.PointSize, face.FaceIndex);
      if (loaded != FontEngineError.Success)
        return false;
      foreach (char character in LatinSample)
        if (!FontEngine.TryGetGlyphIndex(character, out uint glyph) || glyph == 0)
          return true;
    }
    catch (Exception exception)
    {
      _error("Check the Latin letters of the font " + face.Key, exception);
    }
    return false;
  }

  public TMP_FontAsset CreateLatinFallback(string style)
  {
    foreach (string family in LatinFallbackFamilies())
    {
      OverlayFontFace face = SystemFace(family, style) ?? SystemFace(family, OverlayFontKey.RegularStyle);
      TMP_FontAsset asset = face == null ? null : Create(face);
      if (asset != null)
        return asset;
    }
    return null;
  }

  // The fonts the OS itself draws Latin text with when a font has none:
  // CoreText's cascade starts with Helvetica, DirectWrite's with Segoe UI.
  private static string[] LatinFallbackFamilies() =>
    UnityEngine.Application.platform switch
    {
      UnityEngine.RuntimePlatform.OSXPlayer or UnityEngine.RuntimePlatform.OSXEditor => new[]
      {
        "Helvetica",
        "Helvetica Neue",
        "Arial",
      },
      UnityEngine.RuntimePlatform.WindowsPlayer or UnityEngine.RuntimePlatform.WindowsEditor => new[]
      {
        "Segoe UI",
        "Arial",
      },
      _ => new[] { "DejaVu Sans", "Liberation Sans", "Noto Sans" },
    };

  // From the installed list when it is ready; otherwise by name, which the OS
  // resolves without the list.
  private OverlayFontFace SystemFace(string family, string style)
  {
    Poll();
    if (_installed.Faces != null)
      return OverlayFontFaces.Match(_installed.Faces, family, style);
    return FontEngine.LoadFontFace(family, style) == FontEngineError.Success
      ? new OverlayFontFace(family, style, null, 0, false)
      : null;
  }

  public void Poll() => _installed.Poll();

  public void RequestInstalled() => _installed.Request();

  public void RefreshImported() => _folder.Refresh();

  public OverlayFontImport ImportFiles(string title) => _folder.ImportFiles(title);

  public void OpenFolder() => _folder.OpenFolder();
}
