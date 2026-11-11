using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Element.Application.Fonts;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Infrastructure.Platform;
using TMPro;
using UnityEngine.TextCore.LowLevel;

namespace Element.Infrastructure.Storage;

/// <summary>
/// How overlay font atlases are sampled. (padding + 1) / point size matches the
/// bundled assets, so outline and underlay offsets look the same in every font.
/// </summary>
internal static class OverlayFontSampling
{
  public const int PointSize = 48;
  public const int Padding = 3;
  public const int AtlasSize = 1024;
}
