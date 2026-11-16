using System;
using System.Collections.Generic;
using System.IO;
using Element.Application.Fonts;
using Element.Infrastructure.Platform;
using Element.Domain.Overlay.Fonts;

namespace Element.Infrastructure.Storage;

/// <summary>
/// The font files copied into the mod's Fonts folder, which survives updates. Read
/// on demand on the main thread, as FontEngine requires; a file is read again only
/// when its size or write time changed.
/// </summary>
internal sealed class OverlayFontFolder
{
  private readonly string _folder;
  private readonly IFontFilePicker _picker;
  private readonly Action<string> _warn;
  private readonly Action<string, Exception> _error;
  private readonly Dictionary<string, FolderFile> _files = new(StringComparer.Ordinal);
  private List<OverlayFontFace> _faces = new();
  private bool _read;

  public OverlayFontFolder(string folder, IFontFilePicker picker, Action<string> warn, Action<string, Exception> error)
  {
    _folder = folder;
    _picker = picker;
    _warn = warn;
    _error = error;
  }

  /// <summary>Goes up each time the faces in the folder change.</summary>
  public int Revision { get; private set; }

  public List<OverlayFontFace> Faces
  {
    get
    {
      EnsureRead();
      return _faces;
    }
  }

  public bool CanPickFiles => _picker != null;

  /// <summary>Reads the folder again, for files the player put there.</summary>
  public void Refresh()
  {
    _read = false;
    EnsureRead();
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
    _read = false;
    EnsureRead();
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
    if (!OverlayFontFiles.IsFontFile(source) || !File.Exists(source))
    {
      result.Unreadable.Add(name);
      return;
    }
    List<OverlayFontFace> faces = OverlayFontFiles.ReadFaces(source, true, _error);
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

  private void EnsureRead()
  {
    if (_read)
      return;
    _read = true;
    List<OverlayFontFace> faces = new();
    HashSet<string> seen = new(StringComparer.Ordinal);
    try
    {
      if (Directory.Exists(_folder))
        foreach (string path in Directory.GetFiles(_folder))
        {
          if (!OverlayFontFiles.IsFontFile(path))
            continue;
          seen.Add(path);
          FileInfo info = new(path);
          if (
            !_files.TryGetValue(path, out FolderFile file)
            || file.Length != info.Length
            || file.Written != info.LastWriteTimeUtc
          )
          {
            file = new FolderFile(info.Length, info.LastWriteTimeUtc, OverlayFontFiles.ReadFaces(path, true, _error));
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
    OverlayFontFaces.Sort(faces);
    if (!SameFaces(faces, _faces))
    {
      _faces = faces;
      Revision++;
    }
  }

  private static bool SameFaces(List<OverlayFontFace> a, List<OverlayFontFace> b)
  {
    if (a.Count != b.Count)
      return false;
    for (int i = 0; i < a.Count; i++)
      if (a[i].Key != b[i].Key || a[i].Path != b[i].Path || a[i].FaceIndex != b[i].FaceIndex)
        return false;
    return true;
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
