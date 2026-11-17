using System;
using System.Linq.Expressions;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Theme;
using ElementUI;
using static ElementUI.UI;

namespace Element.Presentation.OverlayEditor.Parts;

/// <summary>
/// The controls every editor panel is built from: the grip it drags by,
/// icon buttons, buttons of a set width and the line between groups. Bring
/// them in with <c>using static Element.Presentation.OverlayEditor.Parts.EditorControls;</c>.
/// </summary>
internal static class EditorControls
{
  /// <summary>An icon whose button can't be pressed now.</summary>
  private static readonly UiColor Off = UiPalette.Disabled;

  /// <summary>
  /// The dots on a panel's left end: the panel drags from anywhere but its
  /// controls, and these say so. <paramref name="name"/> finds it.
  /// </summary>
  public static View Grip(string name) =>
    Image(ElementIcons.Of(UiIconKind.Grip), 24).Foreground(Colors.Muted).Named(name).Frame(width: 14, height: 24);

  /// <summary>
  /// A square button with an icon that runs <paramref name="command"/>; the
  /// icon greys out while it can't run. <paramref name="hint"/> shows on the
  /// status line while the pointer is over it.
  /// </summary>
  public static View IconButton(
    UiIconKind icon,
    string name,
    string hint,
    Expression<Func<Command>> command,
    float height
  ) => IconButton(icon, name, hint, command, height, Ink(command));

  /// <summary>An icon button whose icon takes <paramref name="ink"/>, such as a switch that is on or off.</summary>
  public static View IconButton(
    UiIconKind icon,
    string name,
    string hint,
    Expression<Func<Command>> command,
    float height,
    Expression<Func<UiColor>> ink
  ) =>
    Button(Image(ElementIcons.Of(icon), 20).Foreground(ink), command)
      .Named(name)
      .Hint(hint)
      .Frame(width: 36, height: height);

  /// <summary>
  /// A button of a set <paramref name="width"/> with <paramref name="label"/>
  /// centered in it, as the toolbars line their buttons up.
  /// </summary>
  public static View WideButton(
    View label,
    string name,
    Expression<Func<Command>> command,
    float width,
    float height,
    ButtonStyle style = ButtonStyle.Standard
  ) =>
    Button(label.Frame(maxWidth: float.PositiveInfinity), command, style)
      .Named(name)
      .Frame(width: width, height: height);

  /// <summary>A button of a set width with fixed words.</summary>
  public static View WideButton(
    string label,
    string name,
    Expression<Func<Command>> command,
    float width,
    float height,
    ButtonStyle style = ButtonStyle.Standard
  ) => WideButton(Text(label).LineLimit(1), name, command, width, height, style);

  /// <summary>The short upright line between groups of controls in a row.</summary>
  public static View Separator() => HairLine.Upright(24).Padding(0, 3.5f);

  /// <summary>
  /// The color of a button's label while its command can run, greyed while it
  /// can't: <c>VM.Undo</c> becomes <c>VM.Undo.CanExecute ? Foreground : Off</c>.
  /// </summary>
  public static Expression<Func<UiColor>> Ink(Expression<Func<Command>> command) =>
    Expression.Lambda<Func<UiColor>>(
      Expression.Condition(
        Expression.Property(command.Body, nameof(Command.CanExecute)),
        Expression.Constant(Colors.Foreground),
        Expression.Constant(Off)
      )
    );
}
