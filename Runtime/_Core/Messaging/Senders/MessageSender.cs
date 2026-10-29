using System;
using XRLumina.Client;
using XRLumina.Model;
using XRLumina._Core.Model;
namespace XRLumina._Core.Messaging.Senders
{
    /// <summary>
    /// 엔진 수집 데이터를 네이티브 공용 코어에 전달하는 송신 연결
    /// </summary>
    internal sealed class MessageSender
    {
        private readonly XRLuminaClient _client;
        /// <summary>
        /// 대상 클라이언트 연결
        /// </summary>
        internal MessageSender(XRLuminaClient client)
        {
            _client = client;
        }
        /// <summary>
        /// 자세 데이터 제출
        /// </summary>
        internal void SendStream(Tracking value)
        {
            Require(_client.SendTracking(value));
        }
        /// <summary>
        /// 상호작용 집계 제출
        /// </summary>
        internal void SendStream(InteractionEvent value)
        {
            Require(_client.SendInteractionEvent(value));
        }
        /// <summary>
        /// 공간 메쉬 제출
        /// </summary>
        internal void SendStream(Mesh value)
        {
            Require(_client.SendMesh(value));
        }
        /// <summary>
        /// 시선 프레임 제출
        /// </summary>
        internal void SendStream(Gaze value)
        {
            Require(_client.SendGaze(value));
        }
        /// <summary>
        /// 카메라 자세 제출
        /// </summary>
        internal void SendStream(CameraPose value)
        {
            Require(_client.SendCameraPose(value));
        }
        /// <summary>
        /// 미러링 프레임 제출
        /// </summary>
        internal bool SendMirrorFrame(byte[] value)
        {
            return _client.SendMirrorFrame(value);
        }
        /// <summary>
        /// 측정 캡처 제출
        /// </summary>
        internal void SendCapture(string name, byte[] value)
        {
            Require(_client.SendCapture(name, value));
        }
        /// <summary>
        /// 휴리스틱 캡처 제출
        /// </summary>
        internal void SendScreenshot(string name, HeuristicScreenshotTrigger trigger, byte[] value)
        {
            Require(_client.SendScreenshot(name, trigger, value));
        }
        /// <summary>
        /// 세션 단절과 대기열 초과의 명시적 실패 처리
        /// </summary>
        private static void Require(bool sent)
        {
            if (!sent)
            {
                throw new InvalidOperationException("Session or native send queue unavailable.");
            }
        }
    }
}
