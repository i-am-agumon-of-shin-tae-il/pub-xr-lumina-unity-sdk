using System;

using XRLumina.Core.Contracts;
using XRLumina.Core.Interop;

namespace XRLumina.Core.API
{
    /// <summary>
    /// 엔진 콜백을 연결한 공용 코어 트리거 인터페이스 생성
    /// </summary>
    public static class XRLuminaClient
    {
        /// <summary>
        /// 생성 스레드에서 사용할 트리거와 콜백 연결 생성
        /// </summary>
        public static IXRLuminaTriggers Create(XRLuminaClientOptions options, IXRLuminaCallbacks callbacks)
        {
            return new NativeCoreBridge(options, callbacks);
        }
        /// <summary>
        /// 전송 대기열 등록 실패의 명시적 확인
        /// </summary>
        public static void RequireSubmitted(bool submitted)
        {
            if (!submitted)
            {
                throw new InvalidOperationException("Session or native send queue unavailable.");
            }
        }
    }
}
