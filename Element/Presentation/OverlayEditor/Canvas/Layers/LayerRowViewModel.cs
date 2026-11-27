using System;
using System.Collections.Generic;
using ElementUI;

namespace Element.Presentation.OverlayEditor.Canvas.Layers;

/// <summary>
/// One card of the layers panel: an element, a group's card, or an element
/// shown inside an open group. The list keeps a card while its key stays.
/// </summary>
internal sealed class LayerRowViewModel : ViewModel
{
  public LayerRowViewModel(string key) => Key = key;

  /// <summary>"e:" and the element's id, or "g:" and the group's.</summary>
  public readonly string Key;

  /// <summary>The element it stands for; null for a group's card.</summary>
  public string Id;

  /// <summary>Its group: the group a group card is for, or the one an element is shown in.</summary>
  public string Group;

  /// <summary>A whole group's card.</summary>
  public bool Header;

  /// <summary>An element shown inside an open group, set in under it.</summary>
  public bool Child;

  public string Name = "";

  /// <summary>What it is, under its name: "Text", "Key viewer", a mod's name, or for a group how many elements it holds.</summary>
  public string What = "";

  /// <summary>How many elements a group card holds.</summary>
  public string Count = "";

  /// <summary>Whether a group card shows its elements under it.</summary>
  public bool Open;

  /// <summary>Picked on the canvas.</summary>
  public bool Selected;

  /// <summary>The group's color: the stripe on its card and beside its elements.</summary>
  public UiColor Rail;

  /// <summary>A carried card would land just above this one.</summary>
  public bool InsertAbove;

  /// <summary>That card would join this one's group.</summary>
  public bool InsertInside;

  /// <summary>The elements it stands for, back to front.</summary>
  public IReadOnlyList<string> Ids = Array.Empty<string>();
}
