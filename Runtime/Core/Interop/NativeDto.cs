using System;
using System.Runtime.InteropServices;
using XRLumina.Core.API;
namespace XRLumina.Core.Interop
{
    /// <summary>
    /// C#과 C++ 사이의 메모리 배치를 공유하는 내부 전달 자료형
    /// </summary>
    internal static class NativeDto
    {
        /// <summary>
        /// 공용 코어 생성 설정 전달
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct Options
        {
            internal uint Version;
            [MarshalAs(UnmanagedType.LPUTF8Str)]
            internal string ClientId;
            [MarshalAs(UnmanagedType.LPUTF8Str)]
            internal string Model;
            [MarshalAs(UnmanagedType.LPUTF8Str)]
            internal string Name;
            [MarshalAs(UnmanagedType.LPUTF8Str)]
            internal string Host;
            internal ushort Port, DiscoveryPort, UsbPort;
            internal byte UsbEnabled;
            internal uint RetryDelayMs;
        }
        /// <summary>
        /// 공용 코어의 xrl_event_type과 동일한 ABI 이벤트 값
        /// </summary>
        internal enum EventType : uint
        {
            SessionReady = 0,
            SessionAccepted = 1,
            Disconnected = 2,
            MeasurementStarted = 3,
            MeasurementFinished = 4,
            CommandReceived = 5,
            Faulted = 6,
            FlushCompleted = 7
        }
        /// <summary>
        /// 공용 코어 결과 콜백 전달
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct Event
        {
            internal EventType Type;
            internal uint Action;
            internal int Seq;
            internal uint Ok;
            internal IntPtr SessionUuid, Id, Message;
        }
        /// <summary>
        /// 사용자 자세 데이터 전달
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct Tracking
        {
            internal long Timestamp;
            internal Pose Head, Body;
        }
        /// <summary>
        /// 상호작용 집계 데이터 전달
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct Interaction
        {
            internal uint Type, Operation;
            internal int Count;
            internal long Timestamp;
            internal Vector3 Position;
            internal uint HasY;
        }
        /// <summary>
        /// 고정된 시선 좌표 배열과 프레임 정보 전달
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct GazeFrame
        {
            internal int Frame;
            internal float Time;
            internal IntPtr Points;
            internal UIntPtr Count;
        }
        /// <summary>
        /// 고정된 이미지와 휴리스틱 발생 정보 전달
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct Screenshot
        {
            [MarshalAs(UnmanagedType.LPUTF8Str)]
            internal string Name;
            [MarshalAs(UnmanagedType.LPUTF8Str)]
            internal string ObjectName;
            internal uint Trigger;
            internal float CapturedAt;
            internal uint Input, Hand;
            internal IntPtr Data;
            internal UIntPtr Size;
        }
    }
}
