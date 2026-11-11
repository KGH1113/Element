using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Element.Domain.DmNote;
using Element.Domain.Layout;
using UnityEngine;

namespace Element.Infrastructure.ElementNote.Viewers;

internal sealed class NativeViewerHostException : Exception
{
  public NativeViewerStatus Status { get; }

  public NativeViewerHostException(NativeViewerStatus status, string message)
    : base(message) => Status = status;
}
