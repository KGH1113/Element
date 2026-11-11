using System;
using System.Collections.Generic;
using ElementKit;
using UnityEngine;

namespace Element.Presentation.Layout.ContentEditors.Mods;

/// <summary>A color part asking the editor to open the picker beside it.</summary>
internal sealed class ModColorRequest
{
  public ModColorRequest(string title, Color color, bool opacity, RectTransform anchor, Action<Color> changed)
  {
    Title = title;
    Color = color;
    Opacity = opacity;
    Anchor = anchor;
    Changed = changed;
  }

  public string Title { get; }
  public Color Color { get; }
  public bool Opacity { get; }
  public RectTransform Anchor { get; }
  public Action<Color> Changed { get; }
}

/// <summary>A paint part asking for the full picker: highlight, custom color or gradient.</summary>
internal sealed class ModPaintRequest
{
  public ModPaintRequest(string title, Paint paint, RectTransform anchor, Action<Paint> changed)
  {
    Title = title;
    Paint = paint;
    Anchor = anchor;
    Changed = changed;
  }

  public string Title { get; }
  public Paint Paint { get; }
  public RectTransform Anchor { get; }
  public Action<Paint> Changed { get; }
}

/// <summary>A dropdown asking for its list of options under it.</summary>
internal sealed class ModListRequest
{
  public ModListRequest(
    string title,
    IReadOnlyList<string> options,
    int selected,
    RectTransform anchor,
    Action<int> picked
  )
  {
    Title = title;
    Options = options;
    Selected = selected;
    Anchor = anchor;
    Picked = picked;
  }

  public string Title { get; }
  public IReadOnlyList<string> Options { get; }
  public int Selected { get; }
  public RectTransform Anchor { get; }
  public Action<int> Picked { get; }
}

/// <summary>A font part asking for Element's font list beside it.</summary>
internal sealed class ModFontRequest
{
  public ModFontRequest(string title, string font, RectTransform anchor, Action<string> picked)
  {
    Title = title;
    Font = font;
    Anchor = anchor;
    Picked = picked;
  }

  public string Title { get; }
  public string Font { get; }
  public RectTransform Anchor { get; }
  public Action<string> Picked { get; }
}
