using System;
using System.Threading.Tasks;

namespace Element.Application.Profiles;

/// <summary>
/// ElementNote as profiles see it: it shows one profile's key viewers, says whose, and saves
/// or swaps every key viewer at once when asked. It does the work in its own process.
/// </summary>
internal interface IElementNoteKeyViewers
{
  /// <summary>An ElementNote that keeps profiles' key viewers is connected.</summary>
  bool Connected { get; }

  /// <summary>The profile whose key viewers ElementNote shows; null when it hasn't said.</summary>
  string Owner { get; }

  /// <summary>Main thread: ElementNote connected, left, or now shows another profile's key viewers.</summary>
  event Action Changed;

  /// <summary>Saves the key viewers on screen to <paramref name="path"/>. False when ElementNote didn't.</summary>
  Task<bool> SaveAsync(string path);

  /// <summary>
  /// Saves the key viewers on screen to <paramref name="savePath"/> unless it is null, then
  /// shows <paramref name="load"/> as <paramref name="owner"/>'s. ElementNote refuses unless
  /// it shows <paramref name="expectedOwner"/>'s, the owner the swap was planned on, and says
  /// whose it shows instead. False when ElementNote didn't swap.
  /// </summary>
  Task<bool> SwapAsync(string savePath, KeyViewersSource load, string owner, string expectedOwner);
}
