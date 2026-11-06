# ElementNote의 DmNote v2 기반 이식

## 범위

기준은 [DmNote 2.1.0](https://github.com/DmNote-App/DmNote/releases/tag/2.1.0),
커밋 `7b4b9cf524ceca4a2c98abb6011094c2b6e38085`이다. 기존 기반은 1.6.1,
커밋 `167f7c8223d790f8d1240737293dba46bedf4753`이다.

v2 네이티브 애플리케이션과 편집·저장 엔진을 기반으로 사용하고, 그 위에
Element의 기능과 현재 UI를 연결한다. 키뷰어는 v1 외형과 사용자 CSS의
선택자·우선순위를 유지한다.

## 유지할 계약

- ElementNote의 앱 이름, 데이터 경로, 배포·업데이트 채널
- 손·발의 독립적인 탭 선택과 창, 고스트 키, 키 리미터 프로필 동기화
- 게임과 함께 열고 닫히는 수명주기, 게임 화면 키뷰어, 리플레이 렌더 세션
- 기존 Element UI와 shadcn 구성요소, 메인 창과 키뷰어의 스타일 격리
- v1 키 DOM, CSS 변수, 테두리·둥글기·글꼴·카운터·이미지 표현
- 기존 프리셋을 읽고 수정하고 다시 저장할 때의 데이터 보존

## 편집·저장 경계

v2의 요소 ID, 편집 문서 revision, 원자적 commit, 이력과 자산 복구를
저장의 기준으로 삼는다. 기존 UI가 쓰는 배열 인덱스와 세분화된 저장 요청은
이 경계에서 변환한다. 기존 UI에 노출되지 않은 v2 필드를 지우거나 다중 키를
문자열 하나로 축소해서 저장하지 않는다. Element 전용 필드도 문서의 저장·복구·
프리셋 경로를 통과해야 한다.

v2의 회전, 그라데이션, 그림자, 이미지 레이어·변환과 별도 글꼴 굵기는
저장된 속성이 있는 요소에 적용한다. 이 속성이 없는 기존 요소에는 v1의
테두리·글꼴·이미지 기본값을 적용한다. 카운터 외곽선도 그대로 보존한다.

스프라이트는 상단 추가 메뉴와 캔버스 컨텍스트 메뉴에서 만들 수 있다.
캔버스의 스프라이트를 클릭하거나 컨텍스트 메뉴의 스프라이트 목록에서
이미지·자세·담당 키·전환 효과를 편집한다. 숨겨진 스프라이트도 목록에서
열 수 있다. 저장·삭제는 다른 요소와 같은 원자적 편집 이력에 기록한다.

## 외형 검증

`apps/element-note/tests/visual/viewer-baseline.spec.ts`의 기존 스크린샷과
DOM/CSS 계약을 기준으로 삼는다. 메인 미리보기·오버레이·OBS에 대해 기본 손,
커스텀 CSS 손, 노트와 카운터가 있는 발 키뷰어를 검사한다. 이식을 통과시키기
위해 스크린샷 기준을 다시 생성하지 않는다.

v2는 노트 캔버스의 내부 버퍼를 트랙 영역으로 줄인다. 계약 검사는 논리적
뷰포트 크기를 확인하고, 노트의 실제 위치와 외형은 기존 PNG로 비교한다.

브라우저 fixture는 프론트엔드 기본값을 사용하므로 네이티브 bootstrap 기본값도
별도로 검사해야 한다. 특히 카운터의 16px·700 기본값과 노트 속도 180,
트랙 높이 150을 보존한다.

## 검증 명령

저장소 루트에서 실행한다.

```bash
./scripts/run.sh check
./scripts/run.sh desktop-check
./scripts/run.sh desktop-visual-check
./scripts/run.sh desktop-message-test
./scripts/run.sh desktop-render-test
./scripts/run.sh desktop-build
./scripts/run.sh desktop-bundle --release
```

macOS에서의 검증 결과와 Windows 실행 검증은 구분한다. 빌드·회귀 검증을
마친 산출물만 `./scripts/run.sh install`로 설치한다.

## 검증 결과 (2026-11-05)

- Rust 전체 테스트 1,225개 통과 및 Clippy 통과.
- 프로세스 단위 파일 크기 제한이 필요한 사운드·프리셋 원자적 저장 테스트를
  각각 단독 실행하여 2개 모두 통과. 개발자 개인 저장소 fixture가 필요한
  upstream 수동 테스트 4개는 실행하지 않았다.
- 프런트엔드 67개 파일의 517개 테스트, SDK 검증, TypeScript, ESLint,
  Prettier 및 프로덕션 Vite 빌드 통과.
- 기존 PNG를 유지한 시각 회귀 24개 통과. Element UI와 메인·오버레이·OBS의
  v1 키뷰어 기본값 및 커스텀 CSS를 비교했다.
- 격리된 macOS WKWebView 렌더 검증 통과. 투명도·전역/탭 CSS·로컬 폰트·
  손/발 프리셋·짧은 입력·홀드·마우스 입력과 세션 격리를 검증했으며,
  PNG/raw 픽셀 비교의 평균 및 최대 차이는 0이다.
- C# 모드 Release 빌드와 32개 테스트, 셸 스크립트 46개 검사 통과.
- macOS Release 앱 패키징과 로컬 서명 검증 완료. `./scripts/run.sh install`에
  검증한 앱 경로를 지정해 `Mods/Element/Runtime/versions/1.2.0`에 설치했다.
  설치된 모드 DLL과 앱 아카이브의 실행 파일이 빌드 산출물과 동일함을 SHA-256으로 확인했다.

Element 제품 버전은 기존 `1.2.0`을 유지한다. DmNote 기반 버전은 `2.1.0`이다.

Windows 네이티브 앱의 실행 검증과 실제 게임을 켠 상태의 수동 검증은
이번 macOS 검사 결과에 포함하지 않는다.
