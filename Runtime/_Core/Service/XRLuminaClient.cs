using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using XRLumina.Model;
using XRLumina._Core.Infrastructure;
using XRLumina._Core.Messaging.Senders;
using XRLumina._Core.Model;
namespace XRLumina.Client
{
    /// <summary>
    /// 엔진 호출과 네이티브 공용 코어를 연결하는 관리형 브릿지
    /// </summary>
    public sealed class XRLuminaClient : IDisposable
    {
        private IntPtr _native;
        private GCHandle _self;
        private static readonly NativeMethods.Callback Callback = Receive;
        private readonly int _owner = Thread.CurrentThread.ManagedThreadId;
        private readonly Dictionary<string, Action<bool, string>> _flushes = new();
        private readonly List<Exception> _callbackErrors = new();
        private bool _inTick;
        private bool _disposeRequested;
        private bool _running, _authenticated, _measuring;
        internal MessageSender MessageSender { get; }
        public event Action<TestAction> CommandReceived;
        public event Action SessionAccepted;
        public event Action Disconnected;
        public event Action MeasurementStarted;
        public event Action MeasurementFinished;
        public event Action SessionReady;
        public event Action<Exception> Faulted;
        public XRLuminaSession Session { get; private set; }
        public bool IsAuthenticated => _authenticated;
        public bool IsMeasuring => _measuring;
        public bool IsRunning => _running;

        /// <summary>
        /// 엔진 설정을 복사한 네이티브 클라이언트 생성
        /// </summary>
        public XRLuminaClient(XRLuminaClientOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            if (string.IsNullOrWhiteSpace(options.ClientId) || options.Port < 1 || options.Port > 65535 ||
                options.DiscoveryPort < 1 || options.DiscoveryPort > 65535 || options.UsbPort < 1 ||
                options.UsbPort > 65535 || options.RetryDelayMs < 1)
            {
                throw new ArgumentException("Invalid client options.", nameof(options));
            }
            var native = new NativeMethods.Options {
                Version = 1,
                ClientId = options.ClientId,
                Model = options.Model,
                Name = options.Name,
                Host = options.Host,
                Port = (ushort)options.Port,
                DiscoveryPort = (ushort)options.DiscoveryPort,
                UsbPort = (ushort)options.UsbPort,
                UsbEnabled = options.UsbEnabled ? (byte)1 : (byte)0,
                RetryDelayMs = (uint)options.RetryDelayMs,
            };
            _self = GCHandle.Alloc(this);
            try
            {
                NativeMethods.Check(NativeMethods.Create(ref native, Callback, GCHandle.ToIntPtr(_self), out _native));
            }
            catch
            {
                _self.Free();
                throw;
            }
            MessageSender = new MessageSender(this);
        }
        /// <summary>
        /// 생성 스레드와 유효한 네이티브 핸들 검증
        /// </summary>
        private void VerifyAccess()
        {
            if (Thread.CurrentThread.ManagedThreadId != _owner)
            {
                throw new InvalidOperationException("Call on the creating engine thread.");
            }
            if (_native == IntPtr.Zero || _disposeRequested)
            {
                throw new ObjectDisposedException(nameof(XRLuminaClient));
            }
        }
        /// <summary>
        /// 코어 실행과 연결 시작
        /// </summary>
        public void Start()
        {
            VerifyAccess();
            NativeMethods.Check(NativeMethods.Start(_native));
            RefreshState();
        }
        /// <summary>
        /// 엔진 갱신 시 네이티브 수신 처리와 완료 이벤트 전달
        /// </summary>
        public void Tick(double deltaSeconds)
        {
            VerifyAccess();
            if (_inTick)
            {
                throw new InvalidOperationException("Recursive Tick is not allowed.");
            }
            _inTick = true;
            try
            {
                NativeMethods.Check(NativeMethods.Tick(_native, deltaSeconds));
                RefreshState();
            }
            finally
            {
                _inTick = false;
                if (_disposeRequested)
                {
                    Release();
                }
            }
            var errors = _callbackErrors.ToArray();
            _callbackErrors.Clear();
            foreach (var error in errors)
            {
                Faulted?.Invoke(error);
            }
        }
        /// <summary>
        /// 연결 종료와 요청 취소 예약
        /// </summary>
        public void Stop()
        {
            VerifyAccess();
            NativeMethods.Check(NativeMethods.Stop(_native));
            Session = null;
            RefreshState();
        }
        /// <summary>
        /// 측정 시작 요청
        /// </summary>
        public bool RequestStart()
        {
            VerifyAccess();
            return NativeMethods.Check(NativeMethods.Request(_native, (uint)TestAction.Start));
        }
        /// <summary>
        /// 측정 종료 요청
        /// </summary>
        public bool RequestFinish()
        {
            VerifyAccess();
            return NativeMethods.Check(NativeMethods.Request(_native, (uint)TestAction.Finish));
        }
        /// <summary>
        /// 자세 데이터의 네이티브 전송
        /// </summary>
        public bool SendTracking(Tracking value)
        {
            VerifyAccess();
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            var native = new NativeMethods.Tracking {
                Timestamp = value.Timestamp,
                Head = value.Head,
                Body = value.Body,
            };
            return NativeMethods.Check(NativeMethods.SendTracking(_native, ref native));
        }
        /// <summary>
        /// 상호작용 집계의 네이티브 전송
        /// </summary>
        public bool SendInteractionEvent(InteractionEvent value)
        {
            VerifyAccess();
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            var native = new NativeMethods.Interaction {
                Type = (uint)value.Type,     Operation = (uint)value.OpName, Count = value.Count,
                Timestamp = value.Timestamp, Position = value.Position,      HasY = value.HasY ? 1u : 0u,
            };
            return NativeMethods.Check(NativeMethods.SendInteraction(_native, ref native));
        }
        /// <summary>
        /// 공간 메쉬 배열의 네이티브 전송
        /// </summary>
        public bool SendMesh(Mesh value)
        {
            VerifyAccess();
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            var vertices = value.Vertices.ToArray();
            var indices = value.Indices.ToArray();
            return NativeMethods.Check(
                NativeMethods.SendMesh(_native, vertices, (UIntPtr)vertices.Length, indices, (UIntPtr)indices.Length));
        }
        /// <summary>
        /// 시선 좌표를 호출 동안 고정한 네이티브 전송
        /// </summary>
        public bool SendGaze(Gaze value)
        {
            VerifyAccess();
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            var pins = new List<GCHandle>();
            var frames = new NativeMethods.GazeFrame[value.Items.Count];
            try
            {
                for (int index = 0; index < frames.Length; index++)
                {
                    var item = value.Items[index];
                    var points = item.Points.ToArray();
                    var pin = GCHandle.Alloc(points, GCHandleType.Pinned);
                    pins.Add(pin);
                    frames[index] = new NativeMethods.GazeFrame {
                        Frame = item.Frame,
                        Time = item.Time,
                        Points = pin.AddrOfPinnedObject(),
                        Count = (UIntPtr)points.Length,
                    };
                }
                return NativeMethods.Check(NativeMethods.SendGaze(_native, value.Name, frames, (UIntPtr)frames.Length));
            }
            finally
            {
                foreach (var pin in pins)
                {
                    pin.Free();
                }
            }
        }
        /// <summary>
        /// 카메라 위치와 회전의 네이티브 전송
        /// </summary>
        public bool SendCameraPose(CameraPose value)
        {
            VerifyAccess();
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            return NativeMethods.Check(
                NativeMethods.SendCameraPose(_native, value.Name, value.Position, value.Rotation));
        }
        /// <summary>
        /// 미러링 이미지의 네이티브 전송
        /// </summary>
        public bool SendMirrorFrame(byte[] bytes)
        {
            VerifyAccess();
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }
            return NativeMethods.Check(
                NativeMethods.SendMirror(_native, bytes, (UIntPtr)bytes.Length, (uint)ImageFormat.Jpeg));
        }
        /// <summary>
        /// 측정 캡처 이미지의 네이티브 전송
        /// </summary>
        public bool SendCapture(string name, byte[] bytes)
        {
            VerifyAccess();
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }
            return NativeMethods.Check(NativeMethods.SendCapture(_native, name, bytes, (UIntPtr)bytes.Length));
        }
        /// <summary>
        /// 휴리스틱 입력 정보와 이미지의 네이티브 전송
        /// </summary>
        internal bool SendScreenshot(string name, HeuristicScreenshotTrigger trigger, byte[] bytes)
        {
            VerifyAccess();
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }
            var pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                var native = new NativeMethods.Screenshot {
                    Name = name,
                    ObjectName = trigger.ObjectName,
                    Trigger = (uint)trigger.Type,
                    CapturedAt = trigger.CapturedAt,
                    Input = (uint)trigger.ControllerInput,
                    Hand = (uint)trigger.Hand,
                    Data = pin.AddrOfPinnedObject(),
                    Size = (UIntPtr)bytes.Length,
                };
                return NativeMethods.Check(NativeMethods.SendScreenshot(_native, ref native));
            }
            finally
            {
                pin.Free();
            }
        }
        /// <summary>
        /// Flush 요청 식별자와 Tick 완료 콜백 연결
        /// </summary>
        public string Flush(FlushGroup group, float interval, Action<bool, string> onResult, double timeoutSeconds = 10)
        {
            VerifyAccess();
            var bytes = new byte[128];
            NativeMethods.Check(
                NativeMethods.Flush(_native, (uint)group, interval, timeoutSeconds, bytes, (UIntPtr)bytes.Length));
            var id = Encoding.UTF8.GetString(bytes, 0, Array.IndexOf(bytes, (byte)0));
            _flushes.Add(id, onResult);
            return id;
        }
        /// <summary>
        /// 네이티브 상태를 관리형 조회 값에 반영
        /// </summary>
        private void RefreshState()
        {
            if (_native == IntPtr.Zero)
            {
                return;
            }
            NativeMethods.Check(
                NativeMethods.State(_native, out var running, out var authenticated, out var measuring));
            _running = running != 0;
            _authenticated = authenticated != 0;
            _measuring = measuring != 0;
        }
        /// <summary>
        /// 예외가 네이티브 경계를 넘지 않는 이벤트 전달
        /// </summary>
#if ENABLE_IL2CPP
        [AOT.MonoPInvokeCallback(typeof(NativeMethods.Callback))]
#endif
        private static void Receive(IntPtr user, ref NativeMethods.Event value)
        {
            var client = (XRLuminaClient)GCHandle.FromIntPtr(user).Target;
            if (client._disposeRequested)
            {
                return;
            }
            try
            {
                client.Dispatch(value);
            }
            catch (Exception error)
            {
                client._callbackErrors.Add(error);
            }
        }
        /// <summary>
        /// Tick 이벤트를 SDK의 공개 이벤트로 변환
        /// </summary>
        private void Dispatch(NativeMethods.Event value)
        {
            RefreshState();
            switch (value.Type)
            {
            case 0:
                Session = new XRLuminaSession {
                    uuid = NativeMethods.Text(value.SessionUuid),
                    seq = value.Seq,
                };
                SessionReady?.Invoke();
                break;
            case 1:
                Session = new XRLuminaSession {
                    uuid = NativeMethods.Text(value.SessionUuid),
                    seq = value.Seq,
                };
                SessionAccepted?.Invoke();
                break;
            case 2:
                Session = null;
                Disconnected?.Invoke();
                break;
            case 3:
                _measuring = true;
                MeasurementStarted?.Invoke();
                break;
            case 4:
                _measuring = false;
                MeasurementFinished?.Invoke();
                break;
            case 5:
                CommandReceived?.Invoke((TestAction)value.Action);
                break;
            case 6:
                Faulted?.Invoke(new InvalidOperationException(NativeMethods.Text(value.Message)));
                break;
            case 7:
                var id = NativeMethods.Text(value.Id);
                if (_flushes.TryGetValue(id, out var callback))
                {
                    _flushes.Remove(id);
                    callback?.Invoke(value.Ok != 0, value.Ok != 0 ? null : NativeMethods.Text(value.Message));
                }
                break;
            }
        }
        /// <summary>
        /// 콜백 도중 폐기를 Tick 종료까지 지연하는 클라이언트 정리
        /// </summary>
        public void Dispose()
        {
            if (_native == IntPtr.Zero || _disposeRequested)
            {
                return;
            }
            VerifyAccess();
            _disposeRequested = true;
            if (!_inTick)
            {
                Release();
            }
        }
        /// <summary>
        /// 네이티브 핸들과 콜백 참조 해제
        /// </summary>
        private void Release()
        {
            NativeMethods.Check(NativeMethods.Destroy(_native));
            _native = IntPtr.Zero;
            _self.Free();
            _flushes.Clear();
            _running = _authenticated = _measuring = false;
            Session = null;
        }
    }
}
