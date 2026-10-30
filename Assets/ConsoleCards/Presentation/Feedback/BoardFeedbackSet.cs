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
        [Tooltip("The table ping (UX-1): a ring at the spot a player pings. Empty: the active-pawn ring is used.")]
        [SerializeField] private BoardFeedbackMarker tablePing;
        [Header("Board feedback v2 (board 18): one look per meaning, for every game")]
        [Tooltip("Under the pointer: a thin frame. Empty: the move-target marker is used.")]
        [SerializeField] private BoardFeedbackMarker hoverOutline;
        [Tooltip("Where a carried piece will land. Empty: the move-target marker is used.")]
        [SerializeField] private BoardFeedbackMarker landingGhost;
        [Tooltip("Around each selected piece. Empty: the move-target marker is used.")]
        [SerializeField] private BoardFeedbackMarker selectionOutline;
        [Tooltip("Something made harmless (a disarmed trap). Empty: the move-target marker is used.")]
        [SerializeField] private BoardFeedbackMarker disabledMark;
        [Tooltip("Board 18: the colours below follow the meaning of a mark, whichever game asks for it.")]
        [SerializeField] private bool coloursByMeaning;
        [SerializeField] private Color targetColour = new Color(0.024f, 0.937f, 0.941f, 1f);
        [SerializeField] private Color dangerColour = new Color(1f, 0.231f, 0.231f, 1f);
        [SerializeField] private Color eventColour = new Color(0.949f, 0.463f, 0.180f, 1f);
        [SerializeField] private Color disabledColour = new Color(0.608f, 0.643f, 0.655f, 0.9f);
        [SerializeField] private Color hoverColour = new Color(0.910f, 0.925f, 0.929f, 0.9f);
        [Tooltip("Reach dots (where a picked-up pawn can go): white with a dark ring, to read on every card.")]
        [SerializeField] private Color reachColour = new Color(1f, 1f, 1f, 1f);
        [Header("Drawing tool (Final World prefabs)")]
        [Tooltip("A drawn line: a flat Line Renderer the drawing tool copies for each pen or highlighter line.")]
        [SerializeField] private LineRenderer tableMarkLine;
        [Tooltip("A label on the table: a Text Mesh the drawing tool copies for each label.")]
        [SerializeField] private TextMesh tableMarkLabel;
        [Tooltip("The repeating pictures of the Dashed and Dotted line styles (empty: those lines are plain).")]
        [SerializeField] private Material tableMarkDashMaterial;
        [SerializeField] private Material tableMarkDotMaterial;
        [Tooltip("Sizes in table units (a card is about 2 wide): the pen, the highlighter, a label's letters, the eraser.")]
        [SerializeField] private float penWidth = 0.07f;
        [SerializeField] private float highlighterWidth = 0.4f;
        [SerializeField] private float labelHeight = 0.45f;
        [SerializeField] private float eraserRadius = 0.3f;
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
        public BoardFeedbackMarker TablePing => tablePing != null ? tablePing : activePawnRing;
        public BoardFeedbackMarker HoverOutline => hoverOutline != null ? hoverOutline : moveTarget;
        public BoardFeedbackMarker LandingGhost => landingGhost != null ? landingGhost : moveTarget;
        public BoardFeedbackMarker SelectionOutline => selectionOutline != null ? selectionOutline : moveTarget;
        public BoardFeedbackMarker DisabledMark => disabledMark != null ? disabledMark : moveTarget;
        public bool ColoursByMeaning => coloursByMeaning;
        public Color TargetColour => targetColour;
        public Color DangerColour => dangerColour;
        public Color EventColour => eventColour;
        public Color DisabledColour => disabledColour;
        public Color HoverColour => hoverColour;
        public Color ReachColour => reachColour;
        public LineRenderer TableMarkLine => tableMarkLine;
        public TextMesh TableMarkLabel => tableMarkLabel;
        public Material TableMarkDashMaterial => tableMarkDashMaterial;
        public Material TableMarkDotMaterial => tableMarkDotMaterial;
        public float PenWidth => penWidth;
        public float HighlighterWidth => highlighterWidth;
        public float LabelHeight => labelHeight;
        public float EraserRadius => eraserRadius;
        public float TileMarkerLift => tileMarkerLift;
        public float TrapWarningLift => trapWarningLift;
        public float TrapKick => trapKick;
        public float EliminatedKick => eliminatedKick;
        public float CollapseKick => collapseKick;
        public float KickSeconds => kickSeconds;
        public float CollapseGlanceSeconds => collapseGlanceSeconds;
    }
}
