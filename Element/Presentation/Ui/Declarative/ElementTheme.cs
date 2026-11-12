using Element.Presentation.Ui.Theme;
using ElementUI;
using ElementUI.Ugui;
using TMPro;
using UnityEngine;

namespace Element.Presentation.Ui.Declarative;

/// <summary>
/// Element's look for ElementUI screens: the dark surfaces, the highlight
/// color the player picked, the overlay font and the sizes of the settings window.
/// </summary>
internal sealed class ElementTheme : UguiTheme
{
  public ElementTheme(TMP_FontAsset font, bool reduceMotion)
  {
    Font = font;
    // The bundled face is already heavy; synthesized bold smears it.
    BoldIsHeavy = true;
    MotionSeconds = reduceMotion ? 0 : .16f;
    SetColor(ColorToken.Foreground, UiPalette.Foreground);
    SetColor(ColorToken.Muted, UiPalette.Muted);
    SetColor(ColorToken.Background, UiPalette.Background);
    SetColor(ColorToken.Surface, UiPalette.Surface);
    // Raised parts (an off switch, a slider's track, a field) sit on cards: lighter than the card.
    SetColor(ColorToken.SurfaceRaised, new Color(.19f, .19f, .19f));
    SetColor(ColorToken.Line, new Color(1, 1, 1, .1f));
    SetColor(ColorToken.Danger, UiPalette.Error);
    FollowHighlight();
    SetFontSize(FontRole.Body, 16);
    SetFontSize(FontRole.Title, 27);
    SetFontSize(FontRole.Heading, 18);
    SetFontSize(FontRole.Callout, 15);
    SetFontSize(FontRole.Caption, 14);
    SetFontSize(FontRole.Mono, 13);
  }

  /// <summary>Takes the highlight color as it is now; screens are mounted again when it changes.</summary>
  public void FollowHighlight()
  {
    SetColor(ColorToken.Accent, UiPalette.Action);
    SetColor(ColorToken.OnAccent, UiPalette.OnAction);
  }

  /// <summary>
  /// The layout editor's panels: many controls in a fixed width, so buttons
  /// keep 12 units beside their words, and buttons and fields take the
  /// panels' 8-unit corners.
  /// </summary>
  public bool Compact { get; set; }

  public override float StackSpacing => 12;

  public override float ControlHeight => 40;

  public override float LabelWidth => 170;

  public override float CornerRadius => Compact ? 8 : 12;

  public override Insets ButtonInsets(ElementUI.ButtonStyle style) =>
    Compact && style is ElementUI.ButtonStyle.Standard or ElementUI.ButtonStyle.Prominent
      ? new Insets(8, 12)
      : base.ButtonInsets(style);

  public override float ButtonRadius(ElementUI.ButtonStyle style, float height) =>
    Compact && style is ElementUI.ButtonStyle.Standard or ElementUI.ButtonStyle.Prominent
      ? CornerRadius
      : base.ButtonRadius(style, height);

  // Element's main action is a tinted fill with the soft accent as its label.
  public override UiColor? ButtonForeground(ElementUI.ButtonStyle style) =>
    style == ElementUI.ButtonStyle.Prominent ? UiPalette.SoftAccent : base.ButtonForeground(style);

  public override Color ButtonFill(ElementUI.ButtonStyle style, ElementUI.Ugui.ButtonState state)
  {
    Color basis = style switch
    {
      ElementUI.ButtonStyle.Prominent => UiPalette.AccentSurface,
      ElementUI.ButtonStyle.Plain or ElementUI.ButtonStyle.Icon or ElementUI.ButtonStyle.Row => new Color(1, 1, 1, 0),
      ElementUI.ButtonStyle.Chip => new Color(.16f, .16f, .16f),
      _ => UiPalette.Surface,
    };
    if ((state & ElementUI.Ugui.ButtonState.Selected) != 0)
      basis =
        style == ElementUI.ButtonStyle.Row
          ? new Color(1, 1, 1, .08f)
          : UnityEngine.Color.Lerp(UiPalette.Surface, UiPalette.SoftAccent, .35f);
    // Off, a filled button sinks toward the background; a see-through one
    // stays see-through (what is behind it, such as its own background, dims it).
    if ((state & ElementUI.Ugui.ButtonState.Disabled) != 0)
      return basis.a == 0 ? basis : UnityEngine.Color.Lerp(basis, UiPalette.Background, .45f);
    if ((state & ElementUI.Ugui.ButtonState.Pressed) != 0)
      return Lighten(basis, -.2f, .12f);
    if ((state & ElementUI.Ugui.ButtonState.Hovered) != 0)
      return basis.a == 0 ? new Color(1, 1, 1, .08f) : UnityEngine.Color.Lerp(basis, UiPalette.Foreground, .13f);
    return basis;
  }
}
