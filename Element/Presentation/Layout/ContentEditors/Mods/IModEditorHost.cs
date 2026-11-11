using System;
using System.Collections.Generic;
using Element.Presentation.Overlay.Shared;
using ElementKit;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Element.Presentation.Layout.ContentEditors.Mods;

/// <summary>What a mod's editing screen asks of the editor around it.</summary>
internal interface IModEditorHost
{
  /// <summary>
  /// Runs a part's change as one edit: undo, then the mod's callback, then the
  /// preview; <paramref name="rebuild"/> makes the parts again to show it.
  /// </summary>
  void Change(object key, Action apply, bool rebuild);

  void OpenColor(ModColorRequest request);

  void OpenPaint(ModPaintRequest request);

  void OpenList(ModListRequest request);

  void OpenFont(ModFontRequest request);

  void ShowTip(string text, RectTransform anchor);

  void HideTip();

  /// <summary>The tab last picked in the tabs named <paramref name="key"/>, so undo keeps it.</summary>
  int TabOf(string key);

  void SetTab(string key, int index);

  OverlayFonts Fonts { get; }
}
