using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using ElementUI;
using static ElementUI.UI;

namespace Element.Presentation.Ui.Declarative.Controls;

/// <summary>
/// Cards in rows of a fixed number of equal columns, for a list that changes:
/// <c>CardRows.Of(() =&gt; VM.Cards, card =&gt; card.Id, 3, 12, (row, column) =&gt; Card(row, column))</c>.
/// A card reads its item with <c>CardRows.At(row.Value, column)</c>.
/// </summary>
/// <remarks>
/// ElementUI's <c>Grid</c> lays out a <c>ForEach</c>'s rows itself and never
/// measures the <c>ForEach</c>, so a card added, removed or resized later is
/// not laid out again. Here the list is a <c>ForEach</c> of rows that its
/// frame measures, and each card sits in a frame, so every change lays out.
/// </remarks>
internal static class CardRows
{
  private static readonly Dictionary<Type, System.Reflection.MethodInfo> Chunkers = new();

  public static View Of<T>(
    Expression<Func<T[]>> items,
    Func<T, string> key,
    int columns,
    float spacing,
    Func<ItemSlot<T[]>, int, View> card
  )
    where T : class
  {
    // () => Chunk(items(), columns), lowered by ForEach like any expression.
    Expression<Func<IReadOnlyList<T[]>>> rows = Expression.Lambda<Func<IReadOnlyList<T[]>>>(
      Expression.Convert(
        Expression.Call(Chunker<T>(), items.Body, Expression.Constant(columns)),
        typeof(IReadOnlyList<T[]>)
      )
    );
    return ForEach(rows, row => key(row[0]), slot => Row(slot, columns, spacing, card))
      .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.TopLeading);
  }

  /// <summary>The card at <paramref name="column"/> of a row; the row's last while the row is shrinking.</summary>
  [ElementUI.Pure]
  public static T At<T>(T[] row, int column) => row[Math.Min(column, row.Length - 1)];

  /// <summary>The items in rows of <paramref name="columns"/>; the last row may be shorter.</summary>
  [ElementUI.Pure]
  public static T[][] Chunk<T>(T[] items, int columns)
  {
    if (items == null || items.Length == 0)
      return Array.Empty<T[]>();
    var rows = new T[(items.Length + columns - 1) / columns][];
    for (int r = 0; r < rows.Length; r++)
    {
      int start = r * columns;
      rows[r] = new T[Math.Min(columns, items.Length - start)];
      Array.Copy(items, start, rows[r], 0, rows[r].Length);
    }
    return rows;
  }

  private static System.Reflection.MethodInfo Chunker<T>()
  {
    if (!Chunkers.TryGetValue(typeof(T), out System.Reflection.MethodInfo method))
      Chunkers[typeof(T)] = method = typeof(CardRows).GetMethod(nameof(Chunk)).MakeGenericMethod(typeof(T));
    return method;
  }

  // A row: the cards in equal columns, empty columns keeping their width; the gap under it.
  private static View Row<T>(ItemSlot<T[]> slot, int columns, float spacing, Func<ItemSlot<T[]>, int, View> card)
  {
    var cells = new View[columns];
    for (int c = 0; c < columns; c++)
    {
      int column = c;
      cells[c] = If(() => slot.Value.Length > column, () => card(slot, column))
        .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.TopLeading);
    }
    return HStack(spacing, VAlignment.Top, cells).Padding(new Insets(0, 0, spacing, 0));
  }
}
