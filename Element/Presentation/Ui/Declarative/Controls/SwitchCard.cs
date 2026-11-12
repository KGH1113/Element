using System;
using System.Linq.Expressions;
using ElementUI;
using static ElementUI.UI;

namespace Element.Presentation.Ui.Declarative.Controls;

/// <summary>
/// A setting that is on or off, as a card: what it is and what it does on the
/// left, its switch on the right. Narrow windows wrap the words, not the switch.
/// </summary>
internal static class SwitchCard
{
  public static View Of(string title, string description, Binding<bool> value) => Card(title, Text(description), value);

  /// <summary>A switch whose description follows the view model, such as a status.</summary>
  public static View Of(string title, Expression<Func<string>> description, Binding<bool> value) =>
    Card(title, Text(description), value);

  /// <summary>A switch whose words come from a list row: <c>() =&gt; slot.Value.Title</c>.</summary>
  public static View Of(Expression<Func<string>> title, Expression<Func<string>> description, Binding<bool> value) =>
    Card(Text(title), Text(description), value);

  private static View Card(string title, View description, Binding<bool> value) =>
    Card(Text(title), description, value);

  private static View Card(View title, View description, Binding<bool> value) =>
    HStack(
        spacing: 16,
        VStack(spacing: 4, title.Font(Font.Heading.Sized(17)), description.Font(Font.Caption).Foreground(Colors.Muted))
          .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading),
        Toggle(null, value)
      )
      .Padding(18, 16)
      .Frame(maxWidth: float.PositiveInfinity, minHeight: 88)
      .Background(Colors.Surface, 12);
}
