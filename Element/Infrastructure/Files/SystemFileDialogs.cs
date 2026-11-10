using Element.Application.Files;
using UnityEngine;

namespace Element.Infrastructure.Files;

internal static class SystemFileDialogs
{
  /// <summary>This platform's file windows, or null where the mod has none (Linux).</summary>
  public static IFileDialogs Create(string runtimePath) =>
    UnityEngine.Application.platform switch
    {
      RuntimePlatform.OSXPlayer or RuntimePlatform.OSXEditor => new MacFileDialogs(runtimePath),
      RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor => new WindowsFileDialogs(),
      _ => null,
    };
}
