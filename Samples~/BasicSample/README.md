# Basic Sample

SDK의 트리거와 콜백을 Unity 콘텐츠에 연결하는 실행 예제입니다. SDK 본체의 구현을 복제하지 않고 공개 API를 사용합니다.

## 실행

1. 프로젝트에 URP, AI Navigation, Input System, UGUI, Collections 패키지를 준비합니다.
2. Package Manager에서 Basic Sample을 가져옵니다.
3. `Scenes/XRLuminaSampleScene.unity`를 열고 실행합니다.
4. 데스크톱 앱에서 장치를 선택하고 세션을 연결합니다.
5. 데스크톱 명령 또는 샘플 타깃으로 측정을 시작하고 종료합니다.

제공 워크스페이스에는 필요한 패키지가 등록되어 있습니다. 다른 씬에 연결할 때는 `Prefabs/XRLuminaClient.prefab`을 배치하고 연결 컴포넌트의 Head와 Body를 지정합니다. Head는 HMD Camera Transform, Body는 플레이어 루트입니다.

## 구성

| 폴더 | 내용 |
| --- | --- |
| `Scripts/Components/Features` | SDK 기능에 콘텐츠 캡처 정책을 등록하는 설정 |
| `Scripts/Components/Targets` | 콘텐츠 타깃의 입력·측정 요청과 반응 |
| `Scripts/Components/Player` | 플레이어 이동 |
| `Scripts/Components/UI` | 터치 입력과 화면 표시 |
| `Scripts/Policies` | 콘텐츠별 캡처 조건 판단 |
| `Scenes` | 실행 씬과 NavMesh |
| `Prefabs` | 예제 컴포넌트 연결 구성 |
| `Materials` | 샘플 표시 자원 |

예제 코드는 컴포넌트의 부착 대상과 콘텐츠 정책으로 구분합니다. SDK의 연결·수집·촬영 구현을 샘플에 중복 포함하지 않습니다. 샘플 코드는 가져온 콘텐츠에서 수정할 수 있습니다.

## 연결과 완료 대기

SDK가 제공하는 `XRLuminaClientController`가 코어 생성, `Start`, 프레임별 `Tick`, 종료 시 `Dispose`를 연결합니다. SDK 콜백을 샘플 기능과 연결하며 통신 메시지를 직접 처리하지 않습니다. `Client`가 코어 호출 접근점입니다.

업로드는 SDK의 `FlushAsync`를 사용합니다. 코어가 요청과 시간 제한을 관리하고 SDK가 완료 결과를 연결합니다. SDK의 `WaitForUpload`가 Task를 Unity 코루틴 완료 대기로 연결합니다.

## 사용하는 SDK 컴포넌트

아래 기능 구현은 SDK에 있으며 샘플은 제공 컴포넌트를 사용합니다.

| 컴포넌트 | 예제 역할 |
| --- | --- |
| `XRLuminaInteractionController` | 자세·입력·메쉬 기록과 휴리스틱 촬영 연결 |
| `XRLuminaEyeTrackingController` | 시선 기록과 파노라마 촬영 연결 |
| `XRLuminaMirroringController` | HMD 카메라 미러링 |
| `XRLuminaSampleCaptureSettings` | 콘텐츠별 캡처 조건 등록 |

Head의 Camera를 수집과 촬영에 사용합니다. Body는 이동 판정에 사용합니다. 시선 레이캐스트 대상에는 Collider가 있어야 합니다. 미러링 해상도와 FPS, 기록 FPS, 레이어 마스크와 촬영 해상도는 예제 컴포넌트에서 설정합니다.

## 콘텐츠별 캡처 조건

`XRLuminaSampleCaptureSettings`가 `SampleHeuristicCapturePolicy`와 `SampleGazeCapturePolicy`를 측정 전에 등록합니다. 정지 구간, 머리 방향 반전, 동일 입력 반복과 시선 캡처 구간은 이 샘플의 판단 기준입니다. 다른 콘텐츠는 SDK의 정책 규약을 구현하거나 샘플 정책의 조건을 수정합니다.

| 설정 | 기준 |
| --- | --- |
| Stationary Duration Seconds / Movement Threshold | 휴리스틱 정지 시간과 XZ 누적 이동 거리 |
| Yaw Window Seconds / Required Reversals / Minimum Swing Degrees | 머리 방향 반전 |
| Interaction Window Seconds / Required Count | 동일 대상·입력 반복 |
| Gaze Stationary Duration Ms / Movement Threshold Meters | 시선 캡처 구간 |

시선 기록 간격은 `XRLuminaEyeTrackingController.FramesPerSecond`를 사용합니다. 정책이 없으면 해당 자동 캡처는 생략합니다. 캡처 구간 결과는 SDK의 정책 연결 DTO이며, 콘텐츠의 자체 상태는 정책 구현 내부에서 자유롭게 관리합니다.

## 확인 사항

- 연결되지 않으면 데스크톱 앱과 세션 선택을 확인합니다.
- 포트 충돌 경고가 있으면 같은 로컬 포트를 사용하는 프로그램을 확인합니다.
- 촬영되지 않으면 Head Camera, 캡처 설정 컴포넌트와 정책 연결을 확인합니다.
- 완료 대기는 Tick이 계속 호출되어야 진행됩니다. 업로드 성공과 서버의 전체 분석 완료는 별도 상태입니다.
