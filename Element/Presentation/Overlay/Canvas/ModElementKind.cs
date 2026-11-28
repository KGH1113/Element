using System.Collections.Generic;
using Element.Application.Mods;
using Element.Configuration.Overlay;
using Element.Domain.Layout;
using Element.Domain.Overlay.Values;
using Element.Presentation.Overlay.Fonts;
using Element.Presentation.Overlay.Mods;

namespace Element.Presentation.Overlay.Canvas;

/// <summary>Elements other mods draw, their types looked up again each frame.</summary>
internal sealed class ModElementKind : OverlayElementKind<ModOverlayElement>
{
  private readonly OverlayCanvasHost _host;

  public ModElementKind(OverlayCanvasHost host) => _host = host;

  public override bool Owns(string templateId) => OverlayElementKinds.IsMod(templateId);

  protected override ModOverlayElement Make()
  {
    _host.ModElements?.Refresh();
    return new ModOverlayElement(Layer, _host.ModElements, _host.Fonts);
  }

  public override void Tick(OverlayValueMask changes, bool settle)
  {
    if (Elements.Count == 0)
      return;
    _host.ModElements?.Refresh();
    base.Tick(changes, settle);
  }

  public override void SetFonts(OverlayFonts fonts)
  {
    foreach (ModOverlayElement element in Elements)
      element.SetFonts(fonts);
  }

  /// <summary>The fonts the mods' elements draw with, kept when unused fonts are let go.</summary>
  public IEnumerable<string> UsedFonts()
  {
    foreach (ModOverlayElement mod in Elements)
    foreach (string font in mod.UsedFonts)
      yield return font;
  }

  /// <summary>A new element's saved layout, its settings at the mod's defaults; null without a type.</summary>
  public static OverlayLayoutSettings NewSettings(ElementClass type) =>
    type == null
      ? null
      : new OverlayLayoutSettings
      {
        Id = OverlayElementIds.New(OverlayElementIds.ModPrefix),
        TemplateId = type.FullKey,
        Mod = new OverlayModSettings { KoreanName = type.Info.KoreanName, EnglishName = type.Info.EnglishName },
      };
}
