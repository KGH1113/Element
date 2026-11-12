using System;
using Element.Configuration;
using Element.Configuration.Overlay;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Fonts;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Theme;
using ElementKit;
using ElementUI;
using TMPro;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.Layout.ContentEditors.Mods;

/// <summary>
/// Element's own pickers for a mod's editing screen, as views bound to a
/// value: colors, paints, fonts, the nine points and shortcuts, each in a row
/// with its label. <c>EditorPage</c>'s parts bind them to the values they hand
/// the mod; a screen a mod writes with ElementUI binds them to its settings.
/// </summary>
internal static class ModParts
{
  /// <summary>A row's height, and the width of the label left of its control.</summary>
  public const float RowHeight = 40,
    LabelWidth = 170;

  private static readonly UiColor Missing = UiPalette.Error;

  /// <summary>
  /// <paramref name="label"/> on the left and <paramref name="control"/> after
  /// it, the control starting where every row's does; no label, the control alone.
  /// </summary>
  public static View Row(string label, View control)
  {
    View row = string.IsNullOrEmpty(label)
      ? control
      : HStack(
        spacing: 12,
        UI.Text(label).Font(Font.Callout).LineLimit(1).Frame(width: LabelWidth - 12, alignment: Alignment.Leading),
        control
      );
    return row.Frame(maxWidth: float.PositiveInfinity, minHeight: RowHeight, alignment: Alignment.Leading);
  }

  /// <summary>
  /// A text field drawn the way Element's own are: its words on the middle
  /// line rather than the midline, so the caret takes the same line as the text.
  /// </summary>
  public static View Typed(View field) =>
    field.Configure<TMP_InputField>(input => input.textComponent.alignment = TextAlignmentOptions.Left);

  /// <summary>A swatch with its HEX (and opacity) that opens Element's color picker.</summary>
  public static View ColorPicker(string label, Binding<Color> color, bool opacity) =>
    Row(
      label,
      ColorSwatch
        .Of(
          Title(label, L("색", "Color")),
          color,
          opacity,
          UI.Text(() => ColorCaption(color.Value, opacity))
            .Font(Font.Caption)
            .Foreground(Colors.Muted)
            .LineLimit(1)
            .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading)
        )
        .Named("Color " + label)
        .Frame(maxWidth: 240, alignment: Alignment.Leading)
    );

  /// <summary>A swatch that opens the full picker: the highlight color, a color of one's own, or a gradient.</summary>
  public static View PaintPicker(string label, Binding<Paint> paint)
  {
    Opener opener = new();
    Binding<bool> open = Bind(() => opener.Open);
    string title = Title(label, L("색", "Color"));
    return Row(
      label,
      Button(
          HStack(
            spacing: 8,
            new SwatchDot(() => SwatchOf(paint.Value), () => NoGradient()),
            UI.Text(() => PaintCaption(paint.Value))
              .Font(Font.Caption)
              .Foreground(Colors.Muted)
              .LineLimit(1)
              .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading)
          ),
          () => opener.Show
        )
        .Named("Paint " + label)
        .Popover(open, () => new ModPaintPicker(title, paint, open))
        .Frame(maxWidth: 240, alignment: Alignment.Leading)
    );
  }

  /// <summary>The font's name (or that it is missing on this PC) that opens Element's font list.</summary>
  public static View FontPicker(string label, Binding<string> font, ModEditorContext context)
  {
    Opener opener = new();
    Binding<bool> open = Bind(() => opener.Open);
    OverlayFonts fonts = context.Fonts;
    string title = Title(label, L("폰트", "Font"));
    return Row(
      label,
      Button(
          HStack(
            spacing: 8,
            Image(ElementIcons.Of(UiIconKind.Font), 20).Foreground(Colors.Muted),
            UI.Text(() => FontName(font.Value, fonts))
              .Foreground(() => FontInk(font.Value, fonts))
              .LineLimit(1)
              .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading)
          ),
          () => opener.Show
        )
        .Named("Font " + label)
        .Popover(
          open,
          () =>
            new FontList(
              new FontListRequest
              {
                Fonts = fonts,
                Title = title,
                Current = OverlayFontKey.Normalize(font.Value),
                DefaultName = L("기본 폰트", "Default font"),
                DefaultDetail = L("오버레이 기본 폰트를 따라가요", "Follows the overlay's default font"),
                ReduceMotion = context.ReduceMotion,
                Picked = key => PickFont(font, key, context),
                Ticks = context.FontTicks,
              }
            )
        )
    );
  }

  /// <summary>The nine points of a box; the one picked is lit, arrows show the way it grows.</summary>
  public static View AnchorPicker(string label, Binding<Anchor> anchor, ModEditorContext context) =>
    AnchorPicker(label, AnchorModel(anchor, context));

  public static View AnchorPicker(string label, OverlayGrowPickerModel picker) =>
    Row(label, new OverlayGrowPointsView(picker));

  /// <summary>What the nine points show and do for <paramref name="anchor"/>; made once per part.</summary>
  public static OverlayGrowPickerModel AnchorModel(Binding<Anchor> anchor, ModEditorContext context)
  {
    OverlayGrowPickerModel picker = null;
    void Show() => picker.Show((OverlayGrowth)(int)anchor.Value, (Anchor9)(int)anchor.Value);
    // Only the nine points: an element's own Auto and caption don't apply here.
    picker = new OverlayGrowPickerModel(growth =>
    {
      if (!Enum.IsDefined(typeof(Anchor), (int)growth) || (Anchor)(int)growth == anchor.Value)
        return;
      anchor.Value = (Anchor)(int)growth;
      Show();
    });
    Show();
    context.OnRestored(Show);
    return picker;
  }

  /// <summary>A shortcut as keycaps: a click records the next key with at most one modifier; × clears it.</summary>
  public static View ShortcutField(string label, Binding<ElementKit.Shortcut> shortcut, ModEditorContext context) =>
    ShortcutField(label, ShortcutModel(shortcut, context));

  public static View ShortcutField(string label, ShortcutFieldModel model) =>
    Row(label, new ShortcutField(model, UiIconKind.Close, L("지우기", "Clear"), "Shortcut " + label));

  /// <summary>What a shortcut field shows and records for <paramref name="shortcut"/>; made once per part.</summary>
  public static ShortcutFieldModel ShortcutModel(Binding<ElementKit.Shortcut> shortcut, ModEditorContext context)
  {
    ShortcutFieldModel model = new(
      () => shortcut.Value.Key,
      () => shortcut.Value.Modifier,
      (key, modifier) =>
      {
        ElementKit.Shortcut next = new(key, modifier);
        if (next.Key != shortcut.Value.Key || next.Modifier != shortcut.Value.Modifier)
        {
          shortcut.Value = next;
          context.Edited();
        }
        return null;
      },
      () => shortcut.Value = default,
      L("왼쪽 Alt + 1", "Left Alt + 1"),
      L("없음", "None"),
      actNeedsShortcut: true
    );
    context.Track(model);
    return model;
  }

  private static string Title(string label, string fallback) => string.IsNullOrEmpty(label) ? fallback : label;

  private static void PickFont(Binding<string> font, string key, ModEditorContext context)
  {
    string next = OverlayFontKey.Normalize(key);
    if (OverlayFontKey.Same(next, OverlayFontKey.Normalize(font.Value)))
      return;
    font.Value = next;
    Runtime.Probe();
    context.Edited();
  }

  [ElementUI.Pure]
  private static string ColorCaption(Color color, bool opacity) =>
    "#" + ColorUtility.ToHtmlStringRGB(color) + Percent(opacity ? color.a : 1);

  [ElementUI.Pure]
  private static Color SwatchOf(Paint paint)
  {
    Color color = (paint ?? Paint.Solid(Color.white)).ToColor(UiTheme.Accent);
    color.a = 1;
    return color;
  }

  [ElementUI.Pure]
  private static OverlayResolvedGradient NoGradient() => null;

  [ElementUI.Pure]
  private static string PaintCaption(Paint paint)
  {
    paint ??= Paint.Solid(Color.white);
    if (paint.IsGradient)
      return L("그라디언트 · 색 ", "Gradient · ") + paint.Gradient.Stops.Count + L("개", " colors");
    return paint.Highlight
      ? L("하이라이트 색", "Highlight") + Percent(paint.Color.a)
      : "#" + ColorUtility.ToHtmlStringRGB(paint.Color) + Percent(paint.Color.a);
  }

  [ElementUI.Pure]
  private static string FontName(string font, OverlayFonts fonts)
  {
    string key = OverlayFontKey.Normalize(font);
    if (key.Length == 0)
      return L("기본 폰트", "Default font");
    return StatusOf(key, fonts) == OverlayFontStatus.Missing
      ? OverlayFonts.DisplayName(key) + L(" · 이 PC에 없음", " · not on this PC")
      : OverlayFonts.DisplayName(key);
  }

  [ElementUI.Pure]
  private static UiColor FontInk(string font, OverlayFonts fonts)
  {
    string key = OverlayFontKey.Normalize(font);
    return key.Length > 0 && StatusOf(key, fonts) == OverlayFontStatus.Missing ? Missing : Colors.Foreground;
  }

  private static OverlayFontStatus StatusOf(string key, OverlayFonts fonts) =>
    fonts?.Status(key) ?? OverlayFontStatus.Missing;

  private static string Percent(float alpha) => alpha < .995f ? "  " + Mathf.RoundToInt(alpha * 100) + "%" : "";

  /// <summary>Whether a part's popover is open.</summary>
  private sealed class Opener : ViewModel
  {
    public bool Open;

    public Opener() => Show = Command(() => Open = true);

    public Command Show { get; }
  }
}
