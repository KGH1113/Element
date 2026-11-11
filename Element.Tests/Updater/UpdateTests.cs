using Element.Tests.Launcher;
using Element.Tests.Support;

namespace Element.Tests.Updater;

/// <summary>
/// Automatic updates, one file per part: the updater against a local release
/// server, and the launcher's version bookkeeping.
/// </summary>
internal static class UpdateTests
{
  [Suite("auto update: versions, verified downloads, background finish, rollback and launcher switch")]
  public static void Run()
  {
    UpdateManagerTests.Run();
    RuntimeStoreTests.Run();
  }
}
