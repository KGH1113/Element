using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

namespace Element.Editor
{
  // Regenerates the static SDF assets from the unmodified OTF. The font license forbids editing the font,
  // so only the rasterized code points are chosen. The OTF maps every Hangul syllable but draws only some,
  // and empty glyphs are skipped so those characters reach the fallback fonts instead of rendering blank.
  public static class FontAssetGenerator
  {
    public const string PrimaryAssetPath = "Assets/Font/MAPLESTORY_OTF_BOLD SDF.asset";
    public const string KoreanAssetPath = "Assets/Font/MAPLESTORY_OTF_BOLD KR SDF.asset";
    public const string SourceFontPath = "Assets/Font/MAPLESTORY_OTF_BOLD.OTF";

    private const string DistanceFieldShader = "TextMeshPro/Distance Field";

    // Latin keeps the original 74 pt sampling because the combo digits render at 108 pt.
    private static readonly FontAssetSpec Primary = new(
      PrimaryAssetPath,
      74,
      5,
      1024,
      1024,
      new[] { (0x20u, 0x7Eu), (0xA0u, 0x17Fu), (0x2010u, 0x205Eu), (0x20A0u, 0x20CFu) },
      new[] { 0x20u, 0xA0u }
    );

    // Body-size scripts. (padding + 1) / point size stays close to the primary 6 / 74, so outline and
    // underlay offsets copied from the primary material look the same on fallback glyphs.
    private static readonly FontAssetSpec Korean = new(
      KoreanAssetPath,
      36,
      2,
      2048,
      2048,
      new[]
      {
        (0xAC00u, 0xD7A3u),
        (0x3131u, 0x318Eu),
        (0x1100u, 0x11FFu),
        (0x0370u, 0x03FFu),
        (0x0400u, 0x04FFu),
        (0x2070u, 0x209Fu),
        (0x2100u, 0x26FFu),
      },
      Array.Empty<uint>()
    );

    [MenuItem("Element/Generate Font Assets")]
    public static void GenerateAll()
    {
      Font font =
        AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath)
        ?? throw new FileNotFoundException("Source font was not found.", SourceFontPath);

      TMP_FontAsset korean = Generate(font, Korean);
      TMP_FontAsset primary = Generate(font, Primary);
      korean.fallbackFontAssetTable = new List<TMP_FontAsset>();
      // Serialized so editor previews and the bundle resolve Hangul the same way.
      primary.fallbackFontAssetTable = new List<TMP_FontAsset> { korean };
      EditorUtility.SetDirty(korean);
      EditorUtility.SetDirty(primary);
      AssetDatabase.SaveAssets();

      Validate(primary, Primary, requireAscii: true);
      Validate(korean, Korean, requireAscii: false);
    }

    private static TMP_FontAsset Generate(Font font, FontAssetSpec spec)
    {
      uint[] unicodes = CollectOutlinedUnicodes(font, spec);
      TMP_FontAsset generated =
        TMP_FontAsset.CreateFontAsset(
          font,
          spec.PointSize,
          spec.Padding,
          GlyphRenderMode.SDFAA,
          spec.Width,
          spec.Height,
          AtlasPopulationMode.Dynamic,
          false
        ) ?? throw new InvalidOperationException("TMP could not load " + SourceFontPath);

      generated.TryAddCharacters(unicodes, out uint[] missing, false);
      if (missing != null && missing.Length > 0)
        throw new InvalidOperationException(
          $"{spec.Name} could not fit {missing.Length} of {unicodes.Length} code points in "
            + $"{spec.Width}x{spec.Height}. Enlarge the atlas or drop a range. First missing: "
            + FormatRanges(missing.Take(16))
        );

      // The setter also drops the source font reference, keeping the OTF out of the bundle.
      generated.atlasPopulationMode = AtlasPopulationMode.Static;
      generated.name = spec.Name;
      generated.glyphTable.Sort((left, right) => left.index.CompareTo(right.index));
      generated.characterTable.Sort((left, right) => left.unicode.CompareTo(right.unicode));
      generated.creationSettings = CreateSettings(spec, unicodes);

      Material material = CreateMaterial(generated, spec);
      Object.DestroyImmediate(generated.material);
      generated.material = material;
      return Save(generated, spec.Path);
    }

    private static uint[] CollectOutlinedUnicodes(Font font, FontAssetSpec spec)
    {
      if (FontEngine.LoadFontFace(font, spec.PointSize) != FontEngineError.Success)
        throw new InvalidOperationException("FontEngine could not load " + SourceFontPath);

      List<(uint Unicode, float Width, float Height)> glyphs = new();
      HashSet<uint> seen = new();
      foreach ((uint first, uint last) in spec.Ranges)
      {
        for (uint unicode = first; unicode <= last; unicode++)
        {
          if (!seen.Add(unicode))
            continue;

          bool outlined =
            FontEngine.TryGetGlyphWithUnicodeValue(unicode, GlyphLoadFlags.LOAD_NO_BITMAP, out Glyph glyph)
            && glyph.metrics.width > 0
            && glyph.metrics.height > 0;
          if (outlined)
            glyphs.Add((unicode, glyph.metrics.width, glyph.metrics.height));
          else if (Array.IndexOf(spec.Whitespace, unicode) >= 0)
            glyphs.Add((unicode, 0f, 0f));
        }
      }

      // Packing tall glyphs first fills the atlas tighter; the code point tie-break keeps runs identical.
      return glyphs
        .OrderByDescending(glyph => glyph.Height)
        .ThenByDescending(glyph => glyph.Width)
        .ThenBy(glyph => glyph.Unicode)
        .Select(glyph => glyph.Unicode)
        .ToArray();
    }

    private static Material CreateMaterial(TMP_FontAsset asset, FontAssetSpec spec)
    {
      Shader shader =
        Shader.Find(DistanceFieldShader)
        ?? throw new InvalidOperationException("Shader was not found: " + DistanceFieldShader);
      Texture2D atlas = asset.atlasTexture;
      Material material = new(shader) { name = spec.Name + " Material" };
      material.SetTexture(ShaderUtilities.ID_MainTex, atlas);
      material.SetFloat(ShaderUtilities.ID_TextureWidth, atlas.width);
      material.SetFloat(ShaderUtilities.ID_TextureHeight, atlas.height);
      material.SetFloat(ShaderUtilities.ID_GradientScale, spec.Padding + 1);
      material.SetFloat(ShaderUtilities.ID_WeightNormal, asset.normalStyle);
      material.SetFloat(ShaderUtilities.ID_WeightBold, asset.boldStyle);

      // Same drop shadow as the original asset material; the settings UI renders with it directly.
      material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
      material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.5f));
      material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 1f);
      material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -1f);
      material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 1f);
      ShaderUtilities.UpdateShaderRatios(material);
      return material;
    }

    private static TMP_FontAsset Save(TMP_FontAsset generated, string path)
    {
      Texture2D generatedAtlas = generated.atlasTexture;
      generatedAtlas.name = generated.name + " Atlas";

      TMP_FontAsset asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
      if (asset == null)
      {
        AssetDatabase.CreateAsset(generated, path);
        AssetDatabase.AddObjectToAsset(generatedAtlas, generated);
        AssetDatabase.AddObjectToAsset(generated.material, generated);
        asset = generated;
      }
      else
      {
        // Overwrite the existing objects in place so the GUID and sub-asset file IDs stay stable.
        Texture2D atlas = ReplaceSubAsset(path, asset, generatedAtlas);
        Material material = ReplaceSubAsset(path, asset, generated.material);
        material.SetTexture(ShaderUtilities.ID_MainTex, atlas);
        EditorUtility.CopySerialized(generated, asset);
        asset.atlasTextures = new[] { atlas };
        asset.material = material;

        // Its OnDestroy also releases the temporary atlas and material.
        Object.DestroyImmediate(generated);
      }

      if (!HasPixels(asset.atlasTexture))
        throw new InvalidOperationException(path + " atlas is empty after saving.");

      SerializedObject serializedAsset = new(asset);
      serializedAsset.FindProperty("m_SourceFontFile").objectReferenceValue = null;
      serializedAsset.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
      serializedAsset.ApplyModifiedPropertiesWithoutUndo();

      SerializedObject serializedAtlas = new(asset.atlasTexture);
      serializedAtlas.FindProperty("m_IsReadable").boolValue = false;
      serializedAtlas.ApplyModifiedPropertiesWithoutUndo();

      AssetImporter importer = AssetImporter.GetAtPath(path);
      if (importer != null && importer.assetBundleName != FontBundleBuilder.BundleName)
        importer.SetAssetBundleNameAndVariant(FontBundleBuilder.BundleName, string.Empty);

      asset.ReadFontAssetDefinition();
      EditorUtility.SetDirty(asset.atlasTexture);
      EditorUtility.SetDirty(asset.material);
      EditorUtility.SetDirty(asset);
      return asset;
    }

    private static T ReplaceSubAsset<T>(string path, TMP_FontAsset owner, T source)
      where T : Object
    {
      T[] existing = AssetDatabase.LoadAllAssetsAtPath(path).OfType<T>().ToArray();
      for (int i = 1; i < existing.Length; i++)
      {
        AssetDatabase.RemoveObjectFromAsset(existing[i]);
        Object.DestroyImmediate(existing[i], true);
      }

      if (existing.Length > 0)
      {
        EditorUtility.CopySerialized(source, existing[0]);
        return existing[0];
      }

      // A copy keeps the temporary source disposable together with the temporary font asset.
      T copy = Object.Instantiate(source);
      copy.name = source.name;
      AssetDatabase.AddObjectToAsset(copy, owner);
      return copy;
    }

    // Guards against a copy that kept the size but lost the rendered SDF data.
    private static bool HasPixels(Texture2D texture)
    {
      if (texture == null)
        return false;
      if (!texture.isReadable)
        throw new InvalidOperationException(texture.name + " must stay readable until it is saved.");

      foreach (byte value in texture.GetRawTextureData<byte>())
      {
        if (value != 0)
          return true;
      }
      return false;
    }

    private static FontAssetCreationSettings CreateSettings(FontAssetSpec spec, IEnumerable<uint> unicodes)
    {
      return new FontAssetCreationSettings
      {
        sourceFontFileName = string.Empty,
        sourceFontFileGUID = AssetDatabase.AssetPathToGUID(SourceFontPath),
        pointSizeSamplingMode = 1,
        pointSize = spec.PointSize,
        padding = spec.Padding,
        paddingMode = 2,
        packingMode = 0,
        atlasWidth = spec.Width,
        atlasHeight = spec.Height,
        characterSetSelectionMode = 6,
        characterSequence = FormatRanges(unicodes),
        referencedFontAssetGUID = string.Empty,
        referencedTextAssetGUID = string.Empty,
        renderMode = (int)GlyphRenderMode.SDFAA,
        includeFontFeatures = false,
      };
    }

    private static void Validate(TMP_FontAsset asset, FontAssetSpec spec, bool requireAscii)
    {
      Texture2D atlas = asset.atlasTexture;
      if (asset.atlasTextures.Length != 1 || atlas.width != spec.Width || atlas.height != spec.Height)
        throw new InvalidOperationException(spec.Name + " must use one " + spec.Width + "x" + spec.Height + " atlas.");
      if (asset.atlasPopulationMode != AtlasPopulationMode.Static || asset.sourceFontFile != null)
        throw new InvalidOperationException(spec.Name + " must be static without a source font reference.");

      List<uint> empty = asset
        .characterTable.Where(character =>
          Array.IndexOf(spec.Whitespace, character.unicode) < 0
          && (character.glyph == null || character.glyph.glyphRect.width == 0 || character.glyph.glyphRect.height == 0)
        )
        .Select(character => character.unicode)
        .ToList();
      if (empty.Count > 0)
        throw new InvalidOperationException(spec.Name + " contains empty glyphs: " + FormatRanges(empty.Take(16)));

      if (requireAscii)
      {
        List<uint> missingAscii = new();
        for (uint unicode = 0x20; unicode <= 0x7E; unicode++)
        {
          if (!asset.HasCharacter((int)unicode))
            missingAscii.Add(unicode);
        }
        if (missingAscii.Count > 0)
          throw new InvalidOperationException(spec.Name + " is missing ASCII: " + FormatRanges(missingAscii));
      }

      int spacing = spec.Padding * 2;
      long used = asset.glyphTable.Sum(glyph =>
        (long)(glyph.glyphRect.width + spacing) * (glyph.glyphRect.height + spacing)
      );
      Debug.Log(
        string.Format(
          "{0}: {1} characters, {2} glyphs, {3}x{4} atlas, {5:P1} used, {6} pt, padding {7}",
          spec.Name,
          asset.characterTable.Count,
          asset.glyphTable.Count,
          atlas.width,
          atlas.height,
          (double)used / ((long)atlas.width * atlas.height),
          spec.PointSize,
          spec.Padding
        )
      );
    }

    private static string FormatRanges(IEnumerable<uint> unicodes)
    {
      StringBuilder builder = new();
      uint[] sorted = unicodes.Distinct().OrderBy(unicode => unicode).ToArray();
      for (int i = 0; i < sorted.Length; i++)
      {
        int end = i;
        while (end + 1 < sorted.Length && sorted[end + 1] == sorted[end] + 1)
          end++;

        if (builder.Length > 0)
          builder.Append(',');
        builder.Append(sorted[i].ToString("X4"));
        if (end > i)
          builder.Append('-').Append(sorted[end].ToString("X4"));
        i = end;
      }
      return builder.ToString();
    }

    private sealed class FontAssetSpec
    {
      public FontAssetSpec(
        string path,
        int pointSize,
        int padding,
        int width,
        int height,
        (uint First, uint Last)[] ranges,
        uint[] whitespace
      )
      {
        Path = path;
        Name = System.IO.Path.GetFileNameWithoutExtension(path);
        PointSize = pointSize;
        Padding = padding;
        Width = width;
        Height = height;
        Ranges = ranges;
        Whitespace = whitespace;
      }

      public string Path { get; }
      public string Name { get; }
      public int PointSize { get; }
      public int Padding { get; }
      public int Width { get; }
      public int Height { get; }
      public (uint First, uint Last)[] Ranges { get; }

      // Spaces have no outline but must stay in the primary asset.
      public uint[] Whitespace { get; }
    }
  }
}
