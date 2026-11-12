using System;
using System.Linq.Expressions;
using Element.Configuration.Profiles;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using ElementUI;
using TMPro;
using UnityEngine;
using static Element.Presentation.Ui.Declarative.Controls.CardRows;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.Settings.Pages;

/// <summary>
/// Profiles: how many, Import and New profile; a card per profile in three,
/// two or one columns; the last action's message under the cards; and after
/// an import, the fonts this PC lacks.
/// </summary>
internal sealed class ProfilesView : View<ProfilesModel>
{
  internal const float CardHeight = 140;
  private const float ActionSize = 30;

  private readonly State<float> _width = new(1000);

  public ProfilesView(ProfilesModel vm)
    : base(vm) { }

  public override View Body =>
    ZStack(
      Alignment.TopLeading,
      new WidthReader(_width),
      VStack(
        spacing: 12,
        If(() => _width.Value >= 440, () => Heading(true), () => Heading(false))
          .Frame(maxWidth: float.PositiveInfinity),
        If(
            () => _width.Value >= 600,
            () => Gallery(3),
            () =>
              If(() => _width.Value >= 440, () => Gallery(2), () => Gallery(1)).Frame(maxWidth: float.PositiveInfinity)
          )
          .Frame(maxWidth: float.PositiveInfinity),
        // Under the cards, so a message coming or going never moves them. Each part
        // brings its own gap, so one that isn't shown leaves none.
        VStack(
          spacing: 0,
          If(
              () => VM.HasMessage.Value,
              () =>
                Text(() => VM.Message)
                  .Font(Font.Caption)
                  .Foreground(() => VM.MessageIsProblem ? Colors.Danger : Colors.Accent)
                  .Padding(new Insets(4, 0, 12, 0))
            )
            .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading),
          If(() => VM.Fonts.Visible.Value, () => FontCard().Padding(new Insets(4, 0, 0, 0)))
            .Frame(maxWidth: float.PositiveInfinity)
        )
      )
    );

  // ---- Heading: the count, Import and New profile; narrow windows put the buttons on a row of their own ----

  private View Heading(bool wide)
  {
    View count = Text(() => VM.Count).Font(Font.Heading);
    // Import needs the system's file window; without it the button is left out.
    View import = VM.FilesSupported
      ? Button(Text(L("가져오기", "Import")).Frame(maxWidth: float.PositiveInfinity), () => VM.Import).Frame(height: 44)
      : null;
    View create = Button(
        Text(L("새 프로필", "New profile")).Foreground(Colors.Accent).Frame(maxWidth: float.PositiveInfinity),
        () => VM.NewProfile,
        ButtonStyle.Prominent
      )
      .Frame(height: 44)
      .Popover(Bind(() => VM.Creating.Open), NewProfilePanel);
    return wide
      ? HStack(spacing: 8, count, Spacer(), import?.Frame(width: 120), create.Frame(width: 156))
      : VStack(
        spacing: 8,
        count,
        HStack(
          spacing: 8,
          import?.Frame(maxWidth: float.PositiveInfinity),
          create.Frame(maxWidth: float.PositiveInfinity)
        )
      );
  }

  // The panel under New profile: it starts from the initial settings.
  private View NewProfilePanel() => ZStack(Alignment.TopLeading, new RaisePopover(), NewProfileForm());

  private View NewProfileForm() =>
    VStack(
        spacing: 0,
        Text(L("처음 설정으로 시작해요.", "Starts with the initial settings."))
          .Font(Font.Caption)
          .Foreground(Colors.Muted),
        HStack(
            spacing: 8,
            TextField(null, Bind(() => VM.Creating.Name), L("프로필 이름", "Profile name"))
              .Configure<TMP_InputField>(field =>
              {
                field.characterLimit = ElementProfile.MaximumNameLength;
                field.onSubmit.AddListener(_ => VM.Creating.Submit());
              })
              .Configure<TMP_InputField, int>(() => VM.Creating.FocusName, Focus)
              .Frame(maxWidth: float.PositiveInfinity),
            Button(
                Text(L("만들기", "Create"))
                  .Foreground(() => VM.Creating.CanCreate.Value ? Colors.Accent : Colors.Muted)
                  .Frame(maxWidth: float.PositiveInfinity),
                () => VM.Creating.Create,
                ButtonStyle.Prominent
              )
              .Frame(width: 100)
          )
          .Padding(new Insets(8, 0, 0, 0)),
        If(
            () => VM.Creating.Problem.Value.Length > 0,
            () =>
              Text(() => VM.Creating.Problem.Value)
                .Font(Font.Caption)
                .Foreground(Colors.Danger)
                .Padding(new Insets(8, 0, 0, 0))
          )
          .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading)
      )
      .Padding(8)
      // As wide as the old panel, narrower on a narrow window.
      .Frame(width: PanelWidth(_width.Value), alignment: Alignment.TopLeading);

  private static float PanelWidth(float page) => Mathf.Min(340, Mathf.Max(240, page + 40));

  private static void Focus(TMP_InputField field, int requests)
  {
    if (requests == 0 || !field.gameObject.activeInHierarchy)
      return;
    field.Select();
    field.ActivateInputField();
  }

  // ---- Cards ----

  private View Gallery(int columns) => CardRows.Of(() => VM.Cards, card => card.Id, columns, 12, Card);

  private View Card(ItemSlot<ProfileCardModel[]> row, int column) =>
    ZStack(
      Alignment.TopLeading,
      Button(
          VStack(
              spacing: 6,
              If(
                  () => At(row.Value, column).Renaming.Value,
                  () => RenameField(row, column),
                  () =>
                    Text(() => At(row.Value, column).Name)
                      .Font(Font.Heading.Sized(20))
                      .Foreground(Colors.Foreground)
                      .Configure<TMP_Text>(text => text.overflowMode = TextOverflowModes.Ellipsis)
                      .Frame(maxHeight: 54, alignment: Alignment.TopLeading)
                      .Padding(new Insets(0, 0, 0, 4 * ActionSize + 8))
                )
                .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.TopLeading),
              Spacer(),
              Text(() => At(row.Value, column).Line.Value)
                .Foreground(() => At(row.Value, column).LineIsProblem.Value ? Colors.Danger : Colors.Muted),
              Text(() => At(row.Value, column).State.Value)
                .Foreground(() =>
                  At(row.Value, column).Armed.Value ? Colors.Danger
                  : At(row.Value, column).InUse ? Colors.Accent
                  : Colors.Muted
                )
            )
            .Font(Font.Caption)
            .Frame(
              maxWidth: float.PositiveInfinity,
              maxHeight: float.PositiveInfinity,
              alignment: Alignment.TopLeading
            ),
          () => At(row.Value, column).Switch,
          ButtonStyle.Card
        )
        .Frame(height: CardHeight),
      Spacer()
        .Frame(width: 3, height: CardHeight - 28)
        .Background(Colors.Accent, 1.5f)
        .Padding(new Insets(14, 0, 0, 0))
        .Hidden(() => !At(row.Value, column).InUse),
      If(
        () => !At(row.Value, column).Renaming.Value,
        () =>
          HStack(
              spacing: 2,
              CardAction(UiIconKind.Edit, () => At(row.Value, column).Rename, () => Colors.Muted),
              CardAction(UiIconKind.Duplicate, () => At(row.Value, column).Duplicate, () => Colors.Muted),
              VM.FilesSupported
                ? CardAction(UiIconKind.Export, () => At(row.Value, column).Export, () => Colors.Muted)
                : null,
              If(
                  () => VM.CanDelete,
                  () =>
                    CardAction(
                      UiIconKind.Delete,
                      () => At(row.Value, column).Delete,
                      () => At(row.Value, column).Armed.Value ? Colors.Danger : Colors.Muted
                    )
                )
                .Frame()
            )
            .Padding(10)
            .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.TopTrailing)
      )
    );

  // A glyph in the card's top right: no surface of its own until the pointer is over it.
  private static View CardAction(UiIconKind icon, Expression<Func<Command>> command, Expression<Func<UiColor>> color) =>
    Button(Image(ElementIcons.Of(icon), 18).Foreground(color), command, ButtonStyle.Icon)
      .Frame(width: ActionSize, height: ActionSize);

  // The card's name as a field: the caret after the name; Enter or a click
  // elsewhere saves, Escape keeps the old name.
  private View RenameField(ItemSlot<ProfileCardModel[]> row, int column) =>
    TextField(null, Bind(() => VM.RenameText))
      .Configure<TMP_InputField>(field =>
      {
        field.characterLimit = ElementProfile.MaximumNameLength;
        field.onFocusSelectAll = false;
        field.onEndEdit.AddListener(_ => VM.EndRename(field.wasCanceled));
      })
      .Configure<TMP_InputField, bool>(
        () => At(row.Value, column).Renaming.Value,
        (field, renaming) => Focus(field, renaming ? 1 : 0)
      )
      .Frame(maxWidth: float.PositiveInfinity);

  // ---- After an import: the fonts this PC lacks ----

  private View FontCard() =>
    VStack(
        spacing: 12,
        Text(L("이 PC에 없는 폰트가 있어요", "Some fonts aren't on this PC")).Font(Font.Heading.Sized(17)),
        Text(
            L(
              "가져온 프로필이 쓰는 폰트예요. 대신 쓸 폰트를 고르면 바로 바뀝니다. 그대로 두면 폰트를 설치하거나 추가할 때까지 기본 폰트로 보여요.",
              "The profile you imported uses these fonts. Pick one to use instead and it changes right away. Left as is, they show in the default font until you install or add them."
            )
          )
          .Font(Font.Caption)
          .Foreground(Colors.Muted),
        ForEach(() => VM.Fonts.Rows, row => row.Use.Key, FontRow)
          .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.TopLeading),
        Button(Text(L("완료", "Done")).Frame(maxWidth: float.PositiveInfinity), () => VM.Fonts.Done)
          .Frame(width: 120)
          .Padding(new Insets(4, 0, 0, 0))
      )
      .Padding(16)
      .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.TopLeading)
      .Background(Colors.Surface, 12);

  // A missing font, what its texts show now, and the list to pick another; the button goes under on narrow windows.
  private View FontRow(ItemSlot<ProfileFontsModel.Row> slot) =>
    If(
        () => _width.Value >= 480,
        () => HStack(spacing: 12, FontWords(slot), ChooseFont(slot)),
        () => VStack(spacing: 8, FontWords(slot), ChooseFont(slot))
      )
      .Padding(new Insets(4, 0, 8, 0));

  private static View FontWords(ItemSlot<ProfileFontsModel.Row> slot) =>
    VStack(
        spacing: 4,
        Text(() => slot.Value.Name).Font(Font.Body),
        Text(() => slot.Value.State)
          .Font(Font.Caption)
          .Foreground(() => slot.Value.Replaced ? Colors.Accent : Colors.Muted)
      )
      .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading);

  private View ChooseFont(ItemSlot<ProfileFontsModel.Row> slot) =>
    Button(
        Text(L("폰트 고르기", "Choose font")).Foreground(Colors.Accent).Frame(maxWidth: float.PositiveInfinity),
        () => slot.Value.Choose,
        ButtonStyle.Prominent
      )
      .Frame(width: 140, height: 40)
      .Popover(Bind(() => slot.Value.PickerOpen), () => FontPicker(slot.Value));

  // Opened with the font the row uses now; a pick shows at once and the list stays open.
  private static View FontPicker(ProfileFontsModel.Row row) =>
    ZStack(Alignment.TopLeading, new RaisePopover(), new FontList(row.Picker()));
}
