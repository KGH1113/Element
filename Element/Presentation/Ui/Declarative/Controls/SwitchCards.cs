using System;
using System.Linq.Expressions;
using ElementUI;
using static ElementUI.UI;

namespace Element.Presentation.Ui.Declarative.Controls;

/// <summary>
/// Settings that are on or off, as cards. Bring them in with
/// <c>using static Element.Presentation.Ui.Declarative.Controls.SwitchCards;</c>.
/// </summary>
internal static class SwitchCards
{
  /// <summary>
  /// A setting that is on or off: its name and what it does on the left, the
  /// switch on the right, on a raised card. A click anywhere on the card flips it.
  /// </summary>
  public static View SwitchCard(string title, string detail, Binding<bool> on) =>
    Button(SwitchCopy(title, detail, on), FlipOf(on), ButtonStyle.Card);

  /// <summary>
  /// A <see cref="SwitchCard(string, string, Binding{bool})"/> that is off, and dimmed,
  /// while <paramref name="enabled"/> is false: <c>() =&gt; VM.CanExclude.Value</c>.
  /// </summary>
  public static View SwitchCard(string title, string detail, Binding<bool> on, Expression<Func<bool>> enabled)
  {
    // The same condition, once as "off" and once as the title's color.
    Expression<Func<bool>> disabled = Expression.Lambda<Func<bool>>(Expression.Not(enabled.Body));
    Expression<Func<UiColor>> ink = Expression.Lambda<Func<UiColor>>(
      Expression.Condition(enabled.Body, Expression.Constant(Colors.Foreground), Expression.Constant(Colors.Muted))
    );
    return Button(SwitchCopy(title, detail, on).Foreground(ink), FlipOf(on), ButtonStyle.Card).Disabled(disabled);
  }

  private static View SwitchCopy(string title, string detail, Binding<bool> on) =>
    HStack(
        spacing: 16,
        // The words take the room the switch leaves. A Spacer would let the card
        // grow taller too: ElementUI's Spacer stretches along both axes.
        VStack(spacing: 4, Text(title).Font(Font.Heading), Text(detail).Font(Font.Caption).Foreground(Colors.Muted))
          .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading),
        Toggle(null, on)
      )
      // The card is at least 88 tall, its words and switch centered in it.
      .Frame(maxWidth: float.PositiveInfinity, minHeight: 48, alignment: Alignment.Leading);

  private static Expression<Func<Command>> FlipOf(Binding<bool> on)
  {
    Flip flip = new(on);
    return () => flip.Toggle;
  }

  /// <summary>What a click on a switch card does: the value becomes the other one.</summary>
  private sealed class Flip : ViewModel
  {
    public Flip(Binding<bool> on) => Toggle = Command(() => on.Value = !on.Value);

    public Command Toggle { get; }
  }
}
