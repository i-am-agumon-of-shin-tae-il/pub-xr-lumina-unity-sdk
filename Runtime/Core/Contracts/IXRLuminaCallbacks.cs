using System;

using XRLumina.Core.Models;

namespace XRLumina.Core.Contracts
{
    /// <summary>
    /// 공용 코어가 엔진의 Tick 호출 중 전달하는 결과 계약
    /// </summary>
    public interface IXRLuminaCallbacks
    {
        /// <summary>
        /// 세션 준비 결과 전달
        /// </summary>
        void OnSessionReady(SessionData session);
        /// <summary>
        /// 세션 수락 결과 전달
        /// </summary>
        void OnSessionAccepted(SessionData session);
        /// <summary>
        /// 선택 연결 해제 전달
        /// </summary>
        void OnDisconnected();
        /// <summary>
        /// 측정 시작 확정 전달
        /// </summary>
        void OnMeasurementStarted();
        /// <summary>
        /// 측정 종료 확정 전달
        /// </summary>
        void OnMeasurementFinished();
        /// <summary>
        /// 데스크톱 측정 제어 명령 전달
        /// </summary>
        void OnCommand(TestAction action);
        /// <summary>
        /// 코어 오류 또는 콜백 처리 예외 전달
        /// </summary>
        void OnFault(Exception error);
        /// <summary>
        /// 요청 식별자에 대응하는 업로드 완료 결과 전달
        /// </summary>
        void OnFlushCompleted(string id, bool ok, string error);
    }
}
