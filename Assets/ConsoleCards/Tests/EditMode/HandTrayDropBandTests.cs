using ConsoleCards.Presentation.Views.Containers;
using NUnit.Framework;
using UnityEngine;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// CONSOLE-1 (2026-11-06): a hand card carried out of the hand comes back only over the hand's drop band, not over
    /// the whole tray. The whole tray reaches up over the player's own Console, so a card held over a Console slot was
    /// pulled back into the hand and its release became a return to the hand. Geometry only: a camera and the tray rig.
    /// </summary>
    public sealed class HandTrayDropBandTests
    {
        private const float CardDepth = 1.4f;
        private const float RowWidth = 4f;
        private const float SelectedRise = 0.5f;

        private GameObject cameraObject;
        private Material material;
        private HandTrayRig rig;
        private Camera camera;

        [SetUp]
        public void SetUp()
        {
            cameraObject = new GameObject("CONSOLE-1 test camera");
            camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.05f;
            Shader shader = Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Sprites/Default");
            material = new Material(shader);
            rig = HandTrayRig.Create(camera, 0, material);
            rig.Activate();
            rig.UpdateFrame(CardDepth, RowWidth, SelectedRise);
        }

        [TearDown]
        public void TearDown()
        {
            if (rig != null) Object.DestroyImmediate(rig.gameObject);
            if (cameraObject != null) Object.DestroyImmediate(cameraObject);
            if (material != null) Object.DestroyImmediate(material);
        }

        private Vector2 ScreenPointAtTrayLocal(float x, float z)
        {
            Vector3 world = rig.Anchor.TransformPoint(new Vector3(x, 0f, z));
            Vector3 screen = camera.WorldToScreenPoint(world);
            return new Vector2(screen.x, screen.y);
        }

        [Test]
        public void AboveTheCards_IsTrayButNotDropBand()
        {
            // Over the space the tray keeps above the cards for lifted cards (where the own Console shows).
            Vector2 point = ScreenPointAtTrayLocal(0f, (CardDepth * 0.5f) + (SelectedRise * 0.5f));
            Assert.IsTrue(rig.ContainsScreenPoint(point), "the whole tray still covers the space above the cards");
            Assert.IsFalse(rig.ContainsDropBandScreenPoint(point), "a carried-out card is not pulled back from there");
        }

        [Test]
        public void LowerPartOfTheCards_IsDropBand()
        {
            Vector2 point = ScreenPointAtTrayLocal(0f, -CardDepth * 0.25f);
            Assert.IsTrue(rig.ContainsScreenPoint(point));
            Assert.IsTrue(rig.ContainsDropBandScreenPoint(point), "the hand still takes a card back over its lower part");
        }

        [Test]
        public void DropBand_LiesInsideTheTray()
        {
            for (float z = -CardDepth; z <= CardDepth * 1.5f; z += 0.05f)
            {
                Vector2 point = ScreenPointAtTrayLocal(0.3f, z);
                if (rig.ContainsDropBandScreenPoint(point))
                {
                    Assert.IsTrue(rig.ContainsScreenPoint(point), $"drop band point at z={z} lies outside the tray");
                }
            }
        }

        [Test]
        public void BesideTheRow_IsNeither()
        {
            Vector2 point = ScreenPointAtTrayLocal(RowWidth, 0f);
            Assert.IsFalse(rig.ContainsScreenPoint(point));
            Assert.IsFalse(rig.ContainsDropBandScreenPoint(point));
        }
    }
}
