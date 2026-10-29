using XRLumina.Unity.Contracts;
using System.Collections.Generic;
using UnityEngine;
using HeuristicScreenshotTrigger = XRLumina.Core.API.HeuristicScreenshotTrigger;
using ControllerInput = XRLumina.Core.API.ControllerInput;
using ControllerHand = XRLumina.Core.API.ControllerHand;
using Wire = XRLumina.Core.API;
namespace XRLumina.Unity.Policies
{
    /// <summary>기존 Inspector 기준을 사용하는 교체 가능한 기본 휴리스틱 캡처 정책.</summary>
    internal sealed class DefaultHeuristicCapturePolicy : IXRLuminaHeuristicCapturePolicy
    {
        private readonly struct PositionSample
        {
            internal float Time { get; }
            internal Vector2 Position { get; }

            /// <summary>판정 시각과 수평 위치를 저장한다.</summary>
            internal PositionSample(float time, Vector2 position)
            {
                Time = time;
                Position = position;
            }
        }

        private readonly float _stationaryDurationSeconds;
        private readonly float _stationaryMovementThreshold;
        private readonly float _yawWindowSeconds;
        private readonly int _yawRequiredReversals;
        private readonly float _yawMinimumSwingDegrees;
        private readonly float _interactionWindowSeconds;
        private readonly int _interactionRequiredCount;
        private readonly Queue<PositionSample> _positionSamples = new();
        private readonly Queue<float> _yawReversalTimes = new();
        private readonly Dictionary<(int ObjectId, Wire.ControllerHand Hand, Wire.ControllerInput Input), Queue<float>> _interactionTimes = new();
        private float _stationaryDistance;
        private bool _stationaryCaptured;
        private bool _hasYaw;
        private float _lastYaw;
        private int _yawDirection;
        private float _yawSwingDegrees;
        /// <summary>기본 캡처 판단 기준을 저장한다.</summary>
        internal DefaultHeuristicCapturePolicy(float stationaryDurationSeconds, float stationaryMovementThreshold, float yawWindowSeconds, int yawRequiredReversals, float yawMinimumSwingDegrees, float interactionWindowSeconds, int interactionRequiredCount)
        {
            _stationaryDurationSeconds = stationaryDurationSeconds;
            _stationaryMovementThreshold = stationaryMovementThreshold;
            _yawWindowSeconds = yawWindowSeconds;
            _yawRequiredReversals = yawRequiredReversals;
            _yawMinimumSwingDegrees = yawMinimumSwingDegrees;
            _interactionWindowSeconds = interactionWindowSeconds;
            _interactionRequiredCount = interactionRequiredCount;
        }
        /// <summary>현재 위치와 회전으로 캡처 발생 여부를 판단한다.</summary>
        public HeuristicScreenshotTrigger? EvaluatePose(Vector3 position, float yaw, float capturedAt)
        {
            return UpdateStationary(position, capturedAt) ?? UpdateYaw(yaw, capturedAt);
        }
        /// <summary>대상과 입력 조합의 반복 조건을 판단한다.</summary>
        public HeuristicScreenshotTrigger? EvaluateInteraction(int objectId, string objectName,
            ControllerInput controllerInput, ControllerHand hand, float capturedAt)
        {
            var key = (objectId, hand, controllerInput);
            if (!_interactionTimes.TryGetValue(key, out var timestamps))
            {
                timestamps = new Queue<float>();
                _interactionTimes[key] = timestamps;
            }
            timestamps.Enqueue(capturedAt);
            while (timestamps.Count > 0 && capturedAt - timestamps.Peek() > _interactionWindowSeconds)
            {
                timestamps.Dequeue();
            }
            if (timestamps.Count < _interactionRequiredCount)
            {
                return null;
            }

            timestamps.Clear();
            return new HeuristicScreenshotTrigger(
                Wire.HeuristicTrigger.RepeatedInteraction,
                capturedAt,
                objectName,
                controllerInput, hand);
        }
        /// <summary>XZ 누적 이동 거리가 기준 이하로 유지된 정지 구간을 판정한다.</summary>
        private HeuristicScreenshotTrigger? UpdateStationary(Vector3 position, float now)
        {
            var current = new PositionSample(now, new Vector2(position.x, position.z));
            _positionSamples.Enqueue(current);

            while (_positionSamples.Count > 1)
            {
                var samples = _positionSamples.ToArray();
                if (now - samples[1].Time < _stationaryDurationSeconds)
                {
                    break;
                }
                var removed = _positionSamples.Dequeue();
                if (_positionSamples.Count > 0)
                {
                    _stationaryDistance -= Vector2.Distance(removed.Position, _positionSamples.Peek().Position);
                }
            }

            _stationaryDistance = Mathf.Max(0f, CalculateCumulativeDistance(_positionSamples));
            if (_stationaryDistance > _stationaryMovementThreshold)
            {
                _stationaryCaptured = false;
                return null;
            }
            if (
                _stationaryCaptured ||
                _positionSamples.Count < 2 ||
                now - _positionSamples.Peek().Time < _stationaryDurationSeconds)
            {
                return null;
            }

            _stationaryCaptured = true;
            return new HeuristicScreenshotTrigger(Wire.HeuristicTrigger.Stationary, now);
        }
        /// <summary>최근 Yaw 방향 반전 중 30도 이상 회전한 구간 수를 판정한다.</summary>
        private HeuristicScreenshotTrigger? UpdateYaw(float yaw, float now)
        {
            if (!_hasYaw)
            {
                _hasYaw = true;
                _lastYaw = yaw;
                return null;
            }

            var delta = Mathf.DeltaAngle(_lastYaw, yaw);
            _lastYaw = yaw;
            if (Mathf.Abs(delta) < 0.5f)
            {
                return null;
            }

            var direction = delta > 0f ? 1 : -1;
            if (_yawDirection == 0 || direction == _yawDirection)
            {
                _yawDirection = direction;
                _yawSwingDegrees += Mathf.Abs(delta);
                return null;
            }

            if (_yawSwingDegrees >= _yawMinimumSwingDegrees)
            {
                _yawReversalTimes.Enqueue(now);
            }
            _yawDirection = direction;
            _yawSwingDegrees = Mathf.Abs(delta);
            while (_yawReversalTimes.Count > 0 && now - _yawReversalTimes.Peek() > _yawWindowSeconds)
            {
                _yawReversalTimes.Dequeue();
            }
            if (_yawReversalTimes.Count < _yawRequiredReversals)
            {
                return null;
            }

            _yawReversalTimes.Clear();
            return new HeuristicScreenshotTrigger(Wire.HeuristicTrigger.HeadDirectionChange, now);
        }
        /// <summary>위치 샘플 사이의 누적 이동거리를 계산한다.</summary>
        private static float CalculateCumulativeDistance(IEnumerable<PositionSample> samples)
        {
            var hasPrevious = false;
            var previous = Vector2.zero;
            var total = 0f;
            foreach (var sample in samples)
            {
                if (hasPrevious)
                {
                    total += Vector2.Distance(previous, sample.Position);
                }
                previous = sample.Position;
                hasPrevious = true;
            }
            return total;
        }
        /// <summary>새 측정을 위해 정책의 누적 상태를 초기화한다.</summary>
        public void Reset()
        {
            _positionSamples.Clear();
            _yawReversalTimes.Clear();
            _interactionTimes.Clear();
            _stationaryDistance = 0f;
            _stationaryCaptured = false;
            _hasYaw = false;
            _lastYaw = 0f;
            _yawDirection = 0;
            _yawSwingDegrees = 0f;
        }
    }
}
