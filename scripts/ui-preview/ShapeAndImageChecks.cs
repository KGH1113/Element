using ElementUI;
using ElementUI.Ugui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Editor
{
  internal static class ShapeAndImageChecks
  {
    [PageCheck("_tweaks")]
    private static int NativeConfiguration(SettingsCheck check)
    {
      int count = 0;
      void Assert(bool condition, string message)
      {
        check.Assert(condition, message);
        count++;
      }

      GameObject holder = new GameObject("Shape and image configuration host", typeof(RectTransform));
      Texture2D texture = new Texture2D(32, 16);
      try
      {
        RectTransform rect = (RectTransform)holder.transform;
        rect.sizeDelta = new Vector2(240, 180);
        State<string> value = new State<string>("before");
        TMP_InputField clippedInput = null, shapedInput = null;
        RawImage image = null;
        Graphic graphic = null;
        UiHost.Mount(rect, new UguiBackend(new UguiTheme()),
          UI.VStack(4,
            UI.TextField(null, value.ToBinding())
              .ClipShape(Shapes.Capsule)
              .Configure<TMP_InputField>(input => clippedInput = input),
            UI.TextField(null, value.ToBinding())
              .ContentShape(Shapes.RoundedRectangle(6))
              .Configure<TMP_InputField>(input => shapedInput = input),
            UI.Image(texture)
              .Configure<RawImage>(native => image = native)
              .Configure<Graphic>(native => graphic = native)
              .Frame(width: 80, height: 80)));

        Assert(clippedInput != null && shapedInput != null && clippedInput != shapedInput
          && clippedInput.transform.IsChildOf(rect) && shapedInput.transform.IsChildOf(rect),
          "Configure reaches each native text field through both clipping and content shapes");
        Mask clip = clippedInput.transform.parent.GetComponent<Mask>();
        Assert(clip != null && clip.enabled && !clip.showMaskGraphic
          && shapedInput.transform.parent.GetComponent<Mask>() == null,
          "clipping installs a hidden stencil while a content shape only changes hit testing");
        value.Value = "after";
        ElementUI.Runtime.Flush();
        Assert(clippedInput.text == "after" && shapedInput.text == "after",
          "configured fields retain their bindings through shape wrappers");
        Assert(image != null && image == graphic && image.texture == texture
          && image.enabled && !image.raycastTarget && image.transform.IsChildOf(rect),
          "Configure resolves RawImage and Graphic to the same visible child image");
        Assert(Mathf.Abs(image.rectTransform.rect.width - 80) < .01f
          && Mathf.Abs(image.rectTransform.rect.height - 40) < .01f,
          "the configured texture retains aspect fit inside its 80-point frame");
      }
      finally
      {
        RectPeer.DestroyObject(holder);
        RectPeer.DestroyObject(texture);
      }
      return count;
    }
  }
}
