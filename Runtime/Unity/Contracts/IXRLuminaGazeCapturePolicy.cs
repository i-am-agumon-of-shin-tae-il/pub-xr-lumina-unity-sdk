using XRLumina.Unity.Models;
using UnityEngine;
namespace XRLumina.Unity.Contracts
{
    /// <summary>
    /// 시선 캡처 구간의 시작과 확정 시점을 게임에서 선택하는 계약.
    /// </summary>
    public interface IXRLuminaGazeCapturePolicy
    {
        /// <summary>
        /// 새 측정의 구간 판정 상태를 초기화한다.
        /// </summary>
        void Reset();
        /// <summary>
        /// 위치와 시각으로 캡처 구간 및 필요한 센서 기록 범위를 반환한다.
        /// </summary>
        GazeCaptureDecision Evaluate(Vector3 position, int sourceId, float capturedAt, bool captureConfirmed);
    }
}
