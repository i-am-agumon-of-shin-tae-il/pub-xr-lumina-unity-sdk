using UnityEngine;

using XRLumina.Unity.Models;

namespace XRLumina.Unity.Contracts
{
    /// <summary>
    /// 콘텐츠의 시선 캡처 구간을 SDK 기록과 촬영 기능에 연결하는 규약
    /// </summary>
    public interface IXRLuminaGazeCapturePolicy
    {
        /// <summary>
        /// 새 측정의 구간 판단 상태 초기화
        /// </summary>
        void Reset();
        /// <summary>
        /// 현재 위치와 시각에 대한 캡처 구간 반환
        /// </summary>
        GazeCaptureDecision Evaluate(Vector3 position, int sourceId, float capturedAt, bool captureConfirmed);
    }
}
