using System;
using System.Collections.Generic;
using Element.Configuration.Overlay;
using Element.Domain.Layout;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Parts.Grow;

/// <summary>How a point of the grow picker shows: a faint dot, the chosen point, or an arrow away from it.</summary>
internal enum GrowLook : byte
{
  Dot,
  Chosen,
  Arrow,
}
