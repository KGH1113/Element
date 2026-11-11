using Element.Tests.Support;

namespace Element.Tests.Application.Profiles;

/// <summary>Profiles, one file per part: switching and keeping values, and profile files.</summary>
internal static class ProfileTests
{
  [Suite("profiles switch in place, survive saving, export, import, refuse bad files and back up")]
  public static void Run()
  {
    ProfileSwitchTests.Run();
    ProfileFileTests.Run();
  }
}
