using System;
using System.Runtime.InteropServices;

using XRLumina.Core.Models;

namespace XRLumina.Core.Interop
{
    /// <summary>
    /// C ABI 호출과 플랫폼별 라이브러리 이름 선언
    /// </summary>
    internal static class NativeMethods
    {
        /// <summary>
        /// CoreApi.h의 XRL_ABI_VERSION과 동일한 호출 규약 버전
        /// </summary>
        internal const uint AbiVersion = 1;
        /// <summary>
        /// CoreApi.h의 XRL_REQUEST_ID_CAPACITY와 동일한 요청 ID 버퍼 크기
        /// </summary>
        internal const int RequestIdCapacity = 128;
        private const int Success = 0;
        private const int Error = 1;
#if UNITY_IOS && !UNITY_EDITOR
        private const string Library = "__Internal";
#elif UNITY_ANDROID && !UNITY_EDITOR
        private const string Library = "xrlumina_core_android_arm64";
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        private const string Library = "xrlumina_core_osx_universal";
#elif UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        private const string Library = "xrlumina_core_windows_x64";
#else
        private const string Library = "xrlumina_core";
#endif
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void Callback(IntPtr user, ref NativeDto.Event value);
        /// <summary>
        /// UTF-8 오류 문자열 복사
        /// </summary>
        internal static string Text(IntPtr pointer)
        {
            return pointer == IntPtr.Zero ? "" : Marshal.PtrToStringUTF8(pointer);
        }
        /// <summary>
        /// 네이티브 실패 코드의 관리형 예외 변환
        /// </summary>
        internal static bool Check(int result)
        {
            if (result == Error)
            {
                throw new InvalidOperationException(Text(LastError()));
            }
            return result == Success;
        }
        /// <summary>
        /// 호출 스레드의 마지막 네이티브 오류 조회
        /// </summary>
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "xrl_last_error")]
        internal static extern IntPtr LastError();
        /// <summary>
        /// 연결 설정과 콜백을 복사한 네이티브 클라이언트 생성
        /// </summary>
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "xrl_create")]
        internal static extern int Create(ref NativeDto.Options options, Callback callback, IntPtr user, out IntPtr client);
        /// <summary>
        /// 네이티브 클라이언트와 네트워크 자원 폐기
        /// </summary>
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "xrl_destroy")]
        internal static extern int Destroy(IntPtr client);
        /// <summary>
        /// 연결과 호스트 탐색 시작
        /// </summary>
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "xrl_start")]
        internal static extern int Start(IntPtr client);
        /// <summary>
        /// 엔진 스레드의 수신 처리와 완료 이벤트 전달
        /// </summary>
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "xrl_tick")]
        internal static extern int Tick(IntPtr client, double delta);
        /// <summary>
        /// 연결 종료와 대기 요청 취소 예약
        /// </summary>
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "xrl_stop")]
        internal static extern int Stop(IntPtr client);
        /// <summary>
        /// 실행 및 세션 인증과 측정 상태 조회
        /// </summary>
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "xrl_state")]
        internal static extern int State(IntPtr client, out uint running, out uint authenticated, out uint measuring);
        /// <summary>
        /// 측정 시작 또는 종료 요청
        /// </summary>
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "xrl_request")]
        internal static extern int Request(IntPtr client, uint action);
        /// <summary>
        /// 사용자 자세 데이터 전송
        /// </summary>
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "xrl_send_tracking")]
        internal static extern int SendTracking(IntPtr client, ref NativeDto.Tracking value);
        /// <summary>
        /// 상호작용 집계 데이터 전송
        /// </summary>
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "xrl_send_interaction")]
        internal static extern int SendInteraction(IntPtr client, ref NativeDto.Interaction value);
        /// <summary>
        /// 공간 메쉬 정점과 인덱스 전송
        /// </summary>
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "xrl_send_mesh")]
        internal static extern int SendMesh(IntPtr client, [In] Vector3[] vertices, UIntPtr count, [In] int[] indices, UIntPtr indexCount);
        /// <summary>
        /// 프레임별 시선 좌표 전송
        /// </summary>
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "xrl_send_gaze")]
        internal static extern int SendGaze(IntPtr client, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [In] NativeDto.GazeFrame[] frames, UIntPtr count);
        /// <summary>
        /// 카메라 위치와 회전 전송
        /// </summary>
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "xrl_send_camera_pose")]
        internal static extern int SendCameraPose(IntPtr client, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, Vector3 position, Quaternion rotation);
        /// <summary>
        /// 인코딩된 미러링 이미지 전송
        /// </summary>
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "xrl_send_mirror")]
        internal static extern int SendMirror(IntPtr client, [In] byte[] data, UIntPtr size, uint format);
        /// <summary>
        /// 측정 캡처 이미지 전송
        /// </summary>
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "xrl_send_capture")]
        internal static extern int SendCapture(IntPtr client, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [In] byte[] data, UIntPtr size);
        /// <summary>
        /// 휴리스틱 입력 정보와 이미지 전송
        /// </summary>
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "xrl_send_screenshot")]
        internal static extern int SendScreenshot(IntPtr client, ref NativeDto.Screenshot value);
        /// <summary>
        /// 그룹 업로드 요청과 호출자 버퍼의 요청 식별자 반환
        /// </summary>
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "xrl_flush")]
        internal static extern int Flush(IntPtr client, uint group, float interval, double timeout, [Out] byte[] id, UIntPtr capacity);
    }
}
