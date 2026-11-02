using System.Collections.Generic;

namespace XRLumina.Core.Models
{
    public enum TestAction : byte
    {
        Start = 0,
        Finish = 1
    }
    public enum FlushGroup : byte
    {
        Unknown = 0,
        Interaction = 1,
        EyeTracking = 2,
        Heuristics = 3
    }
    public enum ImageFormat : byte
    {
        Jpeg = 0,
        Png = 1
    }
    public enum EventKind : byte
    {
        Task = 0,
        Npc = 1,
        Gesture = 2,
        Controller = 3,
        Object = 4
    }
    public enum EventOperation : byte
    {
        ReportCount = 0
    }
    public enum HeuristicTrigger : byte
    {
        Unknown = 0,
        Stationary = 1,
        HeadDirectionChange = 2,
        RepeatedInteraction = 3
    }
    public enum ControllerHand : byte
    {
        Unknown = 0,
        Left = 1,
        Right = 2
    }
    public enum ControllerInput : byte
    {
        None = 0,
        Primary = 1,
        Secondary = 2,
        Trigger = 3,
        Grip = 4,
        Interact = 5
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
