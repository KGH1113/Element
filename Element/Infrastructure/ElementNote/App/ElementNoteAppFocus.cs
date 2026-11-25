using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Element.Infrastructure.ElementNote.App;

internal static class ElementNoteAppFocus
{
  /// <summary>Null where the mod has no ElementNote (Linux).</summary>
  public static IElementNoteAppFocus ForCurrentPlatform() =>
    UnityEngine.Application.platform switch
    {
      RuntimePlatform.OSXPlayer or RuntimePlatform.OSXEditor => new MacElementNoteAppFocus(),
      RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor => new WindowsElementNoteAppFocus(),
      _ => null,
    };
}
