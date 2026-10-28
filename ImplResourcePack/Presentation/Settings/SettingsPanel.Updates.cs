using ImplResourcePack.Application.Updates;
using TMPro;
using UnityEngine.UI;

namespace ImplResourcePack.Presentation.Settings;

/// <summary>General → Automatic updates: the switch and what happened this launch.</summary>
internal sealed partial class SettingsPanel
{
  private readonly IModUpdates _updates;
  private Button _updateRow;
  private TextMeshProUGUI _updateDescription;

  private void CreateUpdateRow(float y)
  {
    if (_updates == null)
      return;
    _updateRow = _ui.Switch(
      L("자동 업데이트", "Automatic updates"),
      UpdateStatusText(_updates),
      _pages[2].transform,
      y,
      () => _updates.AutomaticUpdates,
      v =>
      {
        _updates.AutomaticUpdates = v;
        RefreshUpdateRow();
      },
      out _updateDescription
    );
  }

  private void RefreshUpdateRow()
  {
    if (_updateDescription != null)
      _updateDescription.text = UpdateStatusText(_updates);
  }

  /// <summary>One line for Settings and the UMM mod list: what happened, and what to do about it.</summary>
  public static string UpdateStatusText(IModUpdates updates)
  {
    if (updates == null)
      return L("자동 업데이트를 쓸 수 없는 설치입니다.", "This install can't update itself.");
    if (!updates.AutomaticUpdates)
      return L(
        "꺼져 있습니다. 새 버전은 GitHub Releases에서 직접 받아 설치해 주세요.",
        "Off. Install new versions yourself from GitHub Releases."
      );
    ModUpdateStatus status = updates.Status;
    string version = status.Version;
    string target = status.Target;
    return status.State switch
    {
      ModUpdateState.Updated => L($"이번 실행에서 {version} 버전으로 업데이트했습니다.", $"Updated to {version} this launch."),
      ModUpdateState.UpToDate => L(
        $"최신 버전({version})입니다. 게임을 켤 때마다 새 버전을 확인합니다.",
        $"{version} is the latest version. Checked each time the game starts."
      ),
      ModUpdateState.Downloading => L(
        $"{target} 버전을 받는 중입니다. 다음에 게임을 켜면 적용됩니다.",
        $"Downloading {target}. It applies the next time you start the game."
      ),
      ModUpdateState.Ready => L(
        $"{target} 버전을 받았습니다. 게임을 다시 켜면 적용됩니다.",
        $"{target} is downloaded. Restart the game to apply it."
      ),
      ModUpdateState.Skipped => L(
        $"{target} 버전이 시작되지 않아 {version} 버전을 계속 씁니다. 다음 버전이 나오면 자동으로 업데이트합니다.",
        $"{target} failed to start, so {version} stays. The next release installs automatically."
      ),
      ModUpdateState.Failed => FailureText(status.Error),
      ModUpdateState.Off => L(
        "다음에 게임을 켤 때부터 새 버전을 확인합니다.",
        "Checks for new versions from the next game start."
      ),
      ModUpdateState.Development => L(
        "개발 빌드는 자동으로 업데이트하지 않습니다.",
        "Development builds don't update themselves."
      ),
      _ => L("다음에 게임을 켤 때 새 버전을 확인합니다.", "Checks for a new version the next time the game starts."),
    };
  }

  private static string FailureText(ModUpdateError error) =>
    error switch
    {
      ModUpdateError.Network => L(
        "새 버전을 확인하지 못했습니다. 인터넷 연결을 확인해 주세요. 다음에 게임을 켤 때 다시 시도합니다.",
        "Couldn't check for a new version. Check your internet connection; it retries the next time the game starts."
      ),
      ModUpdateError.Damaged => L(
        "받은 업데이트 파일이 손상되어 설치하지 않았습니다. 다음에 게임을 켤 때 다시 받습니다.",
        "The downloaded update was damaged, so it wasn't installed. It downloads again the next time the game starts."
      ),
      ModUpdateError.Disk => L(
        "모드 폴더에 업데이트를 저장하지 못했습니다. 남은 디스크 공간과 폴더 권한을 확인해 주세요.",
        "Couldn't save the update in the mod folder. Check free disk space and folder permissions."
      ),
      _ => L(
        "업데이트하지 못했습니다. UMM 로그의 [Update] 줄에 이유가 있습니다.",
        "Couldn't update. The [Update] lines in the UMM log say why."
      ),
    };
}
