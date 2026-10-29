using XRLumina.Core.Contracts;
using XRLumina.Unity.Contracts;
using UnityEngine;
using XRLumina.Unity.Models;
using XRLumina.Unity.Services;
using XRLumina.Unity.Policies;
using System;

namespace XRLumina.Unity.Controllers
{
    /// <summary>인스펙터의 상호작용 설정을 런타임 서비스에 연결하는 SDK 컴포넌트.</summary>
    public sealed class XRLuminaInteractionController : MonoBehaviour
    {
        public static XRLuminaInteractionController Instance { get; private set; }

        [SerializeField] private int framesPerSecond = 5;
        [SerializeField] private float joystickDeadzone = 0.5f;
        [SerializeField] private float stationaryDurationSeconds = 3f;
        [SerializeField] private float stationaryMovementThreshold = 0.15f;
        [SerializeField] private float yawWindowSeconds = 5f;
        [SerializeField] private int yawRequiredReversals = 3;
        [SerializeField] private float yawMinimumSwingDegrees = 30f;
        [SerializeField] private float interactionWindowSeconds = 5f;
        [SerializeField] private int interactionRequiredCount = 3;
        [SerializeField] private LayerMask interactionLayerMask = ~0;
        [SerializeField] private float interactionMaxDistance = 100f;
        [SerializeField] private int heuristicCaptureWidth = 1280;
        [SerializeField] private int heuristicCaptureHeight = 720;

        private IXRLuminaHeuristicCapturePolicy _capturePolicy;
        private InteractionService _service;
        private HeuristicScreenshotService _heuristicScreenshotService;
        private XRLuminaClientController _rig;
        private IXRLuminaTriggers _client;
        private Coroutine _trackingCoroutine;

        /// <summary>현재 상호작용 컴포넌트를 SDK 접근점으로 등록한다.</summary>
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        /// <summary>클라이언트 서비스에 연결하고 측정 상태 이벤트를 구독한다.</summary>
        private void Start()
        {
            var client = XRLuminaClientController.Instance;
            _rig = client;
            _client = client?.Service;
            if (_client == null)
            {
                Debug.LogError("[XRLumina] Client service is unavailable.", this);
                enabled = false;
                return;
            }
            _service = new InteractionService(
                _client,
                () => client.Head,
                () => client.Body,
                framesPerSecond,
                joystickDeadzone);
            _heuristicScreenshotService = new HeuristicScreenshotService(
                _client,
                () => client.HeadCamera,
                () => client.Head,
                _capturePolicy ?? new DefaultHeuristicCapturePolicy(stationaryDurationSeconds,
                    stationaryMovementThreshold, yawWindowSeconds, yawRequiredReversals,
                    yawMinimumSwingDegrees, interactionWindowSeconds, interactionRequiredCount),
                interactionLayerMask,
                interactionMaxDistance,
                heuristicCaptureWidth,
                heuristicCaptureHeight);
            _rig.MeasurementStarted += StartFeature;
            _rig.MeasurementFinished += FinishFeature;
        }

        /// <summary>측정 시작 전에 게임별 휴리스틱 캡처 조건을 등록한다. 미등록 시 Inspector 기본 정책을 사용한다.</summary>
        public void SetCapturePolicy(IXRLuminaHeuristicCapturePolicy policy)
        {
            if (_client?.IsMeasuring == true)
            {
                throw new InvalidOperationException("Change the heuristic capture policy before measurement starts.");
            }
            _capturePolicy = policy ?? throw new ArgumentNullException(nameof(policy));
            _heuristicScreenshotService?.SetPolicy(policy);
        }

        /// <summary>상호작용 측정 시작을 서비스에 전달한다.</summary>
        private void StartFeature()
        {
            StopTracking();
            _heuristicScreenshotService?.StartFeature();
            var routine = _service?.StartFeature();
            if (routine != null)
            {
                _trackingCoroutine = StartCoroutine(routine);
            }
        }

        /// <summary>상호작용 측정 종료를 서비스에 전달한다.</summary>
        private void FinishFeature()
        {
            _service?.FinishFeature();
            StopTracking();
            if (_service != null)
            {
                StartCoroutine(_service.Flush(framesPerSecond, 60f, null));
            }
            if (_heuristicScreenshotService != null)
            {
                StartCoroutine(_heuristicScreenshotService.FinishAndFlush(60f, null));
            }
        }

        /// <summary>Unity 프레임 갱신을 서비스에 전달한다.</summary>
        private void Update()
        {
            _service?.Tick();
            var trigger = _heuristicScreenshotService?.Tick();
            if (trigger.HasValue)
            {
                StartCoroutine(_heuristicScreenshotService.CaptureAndSend(trigger.Value));
            }
        }

        /// <summary>지정 종류의 상호작용 이벤트 기록을 서비스에 전달한다.</summary>
        public void RecordInteractionEvent(XRLumina.Core.API.EventKind type)
        {
            if (_client?.IsMeasuring != true)
            {
                return;
            }

            _service?.RecordEvent(type);
        }

        /// <summary>대상 오브젝트와 Controller 입력을 포함한 상호작용 이벤트를 기록한다.</summary>
        public void RecordInteractionEvent(
            XRLumina.Core.API.EventKind type,
            GameObject target,
            XRLumina.Core.API.ControllerInput controllerInput,
            XRLumina.Core.API.ControllerHand hand = XRLumina.Core.API.ControllerHand.Unknown)
        {
            if (_client?.IsMeasuring != true)
            {
                return;
            }

            _service?.RecordEvent(type);
            var trigger = _heuristicScreenshotService?.RecordInteraction(
                target,
                controllerInput,
                Time.realtimeSinceStartup, hand);
            if (trigger.HasValue)
            {
                StartCoroutine(_heuristicScreenshotService.CaptureAndSend(trigger.Value));
            }
        }

        /// <summary>포즈 추적 종료를 서비스에 전달한다.</summary>
        private void StopTracking()
        {
            if (_trackingCoroutine != null)
            {
                StopCoroutine(_trackingCoroutine);
                _trackingCoroutine = null;
            }
        }

        /// <summary>컴포넌트가 제거될 때 서비스 자원을 정리한다.</summary>
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
    }
}
