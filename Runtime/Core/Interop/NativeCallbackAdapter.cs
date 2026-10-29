using XRLumina.Core.Contracts;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using XRLumina.Core.API;
namespace XRLumina.Core.Interop
{
    /// <summary>
    /// C ABI 결과의 콜백 변환과 관리형 콜백 참조 수명 관리
    /// </summary>
    internal sealed class NativeCallbackAdapter : IDisposable
    {
        private static readonly NativeMethods.Callback NativeCallback = Receive;
        private readonly IXRLuminaCallbacks _callbacks;
        private readonly Func<IntPtr> _nativeHandle;
        private readonly Func<bool> _acceptCallbacks;
        private readonly List<Exception> _callbackErrors = new();
        private GCHandle _self;
        internal NativeMethods.Callback Callback => NativeCallback;
        internal IntPtr UserData => GCHandle.ToIntPtr(_self);
        internal SessionData Session { get; private set; }
        internal bool IsRunning { get; private set; }
        internal bool IsAuthenticated { get; private set; }
        internal bool IsMeasuring { get; private set; }
        /// <summary>
        /// 엔진 콜백과 네이티브 호출 수명 연결
        /// </summary>
        internal NativeCallbackAdapter(IXRLuminaCallbacks callbacks, Func<IntPtr> nativeHandle,
                                       Func<bool> acceptCallbacks)
        {
            _callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
            _nativeHandle = nativeHandle;
            _acceptCallbacks = acceptCallbacks;
            _self = GCHandle.Alloc(this);
        }
        /// <summary>
        /// 연결 종료 후 세션 조회 값 초기화
        /// </summary>
        internal void ClearSession()
        {
            Session = null;
        }
        /// <summary>
        /// 네이티브 호출 반환 후 콜백 처리 예외 전달
        /// </summary>
        internal void ReportErrors()
        {
            var errors = _callbackErrors.ToArray();
            _callbackErrors.Clear();
            foreach (var error in errors)
            {
                _callbacks.OnFault(error);
            }
        }
        /// <summary>
        /// 공용 코어 상태를 조회용 값으로 복사
        /// </summary>
        internal void RefreshState()
        {
            if (_nativeHandle() == IntPtr.Zero)
            {
                return;
            }
            NativeMethods.Check(NativeMethods.State(_nativeHandle(), out var running, out var authenticated, out var measuring));
            IsRunning = running != 0;
            IsAuthenticated = authenticated != 0;
            IsMeasuring = measuring != 0;
        }
        /// <summary>
        /// 네이티브 결과를 콜백 계약으로 전달하고 관리형 예외 격리
        /// </summary>
#if ENABLE_IL2CPP
        [AOT.MonoPInvokeCallback(typeof(NativeMethods.Callback))]
#endif
        private static void Receive(IntPtr user, ref NativeDto.Event value)
        {
            var client = (NativeCallbackAdapter)GCHandle.FromIntPtr(user).Target;
            if (!client._acceptCallbacks())
            {
                return;
            }
            try
            {
                client.RefreshState();
                client.DeliverCallback(value);
            }
            catch (Exception error)
            {
                client._callbackErrors.Add(error);
            }
        }
        /// <summary>
        /// C ABI 결과와 공개 콜백 계약 간 형식 변환
        /// </summary>
        private void DeliverCallback(NativeDto.Event value)
        {
            switch (value.Type)
            {
                case NativeDto.EventType.SessionReady:
                    Session = CopySession(value);
                    _callbacks.OnSessionReady(Session);
                    break;
                case NativeDto.EventType.SessionAccepted:
                    Session = CopySession(value);
                    _callbacks.OnSessionAccepted(Session);
                    break;
                case NativeDto.EventType.Disconnected:
                    Session = null;
                    _callbacks.OnDisconnected();
                    break;
                case NativeDto.EventType.MeasurementStarted:
                    IsMeasuring = true;
                    _callbacks.OnMeasurementStarted();
                    break;
                case NativeDto.EventType.MeasurementFinished:
                    IsMeasuring = false;
                    _callbacks.OnMeasurementFinished();
                    break;
                case NativeDto.EventType.CommandReceived:
                    _callbacks.OnCommand((TestAction)value.Action);
                    break;
                case NativeDto.EventType.Faulted:
                    _callbacks.OnFault(new InvalidOperationException(NativeMethods.Text(value.Message)));
                    break;
                case NativeDto.EventType.FlushCompleted:
                    _callbacks.OnFlushCompleted(NativeMethods.Text(value.Id), value.Ok != 0,
                                                value.Ok != 0 ? null : NativeMethods.Text(value.Message));
                    break;
            }
        }
        /// <summary>
        /// 네이티브 호출 동안 유효한 세션 문자열을 관리형 데이터로 복사
        /// </summary>
        private static SessionData CopySession(NativeDto.Event value)
        {
            return new SessionData
            {
                uuid = NativeMethods.Text(value.SessionUuid),
                seq = value.Seq
            };
        }
        /// <summary>
        /// 코어 종료 이후 콜백 참조와 조회 상태 해제
        /// </summary>
        public void Dispose()
        {
            if (_self.IsAllocated)
            {
                _self.Free();
            }
            IsRunning = IsAuthenticated = IsMeasuring = false;
            Session = null;
        }
    }
}
