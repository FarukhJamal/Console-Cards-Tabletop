using System;
using ConsoleCards.GameTemplates;
using UnityEngine;

namespace ConsoleCards.Definitions
{
    /// <summary>
    /// SETUP-1 (owner, 2026-10-23: "the whole default setup ... the console mat, the board placement, the dice"):
    /// the game's whole default table setup. The board, the Consoles, the shared piles and the dice are places on the
    /// table, in table units from the table's centre (x to the right of Player 1, y away from Player 1). The players'
    /// own saved table layout ("Arrange table…") still wins over them.
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
        [SerializeField] private Vector2 boardCentre;
        [Tooltip("The playmat's width and depth in table units (1 = 2.5\"; 14 x 9.6 is the 35 x 24\" mat).")]
        [SerializeField] private Vector2 boardMatSize = new Vector2(14f, 9.6f);

        [Header("Consoles (each player's Console mat; 2 players use Players 1 and 3, 3 players use 1 to 3)")]
        [Tooltip("Player 1, in front. Turn 0 faces the board from the front.")]
        [SerializeField] private TrapFloorTablePlace player1Console = new TrapFloorTablePlace(new Vector2(0f, -7.5f), 0f);
        [Tooltip("Player 2, on the left. Turn 90 faces the board from the left.")]
        [SerializeField] private TrapFloorTablePlace player2Console = new TrapFloorTablePlace(new Vector2(-9.7f, 0f), 90f);
        [Tooltip("Player 3, at the back. Turn 180 faces the board from the back.")]
        [SerializeField] private TrapFloorTablePlace player3Console = new TrapFloorTablePlace(new Vector2(0f, 7.5f), 180f);
        [Tooltip("Player 4, on the right. Turn 270 faces the board from the right.")]
        [SerializeField] private TrapFloorTablePlace player4Console = new TrapFloorTablePlace(new Vector2(9.7f, 0f), 270f);

        [Header("Shared piles")]
        [Tooltip("The Controller deck everyone draws from.")]
        [SerializeField] private TrapFloorTablePlace controllerDeck = new TrapFloorTablePlace(new Vector2(5.2f, -5.45f), 0f);
        [Tooltip("The Ability deck.")]
        [SerializeField] private TrapFloorTablePlace abilityDeck = new TrapFloorTablePlace(new Vector2(-4.75f, 0f), 0f);
        [Tooltip("The Ability discard pile.")]
        [SerializeField] private TrapFloorTablePlace abilityDiscard = new TrapFloorTablePlace(new Vector2(-4.75f, 2f), 0f);

        [Header("Dice")]
        [Tooltip("The Floorfall die for the column (X).")]
        [SerializeField] private Vector2 xAxisDie = new Vector2(-5.9f, -3.6f);
        [Tooltip("The Floorfall die for the row (Y).")]
        [SerializeField] private Vector2 yAxisDie = new Vector2(-5f, -3.6f);
        [Tooltip("The d4 the Blind trap rolls for its direction.")]
        [SerializeField] private Vector2 directionDie = new Vector2(-4.1f, -3.6f);

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

        public Vector2 BoardCentre => boardCentre;
        public Vector2 BoardMatSize => boardMatSize;
        public TrapFloorTablePlace Player1Console => player1Console;
        public TrapFloorTablePlace Player2Console => player2Console;
        public TrapFloorTablePlace Player3Console => player3Console;
        public TrapFloorTablePlace Player4Console => player4Console;
        public TrapFloorTablePlace ControllerDeck => controllerDeck;
        public TrapFloorTablePlace AbilityDeck => abilityDeck;
        public TrapFloorTablePlace AbilityDiscard => abilityDiscard;
        public Vector2 XAxisDie => xAxisDie;
        public Vector2 YAxisDie => yAxisDie;
        public Vector2 DirectionDie => directionDie;
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

    /// <summary>SETUP-1: one place on the table: where (table units from the table's centre) and its turn.</summary>
    [Serializable]
    public sealed class TrapFloorTablePlace
    {
        [Tooltip("Table units from the table's centre: x to the right of Player 1, y away from Player 1.")]
        [SerializeField] private Vector2 position;
        [Tooltip("Turn in degrees.")]
        [SerializeField] private float turn;

        public TrapFloorTablePlace()
        {
        }

        public TrapFloorTablePlace(Vector2 position, float turn)
        {
            this.position = position;
            this.turn = turn;
        }

        public Vector2 Position => position;
        public float Turn => turn;
    }
}
