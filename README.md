# ImplResourcePack

JALib에 의존하지 않는 A Dance of Fire and Ice용 UnityModManager 오버레이 모드입니다.

## 라이선스 범위

| 범위                        | 라이선스                     | 설명                                                                                                                                                  |
| --------------------------- | ---------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| 저장소 루트(아래 예외 제외) | source-available proprietary | 개인·비상업적 공식 빌드 사용과 공식 저장소 기여 준비만 허용됩니다. 자세한 내용은 [`LICENSE.md`](LICENSE.md)를 확인하세요.                             |
| `apps/impl-dm-note/**`      | GPL-3.0-only                 | DM Note v1.6.1 기반의 독립 구성요소입니다. 앱의 [`LICENSE`](apps/impl-dm-note/LICENSE)와 [`UPSTREAM.md`](apps/impl-dm-note/UPSTREAM.md)가 적용됩니다. |
| 서드파티 코드·폰트·자산     | 각 고유 조건                 | [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md)와 구성요소별 고지를 확인하세요.                                                                    |

기여 절차와 권리 조건은 [`CONTRIBUTING.md`](CONTRIBUTING.md) 및 [`CONTRIBUTOR_AGREEMENT.md`](CONTRIBUTOR_AGREEMENT.md)에 정리되어 있습니다.

현재는 Jipper 스타일의 싱글 플레이 판정 오버레이를 제공합니다. 데스크톱 키뷰어는 `apps/impl-dm-note`의 독립 애플리케이션 ImplDmNote로 제공합니다.

- 진행도 및 진행도 바
- 정확도와 절대 정확도
- 음악 시간과 맵 시간
- BPM
- 판정
- 콤보
- 타이밍 스케일 (하단 중앙에 현재 타일의 판정 배율을 백분율로 표시)
- 최고 기록과 시도 횟수
- ImplDmNote 프로필과 동기화되는 입력 키 리미터

## Runtime dependencies

- A Dance of Fire and Ice
- UnityModManager
- Harmony
- AdofaiIpc 0.4.1

Unity 모드는 JALib에 의존하지 않습니다. ImplResourcePack은 AdofaiIpc 0.4.1 dependency bootstrap을 사용하며, ImplDmNote 데스크톱 앱은 대응하는 ADOFAI-IPC TypeScript 클라이언트를 사용합니다.

## ImplDmNote

`apps/impl-dm-note`는 DM Note v1.6.1을 기반으로 수정한 GPL-3.0-only 데스크톱 애플리케이션입니다. 별도 실행 파일과 앱 데이터 디렉터리를 사용하며 localhost ADOFAI-IPC를 통해 ImplResourcePack과 통신합니다.

```bash
npm install
npm run desktop:dev
npm run desktop:check
npm run desktop:build
```

원본과 변경 이력은 [`apps/impl-dm-note/UPSTREAM.md`](apps/impl-dm-note/UPSTREAM.md), 라이선스는 [`apps/impl-dm-note/LICENSE`](apps/impl-dm-note/LICENSE)를 참고하세요. 커스텀 JavaScript 플러그인은 샌드박스되지 않으며 앱 사용자와 같은 권한으로 실행되므로 신뢰하는 코드만 설치해야 합니다.

판정 오버레이는 JipperResourcePack과 동일한 `MAPLESTORY_OTF_BOLD SDF` 폰트 자산을 플랫폼별 AssetBundle에서 로드합니다.

## Build

빌드 환경 설정은 `tuf-replay`와 같은 `.env` 규약을 사용합니다.

폰트 AssetBundle을 다시 생성하려면 Unity 에디터를 닫은 뒤 다음 명령을 실행합니다.

```bash
./scripts/run.sh assets
```

Unity 프로젝트는 `ImplResourcePack.Unity`에 있으며 Unity `6000.3.10f1`을 사용합니다.

```bash
cp .env.example .env
./scripts/run.sh build
```

`build` 명령은 입력 경로를 검증하고 Debug 빌드를 만든 뒤 기본적으로 ADOFAI의 `Mods/ImplResourcePack`에 설치합니다.

릴리스 패키지는 다음 명령으로 만듭니다.

```bash
./scripts/run.sh package
```

생성물:

- `build/ImplResourcePack.zip`
- `build/ImplResourcePack.version`
- `build/ImplResourcePack.zip.sha256`

셸 스크립트만 검사하려면 다음을 실행합니다.

```bash
./scripts/run.sh check
```

## Formatting

C# 포맷터는 `tuf-replay`와 동일한 CSharpier 1.3.0을 사용합니다.

```bash
dotnet tool restore
dotnet csharpier format ImplResourcePack
dotnet csharpier check ImplResourcePack
```
