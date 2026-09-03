using System.Reflection;
using ConsoleCards.Presentation.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace ConsoleCards.Tests.EditMode.Presentation
{
    public sealed class PhysicalReleaseMotionTests
    {
        [Test]
        public void FirstFollow_DoesNotTurnPickupLiftOrCenteringIntoMomentum()
        {
            var motion = new PhysicalReleaseMotion(new PhysicalInteractionConfig());
            motion.Sample(new Vector3(20f, 15f, 10f), Quaternion.Euler(40f, 90f, 0f), 1f);
            motion.GetRelease(1f, out Vector3 linear, out Vector3 angular);
            Assert.That(linear, Is.EqualTo(Vector3.zero));
            Assert.That(angular, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void ShortNormalDrag_IsGentleButFastDragStillThrows()
        {
            Vector3 gentle = SampleConstantSpeed(2f, 6, 1f / 60f);
            Vector3 fast = SampleConstantSpeed(20f, 12, 1f / 60f);
            Assert.That(gentle.magnitude, Is.InRange(0.1f, 0.5f));
            Assert.That(fast.magnitude, Is.InRange(3f, 4f));
            Assert.That(fast.y, Is.Zero);
        }

        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void Smoothing_IsTimeBasedRatherThanFrameCountBased(int frameRate)
        {
            Vector3 actual = SampleConstantSpeed(8f, frameRate, 1f / frameRate);
            float expected = 2f * (1f - Mathf.Exp(-1f / 0.08f));
            Assert.That(actual.x, Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void LargeSamples_AreSmoothedAndClampedInBothChannels()
        {
            var config = new PhysicalInteractionConfig();
            var motion = new PhysicalReleaseMotion(config);
            motion.Sample(Vector3.zero, Quaternion.identity, 0f);
            motion.Sample(Vector3.one * 100f, Quaternion.Euler(0f, 170f, 0f), 0.01f);
            motion.GetRelease(0.01f, out Vector3 linear, out Vector3 angular);
            Assert.That(linear.magnitude, Is.InRange(0f, 0.5f));
            Assert.That(angular.magnitude, Is.InRange(0f, 0.75f));
            motion.Sample(Vector3.one * 200f, Quaternion.identity, 0.02f);
            motion.GetRelease(0.02f, out linear, out angular);
            Assert.That(linear.magnitude, Is.LessThanOrEqualTo(config.MaximumReleaseVelocity));
            Assert.That(angular.magnitude, Is.LessThanOrEqualTo(config.MaximumReleaseAngularVelocity));
        }

        [Test]
        public void InspectorTuning_IsSharedAndAppliedWithoutRecreatingSampler()
        {
            var config = new PhysicalInteractionConfig();
            var motion = new PhysicalReleaseMotion(config);
            Set(config, "releaseSmoothingSeconds", 0f);
            Set(config, "releaseVelocityMultiplier", 0.5f);
            Set(config, "releaseAngularVelocityMultiplier", 0.5f);
            Set(config, "maximumReleaseVelocity", 1f);
            Set(config, "maximumReleaseAngularVelocity", 2f);
            motion.Sample(Vector3.zero, Quaternion.identity, 0f);
            motion.Sample(Vector3.right, Quaternion.Euler(0f, 90f, 0f), 0.1f);
            motion.GetRelease(0.1f, out Vector3 linear, out Vector3 angular);
            Assert.That(linear.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(angular.y, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void PauseOrNewGrab_DoesNotReuseOldThrowMomentum()
        {
            var motion = new PhysicalReleaseMotion(new PhysicalInteractionConfig());
            motion.Sample(Vector3.zero, Quaternion.identity, 0f);
            motion.Sample(Vector3.right, Quaternion.Euler(0f, 90f, 0f), 0.05f);
            motion.GetRelease(0.2f, out Vector3 linear, out Vector3 angular);
            Assert.That(linear, Is.EqualTo(Vector3.zero));
            Assert.That(angular, Is.EqualTo(Vector3.zero));
            motion.Reset();
            motion.Sample(Vector3.one, Quaternion.identity, 0.21f);
            motion.GetRelease(0.21f, out linear, out angular);
            Assert.That(linear, Is.EqualTo(Vector3.zero));
            Assert.That(angular, Is.EqualTo(Vector3.zero));
        }

        private static Vector3 SampleConstantSpeed(float speed, int frames, float dt)
        {
            var motion = new PhysicalReleaseMotion(new PhysicalInteractionConfig());
            for (int i = 0; i <= frames; i++)
                motion.Sample(Vector3.right * (speed * i * dt), Quaternion.identity, i * dt);
            motion.GetRelease(frames * dt, out Vector3 linear, out _);
            return linear;
        }

        private static void Set(PhysicalInteractionConfig config, string field, float value) =>
            typeof(PhysicalInteractionConfig).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(config, value);
    }
}
