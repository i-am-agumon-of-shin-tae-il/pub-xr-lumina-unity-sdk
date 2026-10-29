using XRLumina.Unity.Controllers;
using XRLumina.Core.Contracts;
using HeuristicScreenshotTrigger = XRLumina.Core.API.HeuristicScreenshotTrigger;
using System;
using Wire = XRLumina.Core.API;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR;
using XRLumina.Unity.Models;

namespace XRLumina.Unity.Services
{
    internal sealed class InteractionService
    {
        private readonly IXRLuminaTriggers _client;
        private readonly Func<Transform> _resolveHead;
        private readonly Func<Transform> _resolveBody;
        private readonly int _framesPerSecond;
        private readonly float _joystickDeadzone;
        private bool _wasControllerPressed;
        private bool _wasLeftJoystickPushed;
        private bool _wasRightJoystickPushed;

        /// <summary>Unity 실행 호스트, SDK 의존성, 인터랙션 설정으로 서비스를 생성한다.</summary>
        internal InteractionService(
            IXRLuminaTriggers client,
            Func<Transform> resolveHead,
            Func<Transform> resolveBody,
            int framesPerSecond,
            float joystickDeadzone)
        {
            _client = client;
            _resolveHead = resolveHead;
            _resolveBody = resolveBody;
            _framesPerSecond = framesPerSecond;
            _joystickDeadzone = joystickDeadzone;
        }

        /// <summary>포즈 추적과 XR 입력 이벤트 기록을 시작한다.</summary>
        internal IEnumerator StartFeature()
        {
            ResetInputState();
            RecordEvent(XRLumina.Core.API.EventKind.Task);
            return CreateTrackingLoop(_framesPerSecond);
        }

        /// <summary>추적을 종료하고 NavMesh와 누적 데이터를 Electron으로 플러시한다.</summary>
        internal void FinishFeature()
        {
            RecordEvent(XRLumina.Core.API.EventKind.Task);
            CaptureAndSendNavMesh();
        }

        /// <summary>측정 중 XR 컨트롤러 버튼과 조이스틱 입력의 시작 시점을 기록한다.</summary>
        internal void Tick()
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
                RecordEvent(XRLumina.Core.API.EventKind.Controller);
            }
            _wasControllerPressed = controllerPressed;

            UpdateJoystickEvent(left, ref _wasLeftJoystickPushed);
            UpdateJoystickEvent(right, ref _wasRightJoystickPushed);
        }

        /// <summary>현재 머리 위치를 기준으로 지정 종류의 상호작용 이벤트를 기록한다.</summary>
        internal void RecordEvent(XRLumina.Core.API.EventKind type)
        {
            var source = _resolveHead?.Invoke();
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

        /// <summary>조이스틱이 데드존 밖으로 처음 이동한 순간을 제스처 이벤트로 기록한다.</summary>
        private void UpdateJoystickEvent(InputDevice device, ref bool wasPushed)
        {
            var isPushed = device.TryGetFeatureValue(CommonUsages.primary2DAxis, out var axis)
                && axis.magnitude > _joystickDeadzone;
            if (isPushed && !wasPushed)
            {
                RecordEvent(XRLumina.Core.API.EventKind.Gesture);
            }
            wasPushed = isPushed;
        }

        /// <summary>장치의 주요 버튼·트리거·그립 중 하나가 눌렸는지 확인한다.</summary>
        private static bool IsControllerPressed(InputDevice device)
        {
            return IsPressed(device, CommonUsages.primaryButton)
                || IsPressed(device, CommonUsages.secondaryButton)
                || IsPressed(device, CommonUsages.triggerButton)
                || IsPressed(device, CommonUsages.gripButton);
        }

        /// <summary>XR 장치에서 지정된 불리언 입력 값을 읽는다.</summary>
        private static bool IsPressed(InputDevice device, InputFeatureUsage<bool> usage)
        {
            return device.TryGetFeatureValue(usage, out var pressed) && pressed;
        }

        /// <summary>새 측정 시작 시 입력의 이전 프레임 상태를 초기화한다.</summary>
        private void ResetInputState()
        {
            _wasControllerPressed = false;
            _wasLeftJoystickPushed = false;
            _wasRightJoystickPushed = false;
        }

        /// <summary>Electron에 상호작용 데이터 조합·업로드를 요청하고 응답을 기다린다.</summary>
        internal IEnumerator Flush(
            int framesPerSecond,
            float timeoutSec,
            Action<bool, string> onResult)
        {
            var session = _client.Session;
            if (session == null)
            {
                onResult?.Invoke(false, "session unavailable");
                yield break;
            }

            var frameIntervalSeconds = 1f / Mathf.Max(1, framesPerSecond);
            yield return XRLuminaClientController.Flush(_client, Wire.FlushGroup.Interaction, frameIntervalSeconds, timeoutSec, onResult);
        }

        /// <summary>지정 머리·몸 Transform을 설정된 간격으로 전송하기 시작한다.</summary>
        private IEnumerator CreateTrackingLoop(int framesPerSecond)
        {
            return TrackingLoop(Mathf.Max(1, framesPerSecond));
        }

        /// <summary>현재 씬의 NavMesh 삼각형 데이터를 계산해 전송한다.</summary>
        private void CaptureAndSendNavMesh()
        {
            var triangulation = NavMesh.CalculateTriangulation();
            SendMesh(triangulation.vertices, triangulation.indices);
        }

        /// <summary>지정된 간격마다 머리와 몸의 위치·회전을 전송한다.</summary>
        private IEnumerator TrackingLoop(int framesPerSecond)
        {
            var delay = new WaitForSeconds(1f / framesPerSecond);
            while (true)
            {
                if (_client.IsMeasuring)
                {
                    SendTracking(
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        _resolveHead?.Invoke(),
                        _resolveBody?.Invoke());
                }
                yield return delay;
            }
        }

        /// <summary>단일 시점의 머리·몸 Transform을 상호작용 추적 패킷으로 전송한다.</summary>
        private void SendTracking(long timestamp, Transform head, Transform body)
        {
            if (head == null || body == null)
            {
                return;
            }

            RequireSubmitted(_client.SendTracking(new Wire.Tracking
            {
                Timestamp = timestamp, Head = ToPose(head), Body = ToPose(body),
            }));
        }

        /// <summary>상호작용 종류와 위치를 이벤트 패킷으로 전송한다.</summary>
        private void SendEvent(
            Wire.EventKind type,
            Wire.EventOperation operationName,
            int count,
            long timestamp,
            Vector3 position,
            bool includeY = true)
        {
            RequireSubmitted(_client.SendInteractionEvent(new Wire.InteractionEvent
            {
                Type = type, OpName = operationName, Count = count, Timestamp = timestamp,
                Position = ToVector(position), HasY = includeY,
            }));
        }

        /// <summary>NavMesh 정점과 인덱스를 메시 패킷으로 전송한다.</summary>
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
            RequireSubmitted(_client.SendMesh(new Wire.Mesh { Vertices = points, Indices = triangles, }));
        }

        /// <summary>Unity Transform을 엔진 독립적인 스키마 포즈로 변환한다.</summary>
        private static Wire.Pose ToPose(Transform target)
        {
            return new Wire.Pose { Position = ToVector(target.position), Rotation = ToVector(target.eulerAngles), };
        }

        /// <summary>Unity 좌표를 공통 스키마의 벡터로 변환한다.</summary>
        private static Wire.Vector3 ToVector(Vector3 value)
        {
            return new Wire.Vector3 { X = value.x, Y = value.y, Z = value.z, };
        }
        /// <summary>
        /// 수집 데이터 제출 실패의 명시적 처리
        /// </summary>
        private static void RequireSubmitted(bool submitted)
        {
            if (!submitted)
            {
                throw new InvalidOperationException("Session or native send queue unavailable.");
            }
        }
    }
}
