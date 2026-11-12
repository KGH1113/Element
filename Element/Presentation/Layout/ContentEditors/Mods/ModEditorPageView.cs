using System.Collections.Generic;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.Layout.ContentEditors.Mods;

/// <summary>
/// A mod's <see cref="ModEditorPage"/> on screen: its parts in columns under
/// the preview, centered, each part standing in the column the page put it
/// in; and over them, the parts the mod placed with <c>At</c>. Cards and tab
/// pages lay their parts out one under another.
/// </summary>
internal sealed class ModEditorPageView : View<ModEditorPage>
{
  public ModEditorPageView(ModEditorPage page)
    : base(page) { }

  public override View Body => Page(VM);

  private static View Page(ModEditorPage page)
  {
    Rect flow = page.FlowArea;
    List<View> layers = new()
    {
      Columns(page)
        .Frame(width: flow.width, alignment: Alignment.Top)
        .Padding(new Insets(flow.y, flow.x, 0, 0)),
    };
    foreach (ModPart part in page.Placed)
    {
      Vector2 at = part.Position ?? Vector2.zero;
      layers.Add(
        new ModPartView(part)
          .Frame(width: part.FrameWidth, alignment: Alignment.TopLeading)
          .Padding(new Insets(Mathf.Max(0, at.y), Mathf.Max(0, at.x), 0, 0))
      );
    }
    return ZStack(Alignment.TopLeading, layers.ToArray())
      .Frame(width: page.Size.x, height: page.Size.y, alignment: Alignment.TopLeading);
  }

  // Only the columns in use take room, so the ones there are stay centered.
  private static View Columns(ModEditorPage page)
  {
    View[] columns = new View[ModEditorPage.MaxColumns];
    for (int i = 0; i < columns.Length; i++)
    {
      int index = i;
      columns[i] = If(() => page.ColumnCount.Value > index, () => Column(page, index));
    }
    return HStack(spacing: ModEditorPage.ColumnGap, VAlignment.Top, columns);
  }

  // Every part that can flow has a place in every column; it shows in the one it stands in.
  private static View Column(ModEditorPage page, int index)
  {
    IReadOnlyList<ModPart> parts = page.Flowing;
    View[] places = new View[parts.Count];
    for (int i = 0; i < places.Length; i++)
    {
      ModPart part = parts[i];
      places[i] = If(() => part.Column.Value == index, () => InColumn(part));
    }
    return VStack(spacing: ModEditorPage.RowGap, HAlignment.Leading, places)
      .Frame(width: ModEditorPage.ColumnWidth, alignment: Alignment.TopLeading);
  }

  private static View InColumn(ModPart part) =>
    new ModPartView(part).Frame(width: part.FrameWidth, alignment: Alignment.TopLeading);

  /// <summary>A card: its title, then its parts, on a raised surface.</summary>
  public static View Card(ModGroup card)
  {
    List<View> rows = new();
    if (!string.IsNullOrEmpty(card.Title))
      rows.Add(UI.Text(card.Title).Font(Font.Caption.Bolded()).LineLimit(1));
    rows.AddRange(Inside(card));
    return VStack(spacing: ModEditorPage.RowGap, HAlignment.Leading, rows.ToArray())
      .Padding(ModEditorPage.CardPadding)
      .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.TopLeading)
      .Background(Colors.Surface, 10, Colors.Line);
  }

  /// <summary>A tab's page inside a card or another page: its parts, while its tab is picked.</summary>
  public static View TabPage(ModGroup page) =>
    VStack(spacing: ModEditorPage.RowGap, HAlignment.Leading, Inside(page))
      .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.TopLeading);

  // A group's parts as wide as it is, or as the mod made them; a tab page only while picked.
  private static View[] Inside(ModGroup group)
  {
    View[] rows = new View[group.Children.Count];
    for (int i = 0; i < rows.Length; i++)
    {
      ModPart child = group.Children[i];
      if (child is ModGroup { Tabs: not null } tab)
      {
        ModValue<int> pick = tab.Tabs.Pick;
        int index = tab.Index;
        rows[i] = If(() => pick.Value == index, () => Fitted(tab));
      }
      else
        rows[i] = Fitted(child);
    }
    return rows;
  }

  private static View Fitted(ModPart part) =>
    part.WidthOverride is float width
      ? new ModPartView(part).Frame(width: width, alignment: Alignment.TopLeading)
      : new ModPartView(part).Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.TopLeading);
}
