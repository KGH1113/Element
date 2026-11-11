using System;
using Element.Configuration;
using Element.Presentation.Ui.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Settings.Pages;

/// <summary>
/// One page of the settings window. It builds its controls, lays them out
/// for the window's width and answers the window's calls; the window shows
/// one page at a time, and Back goes to <see cref="Parent"/>.
/// </summary>
internal abstract class SettingsPage : IDisposable
{
  protected SettingsPage(ISettingsWindow window, string name)
  {
    Window = window;
    Ui = window.Ui;
    Settings = window.Settings;
    Root = Ui.Rect(name, window.Viewport, 0, 0, 936, 480);
  }

  protected ISettingsWindow Window { get; }
  protected SettingsUiFactory Ui { get; }
  protected ElementSettings Settings { get; }

  /// <summary>Holds the page's controls; the window scrolls it.</summary>
  public RectTransform Root { get; }

  /// <summary>The page Back returns to; none for the first page.</summary>
  public SettingsPage Parent { get; private set; }

  /// <summary>The control on <see cref="Parent"/> that Back selects, if any.</summary>
  public Selectable ReturnFocus { get; private set; }

  /// <summary>Where the page was scrolled when it was left; 1 is the top.</summary>
  public float ScrollPosition { get; set; } = 1;

  public abstract string Title { get; }

  /// <summary>Places the controls <paramref name="width"/> wide in a view <paramref name="height"/> tall, and sizes <see cref="Root"/>.</summary>
  public abstract void Layout(float width, float height);

  /// <summary>The window opened; it shows its first page next.</summary>
  public virtual void Opened() { }

  /// <summary>The page became the one shown.</summary>
  public virtual void Shown() { }

  /// <summary>Every frame while the page is shown.</summary>
  public virtual void Tick() { }

  /// <summary>Every frame. While the page records keys it takes them and returns true, and the window does nothing else.</summary>
  public virtual bool TickCapture() => false;

  public virtual void CancelCapture() { }

  /// <summary>Escape closes something on the page; false leaves Escape to Back.</summary>
  public virtual bool HandleEscape() => false;

  /// <summary>Keeps an open popover's edit; false while it can't be kept, and the popover stays.</summary>
  public virtual bool CommitPopovers() => true;

  public virtual void HidePopovers() { }

  /// <summary>Where Tab moves focus while a popover holds it; null for the whole window.</summary>
  public virtual GameObject FocusScope => null;

  /// <summary>Applies the theme to controls the page builds with its own factories.</summary>
  public virtual void RefreshTheme() { }

  /// <summary>Another profile's values replaced the settings in place.</summary>
  public virtual void SettingsReplaced() { }

  public virtual void Dispose() { }

  /// <summary>Makes <paramref name="child"/> a page under this one; Back from it selects <paramref name="returnFocus"/>.</summary>
  protected void Adopt(SettingsPage child, Selectable returnFocus = null)
  {
    child.Parent = this;
    child.ReturnFocus = returnFocus;
  }

  protected Button DetailLink(Transform parent, string title, string description, Action action) =>
    DetailLink(parent, title, description, action, out _);

  /// <summary>A link row whose <paramref name="detail"/> the caller keeps current.</summary>
  protected Button DetailLink(
    Transform parent,
    string title,
    string description,
    Action action,
    out TextMeshProUGUI detail
  )
  {
    Button link = Ui.Button(title, parent, 0, 0, 936, 80, action);
    link.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
    var label = Ui.Text(title, link.transform, 20, 13, 820, 26, 18);
    SettingsUiFactory.StretchWidth(label.rectTransform, 20, 64);
    detail = Ui.Text(description, link.transform, 20, 43, 820, 24, 14, UiPalette.Muted);
    SettingsUiFactory.StretchWidth(detail.rectTransform, 20, 64);
    var arrow = Ui.Rect("Open", link.transform, 892, 28, 20, 24).gameObject.AddComponent<SettingsNavigationIcon>();
    arrow.Kind = SettingsNavigationIconKind.Forward;
    arrow.raycastTarget = false;
    arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = new Vector2(1, .5f);
    arrow.rectTransform.pivot = new Vector2(1, .5f);
    arrow.rectTransform.anchoredPosition = new Vector2(-20, 0);
    Ui.Bind(() => arrow.color = Ui.SoftAccent);
    return link;
  }

  /// <summary>Section tabs across the page; the first starts chosen.</summary>
  protected SettingsSegmentedControl Tabs(Transform parent, string[] names, Action<int> changed)
  {
    var tabs = new SettingsSegmentedControl(
      Ui,
      parent,
      0,
      0,
      936,
      44,
      names,
      index =>
      {
        changed(index);
        Ui.RefreshTheme();
      },
      style: SettingsSegmentStyle.Tabs
    );
    tabs.Selected = 0;
    return tabs;
  }
}
