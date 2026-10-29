using UnityEngine;
using XRLumina.Core.API;
using Vector3 = UnityEngine.Vector3;
namespace XRLumina.Unity.Contracts
{
    /// <summary>
    /// 게임에서 휴리스틱 캡처 발생 조건을 선택하는 Unity 측 정책 계약.
    /// </summary>
    public interface IXRLuminaHeuristicCapturePolicy
    {
        /// <summary>
        /// 새 측정 시작에 맞춰 게임별 누적 상태를 초기화한다.
        /// </summary>
        void Reset();
        /// <summary>
        /// 현재 위치와 회전으로 캡처 발생 여부를 반환한다.
        /// </summary>
        HeuristicScreenshotTrigger? EvaluatePose(Vector3 position, float yaw, float capturedAt);
        /// <summary>
        /// 게임에서 확정한 대상과 입력으로 캡처 발생 여부를 반환한다.
        /// </summary>
        HeuristicScreenshotTrigger? EvaluateInteraction(int objectId, string objectName,
            ControllerInput controllerInput, ControllerHand hand, float capturedAt);
    }
}