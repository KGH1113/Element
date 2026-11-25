using System;
using System.Collections.Generic;
using Element.Application.Fonts;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Fonts;
using Element.Presentation.Overlay.Fonts;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Ui.Popovers;

/// <summary>
/// Picks a font for overlay texts: the default first, then fonts added to the
/// Fonts folder, then the fonts installed on this PC. A click applies the font
/// at once so the text shows it; the list stays open to try another. Only the
/// rows in view exist, so a PC with a thousand fonts scrolls as lightly as one
/// with ten.
/// </summary>
internal sealed class UiFontPopover : IPopover, IDisposable
{
  public const float PreferredWidth = 360,
    PreferredHeight = 440,
    MinimumHeight = 240;

  private const float Padding = 14,
    HeaderHeight = 32,
    SearchHeight = 40,
    RowHeight = 34,
    StatusHeight = 34,
    Gap = 10,
    TrackWidth = 4;

  private enum EntryKind
  {
    Default,
    Header,
    Font,
  }

  private readonly struct Entry
  {
    public Entry(EntryKind kind, string key, string name, string detail, bool warning = false)
    {
      Kind = kind;
      Key = key;
      Name = name;
      Detail = detail;
      Warning = warning;
    }

    public readonly EntryKind Kind;
    public readonly string Key;
    public readonly string Name;
    public readonly string Detail;
    public readonly bool Warning;
  }

  private sealed class Row
  {
    public Button Button;
    public UiCapsuleGraphic Selected;
    public TextMeshProUGUI Name;
    public TextMeshProUGUI Detail;
    public int Index = -1;
  }

  private readonly UiFactory _ui;
  private readonly OverlayFonts _fonts;
  private readonly IOverlayFontLibrary _library;
  private readonly bool _reduceMotion;
  private readonly Action<string> _picked;
  private readonly TextMeshProUGUI _title;
  private readonly Button _add;
  private readonly Button _folder;
  private readonly TMP_InputField _search;
  private readonly RectTransform _viewport;
  private readonly RectTransform _track;
  private readonly UiCapsuleGraphic _thumb;
  private readonly TextMeshProUGUI _status;
  private readonly List<Row> _rows = new();
  private readonly List<Entry> _entries = new();
  private float _width = PreferredWidth,
    _height = PreferredHeight;
  private float _scroll;
  private float _statusHeight = StatusHeight;
  private string _current;
  private string _defaultName;
  private string _defaultDetail;
  private string _message;
  private bool _messageWarning;
  private int _libraryRevision = -1;
  private int _fontVersion = -1;
  private bool _installedReady;
  private readonly float _inset;
  private bool _dragging;
  private bool _scrollPending;
  private float _grab;

  /// <param name="bare">It stands in an editor's column: no panel of its own, its parts on the column's edges.</param>
  public UiFontPopover(
    UiFactory ui,
    Transform parent,
    OverlayFonts fonts,
    bool reduceMotion,
    Action<string> picked,
    bool bare = false
  )
  {
    _inset = bare ? 0 : Padding;
    _ui = ui;
    _fonts = fonts;
    _library = fonts?.Library;
    _reduceMotion = reduceMotion;
    _picked = picked;
    Image panel = ui.Panel("Font popover", parent, 0, 0, _width, _height, bare ? Color.clear : UiPalette.Popover);
    panel.raycastTarget = true;
    Root = panel.rectTransform;
    _title = ui.Text("", Root, _inset, _inset, 10, HeaderHeight, 13, UiPalette.Muted);
    _title.textWrappingMode = TextWrappingModes.NoWrap;
    bool pick = _library?.CanPickFiles ?? false;
    _add = UiCapsules.CapsuleButton(
      ui,
      Root,
      "Add font file",
      pick ? L("파일 추가", "Add file") : L("폴더 열기", "Open folder"),
      0,
      _inset,
      10,
      HeaderHeight,
      13,
      Import,
      false,
      reduceMotion
    );
    _folder = UiCapsules.CapsuleButton(
      ui,
      Root,
      "Open fonts folder",
      L("폴더", "Folder"),
      0,
      _inset,
      10,
      HeaderHeight,
      13,
      () => _library?.OpenFolder(),
      true,
      reduceMotion
    );
    _folder.gameObject.SetActive(pick);
    _search = ui.Input("Font search", "", Root, _inset, 0, 10, _ => Search(), null, 60);
    // Keeps the input's middle alignment: a Midline caret centers on the top
    // edge of the text rect, above the text, while the field is empty.
    _search.textComponent.fontSize = 15;
    TextMeshProUGUI hint = ui.Text(
      L("폰트 이름으로 찾기", "Search by name"),
      _search.textViewport,
      0,
      0,
      10,
      32,
      15,
      new Color(1, 1, 1, .32f)
    );
    UiFactory.Stretch(hint.rectTransform, 0, 0, 0, 0);
    hint.textWrappingMode = TextWrappingModes.NoWrap;
    hint.alignment = _search.textComponent.alignment;
    _search.placeholder = hint;
    _search.onSubmit.AddListener(_ => PickFirst());
    _viewport = ui.Rect("Fonts", Root, _inset, 0, 10, 10);
    _viewport.gameObject.AddComponent<RectMask2D>();
    _track = ui.Rect("Scroll track", Root, 0, 0, TrackWidth, 10);
    _thumb = ui.Capsule("Scroll thumb", _track, 0, 0, TrackWidth, 24, new Color(1, 1, 1, .28f));
    _status = ui.Text("", Root, _inset, 0, 10, StatusHeight, 12, UiPalette.Muted);
    _status.textWrappingMode = TextWrappingModes.Normal;
    _status.overflowMode = TextOverflowModes.Ellipsis;
    Root.gameObject.SetActive(false);
  }

  public RectTransform Root { get; }

  public bool Visible => Root != null && Root.gameObject.activeSelf;

  /// <summary>The search field has the keyboard; the editor leaves keys to it.</summary>
  public bool IsTyping => Visible && _search.isFocused;

  /// <summary>Called once the popover closes, however it closed.</summary>
  public Action Hidden;

  /// <summary>
  /// Opens with <paramref name="current"/> marked: a key, empty for the
  /// default row, or null when the selection mixes fonts.
  /// </summary>
  public void Show(string title, string current, string defaultName, string defaultDetail)
  {
    _title.text = title;
    _current = current == null ? null : OverlayFontKey.Normalize(current);
    _defaultName = defaultName;
    _defaultDetail = defaultDetail;
    _message = null;
    _dragging = false;
    _search.SetTextWithoutNotify("");
    // Files added to the folder by hand show up on the next open.
    _library?.RefreshImported();
    _library?.RequestInstalled();
    Root.gameObject.SetActive(true);
    Root.SetAsLastSibling();
    Rebuild();
    // The owner places the popover next; the list knows its height then.
    _scrollPending = true;
  }

  /// <summary>Lays the popover out in its parent's units; the list takes the height left over.</summary>
  public void Place(float x, float y, float width, float height)
  {
    width = Mathf.Max(260, width);
    height = Mathf.Max(MinimumHeight, height);
    bool resized = !Mathf.Approximately(width, _width) || !Mathf.Approximately(height, _height);
    _width = width;
    _height = height;
    UiFactory.Place(Root, x, y, width, height);
    if (resized || _rows.Count == 0)
      Layout();
    if (_scrollPending)
    {
      _scrollPending = false;
      ScrollToCurrent();
    }
  }

  public void Hide()
  {
    if (!Visible)
      return;
    _search.DeactivateInputField();
    _dragging = false;
    Root.gameObject.SetActive(false);
    Hidden?.Invoke();
  }

  /// <summary>Marks <paramref name="current"/> without reopening, after the owner changed it.</summary>
  public void SetCurrent(string current)
  {
    _current = current == null ? null : OverlayFontKey.Normalize(current);
    RefreshRows();
  }

  /// <summary>The control it opened from, when its owner says so.</summary>
  public RectTransform Anchor { get; set; }

  public bool Contains(Vector2 screen) => Visible && PopoverPlacement.Contains(Root, screen);

  /// <summary>Once per frame while open: fonts that finished listing, and scrolling.</summary>
  public void Tick()
  {
    if (!Visible)
      return;
    Refresh();
    HandlePointer();
  }

  /// <summary>Lists fonts that finished listing or loading since the last look.</summary>
  public void Refresh()
  {
    bool installed = _library?.Installed != null;
    if (
      (_library != null && _library.Revision != _libraryRevision)
      || installed != _installedReady
      || _fonts.Version != _fontVersion
    )
    {
      Rebuild();
      if (installed && !_installedReady)
        ScrollToCurrent();
    }
    _installedReady = installed;
  }

  // The wheel scrolls over the list; the thumb drags, and a click on the track jumps there.
  private void HandlePointer()
  {
    Vector2 mouse = UnityEngine.Input.mousePosition;
    if (PopoverPlacement.Contains(_viewport, mouse) || PopoverPlacement.Contains(_track, mouse))
    {
      float wheel = UnityEngine.Input.mouseScrollDelta.y;
      if (Mathf.Abs(wheel) > .001f)
        ScrollTo(_scroll - wheel * RowHeight * 2);
    }
    if (UnityEngine.Input.GetMouseButtonDown(0) && PopoverPlacement.Contains(_track, mouse))
    {
      _dragging = true;
      float thumbTop = -_thumb.rectTransform.anchoredPosition.y;
      float pointer = LocalY(_track, mouse);
      bool onThumb = pointer >= thumbTop && pointer <= thumbTop + _thumb.rectTransform.rect.height;
      _grab = onThumb ? pointer - thumbTop : _thumb.rectTransform.rect.height * .5f;
    }
    if (_dragging)
    {
      if (!UnityEngine.Input.GetMouseButton(0))
        _dragging = false;
      else
      {
        float travel = Mathf.Max(1, _track.rect.height - _thumb.rectTransform.rect.height);
        float top = Mathf.Clamp(LocalY(_track, mouse) - _grab, 0, travel);
        ScrollTo(top / travel * MaxScroll);
      }
    }
  }

  public void Dispose()
  {
    if (Root != null)
      UiFactory.Destroy(Root.gameObject);
  }

  // ---- Building the list ----

  private void Search()
  {
    _scroll = 0;
    Rebuild();
  }

  private void Rebuild()
  {
    _libraryRevision = _library?.Revision ?? 0;
    _fontVersion = _fonts.Version;
    _entries.Clear();
    string query = _search.text.Trim();
    if (Matches(query, _defaultName, _defaultDetail))
      _entries.Add(new Entry(EntryKind.Default, "", _defaultName, _defaultDetail));
    IReadOnlyList<OverlayFontFace> imported = _library?.Imported;
    IReadOnlyList<OverlayFontFace> installed = _library?.Installed;
    // A saved font this PC lacks still shows, so it's clear what is set.
    if (
      !string.IsNullOrEmpty(_current)
      && _fonts.Status(_current) == OverlayFontStatus.Missing
      && OverlayFontKey.TryParse(_current, out string family, out string style)
      && Matches(query, family, style)
    )
      _entries.Add(new Entry(EntryKind.Font, _current, family, L("이 PC에 없음", "Not on this PC"), true));
    AddSection(L("가져온 폰트", "Added fonts"), imported, query);
    AddSection(L("설치된 폰트", "Installed fonts"), installed, query);
    _scroll = Mathf.Clamp(_scroll, 0, MaxScroll);
    RefreshRows();
    RefreshStatus(query, installed == null);
  }

  private void AddSection(string title, IReadOnlyList<OverlayFontFace> faces, string query)
  {
    if (faces == null)
      return;
    bool any = false;
    foreach (OverlayFontFace face in faces)
    {
      if (!Matches(query, face.Family, face.Style))
        continue;
      if (!any)
        _entries.Add(new Entry(EntryKind.Header, null, title, null));
      any = true;
      _entries.Add(new Entry(EntryKind.Font, face.Key, face.Family, face.Style));
    }
  }

  private static bool Matches(string query, string family, string style) =>
    string.IsNullOrEmpty(query)
    || (family ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
    || ((family ?? "") + " " + (style ?? "")).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

  private void RefreshStatus(string query, bool loading)
  {
    string text;
    bool warning = false;
    if (_message != null)
    {
      text = _message;
      warning = _messageWarning;
    }
    else if (!string.IsNullOrEmpty(_current) && _fonts.Status(_current) == OverlayFontStatus.Missing)
    {
      text = L(
        OverlayFonts.DisplayName(_current)
          + " 폰트가 이 PC에 없어서 기본 폰트로 보여요. 설치하거나 파일 추가로 넣어 주세요.",
        OverlayFonts.DisplayName(_current) + " isn't on this PC, so the default font shows. Install it or add its file."
      );
      warning = true;
    }
    else if (loading)
      text = L("설치된 폰트를 불러오는 중이에요…", "Loading installed fonts…");
    else if (query.Length > 0 && !HasFonts())
      text = L("맞는 폰트가 없어요. 다른 이름으로 찾아보세요.", "No font matches. Try another name.");
    else
      text = L("추가한 폰트는 Mods/Element/Fonts에 보관돼요.", "Added fonts are kept in Mods/Element/Fonts.");
    _status.text = text;
    _status.color = warning ? UiPalette.Error : UiPalette.Muted;
    // A long message takes the lines it needs from the list.
    float needed = Mathf.Max(StatusHeight, Mathf.Ceil(_status.GetPreferredValues(text, _width - _inset * 2, 0).y));
    if (!Mathf.Approximately(needed, _statusHeight))
    {
      _statusHeight = needed;
      Layout();
    }
  }

  private bool HasFonts()
  {
    foreach (Entry entry in _entries)
      if (entry.Kind == EntryKind.Font)
        return true;
    return false;
  }

  // ---- Picking ----

  private void Pick(int index)
  {
    if (index < 0 || index >= _entries.Count || _entries[index].Kind == EntryKind.Header)
      return;
    Entry entry = _entries[index];
    _current = entry.Key;
    _message = null;
    _picked?.Invoke(entry.Key);
    // A font that turned out unreadable now reads as missing.
    Rebuild();
  }

  private void PickFirst()
  {
    for (int i = 0; i < _entries.Count; i++)
      if (_entries[i].Kind == EntryKind.Font || (_entries[i].Kind == EntryKind.Default && _search.text.Length == 0))
      {
        Pick(i);
        ScrollToCurrent();
        return;
      }
  }

  // Files the player chose are copied into the Fonts folder; the first one is applied.
  private void Import()
  {
    if (_library == null)
      return;
    bool pick = _library.CanPickFiles;
    OverlayFontImport result = _library.ImportFiles(
      L("오버레이에 쓸 폰트 파일을 고르세요 (TTF, OTF, TTC)", "Choose font files for the overlay (TTF, OTF, TTC)")
    );
    if (!pick)
    {
      ShowMessage(
        L(
          "Fonts 폴더를 열었어요. 폰트 파일을 넣고 이 목록을 다시 열어 주세요.",
          "The Fonts folder is open. Put font files in it, then open this list again."
        ),
        false
      );
      return;
    }
    if (result.Cancelled)
      return;
    // The font set looks again before the new font is drawn.
    _fonts.Tick();
    _search.SetTextWithoutNotify("");
    if (result.Added.Count > 0)
    {
      string key = result.Added[0].Key;
      _current = key;
      _picked?.Invoke(key);
    }
    Rebuild();
    ScrollToCurrent();
    if (result.Unreadable.Count > 0)
      ShowMessage(
        L(
          string.Join(", ", result.Unreadable) + "에서 폰트를 읽지 못했어요. TTF, OTF, TTC 파일을 골라 주세요.",
          "No font could be read from " + string.Join(", ", result.Unreadable) + ". Choose a TTF, OTF or TTC file."
        ),
        true
      );
    else if (result.Failed.Count > 0)
      ShowMessage(
        L(
          string.Join(", ", result.Failed)
            + "을 Fonts 폴더에 복사하지 못했어요. 저장 공간과 폴더 권한을 확인해 주세요.",
          "Couldn't copy "
            + string.Join(", ", result.Failed)
            + " into the Fonts folder. Check free space and folder permissions."
        ),
        true
      );
    else if (result.Added.Count > 0)
      ShowMessage(
        L(
          "폰트 " + result.Added.Count + "개를 추가하고 첫 번째를 적용했어요.",
          "Added " + result.Added.Count + (result.Added.Count == 1 ? " font" : " fonts") + " and applied the first."
        ),
        false
      );
  }

  private void ShowMessage(string message, bool warning)
  {
    _message = message;
    _messageWarning = warning;
    RefreshStatus(_search.text.Trim(), _library?.Installed == null);
  }

  // ---- Layout and scrolling ----

  private void Layout()
  {
    float inner = _width - _inset * 2;
    float addWidth = Mathf.Ceil(Measure(_add)) + 28;
    float folderWidth = _folder.gameObject.activeSelf ? Mathf.Ceil(Measure(_folder)) + 24 : 0;
    float right = _width - _inset;
    UiFactory.Place((RectTransform)_add.transform, right - addWidth, _inset, addWidth, HeaderHeight);
    right -= addWidth;
    if (folderWidth > 0)
    {
      UiFactory.Place((RectTransform)_folder.transform, right - 4 - folderWidth, _inset, folderWidth, HeaderHeight);
      right -= folderWidth + 4;
    }
    UiFactory.Place(_title.rectTransform, _inset, _inset, Mathf.Max(40, right - _inset - 8), HeaderHeight);
    float y = _inset + HeaderHeight + 8;
    UiFactory.Place((RectTransform)_search.transform, _inset, y, inner, SearchHeight);
    y += SearchHeight + Gap;
    float listHeight = Mathf.Max(RowHeight * 3, _height - y - Gap - _statusHeight - _inset);
    UiFactory.Place(_viewport, _inset, y, inner - TrackWidth - 6, listHeight);
    UiFactory.Place(_track, _width - _inset - TrackWidth, y, TrackWidth, listHeight);
    UiFactory.Place(_status.rectTransform, _inset, y + listHeight + Gap, inner, _statusHeight);
    EnsureRows(Mathf.CeilToInt(listHeight / RowHeight) + 1);
    _scroll = Mathf.Clamp(_scroll, 0, MaxScroll);
    RefreshRows();
  }

  private static float Measure(Button button)
  {
    TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
    return label == null ? 40 : label.GetPreferredValues(label.text).x;
  }

  private void EnsureRows(int count)
  {
    float width = _viewport.rect.width;
    while (_rows.Count < count)
    {
      Row row = new();
      row.Button = UiCapsules.CapsuleButton(
        _ui,
        _viewport,
        "Font row",
        "",
        0,
        0,
        width,
        RowHeight - 2,
        15,
        () => Pick(row.Index),
        true,
        _reduceMotion
      );
      RectTransform root = (RectTransform)row.Button.transform;
      row.Selected = _ui.Capsule("Current", root, 0, 0, width, RowHeight - 2, UiPalette.SoftAccent);
      row.Selected.transform.SetAsFirstSibling();
      row.Name = _ui.Text("", root, 12, 0, 10, RowHeight - 2, 15);
      row.Name.textWrappingMode = TextWrappingModes.NoWrap;
      row.Detail = _ui.Text("", root, 0, 0, 10, RowHeight - 2, 12, UiPalette.Muted);
      row.Detail.textWrappingMode = TextWrappingModes.NoWrap;
      row.Detail.alignment = TextAlignmentOptions.MidlineRight;
      _rows.Add(row);
    }
  }

  private float MaxScroll => Mathf.Max(0, _entries.Count * RowHeight - _viewport.rect.height);

  private void ScrollTo(float scroll)
  {
    scroll = Mathf.Clamp(scroll, 0, MaxScroll);
    if (Mathf.Approximately(scroll, _scroll))
      return;
    _scroll = scroll;
    RefreshRows();
  }

  // The current font in the middle of the list, or the top when it isn't listed.
  private void ScrollToCurrent()
  {
    int index = IndexOf(_current);
    float target = index < 0 ? 0 : index * RowHeight - (_viewport.rect.height - RowHeight) * .5f;
    _scroll = Mathf.Clamp(target, 0, MaxScroll);
    RefreshRows();
  }

  private int IndexOf(string key)
  {
    for (int i = 0; i < _entries.Count; i++)
      if (Names(_entries[i], key))
        return i;
    return -1;
  }

  // Empty names the default row; null (a mixed selection) names none.
  private static bool Names(Entry entry, string key) =>
    key != null
    && (
      key.Length == 0
        ? entry.Kind == EntryKind.Default
        : entry.Kind == EntryKind.Font && OverlayFontKey.Same(entry.Key, key)
    );

  private void RefreshRows()
  {
    float width = _viewport.rect.width;
    int first = Mathf.FloorToInt(Mathf.Max(0, _scroll) / RowHeight);
    for (int i = 0; i < _rows.Count; i++)
    {
      Row row = _rows[i];
      int index = first + i;
      bool shown = index < _entries.Count;
      row.Button.gameObject.SetActive(shown);
      row.Index = shown ? index : -1;
      if (!shown)
        continue;
      Entry entry = _entries[index];
      RectTransform root = (RectTransform)row.Button.transform;
      UiFactory.Place(root, 0, index * RowHeight - _scroll + 1, width, RowHeight - 2);
      bool header = entry.Kind == EntryKind.Header;
      bool current = !header && IsCurrent(entry);
      row.Button.interactable = !header;
      row.Selected.gameObject.SetActive(current);
      row.Selected.color = new Color(UiPalette.SoftAccent.r, UiPalette.SoftAccent.g, UiPalette.SoftAccent.b, .22f);
      UiFactory.Place(row.Selected.rectTransform, 0, 0, width, RowHeight - 2);
      row.Name.text = entry.Name;
      row.Name.fontSize = header ? 12 : 15;
      row.Name.color =
        header ? UiPalette.Muted
        : current ? UiPalette.SoftAccent
        : UiPalette.Foreground;
      row.Detail.text = entry.Detail ?? "";
      row.Detail.color = entry.Warning ? UiPalette.Error : UiPalette.Muted;
      float left = 12;
      // The detail takes what the name leaves, and at least its share of the row.
      float nameWidth = Mathf.Ceil(row.Name.GetPreferredValues(row.Name.text).x) + 2;
      float detailRoom = Mathf.Max(width * .45f, width - left - 12 - 8 - nameWidth);
      float detailWidth = string.IsNullOrEmpty(row.Detail.text)
        ? 0
        : Mathf.Min(detailRoom, Mathf.Ceil(row.Detail.GetPreferredValues(row.Detail.text).x) + 2);
      UiFactory.Place(row.Detail.rectTransform, width - 12 - detailWidth, 0, detailWidth, RowHeight - 2);
      UiFactory.Place(
        row.Name.rectTransform,
        left,
        0,
        Mathf.Max(20, width - left - 12 - detailWidth - (detailWidth > 0 ? 8 : 0)),
        RowHeight - 2
      );
    }
    float content = _entries.Count * RowHeight;
    float view = _viewport.rect.height;
    bool scrolls = content > view + .5f;
    _track.gameObject.SetActive(scrolls);
    if (!scrolls)
      return;
    float thumb = Mathf.Max(24, view * view / content);
    float top = MaxScroll <= 0 ? 0 : _scroll / MaxScroll * (view - thumb);
    UiFactory.Place(_thumb.rectTransform, 0, top, TrackWidth, thumb);
  }

  private bool IsCurrent(Entry entry) => Names(entry, _current);

  private static float LocalY(RectTransform rect, Vector2 screen)
  {
    RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, null, out Vector2 local);
    // Rects here pivot at their top left; y grows downward in layout units.
    return -local.y;
  }
}
