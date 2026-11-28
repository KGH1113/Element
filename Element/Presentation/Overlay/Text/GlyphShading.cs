using TMPro;
using UnityEngine;

namespace Element.Presentation.Overlay.Text;

/// <summary>
/// Where a TMP distance field material draws a glyph, as atlas alpha: the
/// face grown by the outline and, when the material has one, the shadow
/// (underlay) offset from it. Thresholds are where the shader's coverage
/// reaches one half, which no longer depends on the glyph's size on screen:
/// alpha ≥ ½ − weight − outline, as TMP's SDF shaders work it out.
/// </summary>
internal readonly struct GlyphShading
{
  /// <summary>Atlas alpha (0–255) from which the face with its outline is drawn; bold is heavier.</summary>
  public readonly int Face,
    BoldFace;

  /// <summary>Whether a shadow is drawn, and from which alpha, read at the face's UV plus the offset.</summary>
  public readonly bool Shadow;

  public readonly int ShadowFace,
    BoldShadowFace;

  public readonly float ShadowU,
    ShadowV;

  private GlyphShading(
    int face,
    int boldFace,
    bool shadow,
    int shadowFace,
    int boldShadowFace,
    float shadowU,
    float shadowV
  )
  {
    Face = face;
    BoldFace = boldFace;
    Shadow = shadow;
    ShadowFace = shadowFace;
    BoldShadowFace = boldShadowFace;
    ShadowU = shadowU;
    ShadowV = shadowV;
  }

  /// <summary>The shading of <paramref name="material"/> on an atlas of the given size.</summary>
  public static GlyphShading Of(Material material, int atlasWidth, int atlasHeight)
  {
    if (material == null)
      return new GlyphShading(Alpha(.5f), Alpha(.5f), false, 256, 256, 0, 0);
    float ratioA = Ratio(material, ShaderUtilities.ID_ScaleRatio_A);
    float ratioC = Ratio(material, ShaderUtilities.ID_ScaleRatio_C);
    float dilate = Float(material, ShaderUtilities.ID_FaceDilate);
    float normal = (Float(material, ShaderUtilities.ID_WeightNormal) / 4 + dilate) * ratioA * .5f;
    float bold = (Float(material, ShaderUtilities.ID_WeightBold) / 4 + dilate) * ratioA * .5f;
    float outline = material.IsKeywordEnabled(ShaderUtilities.Keyword_Outline)
      ? Float(material, ShaderUtilities.ID_OutlineWidth) * ratioA * .5f
      : 0;
    bool shadow =
      material.IsKeywordEnabled(ShaderUtilities.Keyword_Underlay)
      && material.HasProperty(ShaderUtilities.ID_UnderlayColor)
      && material.GetColor(ShaderUtilities.ID_UnderlayColor).a > 0;
    float shadowDilate = Float(material, ShaderUtilities.ID_UnderlayDilate) * ratioC * .5f;
    float gradient = Float(material, ShaderUtilities.ID_GradientScale);
    float width = Positive(Float(material, ShaderUtilities.ID_TextureWidth), atlasWidth);
    float height = Positive(Float(material, ShaderUtilities.ID_TextureHeight), atlasHeight);
    // The shader samples the shadow at the glyph's UV plus this; it shows down and right of the face.
    float shadowU = -(Float(material, ShaderUtilities.ID_UnderlayOffsetX) * ratioC) * gradient / width;
    float shadowV = -(Float(material, ShaderUtilities.ID_UnderlayOffsetY) * ratioC) * gradient / height;
    return new GlyphShading(
      Alpha(.5f - normal - outline),
      Alpha(.5f - bold - outline),
      shadow,
      Alpha(.5f - normal - shadowDilate),
      Alpha(.5f - bold - shadowDilate),
      shadowU,
      shadowV
    );
  }

  // The first whole alpha at or above a threshold; 0 is every texel, 256 none.
  private static int Alpha(float threshold) => Mathf.Clamp(Mathf.CeilToInt(threshold * 255), 0, 256);

  private static float Float(Material material, int id) => material.HasProperty(id) ? material.GetFloat(id) : 0;

  private static float Ratio(Material material, int id)
  {
    float ratio = material.HasProperty(id) ? material.GetFloat(id) : 1;
    return float.IsNaN(ratio) || float.IsInfinity(ratio) ? 1 : ratio;
  }

  private static float Positive(float value, float fallback) => value > 0 ? value : Mathf.Max(1, fallback);
}
