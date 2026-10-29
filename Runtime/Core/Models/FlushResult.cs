namespace XRLumina.Core.Models
{
    /// <summary>
    /// 요청 ID에 대응하는 코어 업로드 완료 결과
    /// </summary>
    public readonly struct FlushResult
    {
        public string RequestId { get; }
        public bool IsSuccess { get; }
        public string Error { get; }

        /// <summary>
        /// 업로드 완료 결과 데이터 구성
        /// </summary>
        public FlushResult(string requestId, bool isSuccess, string error)
        {
            RequestId = requestId;
            IsSuccess = isSuccess;
            Error = error;
        }
    }
}
