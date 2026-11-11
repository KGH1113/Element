using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Element.Infrastructure.Platform;

internal static class FontFilePickers
{
  public static readonly string[] Extensions = { "ttf", "otf", "ttc", "otc" };

  /// <summary>Null where the mod has no dialog (Linux).</summary>
  public static IFontFilePicker ForPlatform() =>
    UnityEngine.Application.platform switch
    {
      RuntimePlatform.OSXPlayer or RuntimePlatform.OSXEditor => new MacFontFilePicker(),
      RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor => new WindowsFontFilePicker(),
      _ => null,
    };
}
