using System.Collections.Generic;
namespace XRLumina.Core.API
{
    public enum TestAction : byte
    {
        Start,
        Finish
    }
    public enum FlushGroup : byte
    {
        Unknown,
        Interaction,
        EyeTracking,
        Heuristics
    }
    public enum ImageFormat : byte
    {
        Jpeg,
        Png
    }
    public enum EventKind : byte
    {
        Task,
        Npc,
        Gesture,
        Controller,
        Object
    }
    public enum EventOperation : byte
    {
        ReportCount
    }
    public enum HeuristicTrigger : byte
    {
        Unknown,
        Stationary,
        HeadDirectionChange,
        RepeatedInteraction
    }
    public enum ControllerHand : byte
    {
        Unknown,
        Left,
        Right
    }
    public enum ControllerInput : byte
    {
        None,
        Primary,
        Secondary,
        Trigger,
        Grip,
        Interact
    }
    public struct Vector3
    {
        public float X, Y, Z;
    }
    public struct Quaternion
    {
        public float X, Y, Z, W;
    }
    public struct Pose
    {
        public Vector3 Position, Rotation;
    }
    public sealed class Tracking
    {
        public long Timestamp;
        public Pose Head, Body;
    }
    public sealed class InteractionEvent
    {
        public EventKind Type;
        public EventOperation OpName;
        public int Count;
        public long Timestamp;
        public Vector3 Position;
        public bool HasY = true;
    }
    public sealed class Mesh
    {
        public List<Vector3> Vertices = new();
        public List<int> Indices = new();
    }
    public sealed class GazeFrame
    {
        public int Frame;
        public float Time;
        public List<Vector3> Points = new();
    }
    public sealed class Gaze
    {
        public string Name;
        public List<GazeFrame> Items = new();
    }
    public sealed class CameraPose
    {
        public string Name;
        public Vector3 Position;
        public Quaternion Rotation;
    }
}
