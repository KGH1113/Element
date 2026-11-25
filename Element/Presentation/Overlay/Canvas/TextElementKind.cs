using System.Collections.Generic;
using Element.Configuration.Overlay;
using Element.Domain.Layout;
using Element.Presentation.Overlay.Text;

namespace Element.Presentation.Overlay.Canvas;

/// <summary>Texts: drawn only while the overlay font is loaded.</summary>
internal sealed class TextElementKind : OverlayElementKind<TextOverlayElement>
{
  private readonly OverlayCanvasHost _host;

  public TextElementKind(OverlayCanvasHost host) => _host = host;

  public override bool Owns(string templateId) => templateId == OverlayElementKinds.Text;

  public override bool CanCreate => _host.Fonts != null;

  protected override TextOverlayElement Make() => new(Layer, _host.Fonts, _host.Values);

  /// <summary>A new text's saved layout, with <paramref name="runs"/> or empty.</summary>
  public static OverlayLayoutSettings NewSettings(List<OverlayTextRun> runs) =>
    new()
    {
      Id = OverlayElementIds.New(OverlayElementKinds.Text),
      TemplateId = OverlayElementKinds.Text,
      Text = new OverlayTextSettings { Runs = runs ?? new List<OverlayTextRun>() },
    };
}
