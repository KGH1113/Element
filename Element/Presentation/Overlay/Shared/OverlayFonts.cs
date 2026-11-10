using System;
using System.Collections.Generic;
using Element.Configuration;
using Element.Domain.Elements;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Element.Presentation.Overlay.Shared;

internal enum OverlayFontStatus
{
  // The bundled font, or a run that follows the default.
  Default,
  Ready,

  // Installed fonts are still being listed.
  Loading,

  // Not on this PC: drawn with the default font until it is installed or imported.
  Missing,
}

/// <summary>
/// The fonts overlay texts draw with. A text's own font is the default (the
/// bundled font unless one is chosen); a run in another font switches with a
/// <c>&lt;font&gt;</c> tag. Fonts load the first time something draws with
/// them, never during play unless a setting changed, and fonts nothing uses
/// any more are freed a frame later, once no text still points at them.
/// </summary>
internal sealed class OverlayFonts : IDisposable
{
  // TMP's tag registry is global and can't forget an entry, so every load gets
  // a name of its own: a font loaded again never meets its freed self.
  private static int s_serial;

  private readonly TMP_FontAsset _bundled;
  private readonly Material _bundledMaterial;
  private readonly IOverlayFontSource _source;
  private readonly Dictionary<string, Entry> _loaded = new(StringComparer.OrdinalIgnoreCase);
  private readonly Dictionary<string, OverlayFontLookup> _unresolved = new(StringComparer.OrdinalIgnoreCase);
  private readonly List<Entry> _releasing = new();

  // The OS's Latin font by style, for chosen fonts without Latin letters; null
  // where this PC has none. Small, so they stay until the overlay closes.
  private readonly Dictionary<string, TMP_FontAsset> _latin = new(StringComparer.OrdinalIgnoreCase);
  private int _releaseFrame;
  private int _sourceRevision;
  private Entry _default;

  public OverlayFonts(
    TMP_FontAsset bundled,
    Material bundledMaterial,
    IOverlayFontSource source,
    IOverlayFontLibrary library
  )
  {
    _bundled = bundled;
    _bundledMaterial = bundledMaterial;
    _source = source;
    Library = library;
    _sourceRevision = source?.Revision ?? 0;
  }

  /// <summary>
  /// The font shipped with the mod: the default unless one is chosen, and every
  /// font's fallback. A font without Latin letters, such as Arial Hebrew, takes
  /// them from the OS's Latin font first, as the OS itself draws it; otherwise
  /// labels like "Progress" would stay in this font.
  /// </summary>
  public TMP_FontAsset Bundled => _bundled;

  /// <summary>What the font picker lists; null without a font source.</summary>
  public IOverlayFontLibrary Library { get; }

  /// <summary>Changes whenever any key may draw with a different font.</summary>
  public int Version { get; private set; }

  public string DefaultKey { get; private set; } = string.Empty;

  public TMP_FontAsset DefaultAsset => _default?.Asset ?? _bundled;

  public Material DefaultMaterial => _default?.Material ?? _bundledMaterial;

  public void SetDefault(string key)
  {
    key = OverlayFontKey.Normalize(key);
    if (string.Equals(key, DefaultKey, StringComparison.OrdinalIgnoreCase))
      return;
    DefaultKey = key;
    _default = Load(key);
    Version++;
  }

  /// <summary>
  /// The tag that switches a run to <paramref name="key"/> (in saved form);
  /// null when it draws with the default font. Allocates nothing once loaded,
  /// as texts call it on every redraw.
  /// </summary>
  public string OpenTag(string key)
  {
    if (string.IsNullOrEmpty(key) || string.Equals(key, DefaultKey, StringComparison.OrdinalIgnoreCase))
      return null;
    return Load(key)?.Tag;
  }

  public const string CloseTag = "</font>";

  /// <summary>A key as people read it: "Pretendard Bold", or the family alone for its regular face.</summary>
  public static string DisplayName(string key)
  {
    if (!OverlayFontKey.TryParse(key, out string family, out string style))
      return string.Empty;
    return string.Equals(style, OverlayFontKey.RegularStyle, StringComparison.OrdinalIgnoreCase)
      ? family
      : family + " " + style;
  }

  public OverlayFontStatus Status(string key)
  {
    key = OverlayFontKey.Normalize(key);
    if (key.Length == 0)
      return OverlayFontStatus.Default;
    if (Load(key) != null)
      return OverlayFontStatus.Ready;
    return _unresolved.TryGetValue(key, out OverlayFontLookup lookup) && lookup == OverlayFontLookup.Pending
      ? OverlayFontStatus.Loading
      : OverlayFontStatus.Missing;
  }

  /// <summary>
  /// Once per frame while texts are shown: picks up fonts that became
  /// findable (installed list arrived, a file imported) and frees released ones.
  /// </summary>
  public void Tick()
  {
    if (_releasing.Count > 0 && Time.frameCount > _releaseFrame)
    {
      foreach (Entry entry in _releasing)
        entry.Dispose();
      _releasing.Clear();
    }
    if (_source == null)
      return;
    _source.Poll();
    if (_source.Revision == _sourceRevision)
      return;
    _sourceRevision = _source.Revision;
    if (_unresolved.Count == 0)
      return;
    _unresolved.Clear();
    if (_default == null && DefaultKey.Length > 0)
      _default = Load(DefaultKey);
    Version++;
  }

  /// <summary>
  /// Frees every loaded font none of <paramref name="layouts"/> nor the
  /// default uses. Call when no editor holds fonts of its own.
  /// </summary>
  public void Retain(IEnumerable<OverlayLayoutSettings> layouts)
  {
    HashSet<string> used = new(StringComparer.OrdinalIgnoreCase);
    if (DefaultKey.Length > 0)
      used.Add(DefaultKey);
    if (layouts != null)
      foreach (OverlayLayoutSettings layout in layouts)
        if (layout?.Text?.Runs != null)
          foreach (OverlayTextRun run in layout.Text.Runs)
            if (run != null && !string.IsNullOrEmpty(run.Font))
              used.Add(OverlayFontKey.Normalize(run.Font));
    List<string> unused = null;
    foreach (string key in _loaded.Keys)
      if (!used.Contains(key))
        (unused ??= new List<string>()).Add(key);
    if (unused == null)
      return;
    foreach (string key in unused)
    {
      _releasing.Add(_loaded[key]);
      _loaded.Remove(key);
    }
    _releaseFrame = Time.frameCount + 1;
  }

  /// <summary>
  /// Clears glyphs the loaded fonts collected, such as a song title's. Only
  /// while overlay texts are hidden; they draw their glyphs again when shown.
  /// </summary>
  public void ReleaseGlyphs()
  {
    foreach (Entry entry in _loaded.Values)
      if (entry.Asset != null && entry.Asset.characterTable.Count > 0)
        entry.Asset.ClearFontAssetData(setAtlasSizeToZero: true);
    foreach (TMP_FontAsset latin in _latin.Values)
      if (latin != null && latin.characterTable.Count > 0)
        latin.ClearFontAssetData(setAtlasSizeToZero: true);
  }

  public void Dispose()
  {
    foreach (Entry entry in _releasing)
      entry.Dispose();
    _releasing.Clear();
    foreach (Entry entry in _loaded.Values)
      entry.Dispose();
    _loaded.Clear();
    _unresolved.Clear();
    _default = null;
    foreach (TMP_FontAsset latin in _latin.Values)
      if (latin != null)
      {
        TMP_ResourceManager.RemoveFontAsset(latin);
        Object.Destroy(latin);
      }
    _latin.Clear();
  }

  private Entry Load(string key)
  {
    if (string.IsNullOrEmpty(key) || _source == null)
      return null;
    if (_loaded.TryGetValue(key, out Entry entry))
      return entry;
    if (_unresolved.ContainsKey(key))
      return null;
    OverlayFontLookup lookup = _source.Find(key, out OverlayFontFace face);
    TMP_FontAsset asset = lookup == OverlayFontLookup.Found ? _source.Create(face) : null;
    if (asset == null)
    {
      _unresolved[key] = lookup == OverlayFontLookup.Pending ? lookup : OverlayFontLookup.Missing;
      return null;
    }
    TMP_FontAsset latin = _source.LacksLatin(face) ? LatinFallback(face.Style) : null;
    entry = new Entry(asset, latin, _bundled, ++s_serial);
    _loaded[key] = entry;
    return entry;
  }

  private TMP_FontAsset LatinFallback(string style)
  {
    if (_latin.TryGetValue(style, out TMP_FontAsset latin))
      return latin;
    latin = _source.CreateLatinFallback(style);
    if (latin != null)
      latin.name = "ElementLatinFallback " + style;
    _latin[style] = latin;
    return latin;
  }

  private sealed class Entry : IDisposable
  {
    private readonly OverlayMaterialFactory _materials;

    public Entry(TMP_FontAsset asset, TMP_FontAsset latin, TMP_FontAsset fallback, int serial)
    {
      string name = "ElementFont" + serial;
      Asset = asset;
      asset.name = name;
      asset.hashCode = TMP_TextUtilities.GetHashCode(name);
      if (asset.material != null)
        asset.material.name = name + "Atlas";
      asset.materialHashCode = TMP_TextUtilities.GetSimpleHashCode(name + "Atlas");
      // Glyphs the font lacks, such as Hangul in a Latin font, come from the
      // bundled font and its CJK fallbacks; Latin letters it lacks, from the
      // OS's Latin font before that.
      asset.fallbackFontAssetTable = new List<TMP_FontAsset>();
      if (latin != null)
        asset.fallbackFontAssetTable.Add(latin);
      if (fallback != null)
        asset.fallbackFontAssetTable.Add(fallback);
      _materials = new OverlayMaterialFactory(asset);
      Material = _materials.Material;
      string material = name + "Overlay";
      Material.name = material;
      MaterialReferenceManager.AddFontAsset(asset);
      MaterialReferenceManager.AddFontMaterial(TMP_TextUtilities.GetHashCode(material), Material);
      Tag = "<font=\"" + name + "\" material=\"" + material + "\">";
    }

    public TMP_FontAsset Asset { get; private set; }
    public Material Material { get; private set; }
    public string Tag { get; }

    public void Dispose()
    {
      _materials.Dispose();
      Material = null;
      if (Asset == null)
        return;
      TMP_ResourceManager.RemoveFontAsset(Asset);
      // The font asset destroys its atlas textures and material with itself.
      Object.Destroy(Asset);
      Asset = null;
    }
  }
}
