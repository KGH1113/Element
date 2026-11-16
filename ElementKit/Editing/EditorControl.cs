using UnityEngine;

namespace ElementKit;

/// <summary>A part on the editing screen.</summary>
public abstract class EditorControl
{
  // Only Element implements this: a mod uses the one Element hands it and
  // cannot subclass it, so new members never break mods.
  internal EditorControl() { }

  /// <summary>The part's UI object; its pivot is its top left.</summary>
  public abstract RectTransform Rect { get; }

  /// <summary>Takes the part out of the column and puts its top left at (<paramref name="x"/>, <paramref name="y"/>).</summary>
  public abstract EditorControl At(float x, float y);

  /// <summary>Makes the part <paramref name="width"/> wide.</summary>
  public abstract EditorControl Width(float width);

  /// <summary>Shows <paramref name="text"/> above the part while the pointer is over it.</summary>
  public abstract EditorControl Hint(string text);

  /// <summary>Whether the part shows. A hidden part keeps its place in the column.</summary>
  public abstract bool Visible { get; set; }
}
