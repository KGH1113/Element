using System;
using Element.Configuration.Profiles;
using Element.Presentation.Ui.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Settings.Pages;

/// <summary>The small panel under New profile: a name, Create, and why a name can't be used.</summary>
internal sealed class NewProfilePopover
{
  private const float NewProfileWidth = 340,
    NewProfilePadding = 14;

  private readonly RectTransform _window;
  private readonly SettingsPopoverShade _shade;
  private readonly RectTransform _newProfilePopover;
  private readonly TextMeshProUGUI _newProfileHint,
    _newProfileError;
  private readonly TMP_InputField _newProfileName;
  private readonly Button _createProfile;

  public NewProfilePopover(
    SettingsUiFactory ui,
    RectTransform window,
    SettingsPopoverShade shade,
    Action nameChanged,
    Action create
  )
  {
    _window = window;
    _shade = shade;
    Image panel = ui.Panel("New profile", window, 0, 0, NewProfileWidth, 120, new Color(.105f, .105f, .105f));
    panel.raycastTarget = true;
    _newProfilePopover = panel.rectTransform;
    float inner = NewProfileWidth - NewProfilePadding * 2;
    _newProfileHint = ui.Text(
      L("처음 설정으로 시작해요.", "Starts with the initial settings."),
      _newProfilePopover,
      NewProfilePadding,
      NewProfilePadding,
      inner,
      20,
      13,
      UiPalette.Muted
    );
    _newProfileName = ui.Input(
      "Profile name",
      "",
      _newProfilePopover,
      NewProfilePadding,
      42,
      inner - 96,
      _ => nameChanged(),
      null,
      ElementProfile.MaximumNameLength
    );
    _newProfileName.textComponent.textWrappingMode = TextWrappingModes.NoWrap;
    TextMeshProUGUI placeholder = ui.Text(
      L("프로필 이름", "Profile name"),
      _newProfileName.textViewport,
      0,
      0,
      300,
      32,
      16,
      UiPalette.Muted
    );
    SettingsUiFactory.Stretch(placeholder.rectTransform, 0, 0, 0, 0);
    _newProfileName.placeholder = placeholder;
    placeholder.alignment = _newProfileName.textComponent.alignment;
    _newProfileName.onSubmit.AddListener(_ => create());
    _createProfile = ui.Button(
      L("만들기", "Create"),
      _newProfilePopover,
      NewProfileWidth - NewProfilePadding - 88,
      42,
      88,
      40,
      create,
      true
    );
    _newProfileError = ui.Text("", _newProfilePopover, NewProfilePadding, 90, inner, 20, 13, UiPalette.Error);
    _newProfileError.alignment = TextAlignmentOptions.TopLeft;
    _newProfilePopover.gameObject.SetActive(false);
  }

  public bool Visible => _newProfilePopover.gameObject.activeSelf;

  public TMP_InputField Name => _newProfileName;

  public bool CanCreate
  {
    set => _createProfile.interactable = value;
  }

  /// <summary>Why the name can't be used, or empty.</summary>
  public string Problem
  {
    set => _newProfileError.text = value;
  }

  /// <summary>Opens empty over the shade; place it with <see cref="Layout"/>.</summary>
  public void Show()
  {
    _newProfileName.SetTextWithoutNotify("");
    _newProfileError.text = "";
    _createProfile.interactable = false;
    _shade.Show();
    _newProfilePopover.gameObject.SetActive(true);
  }

  public void Hide()
  {
    _newProfileName.DeactivateInputField();
    _newProfilePopover.gameObject.SetActive(false);
  }

  /// <summary>Under <paramref name="anchor"/>, its right edge on the anchor's, kept inside the window.</summary>
  public void Layout(RectTransform anchor)
  {
    if (!Visible)
      return;
    _shade.Layout();
    float width = Mathf.Min(NewProfileWidth, _window.rect.width - 20);
    float inner = width - NewProfilePadding * 2;
    float hintHeight = SettingsUiFactory.TextHeight(_newProfileHint, inner, 20);
    SettingsUiFactory.Place(_newProfileHint.rectTransform, NewProfilePadding, NewProfilePadding, inner, hintHeight);
    float y = NewProfilePadding + hintHeight + 8;
    SettingsUiFactory.Place((RectTransform)_newProfileName.transform, NewProfilePadding, y, inner - 96, 40);
    SettingsUiFactory.Place((RectTransform)_createProfile.transform, width - NewProfilePadding - 88, y, 88, 40);
    y += 40;
    bool hasError = _newProfileError.text.Length > 0;
    _newProfileError.gameObject.SetActive(hasError);
    if (hasError)
    {
      float errorHeight = SettingsUiFactory.TextHeight(_newProfileError, inner, 20);
      SettingsUiFactory.Place(_newProfileError.rectTransform, NewProfilePadding, y + 8, inner, errorHeight);
      y += 8 + errorHeight;
    }
    float height = y + NewProfilePadding;
    Vector3 local = _window.InverseTransformPoint(anchor.position);
    float x = Mathf.Clamp(local.x - _window.rect.xMin + anchor.rect.width - width, 10, _window.rect.width - width - 10);
    float top = Mathf.Clamp(
      _window.rect.yMax - local.y + anchor.rect.height + 8,
      10,
      _window.rect.height - height - 10
    );
    SettingsUiFactory.Place(_newProfilePopover, x, top, width, height);
    _newProfilePopover.SetAsLastSibling();
  }
}
