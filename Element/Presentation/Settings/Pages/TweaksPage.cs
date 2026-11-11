using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Settings.Pages;

/// <summary>Tweaks: links to the editor, judgment, planet and chatter pages, then record mode and the trackpad.</summary>
internal sealed class TweaksPage : SettingsPage
{
  private readonly List<Button> _tweakLinks = new();
  private readonly List<Button> _tweakRows = new();

  public TweaksPage(
    ISettingsWindow window,
    EditorTweaksPage editor,
    JudgmentPage judgment,
    PlanetsPage planets,
    ChatterPage chatter
  )
    : base(window, "Tweaks")
  {
    Link(
      editor,
      L("에디터", "Editor tweaks"),
      L("선택 정보, 정밀 회전, 이벤트 좌표", "Selection info, fine rotation and event positions")
    );
    Link(
      judgment,
      L("판정 표시", "Judgment visuals"),
      L("어떤 판정 문구를 보여줄지 선택합니다.", "Choose which judgment popups appear.")
    );
    Link(
      planets,
      L("행성", "Planet appearance"),
      L("공, 꼬리, 고리의 색상과 불투명도", "Body, trail and ring color & opacity")
    );
    Link(
      chatter,
      L("채터 블로커", "Chatter blocker"),
      L(
        "스위치가 튀어 한 번 누른 키가 두 번 입력되는 것을 막습니다.",
        "Stops a bouncing switch from counting one press twice."
      )
    );
    _tweakRows.Add(
      Ui.Switch(
        L("녹화 모드", "Record mode"),
        L("게임 HUD와 모드 오버레이를 숨깁니다.", "Hide the game HUD and mod overlays."),
        Root,
        100,
        () => Settings.RecordMode,
        v =>
        {
          Settings.RecordMode = v;
          Window.Changed();
        }
      )
    );
    if (
      UnityEngine.Application.platform == RuntimePlatform.OSXPlayer
      || UnityEngine.Application.platform == RuntimePlatform.OSXEditor
    )
      _tweakRows.Add(
        Ui.Switch(
          L("macOS 트랙패드", "macOS Trackpad"),
          L(
            "에디터에서 두 손가락으로 이동하고 핀치로 확대 · 축소합니다.",
            "In the editor, pan with two fingers and pinch to zoom."
          ),
          Root,
          200,
          () => Settings.NativeEditorTrackpad,
          v =>
          {
            Settings.NativeEditorTrackpad = v;
            Window.Changed();
          }
        )
      );
  }

  public override string Title => "Tweaks";

  private void Link(SettingsPage page, string title, string description)
  {
    _tweakLinks.Add(DetailLink(Root, title, description, () => Window.OpenPage(page)));
    Adopt(page);
  }

  public override void Layout(float width, float height)
  {
    float linkHeight = width < 520 ? 100 : 80;
    for (int i = 0; i < _tweakLinks.Count; i++)
    {
      var link = _tweakLinks[i];
      SettingsUiFactory.Place((RectTransform)link.transform, 0, i * (linkHeight + 12), width, linkHeight);
      var labels = link.GetComponentsInChildren<TextMeshProUGUI>();
      // The hidden button caption is excluded from this active-only query.
      SettingsUiFactory.Place(labels[1].rectTransform, 20, 43, width - 84, linkHeight - 51);
    }
    float tweaksTop = _tweakLinks.Count * (linkHeight + 12);
    bool wide = width >= 760;
    float toggleWidth = wide ? (width - 12) / 2 : width;
    for (int i = 0; i < _tweakRows.Count; i++)
      SettingsUiFactory.Place(
        (RectTransform)_tweakRows[i].transform,
        wide ? i * (toggleWidth + 12) : 0,
        tweaksTop + (wide ? 0 : i * 100),
        toggleWidth,
        88
      );
    Root.sizeDelta = new Vector2(width, tweaksTop + (wide ? 88 : _tweakRows.Count * 100 - 12));
  }
}
