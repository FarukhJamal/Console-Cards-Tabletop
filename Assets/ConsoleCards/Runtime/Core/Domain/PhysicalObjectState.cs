using System;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Core.Domain
{
    [Serializable]
    public readonly struct PhysicalVector3
    {
        public PhysicalVector3(float x, float y, float z)
        {
            if (!Finite(x) || !Finite(y) || !Finite(z)) throw new ArgumentOutOfRangeException(nameof(x));
            X = x; Y = y; Z = z;
        }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [Serializable]
    public readonly struct PhysicalRotation
    {
        public PhysicalRotation(float x, float y, float z, float w)
        {
            double length = Math.Sqrt((double)x * x + (double)y * y + (double)z * z + (double)w * w);
            if (double.IsNaN(length) || double.IsInfinity(length) || length < 0.000001d)
                throw new ArgumentOutOfRangeException(nameof(w));
            X = (float)(x / length); Y = (float)(y / length);
            Z = (float)(z / length); W = (float)(w / length);
        }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float W { get; }
        public bool IsValid => X * X + Y * Y + Z * Z + W * W > 0.99f;
        public PhysicalRotation RotateWorldYaw(float degrees)
        {
            double half = degrees * Math.PI / 360d;
            float s = (float)Math.Sin(half), c = (float)Math.Cos(half);
            return new PhysicalRotation(c * X + s * Z, c * Y + s * W, c * Z - s * X, c * W - s * Y);
        }
    }

    public enum PhysicalObjectMode { Dynamic, Held, Sleeping, SleepingUnresolved }

    /// <summary>Immutable accepted world-space state. TabletopPose remains the independent layout pose.</summary>
    [Serializable]
    public sealed class PhysicalObjectState
    {
        public const int SchemaVersion = 1;
        public PhysicalObjectState(PhysicalVector3 position, PhysicalRotation rotation,
            PhysicalVector3 velocity, PhysicalVector3 angularVelocity, PhysicalObjectMode mode,
            PlayerId controllingPlayerId)
        {
            if (!rotation.IsValid || !Enum.IsDefined(typeof(PhysicalObjectMode), mode))
                throw new ArgumentException("Invalid physical rotation or lifecycle.");
            if (controllingPlayerId.IsEmpty) throw new ArgumentException("Physical authority requires an actor.");
            Position = position; Rotation = rotation; Velocity = velocity; AngularVelocity = angularVelocity;
            Mode = mode; ControllingPlayerId = controllingPlayerId;
        }
        public PhysicalVector3 Position { get; }
        public PhysicalRotation Rotation { get; }
        public PhysicalVector3 Velocity { get; }
        public PhysicalVector3 AngularVelocity { get; }
        public PhysicalObjectMode Mode { get; }
        public PlayerId ControllingPlayerId { get; }
        public PhysicalObjectState WithRotation(PhysicalRotation rotation) =>
            new PhysicalObjectState(Position, rotation, Velocity, AngularVelocity, Mode, ControllingPlayerId);
    }
}
