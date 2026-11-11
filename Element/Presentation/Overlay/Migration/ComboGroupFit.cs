using System.Collections.Generic;
using Element.Configuration.Overlay;
using Element.Domain.Layout;
using Element.Presentation.Overlay.Text;
using UnityEngine;

namespace Element.Presentation.Overlay.Migration;

/// <summary>
/// A moved combo was one element; converted, its title and number are two
/// texts of one group. Once both have content, they are measured where the
/// built-in drew them and placed together as the old combo was placed.
/// </summary>
internal sealed class ComboGroupFit
{
  private readonly List<TextOverlayElement> _texts;
  private readonly List<TextOverlayElement> _members = new();
  private readonly List<Rect> _targets = new();

  /// <param name="texts">The canvas's texts, read each time a group is fitted.</param>
  public ComboGroupFit(List<TextOverlayElement> texts) => _texts = texts;

  /// <summary>Whether a group may be ready to place.</summary>
  public bool Pending { get; set; }

  /// <summary>Places the first group that is ready; one group per call.</summary>
  public void Fit()
  {
    Pending = false;
    foreach (TextOverlayElement first in _texts)
    {
      if (!first.PendingFit)
        continue;
      string group = first.Settings.PendingGroup;
      _members.Clear();
      foreach (TextOverlayElement text in _texts)
        if (text.PendingFit && text.Settings.PendingGroup == group)
          _members.Add(text);
      if (!TryUnion(out Rect union))
        continue;
      Place(first.Settings, union);
      Pending = true;
      return;
    }
  }

  private bool TryUnion(out Rect union)
  {
    union = default;
    for (int i = 0; i < _members.Count; i++)
    {
      if (!_members[i].Enabled || !_members[i].HasContent)
        return false;
      Rect bounds = _members[i].AnchorBounds;
      union =
        i == 0
          ? bounds
          : Rect.MinMaxRect(
            Mathf.Min(union.xMin, bounds.xMin),
            Mathf.Min(union.yMin, bounds.yMin),
            Mathf.Max(union.xMax, bounds.xMax),
            Mathf.Max(union.yMax, bounds.yMax)
          );
    }
    return true;
  }

  private void Place(OverlayLayoutSettings old, Rect union)
  {
    bool anchored = OverlayLayoutPosition.IsAnchored(old);
    float scale =
      OverlayLayoutGeometry.Finite(old.Scale) && old.Scale > 0
        ? (anchored ? old.Scale : Mathf.Clamp(old.Scale, .2f, 5f))
        : 1;
    Vector2 from,
      to;
    if (anchored)
    {
      LayoutBounds target = OverlayLayoutPosition.Resolve(
        old,
        union.width * scale,
        union.height * scale,
        Screen.width,
        Screen.height
      );
      from = union.min;
      to = new Vector2(target.X, target.Y);
    }
    else
    {
      float unit = OverlayLayoutPosition.ScreenScale(Screen.width, Screen.height);
      LegacyOverlayPlacement root = LegacyOverlayPlacement.ComboRoot;
      from = new Vector2(Screen.width * .5f, (-root.Position.y + root.Size.y * .5f) * unit);
      to = new Vector2(old.CenterX * Screen.width, old.CenterY * Screen.height);
    }
    _targets.Clear();
    foreach (TextOverlayElement member in _members)
    {
      Rect bounds = member.AnchorBounds;
      Vector2 center = to + (bounds.center - from) * scale;
      _targets.Add(new Rect(center - bounds.size * scale * .5f, bounds.size * scale));
    }
    for (int i = 0; i < _members.Count; i++)
    {
      _members[i].PlaceContent(_targets[i], _members[i].EffectiveScale * scale);
      _members[i].CommitLayout(true, initialize: true);
    }
  }
}
