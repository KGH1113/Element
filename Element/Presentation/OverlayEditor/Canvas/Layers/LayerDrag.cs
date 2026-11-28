using System.Collections.Generic;

namespace Element.Presentation.OverlayEditor.Canvas.Layers;

/// <summary>A layer card being carried: the elements it stands for, and its group when it is a whole group's card.</summary>
internal sealed class LayerDrag
{
  public LayerDrag(string key, IReadOnlyList<string> ids, string group, string name)
  {
    Key = key;
    Ids = ids;
    Group = group;
    Name = name;
  }

  /// <summary>The card's key in the list.</summary>
  public string Key { get; }

  /// <summary>The elements that move, back to front.</summary>
  public IReadOnlyList<string> Ids { get; }

  /// <summary>The group when the whole group is carried; null for one element.</summary>
  public string Group { get; }

  /// <summary>What the card is called, shown beside the pointer.</summary>
  public string Name { get; }
}
