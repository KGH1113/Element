using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Element.Domain.Overlay.Values;
using Element.Presentation.Overlay.Values;
using ElementUI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// The dock's values: a list of places (the built-in categories, then one
/// per mod that adds values) beside the open place's chips, and a search
/// over every value. The chips in view show what their values read now, a
/// few times a second. <see cref="OverlayValueBrowserView"/> draws it.
/// </summary>
internal sealed class OverlayValueBrowserModel : ViewModel
{
  private const float FollowSeconds = .25f;

  // The place last opened, for the rest of the session: the next editor opens there too.
  private static string _remembered;

  private readonly OverlayValues _values;
  private readonly List<ValueFolderModel> _folders = new();
  private readonly List<ValueChipModel> _inView = new();
  private readonly StringBuilder _scratch = new(64);
  private ValueFolderModel _open;
  private string _folded = "";
  private long _seen;
  private float _nextFollow;

  /// <param name="values">What the overlay shows now; null shows names only.</param>
  /// <param name="pick">A value clicked.</param>
  public OverlayValueBrowserModel(OverlayValues values, Action<OverlayValueDefinition> pick)
  {
    _values = values;
    List<ValueFolderModel> categories = new(),
      mods = new();
    foreach (OverlayValueFolder folder in OverlayValueCatalog.Folders())
    {
      ValueFolderModel model = Own(new ValueFolderModel(folder, pick, Open));
      (folder.IsMod ? mods : categories).Add(model);
    }
    Categories = categories;
    Mods = mods;
    _folders.AddRange(categories);
    _folders.AddRange(mods);
    Search = Command(Find);
    ClearSearch = Command(() =>
    {
      Query.Value = "";
      Find();
    });
    PickFirst = Command(PickFirstFound);
    _open = _folders.Find(folder => folder.Key == _remembered) ?? _folders[0];
    Show();
  }

  /// <summary>The built-in categories that have values, in order.</summary>
  public readonly IReadOnlyList<ValueFolderModel> Categories;

  /// <summary>One place per mod that adds values, in the order they arrived; often none.</summary>
  public readonly IReadOnlyList<ValueFolderModel> Mods;

  /// <summary>What is typed in the search.</summary>
  public readonly State<string> Query = new("");

  /// <summary>The search owns keyboard input until its focus ends.</summary>
  public readonly State<bool> SearchFocused = new(false);

  /// <summary>Something is typed: the page shows what it finds instead of the open place.</summary>
  public readonly State<bool> Searching = new(false);

  /// <summary>Over the page: the open place's name, or that it shows a search.</summary>
  public readonly State<string> Title = new("");

  /// <summary>Beside the title while searching: how many values it found.</summary>
  public readonly State<string> FoundCount = new("");

  /// <summary>The search found nothing: <see cref="Nothing"/> says so.</summary>
  public readonly State<bool> NothingFound = new(false);

  public readonly State<string> Nothing = new("");

  /// <summary>The search's words changed.</summary>
  public Command Search { get; }

  /// <summary>× or Esc: the open place comes back.</summary>
  public Command ClearSearch { get; }

  /// <summary>Enter in the search: the first value it found is picked, as a click would.</summary>
  public Command PickFirst { get; }

  /// <summary>The place open now.</summary>
  public ValueFolderModel OpenFolder => _open;

  /// <summary>
  /// Once a frame: a few times a second, the chips in view take what their
  /// values read now. With nothing new it compares one number and makes nothing.
  /// </summary>
  public void Follow(float now)
  {
    if (now < _nextFollow)
      return;
    _nextFollow = now + FollowSeconds;
    if (_values == null || _values.Stamp == _seen)
      return;
    OverlayValueMask changed = _values.ChangedSince(_seen);
    _seen = _values.Stamp;
    for (int i = 0; i < _inView.Count; i++)
      if (_inView[i].Shows(changed))
        _inView[i].Follow(_values, _scratch);
  }

  private void Open(ValueFolderModel folder)
  {
    _open = folder;
    _remembered = folder.Key;
    if (Searching.Value)
    {
      // Picking a place leaves the search.
      Query.Value = "";
      _folded = "";
      foreach (ValueFolderModel each in _folders)
        each.Find("");
    }
    Show();
  }

  private void Find()
  {
    string query = Query.Value ?? "";
    bool searching = query.Trim().Length > 0;
    string folded = OverlayValueSearch.Fold(query);
    if (searching == Searching.Value && folded == _folded)
      return;
    _folded = folded;
    int found = 0;
    foreach (ValueFolderModel folder in _folders)
      found += folder.Find(searching ? folded : "");
    FoundCount.Value = found.ToString(CultureInfo.InvariantCulture);
    NothingFound.Value = searching && found == 0;
    Nothing.Value = NothingFound.Value
      ? L("‘" + query.Trim() + "’ 검색 결과가 없어요", "Nothing matches “" + query.Trim() + "”")
      : "";
    Show();
  }

  // Lights the open place, or dims it under a search, and lets the chips now in view show their values at once.
  private void Show()
  {
    bool searching = (Query.Value ?? "").Trim().Length > 0;
    Searching.Value = searching;
    foreach (ValueFolderModel folder in _folders)
      folder.Show(folder == _open, searching);
    Title.Value = searching ? L("검색 결과", "Results") : _open.Name;
    _inView.Clear();
    foreach (ValueFolderModel folder in _folders)
      if (searching || folder == _open)
        foreach (ValueChipModel chip in folder.Chips)
          if (!searching || chip.Found.Value)
            _inView.Add(chip);
    foreach (ValueChipModel chip in _inView)
      chip.Follow(_values, _scratch);
    _seen = _values?.Stamp ?? 0;
  }

  private void PickFirstFound()
  {
    if (!Searching.Value)
      return;
    foreach (ValueChipModel chip in _inView)
      if (chip.Found.Value)
      {
        chip.Pick.TryExecute();
        return;
      }
  }
}
