using ElementUI;
using ElementUI.Ugui;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Editor
{
  internal static class PresentationChecks
  {
    [PageCheck("_tweaks")]
    private static int NativePresentation(SettingsCheck check)
    {
      int count = 0;
      void Assert(bool condition, string message)
      {
        check.Assert(condition, message);
        count++;
      }
      GameObject host = new GameObject("Presentation smoke host", typeof(RectTransform));
      try
      {
        using (ContainerPeer peer = new ContainerPeer(host.transform, "Presentation smoke"))
        {
          peer.SetOpacity(.5f);
          peer.SetAllowsHitTesting(false);
          CanvasGroup group = peer.GameObject.GetComponent<CanvasGroup>();
          Assert(group != null && group.alpha == .5f && !group.blocksRaycasts,
            "presentation creates its native group and applies opacity and input policy");
          peer.SetOpacity(.75f);
          Assert(peer.GameObject.GetComponents<CanvasGroup>().Length == 1 && group.alpha == .75f,
            "presentation reuses its native group");
          RectPeer.DestroyObject(group);
          peer.SetOpacity(.25f);
          Assert(peer.GameObject.GetComponent<CanvasGroup>().alpha == .25f,
            "presentation replaces a destroyed Unity group using native null semantics");

          bool hovered = false;
          peer.Hovered = inside => hovered = inside;
          peer.GameObject.GetComponent<PointerArea>().OnPointerEnter(null);
          Image target = peer.GameObject.GetComponent<Image>();
          Assert(hovered && target != null && target.raycastTarget && target.color.a == 0,
            "hover creates a transparent native hit target");

          int appeared = 0, disappeared = 0;
          peer.VisibilityChanged = visible => { if (visible) appeared++; else disappeared++; };
          host.SetActive(false);
          host.SetActive(true);
          Assert(appeared == 2 && disappeared == 1,
            "native ancestor activation reports appearance and disappearance exactly once: " + appeared + "/" + disappeared);

          peer.SetFrame(new Frame(10, 20, 80, 40));
          peer.SetTransform(5, 6, .5f, 90, Alignment.Center);
          Assert(peer.Rect.anchoredPosition == new Vector2(55, -46)
            && peer.Rect.localScale == new Vector3(.5f, .5f, 1)
            && Quaternion.Angle(peer.Rect.localRotation, Quaternion.Euler(0, 0, -90)) < .01f,
            "presentation maps top-left coordinates and clockwise rotation to native geometry");
          peer.VisibilityChanged = null;
        }
      }
      finally
      {
        RectPeer.DestroyObject(host);
      }
      return count;
    }
  }
}
