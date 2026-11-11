using System;
using System.Collections.Generic;
using System.IO;
using Element.Domain.Elements;
using Element.Infrastructure.Platform;
using UnityEngine.TextCore.LowLevel;

namespace Element.Infrastructure.Storage;

/// <summary>
/// Reads the faces in a font file. FontEngine keeps one current face, so this runs
/// on the main thread only.
/// </summary>
internal static class OverlayFontFiles
{
  private const int MaximumFacesPerFile = 64;

  public static List<OverlayFontFace> ReadFaces(string path, bool imported, Action<string, Exception> error)
  {
    List<OverlayFontFace> faces = new();
    try
    {
      for (int index = 0; index < MaximumFacesPerFile; index++)
      {
        if (FontEngine.LoadFontFace(path, OverlayFontSampling.PointSize, index) != FontEngineError.Success)
          break;
        UnityEngine.TextCore.FaceInfo info = FontEngine.GetFaceInfo();
        if (!string.IsNullOrWhiteSpace(info.familyName))
          faces.Add(new OverlayFontFace(info.familyName.Trim(), info.styleName?.Trim(), path, index, imported));
        if (!IsCollection(path))
          break;
      }
    }
    catch (Exception exception)
    {
      error("Read the font file " + Path.GetFileName(path), exception);
    }
    return faces;
  }

  public static bool IsFontFile(string path)
  {
    string extension = Path.GetExtension(path);
    if (string.IsNullOrEmpty(extension))
      return false;
    extension = extension.Substring(1);
    foreach (string font in FontFilePickers.Extensions)
      if (string.Equals(extension, font, StringComparison.OrdinalIgnoreCase))
        return true;
    return false;
  }

  private static bool IsCollection(string path)
  {
    string extension = Path.GetExtension(path);
    return string.Equals(extension, ".ttc", StringComparison.OrdinalIgnoreCase)
      || string.Equals(extension, ".otc", StringComparison.OrdinalIgnoreCase);
  }
}
