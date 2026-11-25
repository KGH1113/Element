using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Element.Infrastructure.Platform;

/// <summary>The system's open dialog for font files. Blocks until it closes; main thread only.</summary>
internal interface IFontFilePicker
{
  /// <summary>The chosen files, or none when the dialog was cancelled.</summary>
  IReadOnlyList<string> Pick(string title);
}
