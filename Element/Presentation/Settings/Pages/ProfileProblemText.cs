using Element.Application.Profiles;
using Element.Configuration.Profiles;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Settings.Pages;

/// <summary>Profile problems in the player's terms: what happened and what to do next.</summary>
internal static class ProfileProblemText
{
  public static string Name(ProfileNameProblem problem) =>
    problem switch
    {
      ProfileNameProblem.Empty => L("프로필 이름을 입력하세요.", "Give the profile a name."),
      ProfileNameProblem.TooLong => L(
        "이름은 " + ElementProfile.MaximumNameLength + "자 이내로 입력하세요.",
        "Use " + ElementProfile.MaximumNameLength + " characters or fewer."
      ),
      _ => L(
        "같은 이름의 프로필이 있어요. 다른 이름을 입력하세요.",
        "A profile already has this name. Choose another."
      ),
    };

  public static string For(ProfileOutcome outcome, ProfileAction action) =>
    outcome.Problem switch
    {
      ProfileProblem.NotProfile => L(
        "Element 프로필 파일이 아니에요. 확장자가 ." + ProfileFile.Extension + "인 파일을 골라 주세요.",
        "That isn't an Element profile. Choose a ." + ProfileFile.Extension + " file."
      ),
      ProfileProblem.NewerVersion => string.IsNullOrEmpty(outcome.Detail)
        ? L(
          "더 새로운 Element에서 만든 프로필이에요. Element를 업데이트한 뒤 다시 가져와 주세요.",
          "This profile was made with a newer Element. Update Element, then import it again."
        )
        : L(
          "Element " + outcome.Detail + "에서 만든 프로필이에요. Element를 업데이트한 뒤 다시 가져와 주세요.",
          "This profile was made with Element " + outcome.Detail + ". Update Element, then import it again."
        ),
      ProfileProblem.TooLarge => L(
        "파일이 너무 커서 프로필로 읽지 않았어요. 고른 파일이 프로필이 맞는지 확인해 주세요.",
        "That file is too large to be a profile. Check that you chose the right file."
      ),
      ProfileProblem.ReadFailed => L(
        "파일을 열지 못했어요. 파일이 아직 내려받는 중이거나 다른 프로그램이 쓰고 있지 않은지 확인해 주세요.",
        "Couldn't open the file. Make sure it has finished downloading and no other program is using it."
      ),
      ProfileProblem.WriteFailed => L(
        "파일을 저장하지 못했어요. 다른 폴더를 골라 다시 내보내 주세요.",
        "Couldn't save the file. Choose another folder and export again."
      ),
      ProfileProblem.DialogUnavailable => L(
        "파일 창을 열지 못했어요. Element를 다시 설치한 뒤 시도해 주세요. 이유는 UMM 로그에 있어요.",
        "Couldn't open the file window. Reinstall Element and try again; the UMM log says why."
      ),
      ProfileProblem.LastProfile => L(
        "프로필이 하나뿐이라 삭제할 수 없어요. 새 프로필을 만든 뒤 삭제해 주세요.",
        "This is your only profile, so it can't be deleted. Create another one first."
      ),
      ProfileProblem.Missing => L("이 프로필은 이미 삭제됐어요.", "This profile no longer exists."),
      ProfileProblem.Damaged when action == ProfileAction.Import => L(
        "프로필 파일이 손상되어 가져오지 못했어요. 파일을 다시 받아 가져와 주세요.",
        "The profile file is damaged, so it wasn't imported. Get the file again and import it."
      ),
      _ => L(
        "프로필을 불러오지 못해 지금 설정을 그대로 두었어요. 이유는 UMM 로그에 있어요.",
        "Couldn't load that profile, so your current settings stay. The UMM log says why."
      ),
    };
}
