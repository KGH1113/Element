using System.Collections.Generic;
using Element.Presentation.Layout.ContentEditors.Shell;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;

namespace Element.Presentation.Layout.ContentEditors.Mods;

/// <summary>
/// A mod's <see cref="ModEditorPage"/> in the property column: its parts one
/// under another, as the column's rows; a card is a heading over its parts,
/// a tab page shows while its tab is picked. The parts the mod placed with
/// <c>At</c> stand over the screen instead (<see cref="Placed"/>).
/// </summary>
internal sealed class ModEditorPageView : View<ModEditorPage>
{
  public ModEditorPageView(ModEditorPage page)
    : base(page) { }

  public override View Body => Stacked(VM.Items);

  /// <summary>The parts the mod placed itself, each where it put it on the screen.</summary>
  public static View Placed(ModEditorPage page)
  {
    List<View> layers = new();
    foreach (ModPart part in page.Placed)
    {
      Vector2 at = part.Position ?? Vector2.zero;
      // In a stack it is as tall as it wants rather than the rest of the screen.
      layers.Add(
        VStack(spacing: 0, new ModPartView(part).Frame(width: part.FrameWidth, alignment: Alignment.TopLeading))
          .Padding(new Insets(Mathf.Max(0, at.y), Mathf.Max(0, at.x), 0, 0))
      );
    }
    return ZStack(Alignment.TopLeading, layers.ToArray())
      .Frame(width: page.Size.x, height: page.Size.y, alignment: Alignment.TopLeading);
  }

  /// <summary>A card: its title as a heading over its parts.</summary>
  public static View Card(ModGroup card) => UI.Card(card.Title, Stacked(card.Children));

  /// <summary>A tab's page: its parts, while its tab is picked.</summary>
  public static View TabPage(ModGroup page) => Stacked(page.Children);

  // Parts one under another, as wide as the column or as the mod made them; a tab page only while picked.
  private static View Stacked(IReadOnlyList<ModPart> parts)
  {
    View[] rows = new View[parts.Count];
    for (int i = 0; i < rows.Length; i++)
    {
      ModPart part = parts[i];
      if (part is ModGroup { Tabs: not null } tab)
      {
        ModValue<int> pick = tab.Tabs.Pick;
        int index = tab.Index;
        rows[i] = If(() => pick.Value == index, () => Fitted(tab));
      }
      else
        rows[i] = Fitted(part);
    }
    return VStack(spacing: OverlayInspectorRow.Gap, HAlignment.Leading, rows)
      .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.TopLeading);
  }

  private static View Fitted(ModPart part) =>
    part.WidthOverride is float width
      ? new ModPartView(part).Frame(width: Mathf.Min(width, ModEditorColumn.Width), alignment: Alignment.TopLeading)
      : new ModPartView(part).Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.TopLeading);
}
