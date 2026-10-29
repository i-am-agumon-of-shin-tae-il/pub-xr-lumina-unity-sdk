namespace XRLumina.Unity.Models
{
    /// <summary>
    /// 게임 정책이 결정한 시선 캡처 구간과 센서 기록 범위.
    /// </summary>
    public readonly struct GazeCaptureDecision
    {
        public bool BeginCandidate { get; }
        public bool ResetHistory { get; }
        public bool ConfirmCandidate { get; }
        public float SegmentStartedAt { get; }
        public float HistoryStartedAt { get; }
        /// <summary>
        /// 캡처 구간 처리 결과를 구성한다.
        /// </summary>
        public GazeCaptureDecision(bool beginCandidate, bool resetHistory, bool confirmCandidate,
            float segmentStartedAt, float historyStartedAt)
        {
            BeginCandidate = beginCandidate;
            ResetHistory = resetHistory;
            ConfirmCandidate = confirmCandidate;
            SegmentStartedAt = segmentStartedAt;
            HistoryStartedAt = historyStartedAt;
        }
    }
}
