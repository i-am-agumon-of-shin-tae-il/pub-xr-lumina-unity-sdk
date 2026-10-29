using UnityEngine;

using XRLumina.Sample.Policies;
using XRLumina.Unity.Components.Features;

namespace XRLumina.Sample.Components.Features
{
    /// <summary>
    /// 샘플 콘텐츠의 캡처 조건과 SDK 기능 연결
    /// </summary>
    public sealed class XRLuminaSampleCaptureSettings : MonoBehaviour
    {
        [SerializeField] private XRLuminaInteractionController interaction;
        [SerializeField] private XRLuminaEyeTrackingController eyeTracking;
        [SerializeField] private float stationaryDurationSeconds = 3f;
        [SerializeField] private float stationaryMovementThreshold = 0.15f;
        [SerializeField] private float yawWindowSeconds = 5f;
        [SerializeField] private int yawRequiredReversals = 3;
        [SerializeField] private float yawMinimumSwingDegrees = 30f;
        [SerializeField] private float interactionWindowSeconds = 5f;
        [SerializeField] private int interactionRequiredCount = 3;
        [SerializeField] private int gazeStationaryDurationMs = 1000;
        [SerializeField] private float gazeMovementThresholdMeters = 0.05f;

        /// <summary>
        /// 콘텐츠별 판단 정책을 측정 시작 전에 등록
        /// </summary>
        private void Awake()
        {
            if (interaction != null)
            {
                interaction.SetCapturePolicy(new SampleHeuristicCapturePolicy(
                    stationaryDurationSeconds, stationaryMovementThreshold, yawWindowSeconds,
                    yawRequiredReversals, yawMinimumSwingDegrees, interactionWindowSeconds,
                    interactionRequiredCount));
            }
            if (eyeTracking != null)
            {
                eyeTracking.SetCapturePolicy(new SampleGazeCapturePolicy(
                    gazeStationaryDurationMs, gazeMovementThresholdMeters, eyeTracking.FramesPerSecond));
            }
        }
    }
}
