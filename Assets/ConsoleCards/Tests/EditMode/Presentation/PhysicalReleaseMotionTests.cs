using System.Reflection;
using ConsoleCards.Core.Domain;
using ConsoleCards.Presentation.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace ConsoleCards.Tests.EditMode.Presentation
{
    public sealed class PhysicalReleaseMotionTests
    {
        [Test]
        public void FirstLinearSample_DoesNotManufactureMomentum()
        {
            var motion = new PhysicalReleaseMotion(new PhysicalInteractionConfig());
            motion.SampleLinear(new Vector3(20f, 15f, 10f), 1f);
            Assert.That(motion.GetLinearRelease(1f), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void LinearRelease_UsesRecentTabletopPlaneMotion()
        {
            var motion = new PhysicalReleaseMotion(new PhysicalInteractionConfig());
            motion.SampleLinear(new Vector3(0f, 1f, 0f), 0f);
            motion.SampleLinear(new Vector3(0.08f, 5f, 0.04f), 0.02f);
            motion.SampleLinear(new Vector3(0.16f, 9f, 0.08f), 0.04f);

            Vector3 velocity = motion.GetLinearRelease(0.04f);

            Assert.That(velocity.x, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(velocity.y, Is.Zero);
            Assert.That(velocity.z, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void LinearRelease_UsesOnlyConfiguredRecentWindow()
        {
            var config = new PhysicalInteractionConfig();
            Set(config, "dragVelocitySampleWindowSeconds", 0.05f);
            var motion = new PhysicalReleaseMotion(config);
            motion.SampleLinear(Vector3.zero, 0f);
            motion.SampleLinear(Vector3.right * 0.04f, 0.04f);
            motion.SampleLinear(Vector3.right * 0.16f, 0.08f);

            Vector3 velocity = motion.GetLinearRelease(0.08f);

            Assert.That(velocity.x, Is.EqualTo(3f).Within(0.0001f));
        }

        [Test]
        public void AutomaticRotationWithoutDeliberateSamples_DoesNotCreateAngularRelease()
        {
            var motion = new PhysicalReleaseMotion(new PhysicalInteractionConfig());
            motion.SampleLinear(Vector3.zero, 0f);
            motion.SampleLinear(Vector3.right, 0.1f);
            Assert.That(motion.GetAngularRelease(0.1f), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void DeliberateRotationSamples_CreateClampedAngularRelease()
        {
            var config = new PhysicalInteractionConfig();
            var motion = new PhysicalReleaseMotion(config);
            motion.SampleDeliberateRotation(Quaternion.identity, 0f);
            motion.SampleDeliberateRotation(Quaternion.Euler(0f, 90f, 0f), 0.1f);

            Vector3 angular = motion.GetAngularRelease(0.1f);

            Assert.That(angular.y, Is.EqualTo(config.MaximumReleaseAngularVelocity).Within(0.0001f));
        }

        [Test]
        public void ProfileRelease_AppliesOneLinearRetentionAndClamp()
        {
            PhysicalObjectInteractionProfile die = new PhysicalInteractionConfig()
                .ResolveProfile(TabletopObjectKind.Die);
            Vector3 linear = new Vector3(20f, 4f, 0f);
            Vector3 angular = Vector3.zero;

            die.ApplyRelease(ref linear, ref angular);

            Assert.That(linear, Is.EqualTo(Vector3.right * 8f));
        }

        [Test]
        public void PauseOrNewGrab_DoesNotReuseOldMomentum()
        {
            var motion = new PhysicalReleaseMotion(new PhysicalInteractionConfig());
            motion.SampleLinear(Vector3.zero, 0f);
            motion.SampleLinear(Vector3.right, 0.05f);
            motion.SampleDeliberateRotation(Quaternion.identity, 0f);
            motion.SampleDeliberateRotation(Quaternion.Euler(0f, 90f, 0f), 0.05f);

            Assert.That(motion.GetLinearRelease(0.22f), Is.EqualTo(Vector3.zero));
            Assert.That(motion.GetAngularRelease(0.22f), Is.EqualTo(Vector3.zero));

            motion.Reset();
            motion.SampleLinear(Vector3.one, 0.23f);
            Assert.That(motion.GetLinearRelease(0.23f), Is.EqualTo(Vector3.zero));
            Assert.That(motion.GetAngularRelease(0.23f), Is.EqualTo(Vector3.zero));
        }

        private static void Set(PhysicalInteractionConfig config, string field, float value) =>
            typeof(PhysicalInteractionConfig).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(config, value);
    }
}
