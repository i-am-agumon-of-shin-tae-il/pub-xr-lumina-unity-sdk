using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR;

using XRLumina.Core.Contracts;
using XRLumina.Unity.Capture;
using XRLumina.Unity.Components.Client;
using XRLumina.Unity.Contracts;

using Wire = XRLumina.Core.Models;

namespace XRLumina.Unity.Components.Features
{
    /// <summary>
    /// 사용자 자세와 입력 기록 및 콘텐츠 요청에 따른 휴리스틱 촬영
    /// </summary>
    public sealed class XRLuminaInteractionController : MonoBehaviour
    {
        private bool _wasControllerPressed;
        private bool _wasLeftJoystickPushed;
        private bool _wasRightJoystickPushed;

        public static XRLuminaInteractionController Instance { get; private set; }

        [SerializeField] private int framesPerSecond = 5;
        [SerializeField] private float joystickDeadzone = 0.5f;
        [SerializeField] private LayerMask interactionLayerMask = ~0;
        [SerializeField] private float interactionMaxDistance = 100f;
        [SerializeField] private int heuristicCaptureWidth = 1280;
        [SerializeField] private int heuristicCaptureHeight = 720;

        private IXRLuminaHeuristicCapturePolicy _capturePolicy;
        private HeuristicCapture _heuristicScreenshotService;
        private XRLuminaClientController _rig;
        private IXRLuminaTriggers _client;
        private Coroutine _trackingCoroutine;

        /// <summary>
        /// 현재 상호작용 컴포넌트를 SDK 접근점으로 등록한다.
        /// </summary>
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        /// <summary>
        /// 클라이언트 컴포넌트 연결과 측정 상태 이벤트 구독
        /// </summary>
        private void Start()
        {
            var client = XRLuminaClientController.Instance;
            _rig = client;
            _client = client?.Client;
            if (_client == null)
            {
                Debug.LogError("[XRLumina] Client service is unavailable.", this);
                enabled = false;
                return;
            }

            _heuristicScreenshotService = new HeuristicCapture(
                _client,
                () => client.HeadCamera,
                () => client.Head,
                _capturePolicy,
                interactionLayerMask,
                interactionMaxDistance,
                heuristicCaptureWidth,
                heuristicCaptureHeight);
            _rig.MeasurementStarted += StartFeature;
            _rig.MeasurementFinished += FinishFeature;
        }

        /// <summary>
        /// 측정 시작 전 콘텐츠의 휴리스틱 캡처 조건 등록. 미등록 시 자동 캡처 생략
        /// </summary>
        public void SetCapturePolicy(IXRLuminaHeuristicCapturePolicy policy)
        {
            if (_client?.IsMeasuring == true)
            {
                throw new InvalidOperationException("Change the heuristic capture policy before measurement starts.");
            }
            _capturePolicy = policy ?? throw new ArgumentNullException(nameof(policy));
            _heuristicScreenshotService?.SetPolicy(policy);
        }

        /// <summary>
        /// 상호작용 측정 시작을 직접 실행
        /// </summary>
        private void StartFeature()
        {
            StopTracking();
            _heuristicScreenshotService?.StartFeature();
            ResetInputState();
            RecordEvent(Wire.EventKind.Task);
            _trackingCoroutine = StartCoroutine(TrackingLoop(Mathf.Max(1, framesPerSecond)));
        }

        /// <summary>
        /// 상호작용 측정 종료를 직접 실행
        /// </summary>
        private void FinishFeature()
        {
            RecordEvent(Wire.EventKind.Task);
            CaptureAndSendNavMesh();
            StopTracking();
            StartCoroutine(XRLuminaClientController.WaitForUpload(_client,
                Wire.FlushGroup.Interaction, 1f / Mathf.Max(1, framesPerSecond), 60f, null));
            if (_heuristicScreenshotService != null)
            {
                StartCoroutine(_heuristicScreenshotService.FinishAndFlush(60f, null));
            }
        }

        /// <summary>
        /// Unity 프레임 갱신을 직접 실행
        /// </summary>
        private void Update()
        {
            RecordControllerInputs();
            var trigger = _heuristicScreenshotService?.Tick();
            if (trigger.HasValue)
            {
                StartCoroutine(_heuristicScreenshotService.CaptureAndSend(trigger.Value));
            }
        }

        /// <summary>
        /// 지정 종류의 상호작용 이벤트 기록을 직접 실행
        /// </summary>
        public void RecordInteractionEvent(XRLumina.Core.Models.EventKind type)
        {
            if (_client?.IsMeasuring != true)
            {
                return;
            }

            RecordEvent(type);
        }

        /// <summary>
        /// 대상 오브젝트와 Controller 입력을 포함한 상호작용 이벤트를 기록한다.
        /// </summary>
        public void RecordInteractionEvent(
            XRLumina.Core.Models.EventKind type,
            GameObject target,
            XRLumina.Core.Models.ControllerInput controllerInput,
            XRLumina.Core.Models.ControllerHand hand = XRLumina.Core.Models.ControllerHand.Unknown)
        {
            if (_client?.IsMeasuring != true)
            {
                return;
            }

            RecordEvent(type);
            var trigger = _heuristicScreenshotService?.RecordInteraction(
                target,
                controllerInput,
                Time.realtimeSinceStartup, hand);
            if (trigger.HasValue)
            {
                StartCoroutine(_heuristicScreenshotService.CaptureAndSend(trigger.Value));
            }
        }

        /// <summary>
        /// 포즈 추적 종료를 직접 실행
        /// </summary>
        private void StopTracking()
        {
            if (_trackingCoroutine != null)
            {
                StopCoroutine(_trackingCoroutine);
                _trackingCoroutine = null;
            }
        }

        /// <summary>
        /// 컴포넌트 종료 시 이벤트 구독과 수집 자원 정리
        /// </summary>
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            if (_rig != null)
            {
                _rig.MeasurementStarted -= StartFeature;
                _rig.MeasurementFinished -= FinishFeature;
            }
            StopTracking();
        }

        /// <summary>
        /// 측정 중 XR 컨트롤러 버튼과 조이스틱 입력의 시작 시점을 기록한다.
        /// </summary>
        private void RecordControllerInputs()
        {
            if (!_client.IsMeasuring)
            {
                return;
            }

            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            var controllerPressed = IsControllerPressed(left) || IsControllerPressed(right);
            if (controllerPressed && !_wasControllerPressed)
            {
                RecordEvent(XRLumina.Core.Models.EventKind.Controller);
            }
            _wasControllerPressed = controllerPressed;

            UpdateJoystickEvent(left, ref _wasLeftJoystickPushed);
            UpdateJoystickEvent(right, ref _wasRightJoystickPushed);
        }

        /// <summary>
        /// 현재 머리 위치를 기준으로 지정 종류의 상호작용 이벤트를 기록한다.
        /// </summary>
        internal void RecordEvent(XRLumina.Core.Models.EventKind type)
        {
            var source = _rig != null ? _rig.Head : null;
            if (source == null)
            {
                return;
            }
            SendEvent(
                type,
                Wire.EventOperation.ReportCount,
                0,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                source.position);
        }

        /// <summary>
        /// 조이스틱이 데드존 밖으로 처음 이동한 순간을 제스처 이벤트로 기록한다.
        /// </summary>
        private void UpdateJoystickEvent(InputDevice device, ref bool wasPushed)
        {
            var isPushed = device.TryGetFeatureValue(CommonUsages.primary2DAxis, out var axis)
                && axis.magnitude > joystickDeadzone;
            if (isPushed && !wasPushed)
            {
                RecordEvent(XRLumina.Core.Models.EventKind.Gesture);
            }
            wasPushed = isPushed;
        }

        /// <summary>
        /// 장치의 주요 버튼·트리거·그립 중 하나가 눌렸는지 확인한다.
        /// </summary>
        private static bool IsControllerPressed(InputDevice device)
        {
            return IsPressed(device, CommonUsages.primaryButton)
                || IsPressed(device, CommonUsages.secondaryButton)
                || IsPressed(device, CommonUsages.triggerButton)
                || IsPressed(device, CommonUsages.gripButton);
        }

        /// <summary>
        /// XR 장치에서 지정된 불리언 입력 값을 읽는다.
        /// </summary>
        private static bool IsPressed(InputDevice device, InputFeatureUsage<bool> usage)
        {
            return device.TryGetFeatureValue(usage, out var pressed) && pressed;
        }

        /// <summary>
        /// 새 측정 시작 시 입력의 이전 프레임 상태를 초기화한다.
        /// </summary>
        private void ResetInputState()
        {
            _wasControllerPressed = false;
            _wasLeftJoystickPushed = false;
            _wasRightJoystickPushed = false;
        }

        /// <summary>
        /// 현재 씬의 NavMesh 삼각형 데이터를 계산해 전송한다.
        /// </summary>
        private void CaptureAndSendNavMesh()
        {
            var triangulation = NavMesh.CalculateTriangulation();
            SendMesh(triangulation.vertices, triangulation.indices);
        }

        /// <summary>
        /// 지정된 간격마다 머리와 몸의 위치·회전을 전송한다.
        /// </summary>
        private IEnumerator TrackingLoop(int framesPerSecond)
        {
            var delay = new WaitForSeconds(1f / framesPerSecond);
            while (true)
            {
                if (_client.IsMeasuring)
                {
                    SendTracking(
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        _rig != null ? _rig.Head : null,
                        _rig != null ? _rig.Body : null);
                }
                yield return delay;
            }
        }

        /// <summary>
        /// 단일 시점의 머리·몸 Transform을 상호작용 추적 패킷으로 전송한다.
        /// </summary>
        private void SendTracking(long timestamp, Transform head, Transform body)
        {
            if (head == null || body == null)
            {
                return;
            }

            XRLumina.Core.API.XRLuminaClient.RequireSubmitted(_client.SendTracking(new Wire.Tracking
            {
                Timestamp = timestamp, Head = ToPose(head), Body = ToPose(body),
            }));
        }

        /// <summary>
        /// 상호작용 종류와 위치를 이벤트 패킷으로 전송한다.
        /// </summary>
        private void SendEvent(
            Wire.EventKind type,
            Wire.EventOperation operationName,
            int count,
            long timestamp,
            Vector3 position,
            bool includeY = true)
        {
            XRLumina.Core.API.XRLuminaClient.RequireSubmitted(_client.SendInteractionEvent(new Wire.InteractionEvent
            {
                Type = type, OpName = operationName, Count = count, Timestamp = timestamp,
                Position = ToVector(position), HasY = includeY,
            }));
        }

        /// <summary>
        /// NavMesh 정점과 인덱스를 메시 패킷으로 전송한다.
        /// </summary>
        private void SendMesh(IReadOnlyList<Vector3> vertices, IReadOnlyList<int> indices)
        {
            if (vertices == null || indices == null)
            {
                return;
            }

            var points = new List<Wire.Vector3>(vertices.Count);
            foreach (var vertex in vertices)
            {
                points.Add(ToVector(vertex));
            }
            var triangles = new List<int>(indices.Count);
            foreach (var index in indices)
            {
                triangles.Add(index);
            }
            XRLumina.Core.API.XRLuminaClient.RequireSubmitted(_client.SendMesh(new Wire.Mesh { Vertices = points, Indices = triangles, }));
        }

        /// <summary>
        /// Unity Transform을 엔진 독립적인 스키마 포즈로 변환한다.
        /// </summary>
        private static Wire.Pose ToPose(Transform target)
        {
            return new Wire.Pose { Position = ToVector(target.position), Rotation = ToVector(target.eulerAngles), };
        }

        /// <summary>
        /// Unity 좌표를 공통 스키마의 벡터로 변환한다.
        /// </summary>
        private static Wire.Vector3 ToVector(Vector3 value)
        {
            return new Wire.Vector3 { X = value.x, Y = value.y, Z = value.z, };
        }
    }
}
