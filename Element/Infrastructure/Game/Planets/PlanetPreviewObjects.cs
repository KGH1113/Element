using UnityEngine;

namespace Element.Infrastructure.Game.Planets;

/// <summary>The preview's own objects: hidden, on a layer only its camera sees, and destroyed with it.</summary>
internal static class PlanetPreviewObjects
{
  public const int Layer = 31;

  public static GameObject Child(Transform parent, string name)
  {
    var child = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave, layer = Layer };
    child.transform.SetParent(parent, false);
    return child;
  }

  public static void Destroy(Object value)
  {
    if (value == null)
      return;
    if (UnityEngine.Application.isPlaying)
      Object.Destroy(value);
    else
      Object.DestroyImmediate(value);
  }
}
