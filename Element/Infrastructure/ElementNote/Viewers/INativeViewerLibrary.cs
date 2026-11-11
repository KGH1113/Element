using System;
using System.IO;
using System.Runtime.InteropServices;
using Element.Domain.DmNote;

namespace Element.Infrastructure.ElementNote.Viewers;

/// <summary>Resolves exports from a loaded native viewer library.</summary>
internal interface INativeViewerLibrary
{
  IntPtr Symbol(string name);
}
