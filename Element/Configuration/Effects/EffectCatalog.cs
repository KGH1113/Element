using System;

namespace Element.Configuration.Effects;

public enum EffectGroup
{
  Screen,
  Scene,
  Track,
  Planets,
  Other,
}

public sealed class EffectDefinition
{
  public RemovedEffects Flag { get; }
  public EffectGroup Group { get; }
  public string Name { get; }
  public string NameKo { get; }
  public string Description { get; }
  public string DescriptionKo { get; }

  internal EffectDefinition(
    RemovedEffects flag,
    EffectGroup group,
    string name,
    string nameKo,
    string description,
    string descriptionKo
  )
  {
    Flag = flag;
    Group = group;
    Name = name;
    NameKo = nameKo;
    Description = description;
    DescriptionKo = descriptionKo;
  }
}

public static class EffectCatalog
{
  public const RemovedEffects All = (RemovedEffects)((1L << 22) - 1);
  public const RemovedEffects FilterEffects = RemovedEffects.Filters | RemovedEffects.AdvancedFilters;
  public const RemovedEffects TrackEffects =
    RemovedEffects.TrackAnimations
    | RemovedEffects.TrackMovement
    | RemovedEffects.TrackPosition
    | RemovedEffects.TrackColors
    | RemovedEffects.TrackOpacity;

  public static readonly EffectDefinition[] Entries =
  {
    new(
      RemovedEffects.CameraMotion,
      EffectGroup.Screen,
      "Camera movement",
      "카메라 이동",
      "Follow the planets at your zoom instead of the map's camera moves, rotation and zoom.",
      "맵의 카메라 이동·회전·확대 대신 정한 배율로 행성을 따라갑니다."
    ),
    new(
      RemovedEffects.ScreenFlash,
      EffectGroup.Screen,
      "Flashes",
      "화면 번쩍임",
      "Remove foreground and background flashes.",
      "화면 앞뒤의 번쩍임을 제거합니다."
    ),
    new(
      RemovedEffects.ScreenShake,
      EffectGroup.Screen,
      "Screen shake",
      "화면 흔들림",
      "Remove authored screen shake.",
      "맵에 지정된 화면 흔들림을 제거합니다."
    ),
    new(
      RemovedEffects.Filters,
      EffectGroup.Screen,
      "Screen filters",
      "화면 필터",
      "Remove filters, bloom, mirrors, tiling and scrolling.",
      "필터·블룸·거울·화면 타일·스크롤 효과를 제거합니다."
    ),
    new(
      RemovedEffects.AdvancedFilters,
      EffectGroup.Screen,
      "Advanced filters",
      "고급 필터",
      "Remove advanced filter events.",
      "고급 필터 이벤트를 제거합니다."
    ),
    new(
      RemovedEffects.Backgrounds,
      EffectGroup.Scene,
      "Backgrounds & video",
      "배경과 영상",
      "Use a plain background without images or video.",
      "배경 이미지와 영상 없이 단색 배경을 사용합니다."
    ),
    new(
      RemovedEffects.Decorations,
      EffectGroup.Scene,
      "Decorations",
      "장식",
      "Remove visual decorations; keep those used by gameplay conditions.",
      "시각 장식을 제거하고 게임플레이 조건에 쓰이는 장식은 유지합니다."
    ),
    new(
      RemovedEffects.Particles,
      EffectGroup.Scene,
      "Particles",
      "파티클",
      "Remove custom particle emitters and particle events.",
      "커스텀 파티클과 관련 이벤트를 제거합니다."
    ),
    new(
      RemovedEffects.TrackAnimations,
      EffectGroup.Track,
      "Track animations",
      "타일 등장·퇴장",
      "Use immediate tile appearance and disappearance.",
      "타일의 등장·퇴장 애니메이션을 제거합니다."
    ),
    new(
      RemovedEffects.TrackMovement,
      EffectGroup.Track,
      "Track movement",
      "타일 이동",
      "Remove animated tile movement, rotation and scaling.",
      "타일의 이동·회전·크기 변경 애니메이션을 제거합니다."
    ),
    new(
      RemovedEffects.TrackPosition,
      EffectGroup.Track,
      "Track position offsets",
      "타일 위치 변경",
      "Remove position offsets. This can change the path layout.",
      "위치 오프셋을 제거합니다. 타일 경로 배치가 달라질 수 있습니다."
    ),
    new(
      RemovedEffects.TrackColors,
      EffectGroup.Track,
      "Track colors",
      "타일 색상",
      "Use the default track color and style.",
      "기본 타일 색상과 스타일을 사용합니다."
    ),
    new(
      RemovedEffects.TrackOpacity,
      EffectGroup.Track,
      "Track fading",
      "타일 투명도",
      "Keep authored tile opacity at 100%.",
      "맵에 지정된 타일 투명도를 100%로 유지합니다."
    ),
    new(
      RemovedEffects.PlanetOrbit,
      EffectGroup.Planets,
      "Orbit easing",
      "공 회전 이징",
      "Remove events that change the planets' orbit easing.",
      "공의 회전 이징을 바꾸는 이벤트를 제거합니다."
    ),
    new(
      RemovedEffects.PlanetScale,
      EffectGroup.Planets,
      "Planet scaling",
      "공 크기 변화",
      "Remove planet size animations.",
      "공의 크기 변경 애니메이션을 제거합니다."
    ),
    new(
      RemovedEffects.PlanetRadius,
      EffectGroup.Planets,
      "Orbit radius",
      "공 회전 반경",
      "Remove radius changes. This can change the path layout.",
      "회전 반경 변화를 제거합니다. 타일 경로 배치가 달라질 수 있습니다."
    ),
    new(
      RemovedEffects.HitSounds,
      EffectGroup.Other,
      "Custom hit sounds",
      "커스텀 타격음",
      "Remove custom hit-sound changes and Play Sound events.",
      "커스텀 타격음 변경과 소리 재생 이벤트를 제거합니다."
    ),
    new(
      RemovedEffects.HoldSounds,
      EffectGroup.Other,
      "Custom hold sounds",
      "커스텀 홀드음",
      "Remove hold-sound changes.",
      "홀드 소리 변경 이벤트를 제거합니다."
    ),
    new(
      RemovedEffects.HideIcons,
      EffectGroup.Other,
      "Hidden icons & judgements",
      "아이콘·판정 숨김",
      "Ignore events that hide tile icons or judgement text.",
      "타일 아이콘과 판정 문구를 숨기는 이벤트를 무시합니다."
    ),
    new(
      RemovedEffects.RepeatEvents,
      EffectGroup.Other,
      "Repeated effects",
      "이펙트 반복",
      "Disable Repeat Events; the original effect still runs.",
      "이벤트 반복을 제거합니다. 원본 이펙트는 실행됩니다."
    ),
    new(
      RemovedEffects.FrameRate,
      EffectGroup.Other,
      "Frame-rate changes",
      "프레임률 변경",
      "Ignore map-defined camera frame rates.",
      "맵에서 지정한 카메라 프레임률을 무시합니다."
    ),
    new(
      RemovedEffects.Checkpoints,
      EffectGroup.Other,
      "Checkpoints",
      "체크포인트",
      "Remove checkpoints, so retries start from the beginning.",
      "체크포인트를 제거해 처음부터 다시 시작합니다."
    ),
  };

  public static int Count(RemovedEffects effects)
  {
    ulong value = (ulong)(effects & All);
    int count = 0;
    while (value != 0)
    {
      value &= value - 1;
      count++;
    }
    return count;
  }

  public static string GroupName(EffectGroup group, bool korean) =>
    group switch
    {
      EffectGroup.Screen => korean ? "화면" : "Screen",
      EffectGroup.Scene => korean ? "배경과 장식" : "Scene",
      EffectGroup.Track => korean ? "타일" : "Track",
      EffectGroup.Planets => korean ? "공" : "Planets",
      _ => korean ? "기타" : "Other",
    };
}
