using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Element.Configuration;
using UnityEngine.TextCore.LowLevel;
using Element.Domain.Overlay.Fonts;
using Element.Configuration.Overlay;

namespace Element.Infrastructure.Storage;

/// <summary>
/// The fonts installed on this PC, listed once per session on a worker thread.
/// The main thread takes the list when it is ready.
/// </summary>
internal sealed class InstalledFontList
{
  private readonly Action<string> _warn;
  private readonly Action<string, Exception> _error;
  private List<OverlayFontFace> _faces;
  private List<OverlayFontFace> _result;
  private int _state;

  public InstalledFontList(Action<string> warn, Action<string, Exception> error)
  {
    _warn = warn;
    _error = error;
  }

  /// <summary>Goes up once, when the list arrives.</summary>
  public int Revision { get; private set; }

  /// <summary>Null until the list has arrived; <see cref="Poll"/> takes it.</summary>
  public List<OverlayFontFace> Faces => _faces;

  public void Poll()
  {
    if (_faces != null || Volatile.Read(ref _state) != 2)
      return;
    _faces = _result ?? new List<OverlayFontFace>();
    _result = null;
    Revision++;
  }

  public void Request()
  {
    if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
      return;
    // Listing reads every installed font file, which takes a moment on a PC
    // with many fonts. FontEngine marks it thread-safe.
    ThreadPool.QueueUserWorkItem(_ =>
    {
      List<OverlayFontFace> faces = null;
      try
      {
        faces = List();
      }
      catch (Exception exception)
      {
        _error("List installed fonts", exception);
      }
      _result = faces;
      Volatile.Write(ref _state, 2);
    });
  }

  private List<OverlayFontFace> List()
  {
    List<OverlayFontFace> faces = ListReferences() ?? ListNames();
    // Names starting with a dot are the OS's private UI fonts.
    faces.RemoveAll(face => face.Family.Length == 0 || face.Family[0] == '.');
    OverlayFontFaces.Sort(faces);
    for (int i = faces.Count - 1; i > 0; i--)
      if (faces[i].Matches(faces[i - 1].Family, faces[i - 1].Style))
        faces.RemoveAt(i);
    return faces;
  }

  // FontEngine.GetSystemFontReferences is internal but gives each face's file;
  // reflection keeps it optional.
  private List<OverlayFontFace> ListReferences()
  {
    try
    {
      MethodInfo method = typeof(FontEngine).GetMethod(
        "GetSystemFontReferences",
        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public
      );
      if (method?.Invoke(null, null) is not Array references)
        return null;
      Type type = references.GetType().GetElementType();
      FieldInfo family = type?.GetField("familyName"),
        style = type?.GetField("styleName"),
        index = type?.GetField("faceIndex"),
        path = type?.GetField("filePath");
      if (family == null || style == null || index == null || path == null)
        return null;
      List<OverlayFontFace> faces = new(references.Length);
      foreach (object reference in references)
      {
        string file = path.GetValue(reference) as string;
        faces.Add(
          new OverlayFontFace(
            (family.GetValue(reference) as string)?.Trim(),
            (style.GetValue(reference) as string)?.Trim(),
            string.IsNullOrEmpty(file) ? null : file,
            index.GetValue(reference) is int face ? face : 0,
            false
          )
        );
      }
      return faces;
    }
    catch (Exception exception)
    {
      _warn("Installed fonts are listed by name only: " + exception.Message);
      return null;
    }
  }

  private static List<OverlayFontFace> ListNames()
  {
    List<OverlayFontFace> faces = new();
    string[] names = FontEngine.GetSystemFontNames();
    if (names != null)
      foreach (string name in names)
        if (!string.IsNullOrWhiteSpace(name))
          faces.Add(new OverlayFontFace(name.Trim(), OverlayFontKey.RegularStyle, null, 0, false));
    return faces;
  }
}
