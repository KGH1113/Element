using System;
using System.Collections.Generic;
using Element.Application.Fonts;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Fonts;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Element.Presentation.Overlay.Fonts;

internal enum OverlayFontStatus
{
  // The bundled font, or a run that follows the default.
  Default,
  Ready,

  // Installed fonts are still being listed.
  Loading,

  // Not on this PC: drawn with the default font until it is installed or imported.
  Missing,
}
