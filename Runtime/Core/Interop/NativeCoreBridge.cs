using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using XRLumina.Core.API;
using XRLumina.Core.Contracts;
using XRLumina.Core.Models;

namespace XRLumina.Core.Interop
{
    /// <summary>
    /// 트리거 호출의 접근 검증과 공용 코어 핸들 수명 관리
    /// </summary>
    internal sealed class NativeCoreBridge : IXRLuminaTriggers
    {
        private IntPtr _native;
        private readonly NativeCallbackAdapter _callbacks;
        private readonly int _owner = Thread.CurrentThread.ManagedThreadId;
        private bool _inTick;
        private bool _disposeRequested;
        public SessionData Session => _callbacks.Session;
        public bool IsRunning => _callbacks.IsRunning;
        public bool IsAuthenticated => _callbacks.IsAuthenticated;
        public bool IsMeasuring => _callbacks.IsMeasuring;
        /// <summary>
        /// 엔진 설정을 복사한 네이티브 클라이언트 생성
        /// </summary>
        internal NativeCoreBridge(XRLuminaClientOptions options, IXRLuminaCallbacks callbacks)
        {
            var native = NativeDataMarshaller.CreateOptions(options);
            _callbacks = new NativeCallbackAdapter(callbacks, () => _native, () => !_disposeRequested);
            try
            {
                NativeMethods.Check(NativeMethods.Create(ref native, _callbacks.Callback, _callbacks.UserData, out _native));
            }
            catch
            {
                _callbacks.Dispose();
                throw;
            }
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
                throw new ObjectDisposedException(nameof(NativeCoreBridge));
            }
        }
        /// <summary>
        /// 코어 실행과 연결 시작
        /// </summary>
        public void Start()
        {
            VerifyAccess();
            NativeMethods.Check(NativeMethods.Start(_native));
            _callbacks.RefreshState();
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
                _callbacks.RefreshState();
            }
            finally
            {
                _inTick = false;
                if (_disposeRequested)
                {
                    Release();
                }
            }
            _callbacks.ReportErrors();
        }
        /// <summary>
        /// 연결 종료와 요청 취소 예약
        /// </summary>
        public void Stop()
        {
            VerifyAccess();
            NativeMethods.Check(NativeMethods.Stop(_native));
            _callbacks.ClearSession();
            _callbacks.RefreshState();
        }
        /// <summary>
        /// 측정 시작 트리거 전달
        /// </summary>
        public bool RequestStart()
        {
            VerifyAccess();
            return NativeMethods.Check(NativeMethods.Request(_native, (uint)TestAction.Start));
        }
        /// <summary>
        /// 측정 종료 트리거 전달
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
            return NativeDataMarshaller.SendTracking(_native, value);
        }
        /// <summary>
        /// 상호작용 집계의 네이티브 전송
        /// </summary>
        public bool SendInteractionEvent(InteractionEvent value)
        {
            VerifyAccess();
            return NativeDataMarshaller.SendInteractionEvent(_native, value);
        }
        /// <summary>
        /// 공간 메쉬 배열의 네이티브 전송
        /// </summary>
        public bool SendMesh(Mesh value)
        {
            VerifyAccess();
            return NativeDataMarshaller.SendMesh(_native, value);
        }
        /// <summary>
        /// 시선 좌표를 호출 동안 고정한 네이티브 전송
        /// </summary>
        public bool SendGaze(Gaze value)
        {
            VerifyAccess();
            return NativeDataMarshaller.SendGaze(_native, value);
        }
        /// <summary>
        /// 카메라 위치와 회전의 네이티브 전송
        /// </summary>
        public bool SendCameraPose(CameraPose value)
        {
            VerifyAccess();
            return NativeDataMarshaller.SendCameraPose(_native, value);
        }
        /// <summary>
        /// 미러링 이미지의 네이티브 전송
        /// </summary>
        public bool SendMirrorFrame(byte[] bytes)
        {
            VerifyAccess();
            return NativeDataMarshaller.SendMirrorFrame(_native, bytes);
        }
        /// <summary>
        /// 측정 캡처 이미지의 네이티브 전송
        /// </summary>
        public bool SendCapture(string name, byte[] bytes)
        {
            VerifyAccess();
            return NativeDataMarshaller.SendCapture(_native, name, bytes);
        }
        /// <summary>
        /// 휴리스틱 입력 정보와 이미지의 네이티브 전송
        /// </summary>
        public bool SendScreenshot(string name, HeuristicScreenshotTrigger trigger, byte[] bytes)
        {
            VerifyAccess();
            return NativeDataMarshaller.SendScreenshot(_native, name, trigger, bytes);
        }
        /// <summary>
        /// 업로드 완료 확인 요청과 콜백 식별자 반환
        /// </summary>
        public string Flush(FlushGroup group, float interval, double timeoutSeconds = 10)
        {
            VerifyAccess();
            var bytes = new byte[NativeMethods.RequestIdCapacity];
            NativeMethods.Check(NativeMethods.Flush(_native, (uint)group, interval, timeoutSeconds,
                                                   bytes, (UIntPtr)bytes.Length));
            return Encoding.UTF8.GetString(bytes, 0, Array.IndexOf(bytes, (byte)0));
        }
        /// <summary>
        /// 코어 업로드 요청과 관리형 완료 대기 연결
        /// </summary>
        public Task<FlushResult> FlushAsync(FlushGroup group, float interval, double timeoutSeconds = 10)
        {
            return _callbacks.TrackFlush(Flush(group, interval, timeoutSeconds));
        }

        /// <summary>
        /// 콜백 중 폐기를 Tick 반환 이후로 지연하여 핸들 수명 보장
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
        /// 공용 코어 종료 이후 네이티브 핸들과 콜백 참조 해제
        /// </summary>
        private void Release()
        {
            NativeMethods.Check(NativeMethods.Destroy(_native));
            _native = IntPtr.Zero;
            _callbacks.Dispose();
        }
    }
}
