using XRLumina.Unity.Controllers;
using XRLumina.Core.Contracts;
using XRLumina.Unity.Contracts;
using HeuristicScreenshotTrigger = XRLumina.Core.API.HeuristicScreenshotTrigger;
using System;
using Wire = XRLumina.Core.API;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using XRLumina.Unity.Policies;

namespace XRLumina.Unity.Services
{
    internal sealed class HeuristicScreenshotService
    {
        private readonly IXRLuminaTriggers _client;
        private IXRLuminaHeuristicCapturePolicy _policy;
        private readonly System.Func<Camera> _resolveCamera;
        private readonly Func<Transform> _resolveHead;
        private readonly LayerMask _interactionLayerMask;
        private readonly float _interactionMaxDistance;
        private readonly int _captureWidth;
        private readonly int _captureHeight;
        private readonly Dictionary<(Wire.ControllerHand Hand, Wire.ControllerInput Input), bool> _inputStates = new();
        private bool _captureInProgress;
        private int _captureIndex;

        /// <summary>휴리스틱 판정 기준과 캡처 설정으로 서비스를 생성한다.</summary>
        internal HeuristicScreenshotService(
            IXRLuminaTriggers client,
            System.Func<Camera> resolveCamera,
            Func<Transform> resolveHead,
            IXRLuminaHeuristicCapturePolicy policy,
            LayerMask interactionLayerMask,
            float interactionMaxDistance,
            int captureWidth,
            int captureHeight)
        {
            _client = client;
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _resolveCamera = resolveCamera;
            _resolveHead = resolveHead;
            _interactionLayerMask = interactionLayerMask;
            _interactionMaxDistance = interactionMaxDistance;
            _captureWidth = captureWidth;
            _captureHeight = captureHeight;
        }

        /// <summary>게임에서 선택한 캡처 정책으로 교체하고 누적 상태를 초기화한다.</summary>
        internal void SetPolicy(IXRLuminaHeuristicCapturePolicy policy)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _policy.Reset();
        }

        /// <summary>누적 판정 상태를 초기화하고 휴리스틱 추적을 시작한다.</summary>
        internal void StartFeature()
        {
            ResetState();
        }

        /// <summary>현재 HMD 포즈와 컨트롤러 입력을 판정한다.</summary>
        internal HeuristicScreenshotTrigger? Tick()
        {
            if (!_client.IsMeasuring || _captureInProgress)
            {
                return null;
            }

            var source = ResolveHead();
            if (source == null)
            {
                return null;
            }

            var now = Time.realtimeSinceStartup;
            var trigger = _policy.EvaluatePose(source.position, source.eulerAngles.y, now);
            if (trigger.HasValue)
            {
                return trigger;
            }

            return UpdateControllerInputs(now);
        }

        /// <summary>게임에서 확정된 오브젝트·입력 조합을 반복 인터랙션 판정에 추가한다.</summary>
        internal HeuristicScreenshotTrigger? RecordInteraction(
            GameObject target,
            Wire.ControllerInput controllerInput,
            float capturedAt,
            Wire.ControllerHand hand = Wire.ControllerHand.Unknown)
        {
            if (!_client.IsMeasuring || _captureInProgress || target == null || controllerInput == Wire.ControllerInput.None)
            {
                return null;
            }

            return _policy.EvaluateInteraction(target.GetInstanceID(), target.name, controllerInput, hand, capturedAt);
        }

        /// <summary>판정된 시점의 HMD 카메라 화면을 PNG로 생성해 Electron으로 전송한다.</summary>
        internal IEnumerator CaptureAndSend(HeuristicScreenshotTrigger trigger)
        {
            _captureInProgress = true;
            yield return new WaitForEndOfFrame();
            try
            {
                var camera = ResolveCamera();
                if (camera != null)
                {
                    var png = CaptureCamera(camera);
                    if (png != null && png.Length > 0)
                    {
                        SendCapture(trigger, png);
                        Debug.Log(
                            $"[HeuristicScreenshot] 캡처 전송 완료(type={trigger.Type}, bytes={png.Length}, " +
                            $"object={trigger.ObjectName}, input={trigger.ControllerInput})");
                    }
                }
            }
            finally
            {
                _captureInProgress = false;
            }
        }

        /// <summary>진행 중인 캡처가 끝난 뒤 Electron에 휴리스틱 이미지 업로드를 요청한다.</summary>
        internal IEnumerator FinishAndFlush(float timeoutSec, Action<bool, string> onResult)
        {
            while (_captureInProgress)
            {
                yield return null;
            }

            var session = _client.Session;
            if (session == null)
            {
                onResult?.Invoke(false, "session unavailable");
                yield break;
            }
            yield return XRLuminaClientController.Flush(_client, Wire.FlushGroup.Heuristics, 0f, timeoutSec, onResult);
        }





        /// <summary>좌우 컨트롤러의 버튼 상승 에지를 동일 오브젝트 반복 입력으로 판정한다.</summary>
        private HeuristicScreenshotTrigger? UpdateControllerInputs(float now)
        {
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            return UpdateInput(left, Wire.ControllerHand.Left, CommonUsages.primaryButton, Wire.ControllerInput.Primary, now)
                ?? UpdateInput(left, Wire.ControllerHand.Left, CommonUsages.secondaryButton, Wire.ControllerInput.Secondary, now)
                ?? UpdateInput(left, Wire.ControllerHand.Left, CommonUsages.triggerButton, Wire.ControllerInput.Trigger, now)
                ?? UpdateInput(left, Wire.ControllerHand.Left, CommonUsages.gripButton, Wire.ControllerInput.Grip, now)
                ?? UpdateInput(right, Wire.ControllerHand.Right, CommonUsages.primaryButton, Wire.ControllerInput.Primary, now)
                ?? UpdateInput(right, Wire.ControllerHand.Right, CommonUsages.secondaryButton, Wire.ControllerInput.Secondary, now)
                ?? UpdateInput(right, Wire.ControllerHand.Right, CommonUsages.triggerButton, Wire.ControllerInput.Trigger, now)
                ?? UpdateInput(right, Wire.ControllerHand.Right, CommonUsages.gripButton, Wire.ControllerInput.Grip, now);
        }

        /// <summary>단일 컨트롤러 입력의 상승 에지에서 정면 오브젝트를 찾아 반복 횟수를 누적한다.</summary>
        private HeuristicScreenshotTrigger? UpdateInput(
            InputDevice device,
            Wire.ControllerHand hand,
            InputFeatureUsage<bool> usage,
            Wire.ControllerInput input,
            float now)
        {
            var stateKey = (hand, input);
            var pressed = device.TryGetFeatureValue(usage, out var value) && value;
            var wasPressed = _inputStates.TryGetValue(stateKey, out var previous) && previous;
            _inputStates[stateKey] = pressed;
            if (!pressed || wasPressed)
            {
                return null;
            }

            var camera = ResolveCamera();
            if (camera == null || !Physics.Raycast(
                    camera.transform.position,
                    camera.transform.forward,
                    out var hit,
                    _interactionMaxDistance,
                    _interactionLayerMask))
            {
                return null;
            }
            return RecordInteraction(hit.collider.gameObject, input, now, hand);
        }

        /// <summary>현재 카메라 영상을 지정 해상도의 PNG로 생성한다.</summary>
        private byte[] CaptureCamera(Camera source)
        {
            var width = Mathf.Max(1, _captureWidth);
            var height = Mathf.Max(1, _captureHeight);
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var previousTarget = source.targetTexture;
            var previousActive = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                source.targetTexture = target;
                source.Render();
                RenderTexture.active = target;
                texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply();
                return texture.EncodeToPNG();
            }
            finally
            {
                source.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }
            }
        }

        /// <summary>발생 원인과 PNG를 단일 메시지로 전송해 메타데이터 반복을 제거한다.</summary>
        private void SendCapture(HeuristicScreenshotTrigger trigger, byte[] bytes)
        {
            var fileName = $"{_captureIndex++:D3}-{trigger.Type}-{trigger.CapturedAt:F3}.png";
            RequireSubmitted(_client.SendScreenshot(fileName, trigger, bytes));
        }



        /// <summary>캡처와 정면 Raycast에 사용할 카메라를 반환한다.</summary>
        private Camera ResolveCamera()
        {
            return _resolveCamera?.Invoke();
        }

        /// <summary>위치·Yaw 추적에 사용할 HMD Transform을 반환한다.</summary>
        private Transform ResolveHead()
        {
            return _resolveHead?.Invoke();
        }

        /// <summary>새 측정을 위해 모든 누적 판정 상태를 초기화한다.</summary>
        private void ResetState()
        {
            _policy.Reset();
            _inputStates.Clear();
            _captureInProgress = false;
            _captureIndex = 0;
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
