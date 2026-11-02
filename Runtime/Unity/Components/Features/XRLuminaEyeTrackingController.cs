using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using XRLumina.Core.Contracts;
using XRLumina.Unity.Components.Client;
using XRLumina.Unity.Contracts;
using XRLumina.Unity.Models;

using Wire = XRLumina.Core.Models;

namespace XRLumina.Unity.Components.Features
{
    /// <summary>
    /// 콘텐츠가 지정한 구간의 시선 기록과 촬영
    /// </summary>
    public sealed class XRLuminaEyeTrackingController : MonoBehaviour
    {
        private readonly List<TimedGazeFrame> _recentGaze = new();
        private readonly List<GazeSampleFrame> _requestGaze = new();

        private int _frameIndex;
        private int _segmentIndex;
        private bool _isStationaryConfirmed;
        private byte[] _stationaryPanorama;
        private Quaternion _panoramaRotation;
        private Vector3 _panoramaPosition;
        private TimedGazeFrame? _segmentStartSample;

        [SerializeField] private LayerMask gazeLayerMask = ~0;
        [SerializeField] private float maxDistance = 100f;
        [SerializeField] private int framesPerSecond = 5;
        [SerializeField] private int panoramaWidth = 2048;
        [SerializeField] private int panoramaHeight = 1024;
        [SerializeField] private int panoramaCubemapSize = 512;

        /// <summary>
        /// 시선 수집에 사용하는 초당 프레임 수 조회
        /// </summary>
        public int FramesPerSecond => framesPerSecond;

        private XRLuminaClientController _rig;
        private bool _warnedMissingMovementSource;
        private IXRLuminaGazeCapturePolicy _capturePolicy;
        private IXRLuminaTriggers _client;
        private Coroutine _recordingCoroutine;

        /// <summary>
        /// 클라이언트 컴포넌트 연결과 측정 상태 이벤트 구독
        /// </summary>
        private void Start()
        {
            _rig = XRLuminaClientController.Instance;
            _client = _rig?.Client;
            if (_client == null)
            {
                Debug.LogError("[XRLumina] Client service is unavailable.", this);
                enabled = false;
                return;
            }

            _rig.MeasurementStarted += StartFeature;
            _rig.MeasurementFinished += FinishFeature;
        }

        /// <summary>
        /// 게임별 시선 캡처 구간 정책을 측정 시작 전에 등록한다.
        /// </summary>
        public void SetCapturePolicy(IXRLuminaGazeCapturePolicy policy)
        {
            if (_client?.IsMeasuring == true)
            {
                throw new InvalidOperationException("Change the gaze capture policy before measurement starts.");
            }
            _capturePolicy = policy ?? throw new ArgumentNullException(nameof(policy));
            ResetState();
        }

        /// <summary>
        /// 아이트래킹 이동 판정에 사용할 플레이어 Transform을 반환한다.
        /// </summary>
        private Transform ResolveMovementSource()
        {
            var source = _rig != null ? _rig.Body : null;
            if (source == null && !_warnedMissingMovementSource)
            {
                Debug.LogWarning("[XRLumina] 아이트래킹 이동 판정용 플레이어 루트가 없습니다. XRLuminaClientController의 Body를 연결하세요.", this);
                _warnedMissingMovementSource = true;
            }
            else if (source != null)
            {
                _warnedMissingMovementSource = false;
            }
            return source;
        }

        /// <summary>
        /// 아이트래킹 측정 시작을 직접 실행
        /// </summary>
        private void StartFeature()
        {
            StopRecording();
            ResetState();
            _recordingCoroutine = StartCoroutine(RecordingLoop());
        }

        /// <summary>
        /// 아이트래킹 측정 종료를 직접 실행
        /// </summary>
        private  void FinishFeature()
        {
            StopRecording();
            ClearRecording();
            StartCoroutine(XRLuminaClientController.WaitForUpload(_client,
                Wire.FlushGroup.EyeTracking, 0f, 60f, null));
        }

        /// <summary>
        /// 컴포넌트 종료 시 이벤트 구독과 수집 자원 정리
        /// </summary>
        private void OnDestroy()
        {
            if (_rig != null)
            {
                _rig.MeasurementStarted -= StartFeature;
                _rig.MeasurementFinished -= FinishFeature;
            }
            StopRecording();
        }

        /// <summary>
        /// 실행 중인 아이트래킹 기록 코루틴을 중지한다.
        /// </summary>
        private void StopRecording()
        {
            if (_recordingCoroutine == null)
            {
                return;
            }
            StopCoroutine(_recordingCoroutine);
            _recordingCoroutine = null;
        }

        /// <summary>
        /// 연속 기록을 종료하고 이미 생성된 구간들의 업로드를 요청한다.
        /// </summary>
        private void ClearRecording()
        {
            _recentGaze.Clear();
            _requestGaze.Clear();
            _isStationaryConfirmed = false;
            _stationaryPanorama = null;
        }

        /// <summary>
        /// 설정된 FPS로 시선을 계속 기록하면서 플레이어 위치의 정지 구간을 판정한다.
        /// </summary>
        internal IEnumerator RecordingLoop()
        {
            var delay = new WaitForSeconds(1f / Mathf.Max(1, framesPerSecond));
            while (true)
            {
                var camera = ResolveCamera();
                if (_client.IsMeasuring && camera != null && _capturePolicy != null)
                {
                    ProcessSample(camera, Time.realtimeSinceStartup);
                }
                yield return delay;
            }
        }

        /// <summary>
        /// 플레이어 위치로 구간을 판정하고 HMD 위치·회전으로 시선과 카메라 자세를 기록한다.
        /// </summary>
        private void ProcessSample(Camera camera, float capturedAt)
        {
            var movementSource = ResolveMovementSource();
            if (movementSource == null)
            {
                return;
            }
            var decision = _capturePolicy.Evaluate(movementSource.position, movementSource.GetInstanceID(),
                capturedAt, _isStationaryConfirmed);
            if (decision.BeginCandidate)
            {
                if (_isStationaryConfirmed)
                {
                    SendConfirmedSegment();
                }
                if (decision.ResetHistory)
                {
                    _recentGaze.Clear();
                }
                BeginStationaryCandidate();
            }
            var cameraTransform = camera.transform;
            var gaze = CreateGazeFrame(_frameIndex++, Time.time, cameraTransform);

            _recentGaze.Add(new TimedGazeFrame(
                capturedAt,
                gaze,
                cameraTransform.position,
                cameraTransform.rotation));
            TrimRecentGaze(decision.HistoryStartedAt);

            if (_isStationaryConfirmed)
            {
                _requestGaze.Add(gaze);
            }
            else if (decision.ConfirmCandidate)
            {
                ConfirmStationarySegment(camera, decision.SegmentStartedAt);
            }
        }

        /// <summary>
        /// 정책이 선택한 다음 후보 구간의 캡처 버퍼를 초기화한다.
        /// </summary>
        private void BeginStationaryCandidate()
        {
            _isStationaryConfirmed = false;
            _stationaryPanorama = null;
            _panoramaRotation = Quaternion.identity;
            _segmentStartSample = null;
            _requestGaze.Clear();
        }

        /// <summary>
        /// 정지 판정 순간 n ms 전부터의 시선을 요청 버퍼에 넣고 보정된 파노라마를 촬영한다.
        /// </summary>
        private void ConfirmStationarySegment(Camera camera, float segmentStartedAt)
        {
            var startSample = FindClosestRecentSample(segmentStartedAt);
            if (!startSample.HasValue)
            {
                return;
            }

            var panoramaPosition = startSample.Value.Position;
            var forward = GetLevelRotation(startSample.Value.Rotation) * Vector3.forward;
            var width = Mathf.Max(1, panoramaWidth);
            var yaw = Mathf.Atan2(forward.x, forward.z);
            var shiftPixels = Mathf.RoundToInt(yaw / (2f * Mathf.PI) * width);
            var panoramaRotation = Quaternion.Euler(0f, shiftPixels * 360f / width, 0f);
            var panorama = CapturePanorama(camera, panoramaPosition, shiftPixels);
            if (panorama == null)
            {
                return;
            }

            _stationaryPanorama = panorama;
            _panoramaRotation = panoramaRotation;
            _panoramaPosition = panoramaPosition;
            _segmentStartSample = startSample;
            _requestGaze.Clear();
            foreach (var sample in _recentGaze)
            {
                if (sample.CapturedAt < segmentStartedAt)
                {
                    continue;
                }
                _requestGaze.Add(sample.Frame);
            }
            _isStationaryConfirmed = _requestGaze.Count > 0;
        }

        /// <summary>
        /// n ms 전 시각에 가장 가까운 시선·카메라 자세 샘플을 반환한다.
        /// </summary>
        private TimedGazeFrame? FindClosestRecentSample(float capturedAt)
        {
            if (_recentGaze.Count == 0)
            {
                return null;
            }

            var closest = _recentGaze[0];
            var closestDistance = Mathf.Abs(closest.CapturedAt - capturedAt);
            for (var index = 1; index < _recentGaze.Count; index++)
            {
                var candidate = _recentGaze[index];
                var distance = Mathf.Abs(candidate.CapturedAt - capturedAt);
                if (distance >= closestDistance)
                {
                    continue;
                }
                closest = candidate;
                closestDistance = distance;
            }
            return closest;
        }

        /// <summary>
        /// 회전에서 좌우 방향만 남겨 수평 정면 회전으로 변환한다.
        /// </summary>
        private static Quaternion GetLevelRotation(Quaternion rotation)
        {
            var forward = rotation * Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }
            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        /// <summary>
        /// 정지 판정 순간 n ms 전부터 움직이기 직전까지 확정된 한 세트를 전송한다.
        /// </summary>
        private void SendConfirmedSegment()
        {
            if (_requestGaze.Count == 0 ||
                _stationaryPanorama == null ||
                !_segmentStartSample.HasValue)
            {
                return;
            }

            var suffix = _segmentIndex.ToString("D3");
            SendGaze($"gaze_frames_{suffix}.json", _requestGaze);
            SendCapture($"capture_image_{suffix}.png", _stationaryPanorama);

            // 전송 시선은 캡처 기준 좌표이므로 카메라도 같은 좌표계의 원점·항등 회전을 사용한다.
            SendCameraPose($"camera_pose_{suffix}.json", Vector3.zero, Quaternion.identity);
            _segmentIndex++;
            _requestGaze.Clear();
        }

        /// <summary>
        /// 구간 시작 위치에서 파노라마를 촬영하고 정면이 중앙에 오도록 픽셀을 순환 이동한다.
        /// </summary>
        private byte[] CapturePanorama(Camera source, Vector3 position, int shiftPixels)
        {
            if (source == null)
            {
                return null;
            }

            var cubemapSize = Mathf.Max(1, panoramaCubemapSize);
            var width = Mathf.Max(1, panoramaWidth);
            var height = Mathf.Max(1, panoramaHeight);
            var cubemap = new RenderTexture(
                cubemapSize,
                cubemapSize,
                24,
                RenderTextureFormat.ARGB32)
            {
                dimension = UnityEngine.Rendering.TextureDimension.Cube,
            };
            var panorama = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var captureObject = new GameObject("XRLumina Panorama Camera")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            var captureCamera = captureObject.AddComponent<Camera>();
            captureCamera.CopyFrom(source);
            captureCamera.enabled = false;
            captureCamera.stereoTargetEye = StereoTargetEyeMask.None;
            captureCamera.targetTexture = null;
            captureObject.transform.SetPositionAndRotation(
                position,
                Quaternion.identity);
            var previousActive = RenderTexture.active;
            try
            {
                cubemap.Create();
                panorama.Create();
                if (!captureCamera.RenderToCubemap(cubemap))
                {
                    return null;
                }
                cubemap.ConvertToEquirect(
                    panorama,
                    Camera.MonoOrStereoscopicEye.Mono);
                RenderTexture.active = panorama;
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                var pixels = texture.GetPixels32();
                texture.SetPixels32(ShiftPanoramaRows(pixels, width, height, shiftPixels));
                texture.Apply(false, false);
                return texture.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previousActive;
                cubemap.Release();
                panorama.Release();
                UnityEngine.Object.Destroy(cubemap);
                UnityEngine.Object.Destroy(panorama);
                UnityEngine.Object.Destroy(texture);
                UnityEngine.Object.Destroy(captureObject);
            }
        }

        /// <summary>
        /// 각 행을 왼쪽으로 순환 이동하고 경계를 넘은 픽셀을 같은 행의 반대편에 붙인다.
        /// </summary>
        private static Color32[] ShiftPanoramaRows(Color32[] pixels, int width, int height, int shiftPixels)
        {
            if (pixels == null || width <= 0 || height <= 0 || (long)width * height != pixels.Length)
            {
                throw new ArgumentException("파노라마 크기와 픽셀 배열 길이가 일치해야 합니다.");
            }

            var shift = ((shiftPixels % width) + width) % width;
            if (shift == 0)
            {
                return pixels;
            }

            var shifted = new Color32[pixels.Length];
            for (var row = 0; row < height; row++)
            {
                var offset = row * width;
                Array.Copy(pixels, offset + shift, shifted, offset, width - shift);
                Array.Copy(pixels, offset, shifted, offset + width - shift, shift);
            }
            return shifted;
        }

        /// <summary>
        /// 정지 판정 순간 n ms 전의 시선과 자세를 복원할 최근 기록만 유지한다.
        /// </summary>
        private void TrimRecentGaze(float oldestAllowed)
        {
            var removeCount = 0;
            while (removeCount < _recentGaze.Count &&
                   _recentGaze[removeCount].CapturedAt < oldestAllowed)
            {
                removeCount++;
            }
            if (removeCount > 0)
            {
                _recentGaze.RemoveRange(0, removeCount);
            }
        }

        /// <summary>
        /// 시선 레이캐스트 결과를 전송 가능한 프레임으로 생성한다.
        /// </summary>
        private GazeSampleFrame CreateGazeFrame(int frame, float time, Transform source)
        {
            var origin = source.position;
            var direction = source.forward;
            var hitPosition = Physics.Raycast(
                origin,
                direction,
                out var hit,
                maxDistance,
                gazeLayerMask)
                ? hit.point
                : origin + direction * maxDistance;

            return new GazeSampleFrame(frame, time, new[] { hitPosition });
        }

        /// <summary>
        /// 클라이언트에 연결된 HMD 카메라를 시선 기록 대상으로 반환한다.
        /// </summary>
        private Camera ResolveCamera()
        {
            return _rig != null ? _rig.HeadCamera : null;
        }

        /// <summary>
        /// 새 세션 기록을 위해 런타임 상태와 버퍼를 초기화한다.
        /// </summary>
        private void ResetState()
        {
            _recentGaze.Clear();
            _requestGaze.Clear();
            _capturePolicy?.Reset();
            _frameIndex = 0;
            _segmentIndex = 0;
            _isStationaryConfirmed = false;
            _stationaryPanorama = null;
            _panoramaRotation = Quaternion.identity;
            _segmentStartSample = null;
        }

        /// <summary>
        /// 시선 충돌 지점을 이미지와 동일한 캡처 기준 좌표로 변환해 전송한다.
        /// </summary>
        private void SendGaze(string fileName, IReadOnlyList<GazeSampleFrame> frames)
        {
            if (frames == null)
            {
                return;
            }

            var worldToCapture = Quaternion.Inverse(_panoramaRotation);
            var items = new List<Wire.GazeFrame>(frames.Count);
            foreach (var frame in frames)
            {
                var points = new List<Wire.Vector3>();
                if (frame.Points != null)
                {
                    foreach (var point in frame.Points)
                    {
                        var capturePoint = worldToCapture * (point - _panoramaPosition);
                        points.Add(new Wire.Vector3 { X = capturePoint.x, Y = capturePoint.y, Z = capturePoint.z, });
                    }
                }
                items.Add(new Wire.GazeFrame { Frame = frame.Frame, Time = frame.Time, Points = points, });
            }
            XRLumina.Core.API.XRLuminaClient.RequireSubmitted(_client.SendGaze(new Wire.Gaze { Name = fileName, Items = items, }));
        }

        /// <summary>
        /// PNG 파일을 단일 메시지로 전송하고 스트림의 분할 처리를 사용한다.
        /// </summary>
        private void SendCapture(string fileName, byte[] bytes)
        {
            if (bytes != null)
            {
                XRLumina.Core.API.XRLuminaClient.RequireSubmitted(_client.SendCapture(fileName, bytes));
            }
        }

        /// <summary>
        /// 카메라 위치와 회전을 스키마 필드로 전송한다.
        /// </summary>
        private void SendCameraPose(string fileName, Vector3 position, Quaternion rotation)
        {
            XRLumina.Core.API.XRLuminaClient.RequireSubmitted(_client.SendCameraPose(new Wire.CameraPose
            {
                Name = fileName,
                Position = new Wire.Vector3 { X = position.x, Y = position.y, Z = position.z, },
                Rotation = new Wire.Quaternion { X = rotation.x, Y = rotation.y, Z = rotation.z, W = rotation.w, },
            }));
        }
    }
}
