using System;
using Element.Configuration.Overlay;
using Element.Domain.Layout;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Fonts;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Popovers;
using Element.Presentation.Ui.Theme;
using ElementKit;
using ElementUI;
using TMPro;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;
using Font = ElementUI.Font;
using Row = Element.Presentation.Layout.ContentEditors.Shell.OverlayInspectorRow;

namespace Element.Presentation.Layout.ContentEditors.Mods;

/// <summary>
/// Element's own pickers on a mod's editing screen, as rows of the property
/// column bound to a value: a color or paint (its swatch and HEX), a font,
/// one of nine points and a shortcut, each opening its tool in the column
/// as the text editor's rows do. <c>EditorPage</c>'s parts bind them to the
/// values they hand the mod; a screen a mod writes with ElementUI binds them
/// to its settings.
/// </summary>
internal static class ModParts
{
  private const int FontNameLength = 18;

  private static readonly UiColor Missing = UiPalette.Error;

  /// <summary>
  /// A text field drawn the way Element's own are: its words on the middle
  /// line rather than the midline, so the caret takes the same line as the text.
  /// </summary>
  public static View Typed(View field) =>
    field.Configure<TMP_InputField>(input => input.textComponent.alignment = TextAlignmentOptions.Left);

  /// <summary>A swatch with its HEX (and opacity) that opens Element's color picker.</summary>
  public static View ColorPicker(string label, Binding<Color> color, bool opacity, ModEditorContext context)
  {
    string title = Title(label, L("색", "Color"));
    return Opener(
      context,
      "Color " + label,
      _ => new SwatchDot(() => Opaque(color.Value), () => NoGradient()),
      title,
      on => Row.Value(() => ColorCaption(color.Value, opacity), () => Ink(on.Value)),
      // The color's own controls end with the column, as the text editor's do.
      _ =>
        new ModColorTool(title, color, opacity).Frame(
          maxWidth: float.PositiveInfinity,
          alignment: Alignment.TopTrailing
        )
    );
  }

  /// <summary>A swatch that opens the full picker: the highlight color, a color of one's own, or a gradient.</summary>
  public static View PaintPicker(string label, Binding<Paint> paint, ModEditorContext context)
  {
    string title = Title(label, L("색", "Color"));
    return Opener(
      context,
      "Paint " + label,
      _ => new SwatchDot(() => SwatchOf(paint.Value), () => NoGradient()),
      title,
      on => Row.Value(() => PaintCaption(paint.Value), () => Ink(on.Value)),
      _ => new ModPaintPicker(title, paint),
      ModPaintPicker.Width
    );
  }

  /// <summary>The font's name (or that it is missing on this PC) that opens Element's font list.</summary>
  public static View FontPicker(string label, Binding<string> font, ModEditorContext context)
  {
    OverlayFonts fonts = context.Fonts;
    string title = Title(label, L("폰트", "Font"));
    return Opener(
      context,
      "Font " + label,
      on => Row.Icon(UiIconKind.Font, () => Ink(on.Value)),
      title,
      on => Row.Value(() => FontName(font.Value, fonts), () => FontInk(font.Value, fonts, on.Value)),
      _ => new FontList(
        new FontListRequest
        {
          Fonts = fonts,
          // The column's back row names the list.
          Title = "",
          Current = OverlayFontKey.Normalize(font.Value),
          DefaultName = L("기본 폰트", "Default font"),
          DefaultDetail = L("오버레이 기본 폰트를 따라가요", "Follows the overlay's default font"),
          ReduceMotion = context.ReduceMotion,
          Picked = key => PickFont(font, key, context),
          Ticks = context.FontTicks,
        }
      ),
      UiFontPopover.PreferredWidth,
      ToolHeight.Fill
    );
  }

  /// <summary>The nine points of a box; the row names the one picked and opens them.</summary>
  public static View AnchorPicker(string label, Binding<Anchor> anchor, ModEditorContext context) =>
    AnchorPicker(label, anchor, context, () => AnchorModel(anchor, context));

  /// <summary>Like <see cref="AnchorPicker(string, Binding{Anchor}, ModEditorContext)"/>, with the points' model made by <paramref name="points"/> when they open.</summary>
  public static View AnchorPicker(
    string label,
    Binding<Anchor> anchor,
    ModEditorContext context,
    Func<OverlayGrowPickerModel> points
  )
  {
    string title = Title(label, L("위치", "Point"));
    return Opener(
      context,
      "Anchor " + label,
      on => Row.Icon(UiIconKind.GrowFrom, () => Ink(on.Value)),
      title,
      on => Row.Value(() => AnchorName(anchor.Value), () => Ink(on.Value)),
      commands => AnchorTool(points(), anchor)
    );
  }

  /// <summary>What the nine points show and do for <paramref name="anchor"/>.</summary>
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

  /// <summary>A shortcut's keys; the row opens the recorder: a click records the next key with at most one modifier, × clears it.</summary>
  public static View ShortcutField(string label, Binding<ElementKit.Shortcut> shortcut, ModEditorContext context) =>
    ShortcutField(label, ShortcutModel(shortcut, context), context);

  public static View ShortcutField(string label, ShortcutFieldModel model, ModEditorContext context)
  {
    string title = Title(label, L("단축키", "Shortcut"));
    return Opener(
      context,
      "Shortcut " + label,
      null,
      title,
      on =>
        Row.Value(
          () =>
            ShortcutCaption(
              model.HasShortcut.Value,
              model.HasModifier.Value,
              model.ModifierText.Value,
              model.KeyText.Value
            ),
          () => Ink(on.Value)
        ),
      _ =>
        new ShortcutField(model, UiIconKind.Close, L("지우기", "Clear"), "Shortcut field " + label).Padding(
          new Insets(2, 0, 0, 0)
        )
    );
  }

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

  /// <summary>
  /// A row of the column that opens a tool in it: <paramref name="lead"/>,
  /// the name, <paramref name="value"/> and a chevron, read muted while the
  /// screen turns the row off (each gets whether it is on). <paramref name="tool"/>
  /// makes the tool each time it opens.
  /// </summary>
  public static View Opener(
    ModEditorContext context,
    string name,
    Func<State<bool>, View> lead,
    string title,
    Func<State<bool>, View> value,
    Func<ModCommands, View> tool,
    float width = float.NaN,
    ToolHeight height = ToolHeight.Own
  )
  {
    EnabledWatch watch = new();
    State<bool> on = watch.Enabled;
    Opening opening = new(() => context.Column?.Open(title, tool, width, height));
    return Button(
        ZStack(
          Alignment.Leading,
          Row.Of(lead?.Invoke(on), Row.Name(title, () => Ink(on.Value)), false, value(on), Row.Chevron()),
          watch
        ),
        () => opening.Open,
        ButtonStyle.Plain
      )
      .Background(Row.Fill, Row.Radius)
      .Named(name);
  }

  // The nine points on the popover's panel, the picked one named beside them.
  private static View AnchorTool(OverlayGrowPickerModel points, Binding<Anchor> anchor) =>
    HStack(
        spacing: 14,
        new OverlayGrowPointsView(points),
        UI.Text(() => AnchorName(anchor.Value))
          .Font(Font.Caption.Sized(13))
          .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading)
      )
      .Padding(new Insets(12, 14, 14, 14))
      .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading)
      .Background(UiPalette.Popover, 8);

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
  private static Color Opaque(Color color) => new(color.r, color.g, color.b, 1);

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
      return L("그라디언트", "Gradient");
    return paint.Highlight
      ? L("하이라이트", "Highlight") + Percent(paint.Color.a)
      : "#" + ColorUtility.ToHtmlStringRGB(paint.Color) + Percent(paint.Color.a);
  }

  // Short, as the text editor's Font row names it.
  [ElementUI.Pure]
  private static string FontName(string font, OverlayFonts fonts)
  {
    string key = OverlayFontKey.Normalize(font);
    if (key.Length == 0)
      return L("기본", "Default");
    string name = OverlayFonts.DisplayName(key);
    if (StatusOf(key, fonts) == OverlayFontStatus.Missing)
      return L("이 PC에 없음", "Not on this PC");
    return name.Length > FontNameLength ? name.Substring(0, FontNameLength - 1).TrimEnd() + "…" : name;
  }

  [ElementUI.Pure]
  private static UiColor FontInk(string font, OverlayFonts fonts, bool on)
  {
    string key = OverlayFontKey.Normalize(font);
    return !on ? Colors.Muted
      : key.Length > 0 && StatusOf(key, fonts) == OverlayFontStatus.Missing ? Missing
      : Colors.Foreground;
  }

  [ElementUI.Pure]
  private static UiColor Ink(bool on) => on ? Colors.Foreground : Colors.Muted;

  [ElementUI.Pure]
  private static string AnchorName(Anchor anchor) => OverlayGrowPickerModel.ShortName((OverlayGrowth)(int)anchor);

  [ElementUI.Pure]
  private static string ShortcutCaption(bool has, bool modifier, string modifierText, string key) =>
    !has ? L("없음", "None")
    : modifier ? modifierText + " + " + key
    : key;

  private static OverlayFontStatus StatusOf(string key, OverlayFonts fonts) =>
    fonts?.Status(key) ?? OverlayFontStatus.Missing;

  private static string Percent(float alpha) => alpha < .995f ? "  " + Mathf.RoundToInt(alpha * 100) + "%" : "";

  /// <summary>A row's click, opening its tool.</summary>
  private sealed class Opening : ViewModel
  {
    public Opening(Action open) => Open = Command(open);

    public Command Open { get; }
  }
}
