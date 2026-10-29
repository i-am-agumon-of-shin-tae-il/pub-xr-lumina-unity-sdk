using System;
using XRLumina.Core.API;
namespace XRLumina.Core.Contracts
{
    /// <summary>
    /// 엔진에서 공용 코어로 전달하는 실행 및 데이터 제출 계약
    /// </summary>
    public interface IXRLuminaTriggers : IDisposable
    {
        /// <summary>
        /// 현재 세션 조회
        /// </summary>
        SessionData Session { get; }
        /// <summary>
        /// 코어 실행 상태 조회
        /// </summary>
        bool IsRunning { get; }
        /// <summary>
        /// 세션 인증 상태 조회
        /// </summary>
        bool IsAuthenticated { get; }
        /// <summary>
        /// 코어가 확정한 측정 상태 조회
        /// </summary>
        bool IsMeasuring { get; }
        /// <summary>
        /// 연결과 호스트 탐색 시작
        /// </summary>
        void Start();
        /// <summary>
        /// 엔진 스레드에서 결과 콜백 처리
        /// </summary>
        void Tick(double deltaSeconds);
        /// <summary>
        /// 연결 종료와 대기 요청 취소
        /// </summary>
        void Stop();
        /// <summary>
        /// 측정 시작 요청
        /// </summary>
        bool RequestStart();
        /// <summary>
        /// 측정 종료 요청
        /// </summary>
        bool RequestFinish();
        /// <summary>
        /// 사용자 자세 데이터 제출
        /// </summary>
        bool SendTracking(Tracking value);
        /// <summary>
        /// 상호작용 데이터 제출
        /// </summary>
        bool SendInteractionEvent(InteractionEvent value);
        /// <summary>
        /// 공간 메쉬 제출
        /// </summary>
        bool SendMesh(Mesh value);
        /// <summary>
        /// 시선 데이터 제출
        /// </summary>
        bool SendGaze(Gaze value);
        /// <summary>
        /// 촬영 위치와 회전 제출
        /// </summary>
        bool SendCameraPose(CameraPose value);
        /// <summary>
        /// JPEG 미러링 이미지 제출
        /// </summary>
        bool SendMirrorFrame(byte[] bytes);
        /// <summary>
        /// 측정 캡처 이미지 제출
        /// </summary>
        bool SendCapture(string name, byte[] bytes);
        /// <summary>
        /// 휴리스틱 발생 조건과 이미지 제출
        /// </summary>
        bool SendScreenshot(string name, HeuristicScreenshotTrigger trigger, byte[] bytes);
        /// <summary>
        /// 업로드 완료 확인 요청과 콜백 식별자 반환
        /// </summary>
        string Flush(FlushGroup group, float interval, double timeoutSeconds = 10);
    }
}
