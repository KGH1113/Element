using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Element.Infrastructure.ElementNote.App;

internal static class DmNoteAppFocus
{
  /// <summary>Null where the mod has no ElementNote (Linux).</summary>
  public static IDmNoteAppFocus ForCurrentPlatform() =>
    UnityEngine.Application.platform switch
    {
      RuntimePlatform.OSXPlayer or RuntimePlatform.OSXEditor => new MacDmNoteAppFocus(),
      RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor => new WindowsDmNoteAppFocus(),
      _ => null,
    };
}
