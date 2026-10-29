using System;
using System.Collections;

using UnityEngine;

using XRLumina.Core.API;
using XRLumina.Core.Contracts;
using XRLumina.Core.Models;

namespace XRLumina.Unity.Components.Client
{
    /// <summary>
    /// Unity 생명주기와 공용 코어 연결 관리
    /// </summary>
    public sealed class XRLuminaClientController : MonoBehaviour, IXRLuminaCallbacks
    {
        public static XRLuminaClientController Instance { get; private set; }

        public event Action<TestAction> OnCommand;
        public event Action<string, bool, string> UploadCompleted;
        public event Action SessionReady;
        internal event Action SessionAccepted;
        internal event Action Disconnected;
        internal event Action MeasurementStarted;
        internal event Action MeasurementFinished;

        [SerializeField] private Transform head;
        [SerializeField] private Transform body;
        [SerializeField] private int retryDelayMs = 1500;
        [SerializeField] private bool usbEnabled = true;
        [SerializeField] private int usbPort = 10000;
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _usbBridge;
#endif

        private readonly string _host = "";
        private readonly int _port = 9999;
        private readonly int _discoveryPort = 9998;
        private IXRLuminaTriggers _client;

        public Transform Head => head;
        public Transform Body => body;
        public Camera HeadCamera => head != null ? head.GetComponent<Camera>() : null;

        public SessionData Session => _client?.Session;
        public bool IsAuthenticated => _client?.IsAuthenticated ?? false;
        public bool IsMeasuring => _client?.IsMeasuring ?? false;
        public IXRLuminaTriggers Client => _client;

        /// <summary>
        /// 인스펙터 연결 설정으로 코어 클라이언트 생성
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            DontDestroyOnLoad(gameObject);

            _client = XRLuminaClient.Create(new XRLuminaClientOptions
            {
                ClientId = SystemInfo.deviceUniqueIdentifier,
                Model = SystemInfo.deviceModel,
                Name = SystemInfo.deviceName,
                Host = _host, Port = _port, DiscoveryPort = _discoveryPort,
                RetryDelayMs = retryDelayMs,
                UsbEnabled = usbEnabled, UsbPort = usbPort,
            }, this);
        }

        /// <summary>
        /// SDK 연결 시작을 직접 실행
        /// </summary>
        private IEnumerator Start()
        {
            yield return null;
            _client?.Start();
#if UNITY_ANDROID && !UNITY_EDITOR
            if (usbEnabled)
            {
                try
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    {
                        _usbBridge = new AndroidJavaObject("com.ninefive.xrlumina.UsbAccessoryBridge", activity, usbPort);
                        _usbBridge.Call("start");
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[XRLumina.Usb] USB 초기화 실패: {exception.Message}");
                }
            }
#endif
        }

        /// <summary>
        /// Unity 프레임 갱신을 직접 실행
        /// </summary>
        private void Update()
        {
            _client?.Tick(Time.unscaledDeltaTime);
        }

        /// <summary>
        /// SDK의 시선·캡처·머리 추적에 사용할 HMD Transform을 설정한다.
        /// </summary>
        public void SetHead(Transform target)
        {
            head = target;
        }

        /// <summary>
        /// SDK의 이동 판정·몸 추적에 사용할 플레이어 루트를 설정한다.
        /// </summary>
        public void SetBody(Transform target)
        {
            body = target;
        }

        /// <summary>
        /// Electron 호스트에 테스트 시작을 요청한다.
        /// </summary>
        public bool RequestStart()
        {
            return _client?.RequestStart() ?? false;
        }

        /// <summary>
        /// Electron 호스트에 테스트 종료를 요청한다.
        /// </summary>
        public bool RequestFinish()
        {
            return _client?.RequestFinish() ?? false;
        }

        /// <summary>
        /// 코어 명령 콜백을 컴포넌트 이벤트로 전달
        /// </summary>
        void IXRLuminaCallbacks.OnCommand(TestAction action)
        {
            OnCommand?.Invoke(action);
        }

        /// <summary>
        /// 코어 세션 콜백을 컴포넌트 이벤트로 전달
        /// </summary>
        void IXRLuminaCallbacks.OnSessionReady(SessionData session)
        {
            SessionReady?.Invoke();
        }

        /// <summary>
        /// 코어가 Tick에서 전달한 통신 오류를 Unity 로그에 기록한다.
        /// </summary>
        void IXRLuminaCallbacks.OnFault(Exception exception)
        {
            Debug.LogWarning($"[XRLumina] 통신 오류: {exception.Message}");
        }

        /// <summary>
        /// 세션 수락 콜백을 Unity 기능에 전달한다.
        /// </summary>
        void IXRLuminaCallbacks.OnSessionAccepted(SessionData session)
        {
            SessionAccepted?.Invoke();
        }

        /// <summary>
        /// 연결 해제 콜백을 Unity 기능에 전달한다.
        /// </summary>
        void IXRLuminaCallbacks.OnDisconnected()
        {
            Disconnected?.Invoke();
        }

        /// <summary>
        /// 측정 시작 콜백을 Unity 기능에 전달한다.
        /// </summary>
        void IXRLuminaCallbacks.OnMeasurementStarted()
        {
            MeasurementStarted?.Invoke();
        }

        /// <summary>
        /// 측정 종료 콜백을 Unity 기능에 전달한다.
        /// </summary>
        void IXRLuminaCallbacks.OnMeasurementFinished()
        {
            MeasurementFinished?.Invoke();
        }

        /// <summary>
        /// 업로드 완료 콜백을 컴포넌트 이벤트로 전달
        /// </summary>
        void IXRLuminaCallbacks.OnFlushCompleted(string id, bool ok, string error)
        {
            UploadCompleted?.Invoke(id, ok, error);
        }

        /// <summary>
        /// 공용 코어 완료 결과와 Unity 코루틴 대기 연결
        /// </summary>
        internal static IEnumerator WaitForUpload(IXRLuminaTriggers client, FlushGroup group,
            float intervalSeconds, float timeoutSeconds, Action<bool, string> onResult)
        {
            var pending = client.FlushAsync(group, intervalSeconds, timeoutSeconds);
            while (!pending.IsCompleted)
            {
                yield return null;
            }
            var result = pending.GetAwaiter().GetResult();
            onResult?.Invoke(result.IsSuccess, result.Error);
        }

        /// <summary>
        /// 컴포넌트 종료 시 코어와 USB 연결 자원 정리
        /// </summary>
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            if (_client == null)
            {
                return;
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                _usbBridge?.Call("stop");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[XRLumina.Usb] USB 종료 오류: {exception.Message}");
            }
            finally
            {
                _usbBridge?.Dispose();
                _usbBridge = null;
            }
#endif
            _client.Dispose();
            _client = null;
        }
    }
}
