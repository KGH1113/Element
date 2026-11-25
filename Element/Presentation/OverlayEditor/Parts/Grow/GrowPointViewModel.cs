using System;
using System.Collections.Generic;
using Element.Configuration.Overlay;
using Element.Domain.Layout;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Parts.Grow;

/// <summary>One of the nine points: how it shows, and picking it.</summary>
internal sealed class GrowPointViewModel : ViewModel
{
  public GrowPointViewModel(OverlayGrowth growth, Action<OverlayGrowth> picked)
  {
    Growth = growth;
    Name = "Grow from " + growth;
    Pick = Command(() => picked(growth));
  }

  public readonly OverlayGrowth Growth;

  /// <summary>Names the drawn button, for the checks.</summary>
  public readonly string Name;

  public readonly State<GrowLook> Look = new(GrowLook.Dot);

  public readonly State<UiColor> DotColor = new(Colors.Muted);

  /// <summary>Which way its arrow points: right and down positive, as on screen.</summary>
  public readonly State<Vector2> Direction = new(Vector2.right);

  public Command Pick { get; }
}
