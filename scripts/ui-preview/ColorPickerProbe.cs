using System.Linq;
using Element.Presentation.Ui.ColorPicker;
using ElementUI.Ugui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Editor
{
  /// <summary>
  /// A color picker as the checks see it: its form is an ElementUI screen,
  /// its square and bars are drawn by hand; each is found by the name the
  /// picker gives it, under what the picker is drawn in (its panel, or the
  /// popover that holds it). Where a part shows twice (in the branches for a
  /// color of its own and for following the highlight), the shown one wins.
  /// </summary>
  internal static class ColorPickerProbe
  {
    public static TMP_InputField Hex(Transform root) => Part<TMP_InputField>(root, "HEX color");

    public static TMP_InputField Opacity(Transform root) => Part<TMP_InputField>(root, "Opacity percentage");

    public static Slider Hue(Transform root) => Part<Slider>(root, "Hue");

    public static Slider Alpha(Transform root) => Part<Slider>(root, "Opacity");

    public static UiColorPlane Plane(Transform root) => Part<UiColorPlane>(root, "Saturation and brightness");

    public static TextMeshProUGUI Title(Transform root) => Part<TextMeshProUGUI>(root, "Color picker title");

    public static TextMeshProUGUI Error(Transform root) => Part<TextMeshProUGUI>(root, "Color picker error");

    public static Pressable Close(Transform root) => Part<Pressable>(root, "Close color picker");

    public static Pressable Default(Transform root) => Part<Pressable>(root, "Default color");

    /// <summary>Highlight/Custom's segment <paramref name="index"/>: 0 follows the highlight, 1 is custom.</summary>
    public static Pressable Segment(Transform root, int index)
    {
      // ElementUI's segmented control can't be named; the picker has only this one.
      Transform control = Part<Transform>(root, "Segmented");
      return control == null ? null : control.GetComponentsInChildren<Pressable>(true).Where(option => option.transform.parent == control).ElementAtOrDefault(index);
    }

    public static bool Shown(Component part) => part != null && part.gameObject.activeInHierarchy;

    private static T Part<T>(Transform root, string name) where T : Component =>
      root == null ? null : root.GetComponentsInChildren<T>(true).Where(part => part.name == name).OrderByDescending(Shown).FirstOrDefault();
  }
}
