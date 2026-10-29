using XRLumina.Unity.Models;
using XRLumina.Unity.Contracts;
using UnityEngine;
namespace XRLumina.Unity.Policies
{
    /// <summary>기존 Inspector 기준으로 정지 구간을 판단하는 교체 가능한 기본 정책.</summary>
    internal sealed class DefaultGazeCapturePolicy : IXRLuminaGazeCapturePolicy
    {
        private readonly float _durationSeconds;
        private readonly float _movementThreshold;
        private readonly float _sampleInterval;
        private bool _hasCandidate;
        private int _sourceId;
        private Vector3 _anchor;
        private float _startedAt;
        /// <summary>정지 시간과 이동 기준 및 센서 기록 간격을 저장한다.</summary>
        internal DefaultGazeCapturePolicy(int stationaryDurationMs, float movementThreshold, int framesPerSecond)
        {
            _durationSeconds = Mathf.Max(0, stationaryDurationMs) / 1000f;
            _movementThreshold = movementThreshold;
            _sampleInterval = 1f / Mathf.Max(1, framesPerSecond);
        }
        /// <summary>위치 변화와 정지 시간으로 다음 캡처 구간 동작을 결정한다.</summary>
        public GazeCaptureDecision Evaluate(Vector3 position, int sourceId, float capturedAt, bool captureConfirmed)
        {
            bool resetHistory = !_hasCandidate || _sourceId != sourceId;
            bool beginCandidate = resetHistory || Vector3.Distance(position, _anchor) > _movementThreshold;
            if (beginCandidate)
            {
                _hasCandidate = true;
                _sourceId = sourceId;
                _anchor = position;
                _startedAt = capturedAt;
            }
            bool confirm = (!captureConfirmed || beginCandidate) && capturedAt - _startedAt >= _durationSeconds;
            return new GazeCaptureDecision(beginCandidate, resetHistory, confirm,
                capturedAt - _durationSeconds, capturedAt - _durationSeconds - _sampleInterval);
        }
        /// <summary>측정 시작 또는 정책 교체 시 후보 구간을 초기화한다.</summary>
        public void Reset()
        {
            _hasCandidate = false;
            _sourceId = 0;
            _anchor = default;
            _startedAt = 0f;
        }
    }
}
