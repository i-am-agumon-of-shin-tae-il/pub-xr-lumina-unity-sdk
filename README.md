# XRLumina Unity SDK

XR Lumina 공용 코어를 C#에서 호출하기 위한 패키지입니다. SDK는 공용 코어의 C# 호출 경계와 Unity 컴포넌트를 제공합니다. 샘플은 SDK 컴포넌트를 사용하며 게임별 캡처 정책, 콘텐츠 입력과 UI를 구현합니다.

## 구성

| 위치 | 역할 |
| --- | --- |
| `Runtime/Libs/XRLuminaCore` | 네이티브 공용 코어와 Android USB 라이브러리 |
| `Runtime/Core/API` | 클라이언트 생성과 설정 |
| `Runtime/Core/Contracts` | `IXRLuminaTriggers`, `IXRLuminaCallbacks` 공개 규약 |
| `Runtime/Core/Models` | 측정 데이터와 세션 및 완료 결과 DTO |
| `Runtime/Core/Interop` | 내부 C ABI 호출, 메모리 수명, 데이터와 콜백 변환 |
| `Runtime/Unity/Components/Client` | 코어를 Unity 생명주기에 연결하는 클라이언트 컴포넌트 |
| `Runtime/Unity/Components/Features` | 기록, 시선과 미러링 기능 컴포넌트 |
| `Runtime/Unity/Capture`, `Models`, `Contracts` | 촬영 내부 구현, 기록 데이터와 콘텐츠 정책 연결 규약 |
| `Samples~/BasicSample` | SDK 컴포넌트를 사용하는 콘텐츠 예제 |

공개 네임스페이스는 `XRLumina.Core.API`, `XRLumina.Core.Contracts`, `XRLumina.Core.Models`입니다. `Interop`은 패키지 내부 구현입니다. 호출 측에 FlatBuffers 생성 코드와 런타임을 추가하지 않습니다.

## 설치

Unity Package Manager에서 SDK 저장소를 Git URL 또는 로컬 패키지로 추가합니다. SDK 기능 컴포넌트는 Collections와 Unity 기본 AI·촬영·물리·XR·Android JNI 모듈을 사용합니다. Input System, URP, AI Navigation과 UGUI는 샘플 실행에 필요한 별도 패키지입니다.

## SDK 컴포넌트 사용

[`Runtime/Unity/Prefabs/XRLuminaClient.prefab`](./Runtime/Unity/Prefabs/XRLuminaClient.prefab)을 시작 씬에 배치하고 Head에 HMD Camera Transform, Body에 플레이어 루트를 지정합니다.

| 제공 컴포넌트 | 역할 |
| --- | --- |
| `XRLuminaClientController` | 코어 생성, 연결, Tick, 콜백 전달과 종료 |
| `XRLuminaInteractionController` | 자세·입력·메쉬 기록과 휴리스틱 촬영 |
| `XRLuminaEyeTrackingController` | 시선 기록과 구간별 파노라마 촬영 |
| `XRLuminaMirroringController` | HMD 카메라 미러링 |

클라이언트 오브젝트에 붙는 기능 컴포넌트는 `XRLumina.Unity.Components.Features`에 있습니다. `Client`로 코어 트리거에 접근하며 연결과 실행 수명은 클라이언트 컴포넌트가 관리합니다.

게임별 캡처 조건은 `XRLumina.Unity.Contracts`의 정책 인터페이스를 콘텐츠에서 구현하고 `SetCapturePolicy(...)`로 등록합니다. SDK에는 콘텐츠별 정지 시간이나 반복 횟수를 기본 정책으로 넣지 않습니다. 정책이 없으면 해당 자동 캡처는 생략합니다. 샘플의 정책과 설정 컴포넌트는 이 사용법의 예제입니다.

## 컴포넌트 없이 C# API 사용

이미 Unity 수명 관리 코드가 있거나 직접 연결하려면 아래 API를 사용합니다. 제공 클라이언트 컴포넌트를 사용하는 경우 코어를 중복 생성하지 않습니다.


```csharp
using System;
using XRLumina.Core.API;
using XRLumina.Core.Contracts;
using XRLumina.Core.Models;

IXRLuminaTriggers client = XRLuminaClient.Create(new XRLuminaClientOptions
{
    ClientId = "persistent-device-id",
    Model = "device-model",
    Name = "device-name",
}, callbacks);
client.Start();
```

`callbacks`는 콘텐츠에서 구현한 `IXRLuminaCallbacks`입니다. 구현 예제는 [SDK 연결 컴포넌트](./Runtime/Unity/Components/Client/XRLuminaClientController.cs)에 있습니다.

| 실행 시점 | 호출 |
| --- | --- |
| 콘텐츠 초기화 | `XRLuminaClient.Create(options, callbacks)` 후 `Start()` |
| 매 갱신 | `Tick(deltaSeconds)` |
| 연결 정지 | `Stop()` |
| 콘텐츠 종료 | `Dispose()` |

생성, 실행, 데이터 제출, Tick과 폐기는 같은 스레드에서 수행합니다. Unity 예제는 `Update`에서 `Time.unscaledDeltaTime`을 전달합니다. 콜백과 시간 제한은 Tick에서 처리하므로 완료를 기다리는 중에도 Tick을 계속 호출합니다.

`ClientId`는 실행마다 바뀌지 않는 장치 식별자입니다. `Host`가 비어 있으면 UDP로 탐색하고, 지정되어 있으면 해당 주소에 접속합니다. 기본 포트는 TCP 9999, 탐색 UDP 9998, Android USB 브리지용 로컬 TCP 10000입니다. USB 활성화는 `UsbEnabled`로 지정합니다.

## 트리거와 콜백

| 트리거 | 동작 |
| --- | --- |
| `RequestStart()`, `RequestFinish()` | 데스크톱에 측정 시작과 종료 요청 |
| `SendTracking(...)` | 사용자 자세 제출 |
| `SendInteractionEvent(...)`, `SendMesh(...)` | 상호작용과 공간 메쉬 제출 |
| `SendGaze(...)`, `SendCameraPose(...)` | 시선과 촬영 자세 제출 |
| `SendMirrorFrame(...)` | JPEG 미러링 이미지 제출 |
| `SendCapture(...)`, `SendScreenshot(...)` | 캡처 이미지와 발생 원인 제출 |
| `Flush(...)` | 업로드 요청 ID 반환 |
| `FlushAsync(...)` | 코어 완료 콜백과 연결된 `Task<FlushResult>` 반환 |

| 콜백 | 전달 내용 |
| --- | --- |
| `OnSessionReady(session)` | 새 세션 정보 |
| `OnSessionAccepted(session)` | 세션 수락 결과 |
| `OnDisconnected()` | 선택 연결 해제 |
| `OnMeasurementStarted()`, `OnMeasurementFinished()` | 데스크톱 명령으로 확정된 측정 상태 |
| `OnCommand(action)` | 측정 시작과 종료 명령 |
| `OnFault(error)` | 코어 또는 콜백 처리 오류 |
| `OnFlushCompleted(id, ok, error)` | 요청 ID에 해당하는 업로드 결과 |

요청의 `true`는 코어 송신 대기열 등록 성공입니다. 측정 상태는 요청 반환값이 아니라 데스크톱 명령에 따른 콜백으로 확정합니다. 잘못된 입력은 예외로 전달되고, 제출할 수 없는 세션·연결·대기열 상태는 `false`로 전달됩니다.

## 데이터 제출

```csharp
bool submitted = client.SendTracking(new Tracking
{
    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    Head = new Pose
    {
        Position = new Vector3 { X = 1, Y = 2, Z = 3 },
        Rotation = new Vector3 { X = 0, Y = 90, Z = 0 },
    },
    Body = new Pose
    {
        Position = new Vector3 { X = 1, Y = 0, Z = 3 },
        Rotation = new Vector3 { X = 0, Y = 90, Z = 0 },
    },
});
XRLuminaClient.RequireSubmitted(submitted);
```

`RequireSubmitted`는 `false`를 예외로 처리하는 선택적 보조 API입니다. 직접 반환값을 확인해 콘텐츠에서 처리해도 됩니다. DTO의 `Vector3`는 `UnityEngine.Vector3`와 다르며 좌표 수집과 변환은 콘텐츠가 수행합니다. 전송 함수는 입력 배열과 문자열을 네이티브 호출 반환 전에 복사합니다.

## 완료 대기

```csharp
FlushResult result = await client.FlushAsync(FlushGroup.Interaction, 0.2f);
if (!result.IsSuccess)
{
    Console.Error.WriteLine(result.Error);
}
```

코어가 요청 ID, 응답과 시간 초과를 관리하고, Interop이 그 콜백을 해당 Task의 결과로 연결합니다. 콘텐츠에서 요청 ID별 대기 사전을 다시 만들 필요가 없습니다. `FlushAsync`를 사용해도 `OnFlushCompleted` 콜백은 동일하게 전달됩니다. 기본 제한 시간은 10초이며, 폐기 시 미완료 Task는 실패 결과로 끝납니다. 잘못된 인자나 폐기된 핸들은 요청 시 예외가 발생합니다.

콜백 계약을 직접 사용할 경우 `Flush`로 받은 ID와 `OnFlushCompleted`의 ID를 연결합니다. Flush 성공은 해당 업로드 요청의 응답이며 전체 분석 완료를 뜻하지 않습니다.

Unity 코루틴을 사용하는 기능은 SDK 클라이언트 컴포넌트의 `WaitForUpload`에서 Task 완료를 기다립니다. 코어가 완료와 시간 제한을 판정하고 Interop이 Task를 연결하므로 콘텐츠가 요청 ID별 대기를 중복 구현하지 않습니다.

## Unity 샘플

Package Manager의 Samples에서 Basic Sample을 가져옵니다. [샘플 설명](./Samples~/BasicSample/README.md)에 필요한 패키지, 씬 실행과 기능별 예제를 정리했습니다.

콘텐츠별 정지 시간, 머리 회전과 반복 입력 기준은 샘플에서 구현합니다. SDK 본체가 게임별 캡처 조건이나 Unity 객체를 선택하지 않습니다.

## 포함 라이브러리

| 대상 | 파일 |
| --- | --- |
| macOS Editor와 Player | `libxrlumina_core_osx_universal.dylib` |
| Android ARM64 | `libxrlumina_core_android_arm64.so` |
| Android USB | `xrlumina_usb_android.aar` |
| iOS ARM64 | `libxrlumina_core_ios_arm64.a` |

라이브러리는 `Runtime/Libs/XRLuminaCore`에 있으며 `.meta`로 해당 플랫폼에서만 사용하도록 설정합니다. Android USB의 `Activity` 전달과 시작·종료는 SDK 연결 컴포넌트에서 처리합니다. iOS는 `__Internal`로 C API를 호출합니다.

외부 라이브러리의 고지와 라이선스는 `ThirdPartyNotices.txt`에 포함됩니다.
