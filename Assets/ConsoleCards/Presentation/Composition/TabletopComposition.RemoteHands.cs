using System.Collections.Generic;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Settings;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// HANDS-1, designer note 01 (2026-11-17: "explore a visible hand or cursor for interacting with the table; this
    /// may require a 3D implementation; assess that requirement before building it"). Assessed: on this computer the
    /// pointer already is a hand (Resources/CursorSet: an open hand over a piece, a grab hand while holding one), a 2D
    /// cursor, so it needs nothing in 3D. What a player could not see is the others' hands at an online table: those
    /// are in 3D. Each other player's pointer is shown where it points on the table, as the same hand picture in their
    /// colour (closed while they hold a piece), facing the camera, gliding between the live updates; it goes when their
    /// pointer leaves the table or nothing is heard for a few seconds. No names (designer note 04). Per-frame code does
    /// not allocate; a hand is made once per player.
    /// </summary>
    public sealed partial class TabletopComposition
    {
        private const float RemoteHandSeconds = 6f;
        private const float RemoteHandSize = 1.2f; // table units (about a card wide)
        private const float RemoteHandLift = 2f; // hand sizes toward the camera, along its line of sight
        private const float RemoteHandGlide = 18f;

        private sealed class RemoteHand
        {
            public Transform Root;
            public SpriteRenderer Picture;
            public Vector3 Target;
            public bool Visible;
            public bool Holding;
            public bool Placed;
            public float SeenAt;
        }

        private readonly Dictionary<PlayerId, RemoteHand> remoteHands = new Dictionary<PlayerId, RemoteHand>();
        private Transform remoteHandRoot;
        private Sprite remoteHandOpen;
        private Sprite remoteHandClosed;
        private bool remoteHandSpritesMade;

        /// <summary>
        /// HANDS-1: where this computer's pointer points on the table, and whether it holds a piece. False while it is
        /// over a menu or panel, outside the window, or the window is not in front.
        /// </summary>
        internal bool TryGetLocalHand(out Vector3 point, out bool holding)
        {
            point = Vector3.zero;
            holding = false;
            if (!IsInitialized || !UnityEngine.Application.isFocused) return false;
            Mouse mouse = Mouse.current;
            if (mouse == null) return false;
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.IsPointerOverGameObject()) return false;
            Vector2 screen = mouse.position.ReadValue();
            if (screen.x < 0f || screen.y < 0f || screen.x > Screen.width || screen.y > Screen.height) return false;
            if (!TryResolvePingPoint(screen, out point)) return false;
            holding = interactionRouter != null && interactionRouter.HasActiveInteraction;
            return true;
        }

        /// <summary>HANDS-1: another computer's pointer (the host has checked who sent it).</summary>
        internal void ShowRemoteHand(PlayerId player, Vector3 point, bool visible, bool holding)
        {
            if (!IsInitialized || player.IsEmpty || IsPlayerOnThisComputer(player)) return;
            if (!remoteHands.TryGetValue(player, out RemoteHand hand))
            {
                if (!visible) return;
                hand = MakeRemoteHand(player);
                if (hand == null) return;
                remoteHands.Add(player, hand);
            }

            hand.SeenAt = Time.unscaledTime;
            hand.Visible = visible;
            hand.Holding = holding;
            if (!visible) return;
            hand.Target = point;
            hand.Picture.color = FeedbackColour(player);
        }

        // Once a frame: each other player's hand glides to where they point, turned to this computer's camera.
        private void TickRemoteHands()
        {
            if (remoteHands.Count == 0) return;
            bool online = IsInitialized && IsOnlineTable;
            UnityEngine.Camera viewer = hitResolver != null ? hitResolver.TargetCamera : null;
            float unit = classicFrame.WorldPerTableUnit > 0f ? classicFrame.WorldPerTableUnit : 1f;
            float glide = 1f - Mathf.Exp(-RemoteHandGlide * Time.unscaledDeltaTime);
            float now = Time.unscaledTime;
            foreach (KeyValuePair<PlayerId, RemoteHand> pair in remoteHands)
            {
                RemoteHand hand = pair.Value;
                if (hand.Root == null) continue;
                bool show = online && hand.Visible && now - hand.SeenAt < RemoteHandSeconds;
                if (hand.Root.gameObject.activeSelf != show) hand.Root.gameObject.SetActive(show);
                if (!show)
                {
                    hand.Placed = false;
                    continue;
                }

                // Turned to the camera, the hand's lower part would dip into the table and under the pieces: it comes
                // toward the camera along its line of sight, so on screen it stays exactly over the spot.
                float size = RemoteHandSize * unit;
                Vector3 place = hand.Target;
                if (viewer != null)
                {
                    Vector3 toCamera = viewer.transform.position - hand.Target;
                    float distance = toCamera.magnitude;
                    float lift = Mathf.Min(RemoteHandLift * size, distance * 0.5f);
                    if (distance > 1e-4f) place += toCamera * (lift / distance);
                }

                hand.Root.position = hand.Placed ? Vector3.Lerp(hand.Root.position, place, glide) : place;
                hand.Placed = true;
                if (viewer != null) hand.Root.rotation = viewer.transform.rotation;
                if (Mathf.Abs(hand.Root.localScale.x - size) > 0.0001f) hand.Root.localScale = new Vector3(size, size, size);
                Sprite picture = hand.Holding && remoteHandClosed != null ? remoteHandClosed : remoteHandOpen;
                if (hand.Picture.sprite != picture) hand.Picture.sprite = picture;
            }
        }

        private RemoteHand MakeRemoteHand(PlayerId player)
        {
            if (!remoteHandSpritesMade)
            {
                remoteHandSpritesMade = true;
                CursorSet set = Cursors;
                if (set != null)
                {
                    remoteHandOpen = HandSprite(set.Hand, set.HandHotspot);
                    remoteHandClosed = HandSprite(set.Grab, set.GrabHotspot);
                }
            }

            if (remoteHandOpen == null) return null;
            if (remoteHandRoot == null)
            {
                remoteHandRoot = new GameObject("RemoteHands").transform;
                remoteHandRoot.SetParent(transform, false);
            }

            GameObject made = new GameObject($"RemoteHand {remoteHands.Count + 1}");
            made.transform.SetParent(remoteHandRoot, false);
            SpriteRenderer picture = made.AddComponent<SpriteRenderer>();
            picture.sprite = remoteHandOpen;
            picture.color = FeedbackColour(player);
            picture.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            picture.receiveShadows = false;
            picture.sortingOrder = 50;
            made.SetActive(false);
            return new RemoteHand { Root = made.transform, Picture = picture };
        }

        // The cursor picture as a sprite one unit wide, its hot spot (the fingertip) at the hand's place.
        private static Sprite HandSprite(Texture2D texture, Vector2 hotspot)
        {
            if (texture == null) return null;
            float width = texture.width;
            float height = texture.height;
            Vector2 pivot = new Vector2(
                Mathf.Clamp01(hotspot.x / width),
                Mathf.Clamp01(1f - (hotspot.y / height)));
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), pivot, width);
            sprite.name = texture.name + " (remote hand)";
            return sprite;
        }
    }
}
