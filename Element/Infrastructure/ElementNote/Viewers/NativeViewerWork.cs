using System;
using System.Linq;
using AdofaiIpc.Contracts;
using Element.Domain.DmNote;
using Newtonsoft.Json;

namespace Element.Infrastructure.ElementNote.Viewers;

/// <summary>What the main thread must do to the native host after IPC traffic.</summary>
internal readonly struct NativeViewerWork
{
  public readonly NativeViewerSurface[] Surfaces;
  public readonly bool ResetHost;
  public readonly bool HideHost;

  public NativeViewerWork(NativeViewerSurface[] surfaces, bool resetHost, bool hideHost)
  {
    Surfaces = surfaces;
    ResetHost = resetHost;
    HideHost = hideHost;
  }
}
