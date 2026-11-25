using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using Element.Presentation.Overlay.Values;
using Element.Presentation.OverlayEditor.Parts.Formula;
using ElementUI;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Parts.Dock.ValueBrowser;

/// <summary>
/// The dock's values: a list of places (the built-in categories, then one
/// per mod that adds values) beside the open place's chips, and a search.
/// Only values that fit where the dock puts values now are listed, found
/// and counted, under words that name that place (<see cref="Place"/>); how
/// many were left out is said under them. With nothing open, or a sign to
/// pick, it lists nothing and says what to do. The chips in view show what
/// their values read now, a few times a second. A chip clicked goes up as
/// the dock's <see cref="IInputDockActions"/>. <see cref="ValueBrowserView"/> draws it.
/// </summary>
internal sealed class ValueBrowserViewModel : ViewModel
{
  private const float FollowSeconds = .25f;

  // The place last opened, for the rest of the session: the next editor opens there too.
  private static string _remembered;

  private readonly OverlayValues _values;
  private readonly IInputDockActions _actions;
  private readonly FormulaEditing _editing;
  private readonly bool _caret;
  private readonly List<ValueFolderViewModel> _folders = new();
  private readonly List<ValueChipViewModel> _inView = new();
  private readonly StringBuilder _scratch = new(64);
  private ValueFolderViewModel _open;
  private ValuePlace _place;
  private string _folded = "";
  private long _seen;
  private static readonly string MineKey = OverlayValueCategory.Mine.ToString();
  private int _made = OverlayCustomValues.Version;

  /// <param name="values">What the overlay shows now; null shows names only.</param>
  /// <param name="actions">Where a clicked value goes.</param>
  /// <param name="editing">What the dock has open: where values go.</param>
  /// <param name="caret">With nothing open, values go in at a text's caret.</param>
  public ValueBrowserViewModel(OverlayValues values, IInputDockActions actions, FormulaEditing editing, bool caret)
  {
    _values = values;
    _actions = actions;
    _editing = editing;
    _caret = caret;
    List<ValueFolderViewModel> categories = new(),
      mods = new();
    foreach (OverlayValueFolder folder in OverlayValueCatalog.Folders())
    {
      ValueFolderViewModel model = Own(new ValueFolderViewModel(folder, actions.Pick, Open));
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
    // What the dock has open tells the place when it changes.
    Place = Computed(() => ValuePlace.Of(_editing, _caret));
    Refit(ValuePlace.Of(editing, caret));
  }

  /// <summary>Where the dock puts values now; it changes only as what the dock has open, or picked, changes.</summary>
  public readonly Computed<ValuePlace> Place;

  /// <summary>The place takes values from the list; while it doesn't, <see cref="Rest"/> shows instead.</summary>
  public readonly State<bool> Listing = new(false);

  /// <summary>In place of the list while it lists nothing: what to do.</summary>
  public readonly State<string> Rest = new("");

  /// <summary>What goes where the dock puts values, for the mark beside <see cref="Title"/>.</summary>
  public readonly State<OverlayValueType> Type = new(OverlayValueType.Any);

  /// <summary>Under the list: how many values were left out as not fitting; empty when none were.</summary>
  public readonly State<string> Hidden = new("");

  /// <summary>Some mod's values fit: the mods' heading shows.</summary>
  public readonly State<bool> ModsShown = new(false);

  /// <summary>The built-in categories that have values, in order.</summary>
  public readonly IReadOnlyList<ValueFolderViewModel> Categories;

  /// <summary>One place per mod that adds values, in the order they arrived; often none.</summary>
  public readonly IReadOnlyList<ValueFolderViewModel> Mods;

  /// <summary>What is typed in the search.</summary>
  public readonly State<string> Query = new("");

  /// <summary>The search owns keyboard input until its focus ends.</summary>
  public readonly State<bool> SearchFocused = new(false);

  /// <summary>Something is typed: the page shows what it finds instead of the open place.</summary>
  public readonly State<bool> Searching = new(false);

  /// <summary>Over the page: where the values go, "숫자 칸에 넣을 수 있는 값".</summary>
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
  public ValueFolderViewModel OpenFolder => _open;

  /// <summary>Ticks a few times a second while the chips are shown: when they look at what their values read.</summary>
  public readonly Timeline Live = Timeline.Periodic(FollowSeconds);

  /// <summary>
  /// The chips in view take what their values read now. With nothing new it
  /// compares one number and makes nothing.
  /// </summary>
  public void Follow()
  {
    if (_values == null || _values.Stamp == _seen)
      return;
    OverlayValueMask changed = _values.ChangedSince(_seen);
    _seen = _values.Stamp;
    for (int i = 0; i < _inView.Count; i++)
      if (_inView[i].Shows(changed))
        _inView[i].Follow(_values, _scratch);
  }

  /// <summary>
  /// Each frame: when a value of the player's was made or renamed, "My
  /// values" takes the catalog's list again. With nothing new it compares one
  /// number.
  /// </summary>
  public void FollowMade()
  {
    if (_made == OverlayCustomValues.Version)
      return;
    _made = OverlayCustomValues.Version;
    foreach (OverlayValueFolder folder in OverlayValueCatalog.Folders())
    {
      ValueFolderViewModel model = _folders.Find(each => each.Key == folder.Key);
      if (model != null && folder.Key == MineKey)
        model.Reload(folder);
    }
    Refit(_place);
  }

  /// <summary>
  /// Where the dock puts values is now <paramref name="place"/>: only the
  /// values that fit there stay listed, found and counted, and the open
  /// place is one that has some, the one opened last while it does. Runs
  /// when the place changes, never each frame; chips whose fit didn't
  /// change are left as they are.
  /// </summary>
  public void Refit(ValuePlace place)
  {
    _place = place;
    int hidden = 0;
    foreach (ValueFolderViewModel folder in _folders)
      hidden += folder.Fit(place);
    bool mods = false;
    foreach (ValueFolderViewModel folder in Mods)
      mods |= folder.Shown.Value;
    ModsShown.Value = mods;
    Listing.Value = place.Lists;
    Rest.Value = place.Hint;
    Title.Value = place.Title;
    Type.Value = place.Type;
    Hidden.Value =
      place.Lists && hidden > 0
        ? L(
          "이 자리에 맞지 않는 값 " + hidden.ToString(CultureInfo.InvariantCulture) + "개는 숨겼어요.",
          hidden.ToString(CultureInfo.InvariantCulture)
            + (hidden == 1 ? " value that doesn't fit here is hidden." : " values that don't fit here are hidden.")
        )
        : "";
    ValueFolderViewModel remembered = _folders.Find(folder => folder.Key == _remembered);
    if (remembered != null && remembered.Shown.Value)
      _open = remembered;
    else if (!_open.Shown.Value)
      _open = _folders.Find(folder => folder.Shown.Value) ?? _open;
    Mark(Query.Value ?? "");
  }

  private void Open(ValueFolderViewModel folder)
  {
    _open = folder;
    _remembered = folder.Key;
    if (Searching.Value)
    {
      // Picking a place leaves the search.
      Query.Value = "";
      _folded = "";
      foreach (ValueFolderViewModel each in _folders)
        each.Find("");
    }
    Show();
  }

  private void Find()
  {
    string query = Query.Value ?? "";
    if ((query.Trim().Length > 0) == Searching.Value && OverlayValueSearch.Fold(query) == _folded)
      return;
    Mark(query);
  }

  // Marks what the search finds among the values that fit, and shows it.
  private void Mark(string query)
  {
    bool searching = query.Trim().Length > 0;
    string folded = OverlayValueSearch.Fold(query);
    _folded = folded;
    int found = 0;
    foreach (ValueFolderViewModel folder in _folders)
      found += folder.Find(searching ? folded : "");
    FoundCount.Value = L(
      "· " + found.ToString(CultureInfo.InvariantCulture) + "개 찾음",
      "· " + found.ToString(CultureInfo.InvariantCulture) + " found"
    );
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
    foreach (ValueFolderViewModel folder in _folders)
      folder.Show(folder == _open && folder.Shown.Value, searching);
    _inView.Clear();
    foreach (ValueFolderViewModel folder in _folders)
      if (searching || folder == _open)
        foreach (ValueChipViewModel chip in folder.Chips)
          if (searching ? chip.Found.Value : chip.Fits.Value)
            _inView.Add(chip);
    foreach (ValueChipViewModel chip in _inView)
      chip.Follow(_values, _scratch);
    _seen = _values?.Stamp ?? 0;
  }

  private void PickFirstFound()
  {
    if (!Searching.Value)
      return;
    foreach (ValueChipViewModel chip in _inView)
      if (chip.Found.Value)
      {
        chip.Pick.TryExecute();
        return;
      }
  }

  protected override void OnDisposed() => Live.Dispose();
}
