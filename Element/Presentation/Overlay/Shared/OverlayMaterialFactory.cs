using System;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Element.Presentation.Overlay.Shared;

internal sealed class OverlayMaterialFactory : IDisposable
{
  public Material Material { get; private set; }

  public OverlayMaterialFactory(TMP_FontAsset font)
  {
    if (font == null)
      throw new ArgumentNullException(nameof(font));
    if (font.material == null)
      throw new InvalidOperationException("The overlay font does not contain a material.");

    Material = new Material(font.material) { name = "Element Overlay Font Material" };
    Shader mobileSdf = Shader.Find("TextMeshPro/Mobile/Distance Field");
    if (mobileSdf != null)
      Material.shader = mobileSdf;

    Material.EnableKeyword(ShaderUtilities.Keyword_Outline);
    Material.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
    Material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.01f);
    Material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
    Material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.5f));
    Material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 1f);
    Material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -1f);
    Material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0f);
    Material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
  }

  public void Dispose()
  {
    if (Material == null)
      return;

    Object.Destroy(Material);
    Material = null;
  }
}
