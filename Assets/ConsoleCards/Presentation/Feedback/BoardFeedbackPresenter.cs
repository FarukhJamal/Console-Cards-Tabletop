using System.Collections.Generic;
using UnityEngine;

namespace ConsoleCards.Presentation.Feedback
{
    /// <summary>
    /// Shows the board feedback markers (doc 25, F2) from a small pool made once from the authored prefabs.
    /// Callers pass world positions every frame in reused lists; nothing is allocated while playing.
    /// </summary>
    public sealed class BoardFeedbackPresenter
    {
        private const int TrapWarningPool = 8;
        private const int MoveTargetPool = 16;
        private const int FlashPool = 6;
        private const int BurstPool = 3;
        private const int ReachPool = 16;

        private readonly BoardFeedbackSet set;
        private readonly BoardFeedbackMarker ring;
        private readonly BoardFeedbackMarker[] warnings;
        private readonly BoardFeedbackMarker[] targets;
        private readonly BoardFeedbackMarker[] flashes;
        private readonly BoardFeedbackMarker[] bursts;
        private readonly BoardFeedbackMarker[] reach;
        private readonly BoardFeedbackArrow arrow;
        private readonly List<GameObject> owned = new List<GameObject>();
        private int nextFlash;
        private int nextBurst;

        public BoardFeedbackPresenter(BoardFeedbackSet set, Transform parent)
        {
            this.set = set;
            ring = Make(set.ActivePawnRing, parent);
            warnings = MakePool(set.TrapWarning, parent, TrapWarningPool);
            targets = MakePool(set.MoveTarget, parent, MoveTargetPool);
            flashes = MakePool(set.FlipFlash, parent, FlashPool);
            bursts = MakePool(set.CollapseBurst, parent, BurstPool);
            reach = MakePool(set.ReachTile, parent, ReachPool);
            if (set.MoveArrow != null)
            {
                arrow = Object.Instantiate(set.MoveArrow, parent, false);
                arrow.name = set.MoveArrow.name;
                arrow.gameObject.SetActive(false);
                owned.Add(arrow.gameObject);
            }
        }

        public BoardFeedbackSet Set => set;

        public void ShowActivePawn(bool visible, Vector3 position, Color colour)
        {
            if (ring == null) return;
            if (!visible)
            {
                ring.Hide();
                return;
            }

            ring.Show(position + (Vector3.up * set.TileMarkerLift), colour);
        }

        public void ShowTrapWarnings(List<Vector3> positions)
        {
            ShowPool(warnings, positions, Vector3.up * set.TrapWarningLift, new Color(0.94f, 0.28f, 0.24f, 1f));
        }

        public void ShowMoveTargets(List<Vector3> positions, Color colour, bool showArrow, Vector3 arrowFrom)
        {
            ShowPool(targets, positions, Vector3.up * set.TileMarkerLift, colour);
            if (arrow == null) return;
            if (showArrow && positions.Count == 1) arrow.Show(arrowFrom, positions[0], colour);
            else arrow.Hide();
        }

        /// <summary>
        /// The tiles in reach (hints). Shown on request, not every frame, and kept apart from the other markers:
        /// <see cref="HideAll"/> leaves them; <see cref="HideReach"/> takes them away.
        /// </summary>
        public void ShowReach(List<Vector3> positions, Color colour)
        {
            ShowPool(reach, positions, Vector3.up * set.TileMarkerLift, colour);
        }

        public void HideReach()
        {
            HideRange(reach, 0);
        }

        public void Flash(Vector3 position, Color colour)
        {
            if (flashes.Length == 0) return;
            flashes[nextFlash].Show(position + (Vector3.up * set.TileMarkerLift), colour);
            nextFlash = (nextFlash + 1) % flashes.Length;
        }

        public void Burst(Vector3 position, Color colour)
        {
            if (bursts.Length == 0) return;
            bursts[nextBurst].Show(position + (Vector3.up * set.TileMarkerLift), colour);
            nextBurst = (nextBurst + 1) % bursts.Length;
        }

        public void HideAll()
        {
            ring?.Hide();
            HideRange(warnings, 0);
            HideRange(targets, 0);
            HideRange(flashes, 0);
            HideRange(bursts, 0);
            arrow?.Hide();
        }

        public void Destroy()
        {
            for (int i = 0; i < owned.Count; i++)
                if (owned[i] != null) Object.Destroy(owned[i]);
            owned.Clear();
        }

        private static void ShowPool(BoardFeedbackMarker[] pool, List<Vector3> positions, Vector3 offset, Color colour)
        {
            int shown = Mathf.Min(pool.Length, positions.Count);
            for (int i = 0; i < shown; i++)
            {
                pool[i].Show(positions[i] + offset, colour);
            }

            HideRange(pool, shown);
        }

        private static void HideRange(BoardFeedbackMarker[] pool, int from)
        {
            for (int i = from; i < pool.Length; i++) pool[i].Hide();
        }

        private BoardFeedbackMarker Make(BoardFeedbackMarker prefab, Transform parent)
        {
            if (prefab == null) return null;
            BoardFeedbackMarker instance = Object.Instantiate(prefab, parent, false);
            instance.name = prefab.name;
            instance.gameObject.SetActive(false);
            owned.Add(instance.gameObject);
            return instance;
        }

        private BoardFeedbackMarker[] MakePool(BoardFeedbackMarker prefab, Transform parent, int size)
        {
            if (prefab == null) return new BoardFeedbackMarker[0];
            BoardFeedbackMarker[] pool = new BoardFeedbackMarker[size];
            for (int i = 0; i < size; i++) pool[i] = Make(prefab, parent);
            return pool;
        }
    }
}
