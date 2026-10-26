using System.IO;
using ImplResourcePack.Domain.DmNote;
using UnityEngine;

namespace ImplResourcePack.Infrastructure.Game.DmNote;

/// <summary>Chooses the native viewer library for the running player.</summary>
internal static class NativeViewerPlatform
{
  public const string MacLibrary = "libImplGameWebView.dylib";
  public const string WindowsLibrary = "ImplGameWebView.dll";

  /// <summary>Players with a native viewer library. Others only show the offline sample.</summary>
  public static bool IsSupported =>
    UnityEngine.Application.platform == RuntimePlatform.OSXPlayer
    || UnityEngine.Application.platform == RuntimePlatform.WindowsPlayer;

  public static INativeViewerHost CreateHost(string modPath) =>
    UnityEngine.Application.platform switch
    {
      RuntimePlatform.OSXPlayer or RuntimePlatform.OSXEditor => new GameWebView(
        MacNativeLibrary.Load(Path.Combine(modPath, "native", MacLibrary))
      ),
      RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor => new GameWebView(
        WindowsNativeLibrary.Load(Path.Combine(modPath, "native", WindowsLibrary))
      ),
      _ => throw new NativeViewerHostException(
        NativeViewerStatus.UnsupportedPlatform,
        "Native viewers are available only on macOS and Windows."
      ),
    };
}
