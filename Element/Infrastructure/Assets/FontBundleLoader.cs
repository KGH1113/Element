using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

namespace Element.Infrastructure.Assets;

internal sealed class FontBundleLoader : IDisposable
{
  private const string BundleName = "elementfont";
  private const string FontAssetName = "MAPLESTORY_OTF_BOLD SDF";
  private const string KoreanFontAssetName = "MAPLESTORY_OTF_BOLD KR SDF";
  private const string DynamicFallbackName = "Element CJK Fallback";

  // (padding + 1) / point size matches the bundled assets, so outline and underlay offsets look the same.
  private const int DynamicFallbackPointSize = 48;
  private const int DynamicFallbackPadding = 3;
  private const int DynamicFallbackAtlasSize = 1024;

  private readonly Main _main;
  private readonly List<TMP_FontAsset> _addedFallbacks = new();
  private AssetBundle _bundle;
  private TMP_FontAsset _koreanFontAsset;
  private TMP_FontAsset _dynamicFallback;

  public TMP_FontAsset FontAsset { get; private set; }

  public FontBundleLoader(Main main)
  {
    _main = main;
  }

  public bool Load()
  {
    if (FontAsset != null)
      return true;

    string platformDirectory = GetPlatformDirectory();
    if (platformDirectory == null)
      return false;

    string bundlePath = Path.Combine(_main.RuntimePath, "Assets", "Font", platformDirectory, BundleName);
    if (!File.Exists(bundlePath))
    {
      _main.LogWarning("Font AssetBundle was not found: " + bundlePath);
      return false;
    }

    _bundle = AssetBundle.LoadFromFile(bundlePath);
    if (_bundle == null)
    {
      _main.LogWarning("Font AssetBundle could not be opened: " + bundlePath);
      return false;
    }

    FontAsset = _bundle.LoadAsset<TMP_FontAsset>(FontAssetName);
    if (FontAsset == null)
    {
      _main.LogWarning("TMP font asset was not found in the bundle: " + FontAssetName);
      _bundle.Unload(true);
      _bundle = null;
      return false;
    }

    FontAsset.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
    _koreanFontAsset = _bundle.LoadAsset<TMP_FontAsset>(KoreanFontAssetName);
    if (_koreanFontAsset == null)
      _main.LogWarning("TMP font asset was not found in the bundle: " + KoreanFontAssetName);
    AddFallback(_koreanFontAsset, 0);
    AddFallback(CreateDynamicFallback(), FontAsset.fallbackFontAssetTable.Count);

    _main.Log("Loaded overlay font from " + bundlePath);
    return true;
  }

  // Frees the glyphs the overlay pulled from the game's CJK font. Call only while overlay texts are hidden:
  // texts that stay enabled keep stale glyphs until they regenerate their mesh.
  public void ReleaseFallbackGlyphs()
  {
    if (_dynamicFallback == null || _dynamicFallback.characterTable.Count == 0)
      return;

    _dynamicFallback.ClearFontAssetData(setAtlasSizeToZero: true);
  }

  public void Dispose()
  {
    if (FontAsset != null && FontAsset.fallbackFontAssetTable != null)
    {
      foreach (TMP_FontAsset fallback in _addedFallbacks)
        FontAsset.fallbackFontAssetTable.Remove(fallback);
    }
    _addedFallbacks.Clear();

    if (_dynamicFallback != null)
    {
      TMP_ResourceManager.RemoveFontAsset(_dynamicFallback);
      // The font asset destroys its atlas textures and material with itself.
      Object.Destroy(_dynamicFallback);
      _dynamicFallback = null;
    }

    if (_koreanFontAsset != null)
      TMP_ResourceManager.RemoveFontAsset(_koreanFontAsset);
    if (FontAsset != null)
      TMP_ResourceManager.RemoveFontAsset(FontAsset);

    _koreanFontAsset = null;
    FontAsset = null;
    _bundle?.Unload(true);
    _bundle = null;
  }

  private void AddFallback(TMP_FontAsset fallback, int index)
  {
    List<TMP_FontAsset> fallbacks = FontAsset.fallbackFontAssetTable;
    if (fallback == null || fallbacks.Contains(fallback))
      return;

    fallbacks.Insert(Math.Min(index, fallbacks.Count), fallback);
    _addedFallbacks.Add(fallback);
  }

  // A private dynamic asset keeps overlay-only CJK glyphs out of the game's shared atlas.
  private TMP_FontAsset CreateDynamicFallback()
  {
    TMP_FontAsset gameFont = RDConstants.data.chineseFontTMPro;
    Font sourceFont = gameFont != null ? gameFont.sourceFontFile : null;
    if (sourceFont == null)
      return gameFont;

    try
    {
      _dynamicFallback = TMP_FontAsset.CreateFontAsset(
        sourceFont,
        DynamicFallbackPointSize,
        DynamicFallbackPadding,
        GlyphRenderMode.SDFAA,
        DynamicFallbackAtlasSize,
        DynamicFallbackAtlasSize,
        AtlasPopulationMode.Dynamic,
        true
      );
    }
    catch (Exception exception)
    {
      // The overlay still works with the shared game font, so a failed fallback must not stop loading.
      _main.LogException("Create the CJK fallback font", exception);
      _dynamicFallback = null;
    }

    if (_dynamicFallback == null)
    {
      _main.LogWarning("The CJK fallback font could not be created. The game's font atlas is shared instead.");
      return gameFont;
    }

    _dynamicFallback.name = DynamicFallbackName;
    return _dynamicFallback;
  }

  private string GetPlatformDirectory()
  {
    switch (UnityEngine.Application.platform)
    {
      case RuntimePlatform.WindowsPlayer:
      case RuntimePlatform.WindowsEditor:
        return "Windows";
      case RuntimePlatform.LinuxPlayer:
      case RuntimePlatform.LinuxEditor:
        return "Linux";
      case RuntimePlatform.OSXPlayer:
      case RuntimePlatform.OSXEditor:
        return "Mac";
      default:
        _main.LogWarning("Unsupported runtime platform for the font AssetBundle: " + UnityEngine.Application.platform);
        return null;
    }
  }
}
