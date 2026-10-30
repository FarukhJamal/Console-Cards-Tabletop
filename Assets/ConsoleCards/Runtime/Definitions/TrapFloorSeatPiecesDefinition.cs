using System;
using ConsoleCards.GameTemplates;
using UnityEngine;

namespace ConsoleCards.Definitions
{
    /// <summary>
    /// SETUP-1 (owner, 2026-10-23: "the whole default setup ... the console mat, the board placement, the dice"):
    /// the game's whole default table setup. The board, the Consoles, the shared piles and the dice are places on the
    /// table. SETUP-2 (owner, 2026-10-30: "the axes are different ... give all the three axes, because it's in a 3D
    /// world"): every place is a position in the scene's own X, Y and Z, the same numbers the Transform shows (X to the
    /// right of Player 1, Z away from Player 1, Y up). The pieces always rest on the table top, so Y only shows the
    /// height. The players' own saved table layout ("Arrange table…") still wins over them.
    ///
    /// SEAT-1 (owner, 2026-10-23: "two initial ability cards are edge of the table ... have this data as scriptable so
    /// that i can adjust positioning rotation etc later"): where the pieces beside every Console go. All distances are
    /// table units (1 = a card's width, 2.5"), measured from the Console: "toward board" moves a piece along the
    /// Console's depth (0 is level with the Console's centre, less goes toward the player), "outward" moves it away
    /// from the Console along its side. The same values are used for every seat. A layout that no longer fits (pieces
    /// overlapping) is reported in the Console and the built-in places are used.
    /// </summary>
    [CreateAssetMenu(menuName = "Console Cards/Trap Floor/Table Setup", fileName = "TrapFloorTableSetup")]
    public sealed class TrapFloorSeatPiecesDefinition : ScriptableObject
    {
        [Header("Board (the playmat the floor lies on)")]
        [Tooltip("The board's centre on the table. The floor and its playmat move with it.")]
        [SerializeField] private Vector3 boardCentreAt = new Vector3(-0.2f, TableTop, 0.05f);
        [Tooltip("The playmat's width and depth in table units (1 = 2.5\"; 14 x 9.6 is the 35 x 24\" mat).")]
        [SerializeField] private Vector2 boardMatSize = new Vector2(14f, 9.6f);

        [Header("Consoles (each player's Console mat; 2 players use Players 1 and 3, 3 players use 1 to 3)")]
        [Tooltip("Player 1, in front. Turn 0 faces the board from the front.")]
        [SerializeField] private TrapFloorTablePlace player1Console = new TrapFloorTablePlace(new Vector3(-0.2f, TableTop, -7.45f), 0f);
        [Tooltip("Player 2, on the left. Turn 90 faces the board from the left.")]
        [SerializeField] private TrapFloorTablePlace player2Console = new TrapFloorTablePlace(new Vector3(-9.9f, TableTop, 0.05f), 90f);
        [Tooltip("Player 3, at the back. Turn 180 faces the board from the back.")]
        [SerializeField] private TrapFloorTablePlace player3Console = new TrapFloorTablePlace(new Vector3(-0.2f, TableTop, 7.55f), 180f);
        [Tooltip("Player 4, on the right. Turn 270 faces the board from the right.")]
        [SerializeField] private TrapFloorTablePlace player4Console = new TrapFloorTablePlace(new Vector3(9.5f, TableTop, 0.05f), 270f);

        [Header("Shared piles")]
        [Tooltip("The Controller deck everyone draws from.")]
        [SerializeField] private TrapFloorTablePlace controllerDeck = new TrapFloorTablePlace(new Vector3(9.4f, TableTop, -8.95f), 0f);
        [Tooltip("The Ability deck.")]
        [SerializeField] private TrapFloorTablePlace abilityDeck = new TrapFloorTablePlace(new Vector3(-11.2f, TableTop, 9.05f), 0f);
        [Tooltip("The Ability discard pile.")]
        [SerializeField] private TrapFloorTablePlace abilityDiscard = new TrapFloorTablePlace(new Vector3(-9.0f, TableTop, 9.05f), 0f);

        [Header("Dice")]
        [Tooltip("The Floorfall die for the column (X).")]
        [SerializeField] private Vector3 xAxisDieAt = new Vector3(-11.0f, TableTop, -8.95f);
        [Tooltip("The Floorfall die for the row (Y).")]
        [SerializeField] private Vector3 yAxisDieAt = new Vector3(-10.1f, TableTop, -8.95f);
        [Tooltip("The d4 the Blind trap rolls for its direction (beside the two d6, with room so they do not touch).")]
        [SerializeField] private Vector3 directionDieAt = new Vector3(-8.9f, TableTop, -8.95f);

        [Header("Starting Ability cards (dealt beside the Console)")]
        [Tooltip("The side of the Console the row of starting Ability cards goes on (seen from the player).")]
        [SerializeField] private ConsoleSide startingAbilitiesSide = ConsoleSide.Left;
        [Tooltip("Along the Console's depth: 0 is level with the Console's centre, more goes toward the board, less "
            + "toward the player (the table edge).")]
        [SerializeField] private float startingAbilitiesTowardBoard = 0.9f;
        [Tooltip("Moves the whole row along the side: more goes away from the Console, less (negative) closer. 0 is "
            + "just past the pile on that side.")]
        [SerializeField] private float startingAbilitiesOutward;
        [Tooltip("The gap between two starting Ability cards in the row.")]
        [SerializeField, Min(0f)] private float startingAbilitiesGap = 0.2f;
        [Tooltip("Extra turn of each starting Ability card, in degrees, on top of the Console's own turn (a landscape "
            + "card keeps its quarter turn as well).")]
        [SerializeField] private float startingAbilitiesTurn;

        [Header("Hand zone (right of the Console)")]
        [Tooltip("Along the Console's depth: more goes toward the board, less toward the player.")]
        [SerializeField] private float handZoneTowardBoard;
        [Tooltip("Along the side: more goes away from the Console, less closer.")]
        [SerializeField] private float handZoneOutward;

        [Header("Action stack (left of the Console, where extra Abilities go)")]
        [Tooltip("Along the Console's depth: more goes toward the board, less toward the player.")]
        [SerializeField] private float actionStackTowardBoard;
        [Tooltip("Along the side: more goes away from the Console, less closer.")]
        [SerializeField] private float actionStackOutward;

        /// <summary>SETUP-2: the table top's height in the Tabletop scene (the Y shown for every place).</summary>
        public const float TableTop = 11.6f;

        public Vector3 BoardCentre => boardCentreAt;
        public Vector2 BoardMatSize => boardMatSize;
        public TrapFloorTablePlace Player1Console => player1Console;
        public TrapFloorTablePlace Player2Console => player2Console;
        public TrapFloorTablePlace Player3Console => player3Console;
        public TrapFloorTablePlace Player4Console => player4Console;
        public TrapFloorTablePlace ControllerDeck => controllerDeck;
        public TrapFloorTablePlace AbilityDeck => abilityDeck;
        public TrapFloorTablePlace AbilityDiscard => abilityDiscard;
        public Vector3 XAxisDie => xAxisDieAt;
        public Vector3 YAxisDie => yAxisDieAt;
        public Vector3 DirectionDie => directionDieAt;
        public ConsoleSide StartingAbilitiesSide => startingAbilitiesSide;
        public float StartingAbilitiesTowardBoard => startingAbilitiesTowardBoard;
        public float StartingAbilitiesOutward => startingAbilitiesOutward;
        public float StartingAbilitiesGap => startingAbilitiesGap;
        public float StartingAbilitiesTurn => startingAbilitiesTurn;
        public float HandZoneTowardBoard => handZoneTowardBoard;
        public float HandZoneOutward => handZoneOutward;
        public float ActionStackTowardBoard => actionStackTowardBoard;
        public float ActionStackOutward => actionStackOutward;
    }

    /// <summary>SETUP-1 / SETUP-2: one place on the table: where (the scene's X, Y and Z) and its turn.</summary>
    [Serializable]
    public sealed class TrapFloorTablePlace
    {
        [Tooltip("Where, in the scene's X, Y and Z (the numbers the Transform shows): X to the right of Player 1, Z away "
            + "from Player 1, Y up (pieces rest on the table top, so Y only shows the height).")]
        [SerializeField] private Vector3 at;
        [Tooltip("Turn in degrees.")]
        [SerializeField] private float turn;

        public TrapFloorTablePlace()
        {
        }

        public TrapFloorTablePlace(Vector3 at, float turn)
        {
            this.at = at;
            this.turn = turn;
        }

        public Vector3 Position => at;
        public float Turn => turn;
    }
}
