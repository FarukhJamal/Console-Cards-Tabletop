using UnityEngine;

namespace ConsoleCards.Presentation.Feedback
{
    /// <summary>
    /// The board feedback prefabs and camera-kick strengths (doc 25, F2). Lives at
    /// Resources/BoardFeedbackSet so the game finds it without a scene reference; edit it freely.
    /// </summary>
    [CreateAssetMenu(fileName = "BoardFeedbackSet", menuName = "Console Cards/Feedback/Board Feedback Set")]
    public sealed class BoardFeedbackSet : ScriptableObject
    {
        public const string ResourcePath = "BoardFeedbackSet";

        [Header("Markers (Final World prefabs)")]
        [SerializeField] private BoardFeedbackMarker activePawnRing;
        [SerializeField] private BoardFeedbackMarker trapWarning;
        [SerializeField] private BoardFeedbackMarker moveTarget;
        [SerializeField] private BoardFeedbackMarker flipFlash;
        [SerializeField] private BoardFeedbackMarker collapseBurst;
        [SerializeField] private BoardFeedbackArrow moveArrow;
        [Tooltip("Tiles in reach while the turn player holds their pawn (doc 23, hints). Empty: the move-target marker is used.")]
        [SerializeField] private BoardFeedbackMarker reachTile;
        [Tooltip("The tile the floor dice point at (the Floorfall collapse target, doc 23 H3). Empty: the move-target marker is used.")]
        [SerializeField] private BoardFeedbackMarker collapseTarget;
        [Header("Placement")]
        [SerializeField] private float tileMarkerLift = 0.03f;
        [SerializeField] private float trapWarningLift = 0.9f;
        [Header("Camera kicks (they always return)")]
        [SerializeField] private float trapKick = 0.06f;
        [SerializeField] private float eliminatedKick = 0.12f;
        [SerializeField] private float collapseKick = 0.1f;
        [SerializeField] private float kickSeconds = 0.35f;
        [SerializeField] private float collapseGlanceSeconds = 1.1f;

        public BoardFeedbackMarker ActivePawnRing => activePawnRing;
        public BoardFeedbackMarker TrapWarning => trapWarning;
        public BoardFeedbackMarker MoveTarget => moveTarget;
        public BoardFeedbackMarker FlipFlash => flipFlash;
        public BoardFeedbackMarker CollapseBurst => collapseBurst;
        public BoardFeedbackArrow MoveArrow => moveArrow;
        public BoardFeedbackMarker ReachTile => reachTile != null ? reachTile : moveTarget;
        public BoardFeedbackMarker CollapseTarget => collapseTarget != null ? collapseTarget : moveTarget;
        public float TileMarkerLift => tileMarkerLift;
        public float TrapWarningLift => trapWarningLift;
        public float TrapKick => trapKick;
        public float EliminatedKick => eliminatedKick;
        public float CollapseKick => collapseKick;
        public float KickSeconds => kickSeconds;
        public float CollapseGlanceSeconds => collapseGlanceSeconds;
    }
}
