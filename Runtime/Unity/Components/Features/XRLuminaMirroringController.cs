using System.Collections;

using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

using XRLumina.Core.Contracts;
using XRLumina.Unity.Components.Client;

namespace XRLumina.Unity.Components.Features
{
    /// <summary>
    /// 클라이언트 카메라 미러링
    /// </summary>
    public sealed class XRLuminaMirroringController : MonoBehaviour
    {
        private RenderTexture _renderTexture;
        private int _bufferWidth;
        private int _bufferHeight;
        private bool _readbackPending;
        private bool _isStreaming;
        private int _streamGeneration;

        [SerializeField] private bool streamEnabled = true;
        [SerializeField] private int width = 640;
        [SerializeField] private int height = 480;
        [SerializeField] private int framesPerSecond = 20;
        [SerializeField, Range(1, 100)] private int jpegQuality = 50;

        private XRLuminaClientController _rig;
        private IXRLuminaTriggers _client;
        private Coroutine _streamCoroutine;

        /// <summary>
        /// 클라이언트 컴포넌트 연결과 미러링 상태 이벤트 구독
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

            _rig.SessionAccepted += StartStreaming;
            _rig.MeasurementStarted += StartStreaming;
            _rig.MeasurementFinished += StopStreaming;
            _rig.Disconnected += StopStreaming;
        }

        /// <summary>
        /// 미러링 스트림 시작을 직접 실행
        /// </summary>
        private void StartStreaming()
        {
            if (_streamCoroutine != null || !BeginStreaming())
            {
                return;
            }
            _streamCoroutine = StartCoroutine(StreamLoop());
        }

        /// <summary>
        /// 미러링 스트림 종료를 직접 실행
        /// </summary>
        private void StopStreaming()
        {
            if (_streamCoroutine != null)
            {
                StopCoroutine(_streamCoroutine);
                _streamCoroutine = null;
            }
            EndStreaming();
        }

        /// <summary>
        /// 컴포넌트 종료 시 이벤트 구독과 수집 자원 정리
        /// </summary>
        private void OnDestroy()
        {
            if (_rig != null)
            {
                _rig.SessionAccepted -= StartStreaming;
                _rig.MeasurementStarted -= StartStreaming;
                _rig.MeasurementFinished -= StopStreaming;
                _rig.Disconnected -= StopStreaming;
            }
            StopStreaming();
        }

        /// <summary>
        /// 프레임 생산을 시작한다. 이미 실행 중이면 중복 시작하지 않는다.
        /// </summary>
        private bool BeginStreaming()
        {
            if (!streamEnabled || _isStreaming)
            {
                return false;
            }
            _streamGeneration++;
            _isStreaming = true;
            return true;
        }

        /// <summary>
        /// 프레임 생산을 중단하고 캡처 버퍼를 해제한다.
        /// </summary>
        private void EndStreaming()
        {
            _streamGeneration++;
            _isStreaming = false;
            ReleaseBuffers();
            _readbackPending = false;
        }

        /// <summary>
        /// 설정된 주기에 맞춰 매 프레임 캡처를 요청한다.
        /// </summary>
        internal IEnumerator StreamLoop()
        {
            var delay = new WaitForSeconds(1f / Mathf.Max(1, framesPerSecond));
            while (true)
            {
                yield return new WaitForEndOfFrame();
                CaptureFrame();
                yield return delay;
            }
        }

        /// <summary>
        /// 메인 스레드를 대기시키지 않고 GPU에서 프레임을 비동기로 읽는다.
        /// </summary>
        private void CaptureFrame()
        {
            var camera = ResolveCamera();
            if (!_isStreaming || camera == null || _client == null || _readbackPending)
            {
                return;
            }

            var targetWidth = Mathf.Max(1, width);
            var targetHeight = Mathf.Max(1, height);
            EnsureBuffers(targetWidth, targetHeight);
            var previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = _renderTexture;
                camera.Render();
                _readbackPending = true;
                var generation = _streamGeneration;
                AsyncGPUReadback.Request(
                    _renderTexture,
                    0,
                    TextureFormat.RGB24,
                    request => OnReadbackComplete(request, generation)
                );
            }
            finally
            {
                camera.targetTexture = previousTarget;
            }
        }

        /// <summary>
        /// GPU 읽기 결과를 이미지로 인코딩해 등록된 소비자에게 전달한다.
        /// </summary>
        private void OnReadbackComplete(AsyncGPUReadbackRequest request, int generation)
        {
            if (generation != _streamGeneration)
            {
                return;
            }
            _readbackPending = false;
            if (!_isStreaming || request.hasError || _client == null)
            {
                return;
            }

            NativeArray<byte> encoded = ImageConversion.EncodeNativeArrayToJPG(
                request.GetData<byte>(),
                GraphicsFormat.R8G8B8_UNorm,
                (uint)_bufferWidth,
                (uint)_bufferHeight,
                0,
                Mathf.Clamp(jpegQuality, 1, 100)
            );
            try
            {
                _client.SendMirrorFrame(encoded.ToArray());
            }
            finally
            {
                encoded.Dispose();
            }
        }

        /// <summary>
        /// 현재 해상도에 맞는 렌더링 및 인코딩 버퍼를 준비한다.
        /// </summary>
        private void EnsureBuffers(int targetWidth, int targetHeight)
        {
            if (_renderTexture != null &&
                _bufferWidth == targetWidth && _bufferHeight == targetHeight)
            {
                return;
            }

            ReleaseBuffers();
            _bufferWidth = targetWidth;
            _bufferHeight = targetHeight;
            _renderTexture = new RenderTexture(targetWidth, targetHeight, 24, RenderTextureFormat.ARGB32);
        }

        /// <summary>
        /// 생성된 렌더링 및 인코딩 버퍼를 해제한다.
        /// </summary>
        private void ReleaseBuffers()
        {
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                UnityEngine.Object.Destroy(_renderTexture);
                _renderTexture = null;
            }
            _bufferWidth = 0;
            _bufferHeight = 0;
        }

        /// <summary>
        /// 클라이언트에 연결된 활성 HMD 카메라를 캡처 대상으로 반환한다.
        /// </summary>
        private Camera ResolveCamera()
        {
            var camera = _rig != null ? _rig.HeadCamera : null;
            return camera != null && camera.isActiveAndEnabled ? camera : null;
        }
    }
}
