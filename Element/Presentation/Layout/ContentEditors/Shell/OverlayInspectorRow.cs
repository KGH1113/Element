using System;
using System.Linq.Expressions;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Theme;
using ElementUI;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// How every row of an editor's property column looks, whoever made it: the
/// text and shape editors' rows (<see cref="OverlayInspectorRowView"/>) and
/// a mod's settings drawn in the column. A row is 40 units tall on an opaque
/// rounded fill: what leads it (an icon, a swatch or letters) in a slot of
/// its own, so every name starts at the same place, the name, then on the
/// right what it holds, its switch or its control. Headings sit over groups
/// of rows; a tool opened from a row has a back row naming it.
/// </summary>
internal static class OverlayInspectorRow
{
  public const float Height = 40,
    BackHeight = 36,
    Gap = 6,
    HeadingHeight = 22,
    Radius = 10,
    LeadWidth = 22;

  /// <summary>A row's fill at rest, on the editor's dimmed view.</summary>
  public static readonly UiColor Fill = UiPalette.EditorControl;

  /// <summary>
  /// <paramref name="lead"/> (null leaves its slot empty), the name, room,
  /// then <paramref name="trailing"/> on the right. <paramref name="switchEnd"/>
  /// sets a switch a little closer to the end, as the settings' switches are.
  /// </summary>
  public static View Of(View lead, View name, bool switchEnd, params View[] trailing) =>
    Lay(
      HStack(0, LeadSlot(lead), name.Padding(new Insets(0, 8, 0, 0)), Spacer(minLength: 8), HStack(0, trailing)),
      switchEnd
    );

  /// <summary>
  /// <paramref name="lead"/>, the name, then <paramref name="control"/>
  /// taking the room left (a slider), then <paramref name="trailing"/>.
  /// </summary>
  public static View Filled(View lead, View name, View control, params View[] trailing) =>
    Lay(
      HStack(
        0,
        LeadSlot(lead),
        name.Padding(new Insets(0, 8, 0, 0)),
        control.Padding(new Insets(0, 12, 0, 0)),
        HStack(0, trailing)
      ),
      false
    );

  /// <summary>The name, in the ink <paramref name="ink"/> picks: muted while there is nothing for the row to act on.</summary>
  public static View Name(string label, Expression<Func<UiColor>> ink) =>
    UI.Text(label ?? "").Font(Font.Caption.Sized(13)).Foreground(ink).LineLimit(1);

  /// <summary>What the row holds, on its right.</summary>
  public static View Value(Expression<Func<string>> text, Expression<Func<UiColor>> ink) =>
    UI.Text(text).Font(Font.Caption.Sized(13)).Foreground(ink).LineLimit(1);

  /// <summary>After a value, such as "px".</summary>
  public static View Unit(string unit) =>
    UI.Text(unit).Font(Font.Caption.Sized(12)).Foreground(Colors.Muted).LineLimit(1).Padding(new Insets(0, 3, 0, 0));

  /// <summary>Marks a row that opens a tool in the column.</summary>
  public static View Chevron() =>
    Image(ElementIcons.Of(UiIconKind.Forward), 14).Foreground(Colors.Muted).Padding(new Insets(0, 6, 0, 0));

  /// <summary>An icon leading a row.</summary>
  public static View Icon(UiIconKind icon, Expression<Func<UiColor>> ink) =>
    Image(ElementIcons.Of(icon), 18).Foreground(ink);

  /// <summary>Letters leading a row, such as "Aa".</summary>
  public static View Glyph(string letters) => UI.Text(letters).Font(Font.Callout).Foreground(Colors.Muted).LineLimit(1);

  /// <summary>Over a group of rows, such as "Text".</summary>
  public static View Heading(string title) =>
    UI.Text(title ?? "")
      .Font(Font.Caption.Bolded())
      .Foreground(Colors.Muted)
      .LineLimit(1)
      .Frame(height: HeadingHeight, alignment: Alignment.BottomLeading)
      .Padding(new Insets(0, 4, 0, 0));

  /// <summary>
  /// Back to the rows, naming the tool open below it. It reads as the dimmed
  /// view at rest but is opaque, so its hover doesn't let the overlay through.
  /// </summary>
  public static View Back(Expression<Func<string>> title, Expression<Func<Command>> back, string name) =>
    Button(
        HStack(
            spacing: 6,
            Image(ElementIcons.Of(UiIconKind.Back), 16).Foreground(Colors.Muted),
            UI.Text(title).Font(Font.Caption.Sized(13)).LineLimit(1),
            Spacer()
          )
          .Padding(0, 8)
          .Frame(height: BackHeight),
        back,
        ButtonStyle.Plain
      )
      .ButtonStyle(new ButtonSurfaceStyle(UiPalette.EditorDim, BackHeight / 2))
      .Named(name);

  private static View LeadSlot(View lead) =>
    (lead ?? Spacer().Frame(width: 18, height: 18)).Frame(width: LeadWidth, alignment: Alignment.Leading);

  private static View Lay(View content, bool switchEnd) =>
    content.Padding(new Insets(0, 12, 0, switchEnd ? 10 : 12)).Frame(maxWidth: float.PositiveInfinity, height: Height);
}
