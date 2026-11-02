using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using XRLumina.Core.API;
using XRLumina.Core.Models;

namespace XRLumina.Core.Interop
{
    /// <summary>
    /// 수집 데이터의 C ABI 변환과 호출 동안의 배열 메모리 고정 및 해제
    /// </summary>
    internal static class NativeDataMarshaller
    {
        /// <summary>
        /// 생성 설정 검증과 C ABI 설정 자료형 변환
        /// </summary>
        internal static NativeDto.Options CreateOptions(XRLuminaClientOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            if (string.IsNullOrWhiteSpace(options.ClientId) || options.Port < 1 || options.Port > 65535 ||
                options.DiscoveryPort < 1 || options.DiscoveryPort > 65535 || options.UsbPort < 1 ||
                options.UsbPort > 65535 || options.RetryDelayMs < 1)
            {
                throw new ArgumentException("Invalid client options.", nameof(options));
            }
            return new NativeDto.Options {
                Version = NativeMethods.AbiVersion,
                ClientId = options.ClientId,
                Model = options.Model,
                Name = options.Name,
                Host = options.Host,
                Port = (ushort)options.Port,
                DiscoveryPort = (ushort)options.DiscoveryPort,
                UsbPort = (ushort)options.UsbPort,
                UsbEnabled = options.UsbEnabled ? (byte)1 : (byte)0,
                RetryDelayMs = (uint)options.RetryDelayMs,
            };
        }
        /// <summary>
        /// 자세 데이터의 네이티브 전송
        /// </summary>
        internal static bool SendTracking(IntPtr handle, Tracking value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            var native = new NativeDto.Tracking {
                Timestamp = value.Timestamp,
                Head = value.Head,
                Body = value.Body,
            };
            return NativeMethods.Check(NativeMethods.SendTracking(handle, ref native));
        }
        /// <summary>
        /// 상호작용 집계의 네이티브 전송
        /// </summary>
        internal static bool SendInteractionEvent(IntPtr handle, InteractionEvent value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            var native = new NativeDto.Interaction {
                Type = (uint)value.Type,     Operation = (uint)value.OpName, Count = value.Count,
                Timestamp = value.Timestamp, Position = value.Position,      HasY = value.HasY ? 1u : 0u,
            };
            return NativeMethods.Check(NativeMethods.SendInteraction(handle, ref native));
        }
        /// <summary>
        /// 공간 메쉬 배열의 네이티브 전송
        /// </summary>
        internal static bool SendMesh(IntPtr handle, Mesh value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            var vertices = value.Vertices.ToArray();
            var indices = value.Indices.ToArray();
            return NativeMethods.Check(
                NativeMethods.SendMesh(handle, vertices, (UIntPtr)vertices.Length, indices, (UIntPtr)indices.Length));
        }
        /// <summary>
        /// 시선 좌표를 호출 동안 고정한 네이티브 전송
        /// </summary>
        internal static bool SendGaze(IntPtr handle, Gaze value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            var pins = new List<GCHandle>();
            var frames = new NativeDto.GazeFrame[value.Items.Count];
            try
            {
                for (int index = 0; index < frames.Length; index++)
                {
                    var item = value.Items[index];
                    var points = item.Points.ToArray();
                    var pin = GCHandle.Alloc(points, GCHandleType.Pinned);
                    pins.Add(pin);
                    frames[index] = new NativeDto.GazeFrame {
                        Frame = item.Frame,
                        Time = item.Time,
                        Points = pin.AddrOfPinnedObject(),
                        Count = (UIntPtr)points.Length,
                    };
                }
                return NativeMethods.Check(NativeMethods.SendGaze(handle, value.Name, frames, (UIntPtr)frames.Length));
            }
            finally
            {
                foreach (var pin in pins)
                {
                    pin.Free();
                }
            }
        }
        /// <summary>
        /// 카메라 위치와 회전의 네이티브 전송
        /// </summary>
        internal static bool SendCameraPose(IntPtr handle, CameraPose value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            return NativeMethods.Check(
                NativeMethods.SendCameraPose(handle, value.Name, value.Position, value.Rotation));
        }
        /// <summary>
        /// 미러링 이미지의 네이티브 전송
        /// </summary>
        internal static bool SendMirrorFrame(IntPtr handle, byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }
            return NativeMethods.Check(
                NativeMethods.SendMirror(handle, bytes, (UIntPtr)bytes.Length, (uint)ImageFormat.Jpeg));
        }
        /// <summary>
        /// 측정 캡처 이미지의 네이티브 전송
        /// </summary>
        internal static bool SendCapture(IntPtr handle, string name, byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }
            return NativeMethods.Check(NativeMethods.SendCapture(handle, name, bytes, (UIntPtr)bytes.Length));
        }
        /// <summary>
        /// 휴리스틱 입력 정보와 이미지의 네이티브 전송
        /// </summary>
        internal static bool SendScreenshot(IntPtr handle, string name, HeuristicScreenshotTrigger trigger, byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }
            var pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                var native = new NativeDto.Screenshot {
                    Name = name,
                    ObjectName = trigger.ObjectName,
                    Trigger = (uint)trigger.Type,
                    CapturedAt = trigger.CapturedAt,
                    Input = (uint)trigger.ControllerInput,
                    Hand = (uint)trigger.Hand,
                    Data = pin.AddrOfPinnedObject(),
                    Size = (UIntPtr)bytes.Length,
                };
                return NativeMethods.Check(NativeMethods.SendScreenshot(handle, ref native));
            }
            finally
            {
                pin.Free();
            }
        }
    }
}
