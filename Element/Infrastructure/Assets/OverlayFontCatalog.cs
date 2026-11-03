using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Presentation.Overlay.Shared;
using TMPro;
using UnityEngine.TextCore.LowLevel;

namespace Element.Infrastructure.Assets;

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
  private const int MaximumFacesPerFile = 64;

  private readonly string _folder;
  private readonly IFontFilePicker _picker;
  private readonly Action<string> _warn;
  private readonly Action<string, Exception> _error;
  private readonly Dictionary<string, FolderFile> _files = new(StringComparer.Ordinal);
  private List<OverlayFontFace> _imported = new();
  private bool _importedRead;
  private List<OverlayFontFace> _installed;
  private List<OverlayFontFace> _installedResult;
  private int _installedState;

  public OverlayFontCatalog(string folder, IFontFilePicker picker, Action<string> warn, Action<string, Exception> error)
  {
    _folder = folder;
    _picker = picker;
    _warn = warn;
    _error = error;
  }

  public int Revision { get; private set; }

  public IReadOnlyList<OverlayFontFace> Imported
  {
    get
    {
      EnsureImported();
      return _imported;
    }
  }

  public IReadOnlyList<OverlayFontFace> Installed
  {
    get
    {
      Poll();
      return _installed;
    }
  }

  public bool CanPickFiles => _picker != null;

  public OverlayFontLookup Find(string key, out OverlayFontFace face)
  {
    face = null;
    if (!OverlayFontKey.TryParse(key, out string family, out string style))
      return OverlayFontLookup.Missing;
    EnsureImported();
    face = Match(_imported, family, style);
    if (face != null)
      return OverlayFontLookup.Found;
    Poll();
    if (_installed == null)
    {
      RequestInstalled();
      return OverlayFontLookup.Pending;
    }
    face = Match(_installed, family, style);
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

  public void Poll()
  {
    if (_installed != null || Volatile.Read(ref _installedState) != 2)
      return;
    _installed = _installedResult ?? new List<OverlayFontFace>();
    _installedResult = null;
    Revision++;
  }

  public void RequestInstalled()
  {
    if (Interlocked.CompareExchange(ref _installedState, 1, 0) != 0)
      return;
    // Listing reads every installed font file, which takes a moment on a PC
    // with many fonts. FontEngine marks it thread-safe.
    ThreadPool.QueueUserWorkItem(_ =>
    {
      List<OverlayFontFace> faces = null;
      try
      {
        faces = ListInstalled();
      }
      catch (Exception exception)
      {
        _error("List installed fonts", exception);
      }
      _installedResult = faces;
      Volatile.Write(ref _installedState, 2);
    });
  }

  public void RefreshImported()
  {
    _importedRead = false;
    EnsureImported();
  }

  public OverlayFontImport ImportFiles(string title)
  {
    OverlayFontImport result = new();
    if (_picker == null)
    {
      result.Cancelled = true;
      OpenFolder();
      return result;
    }
    IReadOnlyList<string> paths;
    try
    {
      paths = _picker.Pick(title);
    }
    catch (Exception exception)
    {
      _error("Open the font file dialog", exception);
      result.Cancelled = true;
      OpenFolder();
      return result;
    }
    if (paths == null || paths.Count == 0)
    {
      result.Cancelled = true;
      return result;
    }
    foreach (string path in paths)
      Import(path, result);
    _importedRead = false;
    EnsureImported();
    return result;
  }

  public void OpenFolder()
  {
    try
    {
      Directory.CreateDirectory(_folder);
      UnityEngine.Application.OpenURL(new Uri(_folder + Path.DirectorySeparatorChar).AbsoluteUri);
    }
    catch (Exception exception)
    {
      _error("Open the Fonts folder", exception);
    }
  }

  private void Import(string source, OverlayFontImport result)
  {
    string name = Path.GetFileName(source);
    if (!IsFontFile(source) || !File.Exists(source))
    {
      result.Unreadable.Add(name);
      return;
    }
    List<OverlayFontFace> faces = ReadFaces(source, true);
    if (faces.Count == 0)
    {
      result.Unreadable.Add(name);
      return;
    }
    string target;
    try
    {
      Directory.CreateDirectory(_folder);
      target = CopyTarget(source);
      if (target != null)
        File.Copy(source, target, false);
    }
    catch (Exception exception)
    {
      _error("Copy the font " + name, exception);
      result.Failed.Add(name);
      return;
    }
    foreach (OverlayFontFace face in faces)
      result.Added.Add(face);
  }

  // The file's place in the Fonts folder; null when the same file is already there.
  private string CopyTarget(string source)
  {
    string name = Path.GetFileNameWithoutExtension(source);
    string extension = Path.GetExtension(source);
    string target = Path.Combine(_folder, name + extension);
    if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
      return null;
    for (int copy = 2; File.Exists(target); copy++)
    {
      if (SameContent(source, target))
        return null;
      target = Path.Combine(_folder, name + " (" + copy + ")" + extension);
    }
    return target;
  }

  private static bool SameContent(string a, string b)
  {
    FileInfo first = new(a),
      second = new(b);
    if (first.Length != second.Length)
      return false;
    using FileStream left = first.OpenRead(),
      right = second.OpenRead();
    byte[] x = new byte[81920],
      y = new byte[81920];
    while (true)
    {
      int read = left.Read(x, 0, x.Length);
      if (read == 0)
        return true;
      int offset = 0;
      while (offset < read)
      {
        int count = right.Read(y, offset, read - offset);
        if (count == 0)
          return false;
        offset += count;
      }
      for (int i = 0; i < read; i++)
        if (x[i] != y[i])
          return false;
    }
  }

  private void EnsureImported()
  {
    if (_importedRead)
      return;
    _importedRead = true;
    List<OverlayFontFace> faces = new();
    HashSet<string> seen = new(StringComparer.Ordinal);
    try
    {
      if (Directory.Exists(_folder))
        foreach (string path in Directory.GetFiles(_folder))
        {
          if (!IsFontFile(path))
            continue;
          seen.Add(path);
          FileInfo info = new(path);
          if (
            !_files.TryGetValue(path, out FolderFile file)
            || file.Length != info.Length
            || file.Written != info.LastWriteTimeUtc
          )
          {
            file = new FolderFile(info.Length, info.LastWriteTimeUtc, ReadFaces(path, true));
            _files[path] = file;
            if (file.Faces.Count == 0)
              _warn("The Fonts folder has a file no font could be read from: " + path);
          }
          faces.AddRange(file.Faces);
        }
    }
    catch (Exception exception)
    {
      _error("Read the Fonts folder", exception);
    }
    List<string> gone = new();
    foreach (string path in _files.Keys)
      if (!seen.Contains(path))
        gone.Add(path);
    foreach (string path in gone)
      _files.Remove(path);
    Sort(faces);
    if (!SameFaces(faces, _imported))
    {
      _imported = faces;
      Revision++;
    }
  }

  // FontEngine keeps one current face, so this runs on the main thread only.
  private List<OverlayFontFace> ReadFaces(string path, bool imported)
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
      _error("Read the font file " + Path.GetFileName(path), exception);
    }
    return faces;
  }

  private List<OverlayFontFace> ListInstalled()
  {
    List<OverlayFontFace> faces = ListInstalledReferences() ?? ListInstalledNames();
    // Names starting with a dot are the OS's private UI fonts.
    faces.RemoveAll(face => face.Family.Length == 0 || face.Family[0] == '.');
    Sort(faces);
    for (int i = faces.Count - 1; i > 0; i--)
      if (faces[i].Matches(faces[i - 1].Family, faces[i - 1].Style))
        faces.RemoveAt(i);
    return faces;
  }

  // FontEngine.GetSystemFontReferences is internal but gives each face's file;
  // reflection keeps it optional.
  private List<OverlayFontFace> ListInstalledReferences()
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

  private static List<OverlayFontFace> ListInstalledNames()
  {
    List<OverlayFontFace> faces = new();
    string[] names = FontEngine.GetSystemFontNames();
    if (names != null)
      foreach (string name in names)
        if (!string.IsNullOrWhiteSpace(name))
          faces.Add(new OverlayFontFace(name.Trim(), OverlayFontKey.RegularStyle, null, 0, false));
    return faces;
  }

  // The exact face first; otherwise a family saved with its style in the
  // name, as some systems name weights ("Pretendard Bold" + "Regular").
  private static OverlayFontFace Match(IReadOnlyList<OverlayFontFace> faces, string family, string style)
  {
    foreach (OverlayFontFace face in faces)
      if (face.Matches(family, style))
        return face;
    string joined = family + " " + style;
    foreach (OverlayFontFace face in faces)
      if (
        string.Equals(face.Family + " " + face.Style, joined, StringComparison.OrdinalIgnoreCase)
        || (
          string.Equals(face.Family, joined, StringComparison.OrdinalIgnoreCase)
          && string.Equals(face.Style, OverlayFontKey.RegularStyle, StringComparison.OrdinalIgnoreCase)
        )
      )
        return face;
    return null;
  }

  private static void Sort(List<OverlayFontFace> faces) =>
    faces.Sort(
      (a, b) =>
      {
        int family = string.Compare(a.Family, b.Family, StringComparison.OrdinalIgnoreCase);
        if (family != 0)
          return family;
        int rank = OverlayFontFace.StyleRank(a.Style).CompareTo(OverlayFontFace.StyleRank(b.Style));
        return rank != 0 ? rank : string.Compare(a.Style, b.Style, StringComparison.OrdinalIgnoreCase);
      }
    );

  private static bool SameFaces(List<OverlayFontFace> a, List<OverlayFontFace> b)
  {
    if (a.Count != b.Count)
      return false;
    for (int i = 0; i < a.Count; i++)
      if (a[i].Key != b[i].Key || a[i].Path != b[i].Path || a[i].FaceIndex != b[i].FaceIndex)
        return false;
    return true;
  }

  private static bool IsFontFile(string path)
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

  private sealed class FolderFile
  {
    public FolderFile(long length, DateTime written, List<OverlayFontFace> faces)
    {
      Length = length;
      Written = written;
      Faces = faces;
    }

    public long Length { get; }
    public DateTime Written { get; }
    public List<OverlayFontFace> Faces { get; }
  }
}

/// <summary>
/// How overlay font atlases are sampled. (padding + 1) / point size matches the
/// bundled assets, so outline and underlay offsets look the same in every font.
/// </summary>
internal static class OverlayFontSampling
{
  public const int PointSize = 48;
  public const int Padding = 3;
  public const int AtlasSize = 1024;
}
