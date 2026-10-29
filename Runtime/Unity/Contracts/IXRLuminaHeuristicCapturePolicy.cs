using XRLumina.Core.Models;

using Vector3 = UnityEngine.Vector3;

namespace XRLumina.Unity.Contracts
{
    /// <summary>
    /// 콘텐츠의 휴리스틱 캡처 조건을 SDK 촬영 기능에 연결하는 규약
    /// </summary>
    public interface IXRLuminaHeuristicCapturePolicy
    {
        /// <summary>
        /// 새 측정의 콘텐츠 판단 상태 초기화
        /// </summary>
        void Reset();
        /// <summary>
        /// 현재 위치와 회전에 대한 캡처 원인 반환
        /// </summary>
        HeuristicScreenshotTrigger? EvaluatePose(Vector3 position, float yaw, float capturedAt);
        /// <summary>
        /// 콘텐츠 대상과 입력에 대한 캡처 원인 반환
        /// </summary>
        HeuristicScreenshotTrigger? EvaluateInteraction(int objectId, string objectName,
            ControllerInput input, ControllerHand hand, float capturedAt);
    }
}
