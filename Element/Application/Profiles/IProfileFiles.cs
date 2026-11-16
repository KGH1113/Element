using System;
using Element.Configuration.Profiles;

namespace Element.Application.Profiles;

/// <summary>Profile files where the player picked them.</summary>
internal interface IProfileFiles
{
  ProfileFileResult Read(string path);

  /// <summary>
  /// Writes <paramref name="file"/> to <paramref name="path"/>; a failed write
  /// leaves an existing file whole. Returns null on success.
  /// </summary>
  Exception Write(string path, ProfileFile file);
}
