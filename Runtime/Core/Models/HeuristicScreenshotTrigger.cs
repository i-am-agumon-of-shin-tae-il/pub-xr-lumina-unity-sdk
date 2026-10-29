namespace XRLumina.Core.Models
{
    /// <summary>
    /// 휴리스틱 스크린샷을 생성한 조건과 부가 정보를 표현한다.
    /// </summary>
    public readonly struct HeuristicScreenshotTrigger
    {
        public HeuristicTrigger Type { get; }
        public ControllerHand Hand { get; }
        public float CapturedAt { get; }
        public string ObjectName { get; }
        public ControllerInput ControllerInput { get; }

        /// <summary>
        /// 휴리스틱 캡처 발생 조건과 입력 정보 구성
        /// </summary>
        public HeuristicScreenshotTrigger(
            HeuristicTrigger type,
            float capturedAt,
            string objectName = "",
            ControllerInput controllerInput = ControllerInput.None,
            ControllerHand hand = ControllerHand.Unknown)
        {
            Type = type;
            CapturedAt = capturedAt;
            ObjectName = objectName ?? "";
            ControllerInput = controllerInput;
            Hand = hand;
        }
    }
}
