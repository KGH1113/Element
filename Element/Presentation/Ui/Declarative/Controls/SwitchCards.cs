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
  /// With <paramref name="enabled"/> (<c>() =&gt; VM.CanExclude.Value</c>) the card
  /// is off, and dimmed, while it is false.
  /// </summary>
  public static View SwitchCard(string title, string detail, Binding<bool> on, Expression<Func<bool>> enabled = null) =>
    Card(Text(title), Text(detail), on, enabled);

  /// <summary>A switch card whose description follows the view model, such as a status.</summary>
  public static View SwitchCard(
    string title,
    Expression<Func<string>> detail,
    Binding<bool> on,
    Expression<Func<bool>> enabled = null
  ) => Card(Text(title), Text(detail), on, enabled);

  /// <summary>A switch card whose words come from a list row: <c>() =&gt; slot.Value.Title</c>.</summary>
  public static View SwitchCard(Expression<Func<string>> title, Expression<Func<string>> detail, Binding<bool> on) =>
    Card(Text(title), Text(detail), on, null);

  private static View Card(View title, View detail, Binding<bool> on, Expression<Func<bool>> enabled)
  {
    View copy = SwitchCopy(title, detail, on);
    if (enabled == null)
      return Button(copy, FlipOf(on), ButtonStyle.Card);
    // The same condition, once as "off" and once as the title's color.
    Expression<Func<bool>> disabled = Expression.Lambda<Func<bool>>(Expression.Not(enabled.Body));
    Expression<Func<UiColor>> ink = Expression.Lambda<Func<UiColor>>(
      Expression.Condition(enabled.Body, Expression.Constant(Colors.Foreground), Expression.Constant(Colors.Muted))
    );
    return Button(copy.Foreground(ink), FlipOf(on), ButtonStyle.Card).Disabled(disabled);
  }

  private static View SwitchCopy(View title, View detail, Binding<bool> on) =>
    HStack(
        spacing: 16,
        // The words take the room the switch leaves. A Spacer would let the card
        // grow taller too: ElementUI's Spacer stretches along both axes.
        VStack(spacing: 4, title.Font(Font.Heading.Sized(17)), detail.Font(Font.Caption).Foreground(Colors.Muted))
          .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading),
        Toggle(null, on)
      )
      // With the card's insets, at least 88 tall, its words and switch centered in it.
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
