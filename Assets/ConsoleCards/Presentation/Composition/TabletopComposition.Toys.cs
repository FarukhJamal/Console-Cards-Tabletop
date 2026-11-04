using System;
using System.Collections.Generic;
using System.Globalization;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Feedback;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// Doc 58, steps 1 to 5 (owner, 2026-11-04: "implement everything"): the table things Tabletop Simulator players
    /// expect, made from the table's drawings (<see cref="TableMark"/>), so each one is shared online with the table,
    /// kept in every Undo step and saved with "Save table as a game" without anything new in the match.
    /// <list type="bullet">
    /// <item><b>Zones</b> (right-click the table › Place…): a <b>hidden zone</b> (only its owner sees in; the others see
    /// a frosted lid with a count), a <b>shuffle zone</b> (a deck put in it is shuffled, a die put in it rolls) and a
    /// <b>layout zone</b> (cards put in it line up in a row). Right-click a zone to resize or remove it.</item>
    /// <item><b>Snap points</b>: a loose card or piece that comes to rest near one is set on it.</item>
    /// <item><b>Toys</b>: a counter (+ / - over it, or its menu; it can be a dial that wraps), a note (the words in the
    /// Draw bar's label box), a sand timer (1, 3 or 5 minutes, the same on every computer) and a dice tray (low walls
    /// that keep dice in).</item>
    /// <item><b>Pieces</b>: + / − over a piece scales it, and Tint in the table menu colours pawns, tokens and dice
    /// (kept as a "look" drawing per piece). Ctrl+L locks or frees the piece under the pointer (L is the log),
    /// Ctrl+C / Ctrl+V copy pieces to the pointer, and U while carrying a card puts it under the deck it is let go on.</item>
    /// <item><b>Table flip</b> (table menu): everything loose flies; "Put the table back" puts it all where it was.</item>
    /// </list>
    /// The zones' work (shuffle, line up, snap) is done by one computer (offline, or the host online) when the table
    /// settles, and is folded into the step that caused it. Nothing here allocates per frame: the checks run after a
    /// change, and the timer's text changes once a second.
    /// </summary>
    public sealed partial class TabletopComposition
    {
        private const float ToyLift = 0.006f;
        private const float ZoneLabelHeight = 0.32f; // card widths
        private const float ToyTouchRadius = 0.55f; // card widths
        private const float SnapReach = 0.6f; // card widths
        private const float ToyRecheckSeconds = 0.35f;
        private const float LidHeight = 0.9f; // card widths
        private const float TrayWallHeight = 0.28f; // card widths
        private const float TrayWallThickness = 0.12f; // card widths
        private const float FlipUpSpeed = 7f; // card widths per second
        private const float UnderDropSeconds = 12f;
        private const float FlipQuietSeconds = 8f;

        private static readonly Color ShuffleZoneColour = new Color(0.62f, 0.38f, 0.95f, 1f);
        private static readonly Color LayoutZoneColour = new Color(0.024f, 0.80f, 0.80f, 1f);
        private static readonly Color SnapColour = new Color(0.98f, 0.97f, 0.93f, 1f);
        private static readonly Color NotePaper = new Color(1f, 0.90f, 0.42f, 1f);
        private static readonly Color CounterColour = new Color(0.114f, 0.114f, 0.122f, 1f);
        private static readonly Color TimerColour = new Color(0.95f, 0.66f, 0.11f, 1f);
        private static readonly Color TrayColour = new Color(0.42f, 0.27f, 0.16f, 1f);
        private static readonly Color LidColour = new Color(0.78f, 0.84f, 0.90f, 1f);

        private static readonly string[] ZoneSizeNames = { "Small", "Medium", "Large", "Wide" };
        private static readonly Vector2[] ZoneSizes = // card widths (x across, z deep)
        {
            new Vector2(2.6f, 2f),
            new Vector2(4f, 2.6f),
            new Vector2(6f, 4f),
            new Vector2(8f, 2.2f),
        };

        private static readonly Color[] TintChoices =
        {
            new Color(1f, 1f, 1f, 0f), // none
            new Color(0.184f, 0.482f, 1f, 1f),
            new Color(1f, 0.231f, 0.231f, 1f),
            new Color(0.184f, 0.831f, 0.420f, 1f),
            new Color(1f, 0.788f, 0.231f, 1f),
            new Color(0.55f, 0.3f, 0.75f, 1f),
        };

        private static readonly string[] TintNames = { "No tint", "Blue", "Red", "Green", "Yellow", "Purple" };

        // The things a toy drawing made on the table, so they can be found again (the lid, the timer text).
        private sealed class ToyVisual
        {
            public TableMark Mark;
            public GameObject Root;
            public GameObject Lid;
            public TextMesh Text;
            public string ShownText;
        }

        private readonly List<ToyVisual> toyVisuals = new List<ToyVisual>(8);
        private readonly Dictionary<ContainerId, int> shuffledPileCounts = new Dictionary<ContainerId, int>();
        private readonly HashSet<TabletopObjectId> diceInShuffleZones = new HashSet<TabletopObjectId>();
        private readonly HashSet<TabletopObjectId> rolledThisCheck = new HashSet<TabletopObjectId>();
        private readonly Dictionary<TabletopObjectId, Vector3> pieceBaseScales = new Dictionary<TabletopObjectId, Vector3>();
        private readonly Dictionary<TabletopObjectId, TabletopObjectView> lookedViews = new Dictionary<TabletopObjectId, TabletopObjectView>();
        private readonly List<CardView> layoutCards = new List<CardView>(16);
        private readonly List<TabletopObjectId> copiedPieces = new List<TabletopObjectId>(8);
        private readonly List<Vector3> copiedOffsets = new List<Vector3>(8);
        private readonly Dictionary<TabletopObjectId, PhysicalObjectState> flipSaved = new Dictionary<TabletopObjectId, PhysicalObjectState>();
        private Material toyMaterial;
        private MaterialPropertyBlock toyBlock;
        private Vector2 toyMenuScreen;
        private Vector3 toyMenuPoint;
        private bool toyMenuPointValid;
        internal bool toyCheckPending;
        private float toyRecheckAt = -1f;
        private int lastTimerSecond = -1;
        private TabletopObjectId underDropCard;
        private float underDropUntil;
        private float nextAutoSaveAt = -1f;
        private float flipQuietUntil = -1f;
        private bool cartridgeHeld;
        private Vector3 cartridgeHeldFrom;
        private int recapTraps;
        private int recapKeys;
        private int recapPrizes;
        private int recapExits;

        /// <summary>True for a few seconds after the table is flipped or put back: the game ignores where pieces land.</summary>
        internal bool TableFlipQuiet => Time.unscaledTime < flipQuietUntil;

        // ---------- What a toy drawing is ----------

        private static bool IsToyMark(TableMarkKind kind) => kind >= TableMarkKind.HiddenZone;

        private static bool IsZoneMark(TableMarkKind kind) =>
            kind == TableMarkKind.HiddenZone || kind == TableMarkKind.ShuffleZone || kind == TableMarkKind.LayoutZone
            || kind == TableMarkKind.DiceTray;

        private static void ZoneRect(TableMark mark, out float minX, out float minZ, out float maxX, out float maxZ)
        {
            Vector3 a = mark.Points.Length > 0 ? mark.Points[0] : Vector3.zero;
            Vector3 b = mark.Points.Length > 1 ? mark.Points[1] : a;
            minX = Mathf.Min(a.x, b.x);
            maxX = Mathf.Max(a.x, b.x);
            minZ = Mathf.Min(a.z, b.z);
            maxZ = Mathf.Max(a.z, b.z);
        }

        private static bool InsideZone(TableMark mark, Vector3 world)
        {
            ZoneRect(mark, out float minX, out float minZ, out float maxX, out float maxZ);
            return world.x >= minX && world.x <= maxX && world.z >= minZ && world.z <= maxZ;
        }

        // A hidden zone's owner is kept in its words (the player's ID).
        private static PlayerId ZoneOwner(TableMark mark)
        {
            return Guid.TryParse(mark.Text, out Guid owner) ? new PlayerId(owner) : PlayerId.Empty;
        }

        /// <summary>True when this world point lies in a hidden zone this computer's player does not own.</summary>
        internal bool HiddenFromMe(Vector3 world)
        {
            for (int i = 0; i < tableMarks.Count; i++)
            {
                TableMark mark = tableMarks[i];
                if (mark.Kind != TableMarkKind.HiddenZone || !InsideZone(mark, world)) continue;
                if (ZoneOwner(mark) != localPlayerId) return true;
            }

            return false;
        }

        // ---------- Made and shown (from NewMarkObject) ----------

        private GameObject NewToyObject(TableMark mark)
        {
            if (!EnsureMarkSet() || mark.Points.Length == 0) return null;
            GameObject root = new GameObject($"Table {mark.Kind}");
            root.transform.SetParent(MarkRoot(), false);
            ToyVisual visual = new ToyVisual { Mark = mark, Root = root };
            float unit = WorldPerCardWidth();
            switch (mark.Kind)
            {
                case TableMarkKind.HiddenZone:
                case TableMarkKind.ShuffleZone:
                case TableMarkKind.LayoutZone:
                case TableMarkKind.DiceTray:
                    BuildZone(visual, unit);
                    break;
                case TableMarkKind.SnapPoint:
                    BuildRing(root.transform, mark.Points[0], 0.16f * unit, SnapColour, mark.Width);
                    break;
                case TableMarkKind.Note:
                    BuildTile(root.transform, mark.Points[0], mark.Yaw, new Vector3(1.6f * unit, 0.02f * unit, 1.1f * unit), NotePaper);
                    visual.Text = NewToyText(root.transform, mark.Points[0] + (Vector3.up * 0.03f * unit), mark.Yaw, 0.17f * unit,
                        CounterColour);
                    SetToyText(visual, WrapNote(mark.Text));
                    break;
                case TableMarkKind.Counter:
                    BuildTile(root.transform, mark.Points[0], mark.Yaw, new Vector3(1.1f * unit, 0.12f * unit, 0.9f * unit), CounterColour);
                    visual.Text = NewToyText(root.transform, mark.Points[0] + (Vector3.up * 0.13f * unit), mark.Yaw, 0.42f * unit,
                        Color.white);
                    SetToyText(visual, CounterWords(mark));
                    break;
                case TableMarkKind.Timer:
                    BuildTile(root.transform, mark.Points[0], mark.Yaw, new Vector3(1.4f * unit, 0.12f * unit, 0.8f * unit), TimerColour);
                    visual.Text = NewToyText(root.transform, mark.Points[0] + (Vector3.up * 0.13f * unit), mark.Yaw, 0.34f * unit,
                        CounterColour);
                    SetToyText(visual, TimerWords(mark));
                    break;
                default: // a piece's look: nothing on the table
                    break;
            }

            toyVisuals.Add(visual);
            return root;
        }

        private void BuildZone(ToyVisual visual, float unit)
        {
            TableMark mark = visual.Mark;
            ZoneRect(mark, out float minX, out float minZ, out float maxX, out float maxZ);
            float y = mark.Points[0].y;
            Color colour = mark.Kind == TableMarkKind.ShuffleZone ? ShuffleZoneColour
                : mark.Kind == TableMarkKind.LayoutZone ? LayoutZoneColour
                : mark.Kind == TableMarkKind.DiceTray ? TrayColour
                : mark.Colour;
            if (mark.Kind == TableMarkKind.DiceTray)
            {
                BuildTray(visual.Root.transform, minX, minZ, maxX, maxZ, y, unit);
            }
            else
            {
                LineRenderer line = NewMarkLine(TableMarkKind.Pen, colour, 0.05f * unit, TableMarkStyle.Dashed);
                if (line != null)
                {
                    line.transform.SetParent(visual.Root.transform, true);
                    Vector3[] corners =
                    {
                        new Vector3(minX, y, minZ), new Vector3(maxX, y, minZ), new Vector3(maxX, y, maxZ),
                        new Vector3(minX, y, maxZ), new Vector3(minX, y, minZ),
                    };
                    ShowStroke(line, corners, corners.Length, TableMarkStyle.Dashed, 0.05f * unit, true);
                }
            }

            string title = mark.Kind == TableMarkKind.HiddenZone ? $"HIDDEN · {ZoneOwnerName(mark).ToUpperInvariant()}"
                : mark.Kind == TableMarkKind.ShuffleZone ? "SHUFFLE ZONE"
                : mark.Kind == TableMarkKind.LayoutZone ? "LAYOUT ZONE"
                : "DICE TRAY";
            Vector3 labelAt = new Vector3(minX + (0.12f * unit), y + (0.01f * unit), maxZ - (0.2f * unit));
            TextMesh label = NewToyText(visual.Root.transform, labelAt, 0f, ZoneLabelHeight * 0.5f * unit, colour);
            if (label != null)
            {
                label.anchor = TextAnchor.UpperLeft;
                label.alignment = TextAlignment.Left;
                label.text = title;
            }

            if (mark.Kind != TableMarkKind.HiddenZone) return;
            // The frosted lid the others see (no collider: they can still reach in, as in TTS).
            GameObject lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lid.name = "Hidden Zone Lid";
            Destroy(lid.GetComponent<Collider>());
            lid.transform.SetParent(visual.Root.transform, false);
            float height = LidHeight * unit;
            lid.transform.position = new Vector3((minX + maxX) * 0.5f, y + (0.5f * height), (minZ + maxZ) * 0.5f);
            lid.transform.localScale = new Vector3(Mathf.Max(0.01f, maxX - minX), height, Mathf.Max(0.01f, maxZ - minZ));
            TintToy(lid.GetComponent<Renderer>(), Color.Lerp(LidColour, mark.Colour, 0.25f));
            visual.Lid = lid;
            visual.Text = NewToyText(visual.Root.transform, lid.transform.position + (Vector3.up * ((0.5f * height) + (0.01f * unit))),
                0f, 0.3f * unit, CounterColour);
            RefreshLid(visual);
        }

        private void BuildTray(Transform root, float minX, float minZ, float maxX, float maxZ, float y, float unit)
        {
            float height = TrayWallHeight * unit;
            float thick = TrayWallThickness * unit;
            float width = maxX - minX;
            float depth = maxZ - minZ;
            float midX = (minX + maxX) * 0.5f;
            float midZ = (minZ + maxZ) * 0.5f;
            BuildWall(root, new Vector3(midX, y + (0.5f * height), minZ), new Vector3(width + thick, height, thick));
            BuildWall(root, new Vector3(midX, y + (0.5f * height), maxZ), new Vector3(width + thick, height, thick));
            BuildWall(root, new Vector3(minX, y + (0.5f * height), midZ), new Vector3(thick, height, depth + thick));
            BuildWall(root, new Vector3(maxX, y + (0.5f * height), midZ), new Vector3(thick, height, depth + thick));
            // The felt floor (no collider: the table holds the dice up).
            GameObject felt = GameObject.CreatePrimitive(PrimitiveType.Cube);
            felt.name = "Dice Tray Felt";
            Destroy(felt.GetComponent<Collider>());
            felt.transform.SetParent(root, false);
            felt.transform.position = new Vector3(midX, y + (0.002f * unit), midZ);
            felt.transform.localScale = new Vector3(width, 0.004f * unit, depth);
            TintToy(felt.GetComponent<Renderer>(), new Color(0.10f, 0.32f, 0.22f, 1f));
        }

        // A wall keeps its collider so a rolled die bounces off it.
        private void BuildWall(Transform root, Vector3 centre, Vector3 size)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Dice Tray Wall";
            wall.transform.SetParent(root, false);
            wall.transform.position = centre;
            wall.transform.localScale = size;
            TintToy(wall.GetComponent<Renderer>(), TrayColour);
        }

        private void BuildRing(Transform root, Vector3 centre, float radius, Color colour, float width)
        {
            LineRenderer line = NewMarkLine(TableMarkKind.Pen, colour, Mathf.Max(0.02f * WorldPerCardWidth(), width), TableMarkStyle.Plain);
            if (line == null) return;
            line.transform.SetParent(root, true);
            Vector3[] ring = new Vector3[17];
            for (int i = 0; i < ring.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / (ring.Length - 1);
                ring[i] = centre + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            }

            ShowStroke(line, ring, ring.Length, TableMarkStyle.Plain, line.widthMultiplier, true);
        }

        private void BuildTile(Transform root, Vector3 centre, float yaw, Vector3 size, Color colour)
        {
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = "Tile";
            Destroy(tile.GetComponent<Collider>());
            tile.transform.SetParent(root, false);
            tile.transform.SetPositionAndRotation(centre + (Vector3.up * (0.5f * size.y)), Quaternion.Euler(0f, yaw, 0f));
            tile.transform.localScale = size;
            TintToy(tile.GetComponent<Renderer>(), colour);
        }

        private TextMesh NewToyText(Transform root, Vector3 at, float yaw, float letterHeight, Color colour)
        {
            if (!EnsureMarkSet()) return null;
            TextMesh text = Instantiate(markSet.TableMarkLabel, root);
            text.transform.SetPositionAndRotation(at, Quaternion.Euler(90f, yaw, 0f));
            float scale = letterHeight / LabelPrefabLetterHeight;
            text.transform.localScale = new Vector3(scale, scale, scale);
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = colour;
            text.text = string.Empty;
            return text;
        }

        private static void SetToyText(ToyVisual visual, string words)
        {
            if (visual.Text == null || string.Equals(visual.ShownText, words, StringComparison.Ordinal)) return;
            visual.ShownText = words;
            visual.Text.text = words;
        }

        private void TintToy(Renderer renderer, Color colour)
        {
            if (renderer == null) return;
            if (toyMaterial == null)
            {
                Material source = localHandVisual != null && localHandVisual.FeedbackRenderer != null
                    ? localHandVisual.FeedbackRenderer.sharedMaterial
                    : renderer.sharedMaterial;
                toyMaterial = new Material(source) { name = "Table Toys" };
                toyBlock = new MaterialPropertyBlock();
            }

            renderer.sharedMaterial = toyMaterial;
            renderer.GetPropertyBlock(toyBlock);
            toyBlock.SetColor(AssistBaseColorId, colour);
            toyBlock.SetColor(AssistColorId, colour);
            renderer.SetPropertyBlock(toyBlock);
        }

        // Drawings that went away take their visuals with them (from ShowTableMarks and the eraser).
        private void PruneToyVisuals()
        {
            for (int i = toyVisuals.Count - 1; i >= 0; i--)
            {
                if (toyVisuals[i].Root == null || !tableMarks.Contains(toyVisuals[i].Mark)) toyVisuals.RemoveAt(i);
            }
        }

        // ---------- Every frame (cheap) ----------

        private void TickToys()
        {
            if (!IsInitialized || matchState == null) return;
            if (runtimeUi != null && runtimeUi.PauseExtra == null) runtimeUi.PauseExtra = OpenTableMenuFromPause; // once
            TickTimers();
            TickLids();
            TickUnderDrop();
            TickAutoSave();
            if (toyCheckPending || (toyRecheckAt > 0f && Time.unscaledTime >= toyRecheckAt))
            {
                toyCheckPending = false;
                toyRecheckAt = -1f;
                RunToyChecks();
            }
        }

        private void TickTimers()
        {
            int second = (int)Time.unscaledTime;
            if (second == lastTimerSecond) return;
            lastTimerSecond = second;
            for (int i = 0; i < toyVisuals.Count; i++)
            {
                ToyVisual visual = toyVisuals[i];
                if (visual.Mark.Kind != TableMarkKind.Timer) continue;
                string words = TimerWords(visual.Mark);
                bool wasRunning = visual.ShownText != null && visual.ShownText != "0:00" && visual.ShownText.IndexOf(':') > 0;
                SetToyText(visual, words);
                if (wasRunning && words == "0:00" && TimerStart(visual.Mark) > 0)
                {
                    runtimeUi?.ShowTableNews("The sand timer ran out!", TimerColour);
                    tableSounds?.PlayImpact(TableSoundMaterial.Chip, Feel.ImpactFullSpeed);
                }
            }
        }

        // The lid shows for anyone but the zone's owner (hot-seat changes who that is).
        private void TickLids()
        {
            for (int i = 0; i < toyVisuals.Count; i++)
            {
                if (toyVisuals[i].Mark.Kind == TableMarkKind.HiddenZone) RefreshLid(toyVisuals[i]);
            }
        }

        private void RefreshLid(ToyVisual visual)
        {
            if (visual.Lid == null) return;
            bool mine = ZoneOwner(visual.Mark) == localPlayerId;
            if (visual.Lid.activeSelf == mine) visual.Lid.SetActive(!mine);
            if (visual.Text != null && visual.Text.gameObject.activeSelf == mine) visual.Text.gameObject.SetActive(!mine);
        }

        // Counts what lies in a hidden zone (written on its lid when it changes).
        private void RefreshLidCounts()
        {
            for (int v = 0; v < toyVisuals.Count; v++)
            {
                ToyVisual visual = toyVisuals[v];
                if (visual.Mark.Kind != TableMarkKind.HiddenZone || visual.Text == null) continue;
                int count = 0;
                for (int i = 0; i < Registry.cardViews.Count; i++)
                {
                    CardView card = Registry.cardViews[i];
                    if (card != null && card.IsBound && card.BoundState != null && card.BoundState.ContainerId.IsEmpty
                        && InsideZone(visual.Mark, card.transform.position)) count++;
                }

                foreach (KeyValuePair<ContainerId, ContainerPlacementState> pair in matchState.ContainerPlacements)
                {
                    if (!matchState.Containers.TryGetValue(pair.Key, out ContainerState pile) || !IsSelectablePile(pair.Key)) continue;
                    if (InsideZone(visual.Mark, coordinateConverter.ToWorldPosition(pair.Value.Pose))) count += pile.Count;
                }

                count += CountInZone(Registry.pawnViews, visual.Mark) + CountInZone(Registry.tokenViews, visual.Mark)
                    + CountInZone(Registry.dieViews, visual.Mark);
                SetToyText(visual, count == 0 ? "HIDDEN" : count == 1 ? "HIDDEN · 1 thing" : $"HIDDEN · {count} things");
            }
        }

        private static int CountInZone<TView>(List<TView> views, TableMark mark) where TView : TabletopObjectView
        {
            int count = 0;
            for (int i = 0; i < views.Count; i++)
            {
                TView view = views[i];
                if (view != null && view.IsBound && view.BoundState != null && view.BoundState.ContainerId.IsEmpty
                    && InsideZone(mark, view.transform.position)) count++;
            }

            return count;
        }

        // ---------- After a change: zones, snap points and looks ----------

        private void RunToyChecks()
        {
            ApplyPieceLooks();
            RefreshLidCounts();
            bool anyZone = false;
            for (int i = 0; i < tableMarks.Count && !anyZone; i++)
            {
                TableMarkKind kind = tableMarks[i].Kind;
                anyZone = kind == TableMarkKind.ShuffleZone || kind == TableMarkKind.LayoutZone || kind == TableMarkKind.SnapPoint;
            }

            if (!anyZone) return;
            if (interactionRouter != null && interactionRouter.HasActiveInteraction)
            {
                toyRecheckAt = Time.unscaledTime + ToyRecheckSeconds;
                return;
            }

            // One computer does the zones' work: this one offline, the host online.
            if (IsOnlineTable && !Online.IsHosting) return;
            bool unsettled = false;
            SessionService.BeginUndoAmend();
            try
            {
                for (int i = 0; i < tableMarks.Count; i++)
                {
                    TableMark mark = tableMarks[i];
                    if (mark.Kind == TableMarkKind.ShuffleZone) unsettled |= RunShuffleZone(mark);
                    else if (mark.Kind == TableMarkKind.LayoutZone) unsettled |= RunLayoutZone(mark);
                }

                unsettled |= RunSnapPoints();
            }
            finally
            {
                SessionService.EndUndoAmend();
            }

            if (unsettled) toyRecheckAt = Time.unscaledTime + ToyRecheckSeconds;
        }

        private static bool IsAtRest(TabletopObjectView view)
        {
            TabletopObjectState state = view.BoundState;
            if (view.PhysicalObject == null) return true;
            return !view.PhysicalObject.IsHeld && !view.PhysicalObject.IsGliding
                && state.PhysicalState != null && state.PhysicalState.Mode == PhysicalObjectMode.Sleeping;
        }

        private static Vector3 ViewPosition(TabletopObjectView view)
        {
            return view.PhysicalObject != null && view.PhysicalObject.Body != null ? view.PhysicalObject.Body.position : view.transform.position;
        }

        // A deck in the zone is shuffled when it arrives or its count changes; a die that comes to rest in it rolls once.
        private bool RunShuffleZone(TableMark mark)
        {
            bool unsettled = false;
            foreach (KeyValuePair<ContainerId, ContainerPlacementState> pair in matchState.ContainerPlacements)
            {
                if (!matchState.Containers.TryGetValue(pair.Key, out ContainerState pile)) continue;
                if (pile.Kind != ContainerKind.Deck && pile.Kind != ContainerKind.Stack) continue;
                if (!InsideZone(mark, coordinateConverter.ToWorldPosition(pair.Value.Pose)))
                {
                    shuffledPileCounts.Remove(pair.Key);
                    continue;
                }

                if (pile.Count < 2 || (shuffledPileCounts.TryGetValue(pair.Key, out int seen) && seen == pile.Count)) continue;
                shuffledPileCounts[pair.Key] = pile.Count;
                if (ShuffleDeck(pair.Key).Succeeded) runtimeUi?.ShowTableNews("The shuffle zone shuffled the deck", ShuffleZoneColour);
            }

            rolledThisCheck.Clear();
            for (int i = 0; i < Registry.dieViews.Count; i++)
            {
                DieView die = Registry.dieViews[i];
                if (die == null || !die.IsBound || die.BoundState == null || !die.BoundState.ContainerId.IsEmpty) continue;
                TabletopObjectId id = die.ObjectId;
                if (!InsideZone(mark, ViewPosition(die)))
                {
                    diceInShuffleZones.Remove(id);
                    continue;
                }

                if (diceInShuffleZones.Contains(id) || rolledThisCheck.Contains(id)) continue;
                if (!IsAtRest(die))
                {
                    unsettled = true;
                    continue;
                }

                diceInShuffleZones.Add(id);
                rolledThisCheck.Add(id);
                ContextMenus.RollContextDie(id);
            }

            return unsettled;
        }

        // Loose cards at rest in the zone line up in a row along its width, in the order they lie (left to right).
        private bool RunLayoutZone(TableMark mark)
        {
            bool unsettled = false;
            layoutCards.Clear();
            for (int i = 0; i < Registry.cardViews.Count; i++)
            {
                CardView card = Registry.cardViews[i];
                if (card == null || !card.IsBound || card.BoundState == null) continue;
                TabletopObjectState state = card.BoundState;
                if (!state.ContainerId.IsEmpty || state.IsUserLocked || ActiveCartridge.IsFixedBoardPiece(state.Id)) continue;
                if (!InsideZone(mark, ViewPosition(card))) continue;
                if (!IsAtRest(card))
                {
                    unsettled = true;
                    continue;
                }

                layoutCards.Add(card);
            }

            if (layoutCards.Count == 0 || unsettled) return unsettled;
            layoutCards.Sort((a, b) => ViewPosition(a).x.CompareTo(ViewPosition(b).x));
            ZoneRect(mark, out float minX, out float minZ, out float maxX, out float maxZ);
            float unit = WorldPerCardWidth();
            float room = Mathf.Max(unit, (maxX - minX) - (1.1f * unit));
            float step = layoutCards.Count > 1 ? Mathf.Min(1.1f * unit, room / (layoutCards.Count - 1)) : 0f;
            float startX = ((minX + maxX) * 0.5f) - (0.5f * step * (layoutCards.Count - 1));
            float z = (minZ + maxZ) * 0.5f;
            for (int i = 0; i < layoutCards.Count; i++)
            {
                CardView card = layoutCards[i];
                Vector3 now = ViewPosition(card);
                Vector3 target = new Vector3(startX + (i * step), now.y, z);
                if (Mathf.Abs(now.x - target.x) < 0.04f * unit && Mathf.Abs(now.z - target.z) < 0.04f * unit) continue;
                if (cardsSetOnBoardSpaces.TryGetValue(card.ObjectId, out Vector3 last) && (last - target).sqrMagnitude < 0.0004f * unit * unit)
                {
                    continue; // set here once already; it settled a little off, leave it
                }

                TabletopPose pose = card.BoundState.Pose;
                if (MoveLoosePiece(card, card.BoundState, new TabletopPose(
                        coordinateConverter.ToTableCoordinate(target), pose.RotationDegrees, pose.Layer, pose.LocalOrder)))
                {
                    cardsSetOnBoardSpaces[card.ObjectId] = target;
                }
            }

            return false;
        }

        // A loose card, pawn, token or die at rest near a snap point is set on it.
        private bool RunSnapPoints()
        {
            bool any = false;
            for (int i = 0; i < tableMarks.Count && !any; i++) any = tableMarks[i].Kind == TableMarkKind.SnapPoint;
            if (!any) return false;
            bool unsettled = false;
            unsettled |= SnapViews(Registry.cardViews);
            unsettled |= SnapViews(Registry.pawnViews);
            unsettled |= SnapViews(Registry.tokenViews);
            unsettled |= SnapViews(Registry.dieViews);
            return unsettled;
        }

        private bool SnapViews<TView>(List<TView> views) where TView : TabletopObjectView
        {
            float unit = WorldPerCardWidth();
            float reach = SnapReach * unit;
            bool unsettled = false;
            for (int v = 0; v < views.Count; v++)
            {
                TView view = views[v];
                if (view == null || !view.IsBound || view.BoundState == null) continue;
                TabletopObjectState state = view.BoundState;
                if (!state.ContainerId.IsEmpty || state.IsUserLocked || ActiveCartridge.IsFixedBoardPiece(state.Id)) continue;
                Vector3 position = ViewPosition(view);
                for (int i = 0; i < tableMarks.Count; i++)
                {
                    TableMark mark = tableMarks[i];
                    if (mark.Kind != TableMarkKind.SnapPoint) continue;
                    Vector3 dot = mark.Points[0];
                    float dx = dot.x - position.x;
                    float dz = dot.z - position.z;
                    float distance = (dx * dx) + (dz * dz);
                    if (distance > reach * reach || distance < 0.0009f * unit * unit) continue;
                    if (!IsAtRest(view))
                    {
                        unsettled = true;
                        break;
                    }

                    Vector3 target = new Vector3(dot.x, position.y, dot.z);
                    if (cardsSetOnBoardSpaces.TryGetValue(state.Id, out Vector3 last) && (last - target).sqrMagnitude < 0.0004f * unit * unit)
                    {
                        break; // set here once already
                    }

                    TabletopPose pose = state.Pose;
                    if (MoveLoosePiece(view, state, new TabletopPose(
                            coordinateConverter.ToTableCoordinate(target), pose.RotationDegrees, pose.Layer, pose.LocalOrder)))
                    {
                        cardsSetOnBoardSpaces[state.Id] = target;
                    }

                    break;
                }
            }

            return unsettled;
        }

        // ---------- Scale and tint (a "look" drawing per piece) ----------

        private bool TryLookOf(TabletopObjectId id, out TableMark look)
        {
            string key = id.Value.ToString("N");
            for (int i = 0; i < tableMarks.Count; i++)
            {
                if (tableMarks[i].Kind == TableMarkKind.PieceLook && tableMarks[i].Text == key)
                {
                    look = tableMarks[i];
                    return true;
                }
            }

            look = null;
            return false;
        }

        private bool TryFindAnyView(TabletopObjectId id, out TabletopObjectView view)
        {
            if (Registry.TryGetCardView(id, out CardView card) && card != null)
            {
                view = card;
                return true;
            }

            return TryFindPieceView(id, out view) && view != null;
        }

        // Each piece with a look is scaled (and pawns, tokens and dice tinted) once per view it gets.
        private void ApplyPieceLooks()
        {
            for (int i = 0; i < tableMarks.Count; i++)
            {
                TableMark mark = tableMarks[i];
                if (mark.Kind != TableMarkKind.PieceLook || !Guid.TryParse(mark.Text, out Guid raw)) continue;
                TabletopObjectId id = new TabletopObjectId(raw);
                if (!TryFindAnyView(id, out TabletopObjectView view)) continue;
                bool contained = view.BoundState != null && !view.BoundState.ContainerId.IsEmpty;
                if (contained && view is CardView) continue; // a pile or the hand lays its cards out itself
                if (!pieceBaseScales.TryGetValue(id, out Vector3 baseScale) || !lookedViews.TryGetValue(id, out TabletopObjectView seen)
                    || !ReferenceEquals(seen, view))
                {
                    baseScale = view.transform.localScale;
                    pieceBaseScales[id] = baseScale;
                    lookedViews[id] = view;
                }

                float scale = contained ? 1f : Mathf.Clamp(mark.Width, 0.4f, 4f);
                Vector3 wanted = baseScale * scale;
                if ((view.transform.localScale - wanted).sqrMagnitude > 1e-6f) view.transform.localScale = wanted;
                if (!(view is CardView))
                {
                    Color tint = mark.Colour;
                    pieceColours[id] = tint.a > 0.01f ? tint : Color.white;
                    ApplyPieceColour(view);
                }
            }

            // Pieces whose look was taken away (Undo) go back to as made.
            if (lookedViews.Count == 0) return;
            List<TabletopObjectId> gone = null;
            foreach (KeyValuePair<TabletopObjectId, TabletopObjectView> pair in lookedViews)
            {
                if (TryLookOf(pair.Key, out _)) continue;
                (gone ??= new List<TabletopObjectId>()).Add(pair.Key);
                if (pair.Value != null && pieceBaseScales.TryGetValue(pair.Key, out Vector3 baseScale))
                {
                    pair.Value.transform.localScale = baseScale;
                    if (!(pair.Value is CardView) && pieceColours.ContainsKey(pair.Key))
                    {
                        pieceColours[pair.Key] = Color.white;
                        ApplyPieceColour(pair.Value);
                        pieceColours.Remove(pair.Key);
                    }
                }
            }

            if (gone == null) return;
            for (int i = 0; i < gone.Count; i++)
            {
                lookedViews.Remove(gone[i]);
                pieceBaseScales.Remove(gone[i]);
            }
        }

        private void SetPieceLook(TabletopObjectView view, float scale, Color tint, string step)
        {
            if (view == null || !view.IsBound) return;
            TableMarkSet marks = tableMarks;
            if (TryLookOf(view.ObjectId, out TableMark old)) marks = marks.Without(old);
            bool plain = Mathf.Abs(scale - 1f) < 0.01f && tint.a <= 0.01f;
            if (!plain)
            {
                marks = marks.With(new TableMark(TableMarkKind.PieceLook, tint, scale, new[] { Vector3.zero },
                    view.ObjectId.Value.ToString("N"), 0f));
            }

            ShowTableMarks(marks);
            SessionService.RecordTableMarkStep(step);
            ApplyPieceLooks();
        }

        private void ScalePiece(TabletopObjectView view, int direction)
        {
            float scale = TryLookOf(view.ObjectId, out TableMark look) ? look.Width : 1f;
            Color tint = look != null ? look.Colour : TintChoices[0];
            scale = Mathf.Clamp(Mathf.Round((scale + (direction * 0.25f)) * 4f) / 4f, 0.5f, 3f);
            SetPieceLook(view, scale, tint, direction > 0 ? "made a piece bigger" : "made a piece smaller");
            runtimeUi?.ShowTableNews($"Size x{scale.ToString("0.##", CultureInfo.InvariantCulture)}", FeedbackColour(localPlayerId));
        }

        private void TintPiece(TabletopObjectView view, int choice)
        {
            float scale = TryLookOf(view.ObjectId, out TableMark look) ? look.Width : 1f;
            SetPieceLook(view, scale, TintChoices[Mathf.Clamp(choice, 0, TintChoices.Length - 1)], "tinted a piece");
        }

        // ---------- Keys (from HandlePolishKeys) ----------

        // Ctrl+L lock · Ctrl+C / Ctrl+V copy and paste · + / − size or counter · U under (while carrying a card).
        private bool HandleToyKeys(TabletopObjectView carried)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || matchState == null) return false;
            bool control = keyboard.ctrlKey.isPressed;
            if (carried != null)
            {
                if (!control && keyboard.uKey.wasPressedThisFrame && carried is CardView carriedCard)
                {
                    underDropCard = carriedCard.ObjectId;
                    underDropUntil = Time.unscaledTime + UnderDropSeconds;
                    runtimeUi?.ShowTableNews("Let go on a deck: the card goes under it", FeedbackColour(localPlayerId));
                    return true;
                }

                return false;
            }

            if (control && keyboard.lKey.wasPressedThisFrame)
            {
                if (hoverView != null && hoverPileId.IsEmpty) ToggleLock(hoverView);
                return true;
            }

            if (control && keyboard.cKey.wasPressedThisFrame)
            {
                CopyPieces();
                return true;
            }

            if (control && keyboard.vKey.wasPressedThisFrame)
            {
                PastePieces();
                return true;
            }

            if (control) return false;
            bool plus = keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame;
            bool minus = keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame;
            if (!plus && !minus) return false;
            int direction = plus ? 1 : -1;
            if (TryToyUnderPointer(out TableMark toy) && toy.Kind == TableMarkKind.Counter)
            {
                StepCounter(toy, keyboard.shiftKey.isPressed ? 5 * direction : direction);
                return true;
            }

            if (hoverView != null && hoverPileId.IsEmpty && hoverView.BoundState != null && hoverView.BoundState.ContainerId.IsEmpty)
            {
                ScalePiece(hoverView, direction);
                return true;
            }

            return false;
        }

        private void ToggleLock(TabletopObjectView view)
        {
            if (view == null || !view.IsBound || view.BoundState == null || !view.BoundState.ContainerId.IsEmpty) return;
            bool locking = !view.BoundState.IsUserLocked;
            CommandResult result = TableAuthority.Submit<SetObjectLockedCommand, CommandResult>(
                new SetObjectLockedCommand(CreateCommandContext(), view.ObjectId, locking));
            if (!result.Succeeded)
            {
                ShowMessage("That piece cannot be locked here.");
                return;
            }

            runtimeUi?.ShowTableNews(locking ? "Locked (Ctrl+L frees it)" : "Free to move", FeedbackColour(localPlayerId));
        }

        // The command's views: the piece takes its new physics (from the door, here and on the other computers).
        private void OnObjectLockChanged(SetObjectLockedCommand command, CommandResult result)
        {
            if (!result.Succeeded) return;
            if (TryFindAnyView(command.ObjectId, out TabletopObjectView view) && view.IsBound) view.ApplyAcceptedState();
        }

        private int LockEveryPiece(bool locking)
        {
            int changed = 0;
            SessionService.BeginUndoBatch();
            try
            {
                changed += LockViews(Registry.cardViews, locking);
                changed += LockViews(Registry.pawnViews, locking);
                changed += LockViews(Registry.tokenViews, locking);
                changed += LockViews(Registry.dieViews, locking);
            }
            finally
            {
                SessionService.EndUndoBatch();
            }

            return changed;
        }

        private int LockViews<TView>(List<TView> views, bool locking) where TView : TabletopObjectView
        {
            int changed = 0;
            for (int i = 0; i < views.Count; i++)
            {
                TView view = views[i];
                if (view == null || !view.IsBound || view.BoundState == null || !view.BoundState.ContainerId.IsEmpty) continue;
                if (view.BoundState.IsUserLocked == locking || ActiveCartridge.IsFixedBoardPiece(view.ObjectId)) continue;
                if (TableAuthority.Submit<SetObjectLockedCommand, CommandResult>(
                        new SetObjectLockedCommand(CreateCommandContext(), view.ObjectId, locking)).Succeeded)
                {
                    changed++;
                }
            }

            return changed;
        }

        // Ctrl+C: the selected pieces, or the one under the pointer.
        private void CopyPieces()
        {
            copiedPieces.Clear();
            copiedOffsets.Clear();
            Vector3 first = Vector3.zero;
            for (int i = 0; i < toolSelection.Count; i++)
            {
                ToolPick pick = toolSelection[i];
                if (pick.IsPile || pick.View == null || !pick.View.IsBound || pick.View.BoundState == null) continue;
                if (!pick.View.BoundState.ContainerId.IsEmpty) continue;
                Vector3 at = ViewPosition(pick.View);
                if (copiedPieces.Count == 0) first = at;
                copiedPieces.Add(pick.View.ObjectId);
                copiedOffsets.Add(at - first);
            }

            if (copiedPieces.Count == 0 && hoverView != null && hoverPileId.IsEmpty && hoverView.BoundState != null
                && hoverView.BoundState.ContainerId.IsEmpty)
            {
                copiedPieces.Add(hoverView.ObjectId);
                copiedOffsets.Add(Vector3.zero);
            }

            runtimeUi?.ShowTableNews(copiedPieces.Count == 0
                ? "Point at a piece (or select some) to copy"
                : copiedPieces.Count == 1 ? "Copied 1 piece (Ctrl+V puts a copy at the pointer)" : $"Copied {copiedPieces.Count} pieces",
                FeedbackColour(localPlayerId));
        }

        private void PastePieces()
        {
            if (copiedPieces.Count == 0)
            {
                runtimeUi?.ShowTableNews("Nothing copied yet (Ctrl+C over a piece)", FeedbackColour(localPlayerId));
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null || !TryMarkPoint(mouse.position.ReadValue(), out Vector3 point, out _)) return;
            int made = 0;
            SessionService.BeginUndoBatch();
            try
            {
                for (int i = 0; i < copiedPieces.Count; i++)
                {
                    TabletopObjectId source = copiedPieces[i];
                    if (!matchState.ContainsObject(source)) continue;
                    TabletopObjectState state = matchState.GetObject(source);
                    if (!state.ContainerId.IsEmpty) continue;
                    TabletopPose pose = new TabletopPose(
                        coordinateConverter.ToTableCoordinate(point + copiedOffsets[i]), state.Pose.RotationDegrees, 0,
                        (toolboxSpawnSequence + i) * ToolboxPhysicalOrderStride);
                    if (CommitDuplicatePlacement(source, pose)) made++;
                }
            }
            finally
            {
                SessionService.EndUndoBatch();
            }

            if (made > 0) PlayPlacementSound(TabletopComponentKind.Pawn);
        }

        // U: the carried card, once it lands in a pile, goes to the bottom of it.
        private void TickUnderDrop()
        {
            if (underDropCard.IsEmpty) return;
            if (Time.unscaledTime > underDropUntil || !Registry.TryGetCardView(underDropCard, out CardView card) || card == null
                || !card.IsBound || card.BoundState == null)
            {
                underDropCard = TabletopObjectId.Empty;
                return;
            }

            if (card.PhysicalObject != null && card.PhysicalObject.IsHeld) return;
            ContainerId pileId = card.BoundState.ContainerId;
            if (pileId.IsEmpty) return; // still on its way, or let go on the table (the window runs out)
            underDropCard = TabletopObjectId.Empty;
            if (!matchState.Containers.TryGetValue(pileId, out ContainerState pile)
                || (pile.Kind != ContainerKind.Deck && pile.Kind != ContainerKind.Stack && pile.Kind != ContainerKind.DiscardPile))
            {
                return;
            }

            int from = pile.IndexOf(card.ObjectId);
            if (from <= 0) return;
            SessionService.BeginUndoAmend();
            try
            {
                ReorderCardInContainer(card, pile, from, 0);
            }
            finally
            {
                SessionService.EndUndoAmend();
            }

            runtimeUi?.ShowTableNews("Put under the deck", FeedbackColour(localPlayerId));
        }

        // ---------- Toys under the pointer and their menus ----------

        private bool TryToyUnderPointer(out TableMark toy)
        {
            toy = null;
            Mouse mouse = Mouse.current;
            return mouse != null && TryToyAt(mouse.position.ReadValue(), out toy, out _);
        }

        // The toy nearest the point: a counter, note, timer or snap point first, then a zone the point is in.
        private bool TryToyAt(Vector2 screenPosition, out TableMark toy, out Vector3 point)
        {
            toy = null;
            if (!TryMarkPoint(screenPosition, out point, out _)) return false;
            float unit = WorldPerCardWidth();
            float best = ToyTouchRadius * unit * ToyTouchRadius * unit * 1.6f;
            for (int i = 0; i < tableMarks.Count; i++)
            {
                TableMark mark = tableMarks[i];
                if (mark.Kind != TableMarkKind.Counter && mark.Kind != TableMarkKind.Note && mark.Kind != TableMarkKind.Timer
                    && mark.Kind != TableMarkKind.SnapPoint) continue;
                float distance = FlatDistanceSquared(mark.Points[0], point);
                if (distance >= best) continue;
                best = distance;
                toy = mark;
            }

            if (toy != null) return true;
            for (int i = tableMarks.Count - 1; i >= 0; i--)
            {
                if (IsZoneMark(tableMarks[i].Kind) && InsideZone(tableMarks[i], point))
                {
                    toy = tableMarks[i];
                    return true;
                }
            }

            return false;
        }

        /// <summary>Right-click on the table (nothing else under the pointer): a toy's menu, or the table's.</summary>
        internal bool TryOpenToyMenu(Vector2 screenPosition)
        {
            if (!IsInitialized || runtimeUi == null || !EnsureMarkSet()) return false;
            if (!TryToyAt(screenPosition, out TableMark toy, out Vector3 point)) return false;
            toyMenuScreen = screenPosition;
            toyMenuPoint = point;
            toyMenuPointValid = true;
            ShowToyMenu(toy);
            return true;
        }

        internal void OpenTableMenu(Vector2 screenPosition)
        {
            if (!IsInitialized || runtimeUi == null || !EnsureMarkSet()) return;
            toyMenuScreen = screenPosition;
            toyMenuPointValid = TryMarkPoint(screenPosition, out toyMenuPoint, out _);
            ShowTableMenu();
        }

        private void ShowTableMenu()
        {
            ContextMenus.CloseContextMenu();
            bool here = toyMenuPointValid;
            List<PopupActionOption> actions = new List<PopupActionOption>
            {
                new PopupActionOption("Place a snap point", here, () => PlacePointToy(TableMarkKind.SnapPoint, string.Empty)),
                new PopupActionOption("Lock every piece", true, () => ReportLock(LockEveryPiece(true), true)),
                new PopupActionOption("Free every piece", true, () => ReportLock(LockEveryPiece(false), false)),
                new PopupActionOption("Save progress", activeSession != null, SaveProgress),
                new PopupActionOption("Load progress…", true, ShowLoadMenu),
                new PopupActionOption(flipSaved.Count > 0 ? "Put the table back" : "Flip the table!", true,
                    flipSaved.Count > 0 ? (Action)PutTableBack : FlipTable),
            };
            runtimeUi.ShowContextMenu(
                toyMenuScreen,
                "TABLE",
                "The whole table. Keys: Ctrl+L lock · Ctrl+C/V copy · + - size · U under a deck.",
                actions,
                ContextMenus.CloseContextMenu,
                ContextMenus.DismissPopupFromSecondary);
        }

        private void ReportLock(int changed, bool locking)
        {
            ContextMenus.CloseContextMenu();
            runtimeUi?.ShowTableNews(changed == 0
                ? locking ? "Nothing loose to lock" : "Nothing is locked"
                : locking ? $"Locked {changed} pieces" : $"Freed {changed} pieces", FeedbackColour(localPlayerId));
        }

        private void ShowToyMenu(TableMark toy)
        {
            ContextMenus.CloseContextMenu();
            List<PopupActionOption> actions = new List<PopupActionOption>();
            string title;
            string body;
            switch (toy.Kind)
            {
                case TableMarkKind.Counter:
                    title = "COUNTER";
                    body = $"{CounterWords(toy)}. + / - over it (Shift: 5 at a time).";
                    actions.Add(new PopupActionOption("+1", true, () => StepCounter(toy, 1, true)));
                    actions.Add(new PopupActionOption("-1", true, () => StepCounter(toy, -1, true)));
                    actions.Add(new PopupActionOption("+5", true, () => StepCounter(toy, 5, true)));
                    actions.Add(new PopupActionOption("-5", true, () => StepCounter(toy, -5, true)));
                    actions.Add(new PopupActionOption("Reset to 0", true, () => ReplaceToy(toy, CounterText(0, CounterMax(toy)), "reset a counter")));
                    actions.Add(new PopupActionOption(CounterMax(toy) > 0 ? "Make it a plain counter" : "Make it a dial 1-10", true,
                        () => ReplaceToy(toy, CounterMax(toy) > 0 ? CounterText(CounterValue(toy), 0) : CounterText(1, 10), "changed a counter")));
                    break;
                case TableMarkKind.Timer:
                    title = "SAND TIMER";
                    body = TimerStart(toy) > 0 ? $"Running: {TimerWords(toy)} left." : "Turn it over to start.";
                    actions.Add(new PopupActionOption("Start 1 minute", true, () => StartTimer(toy, 60)));
                    actions.Add(new PopupActionOption("Start 3 minutes", true, () => StartTimer(toy, 180)));
                    actions.Add(new PopupActionOption("Start 5 minutes", true, () => StartTimer(toy, 300)));
                    actions.Add(new PopupActionOption("Stop", TimerStart(toy) > 0, () => StartTimer(toy, -1)));
                    break;
                case TableMarkKind.Note:
                    title = "NOTE";
                    body = toy.Text;
                    actions.Add(new PopupActionOption("Write the Label words on it", true, () =>
                    {
                        string words = DrawLabelWords();
                        if (words.Length > 0) ReplaceToy(toy, words, "wrote a note");
                        else runtimeUi?.ShowTableNews("Type the words in Draw › Label first", FeedbackColour(localPlayerId));
                    }));
                    break;
                case TableMarkKind.SnapPoint:
                    title = "SNAP POINT";
                    body = "A card or piece that comes to rest near it is set on it.";
                    break;
                default:
                    title = toy.Kind == TableMarkKind.HiddenZone ? "HIDDEN ZONE"
                        : toy.Kind == TableMarkKind.ShuffleZone ? "SHUFFLE ZONE"
                        : toy.Kind == TableMarkKind.LayoutZone ? "LAYOUT ZONE"
                        : "DICE TRAY";
                    body = toy.Kind == TableMarkKind.HiddenZone
                        ? $"Only {ZoneOwnerName(toy)} sees inside. The others see a lid with a count."
                        : toy.Kind == TableMarkKind.ShuffleZone ? "A deck put here is shuffled; a die put here rolls."
                        : toy.Kind == TableMarkKind.LayoutZone ? "Cards put here line up in a row."
                        : "Roll dice in it; the walls keep them in.";
                    for (int i = 0; i < ZoneSizes.Length; i++)
                    {
                        int size = i;
                        actions.Add(new PopupActionOption($"Size: {ZoneSizeNames[i]}", true, () => ResizeZone(toy, size)));
                    }

                    if (toy.Kind == TableMarkKind.HiddenZone && ZoneOwner(toy) != localPlayerId)
                    {
                        actions.Add(new PopupActionOption("Make it mine", true, () => ReplaceToyMark(toy, new TableMark(
                            toy.Kind, FeedbackColour(localPlayerId), toy.Width, toy.Points, localPlayerId.Value.ToString(), toy.Yaw),
                            "took a hidden zone")));
                    }

                    break;
            }

            actions.Add(new PopupActionOption("Remove", true, () =>
            {
                ContextMenus.CloseContextMenu();
                ShowTableMarks(tableMarks.Without(toy));
                SessionService.RecordTableMarkStep($"removed a {title.ToLowerInvariant()}");
                toyCheckPending = true;
            }));
            runtimeUi.ShowContextMenu(toyMenuScreen, title, body, actions, ContextMenus.CloseContextMenu,
                ContextMenus.DismissPopupFromSecondary);
        }

        // ---------- Placing and changing toys ----------

        private void PlaceZone(TableMarkKind kind, int size)
        {
            ContextMenus.CloseContextMenu();
            if (!toyMenuPointValid || !RoomForMark()) return;
            float unit = WorldPerCardWidth();
            Vector2 half = ZoneSizes[Mathf.Clamp(size, 0, ZoneSizes.Length - 1)] * (0.5f * unit);
            Vector3 lift = Vector3.up * (ToyLift * unit);
            Vector3 a = toyMenuPoint + new Vector3(-half.x, 0f, -half.y) + lift;
            Vector3 b = toyMenuPoint + new Vector3(half.x, 0f, half.y) + lift;
            Color colour = FeedbackColour(localPlayerId);
            string owner = kind == TableMarkKind.HiddenZone ? localPlayerId.Value.ToString() : string.Empty;
            AddToy(new TableMark(kind, colour, 0.05f * unit, new[] { a, b }, owner, 0f), $"placed a {KindWords(kind)}");
            if (kind == TableMarkKind.HiddenZone)
            {
                runtimeUi?.ShowTableNews("Your hidden zone: only you see what you put in it", colour);
            }
        }

        private void PlacePointToy(TableMarkKind kind, string text)
        {
            ContextMenus.CloseContextMenu();
            if (!toyMenuPointValid || !RoomForMark()) return;
            float unit = WorldPerCardWidth();
            float yaw = cameraInputAdapter != null && cameraInputAdapter.CameraController != null
                ? cameraInputAdapter.CameraController.YawDegrees
                : 0f;
            AddToy(new TableMark(kind, FeedbackColour(localPlayerId), 0.03f * unit, new[] { toyMenuPoint + (Vector3.up * (ToyLift * unit)) },
                text, yaw), $"placed a {KindWords(kind)}");
        }

        private void PlaceNote()
        {
            string words = DrawLabelWords();
            PlacePointToy(TableMarkKind.Note, words.Length > 0 ? words : "Note");
            if (words.Length == 0) runtimeUi?.ShowTableNews("Tip: type words in Draw › Label, then right-click the note › Write", NotePaper);
        }

        private string DrawLabelWords()
        {
            ToolBarView bar = runtimeUi != null ? runtimeUi.ShownToolBar : null;
            TableDrawBarView hud = HudDrawBar;
            string words = hud != null ? hud.LabelText : bar != null ? bar.DrawLabelText : string.Empty;
            return words != null ? words.Trim() : string.Empty;
        }

        private bool RoomForMark()
        {
            if (tableMarks.Count < MaximumTableMarks) return true;
            ShowMessage($"The table holds up to {MaximumTableMarks} drawings and toys. Remove some first.");
            return false;
        }

        private void AddToy(TableMark mark, string step)
        {
            ShowTableMarks(tableMarks.With(mark));
            SessionService.RecordTableMarkStep(step);
            toyCheckPending = true;
            tableSounds?.PlayImpact(TableSoundMaterial.Soft, Feel.ImpactFullSpeed * 0.5f);
        }

        private void ReplaceToy(TableMark toy, string text, string step)
        {
            ReplaceToyMark(toy, new TableMark(toy.Kind, toy.Colour, toy.Width, toy.Points, text, toy.Yaw, toy.Style), step);
        }

        private void ReplaceToyMark(TableMark toy, TableMark next, string step)
        {
            ContextMenus.CloseContextMenu();
            if (!tableMarks.Contains(toy)) return;
            ShowTableMarks(tableMarks.Without(toy).With(next));
            SessionService.RecordTableMarkStep(step);
            toyCheckPending = true;
        }

        private void ResizeZone(TableMark zone, int size)
        {
            ZoneRect(zone, out float minX, out float minZ, out float maxX, out float maxZ);
            float unit = WorldPerCardWidth();
            Vector2 half = ZoneSizes[Mathf.Clamp(size, 0, ZoneSizes.Length - 1)] * (0.5f * unit);
            Vector3 centre = new Vector3((minX + maxX) * 0.5f, zone.Points[0].y, (minZ + maxZ) * 0.5f);
            ReplaceToyMark(zone, new TableMark(zone.Kind, zone.Colour, zone.Width,
                new[] { centre + new Vector3(-half.x, 0f, -half.y), centre + new Vector3(half.x, 0f, half.y) }, zone.Text, zone.Yaw),
                $"resized a {KindWords(zone.Kind)}");
        }

        // A counter's words: "7", or "3/10" for a dial that wraps from 10 back to 1.
        private static int CounterValue(TableMark mark)
        {
            string text = mark.Text ?? string.Empty;
            int slash = text.IndexOf('/');
            string number = slash >= 0 ? text.Substring(0, slash) : text;
            return int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
        }

        private static int CounterMax(TableMark mark)
        {
            string text = mark.Text ?? string.Empty;
            int slash = text.IndexOf('/');
            return slash >= 0 && int.TryParse(text.Substring(slash + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int max)
                ? Mathf.Max(0, max)
                : 0;
        }

        private static string CounterText(int value, int max) =>
            max > 0
                ? $"{value.ToString(CultureInfo.InvariantCulture)}/{max.ToString(CultureInfo.InvariantCulture)}"
                : value.ToString(CultureInfo.InvariantCulture);

        private static string CounterWords(TableMark mark)
        {
            int max = CounterMax(mark);
            return max > 0 ? $"{CounterValue(mark)} of {max}" : CounterValue(mark).ToString(CultureInfo.InvariantCulture);
        }

        private void StepCounter(TableMark toy, int by, bool fromMenu = false)
        {
            int max = CounterMax(toy);
            int value = CounterValue(toy) + by;
            if (max > 0)
            {
                value = ((value - 1) % max + max) % max + 1; // a dial wraps: 10 → 1, 1 → 10
            }

            value = Mathf.Clamp(value, -9999, 99999);
            ReplaceToy(toy, CounterText(value, max), by > 0 ? "counted up" : "counted down");
            tableSounds?.PlayImpact(TableSoundMaterial.Chip, Feel.ImpactFullSpeed * 0.4f);
            if (fromMenu && TryFindToyNow(toy.Points[0], TableMarkKind.Counter, out TableMark again)) ShowToyMenu(again);
        }

        private bool TryFindToyNow(Vector3 at, TableMarkKind kind, out TableMark toy)
        {
            for (int i = 0; i < tableMarks.Count; i++)
            {
                if (tableMarks[i].Kind == kind && FlatDistanceSquared(tableMarks[i].Points[0], at) < 1e-6f)
                {
                    toy = tableMarks[i];
                    return true;
                }
            }

            toy = null;
            return false;
        }

        // A timer's words: "seconds|start" (the start in UTC ticks, so every computer counts down the same; 0: stopped).
        private static int TimerSeconds(TableMark mark)
        {
            string text = mark.Text ?? string.Empty;
            int bar = text.IndexOf('|');
            string number = bar >= 0 ? text.Substring(0, bar) : text;
            return int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds) ? Mathf.Max(1, seconds) : 60;
        }

        private static long TimerStart(TableMark mark)
        {
            string text = mark.Text ?? string.Empty;
            int bar = text.IndexOf('|');
            return bar >= 0 && long.TryParse(text.Substring(bar + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out long start)
                ? start
                : 0L;
        }

        private static string TimerWords(TableMark mark)
        {
            int total = TimerSeconds(mark);
            long start = TimerStart(mark);
            int left = total;
            if (start > 0)
            {
                double gone = (DateTime.UtcNow.Ticks - start) / (double)TimeSpan.TicksPerSecond;
                left = Mathf.Max(0, total - (int)Math.Floor(gone));
            }

            return $"{left / 60}:{(left % 60).ToString("00", CultureInfo.InvariantCulture)}";
        }

        private void StartTimer(TableMark toy, int seconds)
        {
            int total = seconds > 0 ? seconds : TimerSeconds(toy);
            long start = seconds > 0 ? DateTime.UtcNow.Ticks : 0L;
            ReplaceToy(toy, $"{total.ToString(CultureInfo.InvariantCulture)}|{start.ToString(CultureInfo.InvariantCulture)}",
                seconds > 0 ? "turned a sand timer over" : "stopped a sand timer");
            if (seconds > 0) runtimeUi?.ShowTableNews($"Sand timer: {seconds / 60} minute{(seconds >= 120 ? "s" : string.Empty)}", TimerColour);
        }

        private static string WrapNote(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= 16) return text ?? string.Empty;
            System.Text.StringBuilder wrapped = new System.Text.StringBuilder(text.Length + 4);
            int line = 0;
            string[] words = text.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (line > 0 && line + words[i].Length > 16)
                {
                    wrapped.Append('\n');
                    line = 0;
                }
                else if (line > 0)
                {
                    wrapped.Append(' ');
                    line++;
                }

                wrapped.Append(words[i]);
                line += words[i].Length;
            }

            return wrapped.ToString();
        }

        private string ZoneOwnerName(TableMark mark)
        {
            PlayerId owner = ZoneOwner(mark);
            return owner.IsEmpty ? "its owner" : FormatPlayerName(owner);
        }

        private static string KindWords(TableMarkKind kind)
        {
            switch (kind)
            {
                case TableMarkKind.HiddenZone: return "hidden zone";
                case TableMarkKind.ShuffleZone: return "shuffle zone";
                case TableMarkKind.LayoutZone: return "layout zone";
                case TableMarkKind.SnapPoint: return "snap point";
                case TableMarkKind.Note: return "note";
                case TableMarkKind.Counter: return "counter";
                case TableMarkKind.Timer: return "sand timer";
                case TableMarkKind.DiceTray: return "dice tray";
                default: return "table toy";
            }
        }

        // The eraser takes a toy away too (a point toy within reach, or a zone the eraser is in).
        private static bool ToyTouches(TableMark mark, Vector3 point, float radius)
        {
            if (mark.Kind == TableMarkKind.PieceLook || mark.Points.Length == 0) return false;
            if (IsZoneMark(mark.Kind))
            {
                // Only its edge: rubbing inside a zone leaves it (pieces are often put there).
                ZoneRect(mark, out float minX, out float minZ, out float maxX, out float maxZ);
                bool inside = point.x >= minX - radius && point.x <= maxX + radius && point.z >= minZ - radius && point.z <= maxZ + radius;
                bool deep = point.x >= minX + radius && point.x <= maxX - radius && point.z >= minZ + radius && point.z <= maxZ - radius;
                return inside && !deep;
            }

            float reach = radius + 0.3f;
            return FlatDistanceSquared(mark.Points[0], point) <= reach * reach;
        }

        // ---------- Pieces' menus (pawns, tokens, dice) ----------

        /// <summary>Lock / Free and Tint for a loose piece's right-click menu.</summary>
        internal void AddPieceActions(List<PopupActionOption> actions, TabletopObjectId pieceId, bool tint)
        {
            if (!TryFindAnyView(pieceId, out TabletopObjectView view) || view.BoundState == null || !view.BoundState.ContainerId.IsEmpty) return;
            bool locked = view.BoundState.IsUserLocked;
            if (!locked && !(view is CardView))
            {
                actions.Add(new PopupActionOption("Move (click where)", true, () => BeginPieceMove(view)));
            }

            actions.Add(new PopupActionOption(locked ? "Free (Ctrl+L)" : "Lock (Ctrl+L)", true, () =>
            {
                ContextMenus.CloseContextMenu();
                ToggleLock(view);
            }));
            actions.Add(new PopupActionOption("Bigger (+)", true, () =>
            {
                ContextMenus.CloseContextMenu();
                ScalePiece(view, 1);
            }));
            actions.Add(new PopupActionOption("Smaller (-)", true, () =>
            {
                ContextMenus.CloseContextMenu();
                ScalePiece(view, -1);
            }));
            if (!tint) return;
            actions.Add(new PopupActionOption("Tint…", true, () => ShowTintMenu(view)));
        }

        /// <summary>"Play to the table" for a card in your hand (a careful play, without dragging).</summary>
        internal void AddHandPlayAction(List<PopupActionOption> actions, TabletopObjectId cardId, ContainerId containerId)
        {
            if (handView == null || !handView.IsBound || handView.ContainerId != containerId) return;
            actions.Add(new PopupActionOption("Play to the table (face up)", true, () =>
            {
                ContextMenus.CloseContextMenu();
                PlayHandCardToTable(cardId);
            }));
        }

        private void PlayHandCardToTable(TabletopObjectId cardId)
        {
            if (!Registry.TryGetCardView(cardId, out CardView card) || card == null || !TryGetHandEdgePose(out Vector3 surface, out _)) return;
            Vector3 middle = FindClassicTable() ? classicFrame.Centre : Vector3.zero;
            Vector3 inward = middle - surface;
            inward.y = 0f;
            inward = inward.sqrMagnitude > 1e-6f ? inward.normalized : Vector3.forward;
            Vector3 anchor = surface + (inward * (2.2f * WorldPerCardWidth()));
            layoutCards.Clear();
            layoutCards.Add(card);
            SessionService.BeginUndoBatch();
            try
            {
                PlaceHandCards(layoutCards, anchor, ContainerId.Empty);
            }
            finally
            {
                SessionService.EndUndoBatch();
                layoutCards.Clear();
            }
        }

        // A piece follows the pointer and is set down exactly where you click (no toss): for a trackpad, or a careful move.
        private void BeginPieceMove(TabletopObjectView view)
        {
            ContextMenus.CloseContextMenu();
            if (componentPlacementController == null || view == null || !view.IsBound || view.BoundState == null) return;
            TabletopObjectState state = view.BoundState;
            if (!TryResolveDuplicatedComponent(view.ObjectId, state, out TabletopComponentKind kind, out int sides)) return;
            GameObject preview = CreateToolboxPlacementPreview(kind, sides);
            componentPlacementController.BeginCustomComponentPlacement(
                kind,
                preview,
                state.Pose.RotationDegrees,
                state.Pose.Layer,
                state.Pose.LocalOrder,
                pose => view != null && view.IsBound && view.BoundState != null && MoveLoosePiece(view, view.BoundState, pose));
            ShowPlacementHint($"Move the {ToolboxComponentLabel(kind)}"); // one move: a click sets it down
        }

        private void ShowTintMenu(TabletopObjectView view)
        {
            Vector2 anchor = contextMenuAnchorScreenPosition;
            ContextMenus.CloseContextMenu();
            List<PopupActionOption> actions = new List<PopupActionOption>(TintChoices.Length);
            for (int i = 0; i < TintChoices.Length; i++)
            {
                int choice = i;
                actions.Add(new PopupActionOption(TintNames[i], true, () =>
                {
                    ContextMenus.CloseContextMenu();
                    TintPiece(view, choice);
                }));
            }

            runtimeUi.ShowContextMenu(anchor, "TINT", "A colour for this piece (shared with the table).", actions,
                ContextMenus.CloseContextMenu, ContextMenus.DismissPopupFromSecondary);
        }

        // ---------- Table flip (and putting it back) ----------

        private void FlipTable()
        {
            ContextMenus.CloseContextMenu();
            flipSaved.Clear();
            float unit = WorldPerCardWidth();
            int flown = FlipViews(Registry.cardViews, unit) + FlipViews(Registry.pawnViews, unit) + FlipViews(Registry.tokenViews, unit)
                + FlipViews(Registry.dieViews, unit);
            if (flown == 0)
            {
                runtimeUi?.ShowTableNews("Nothing loose to flip", FeedbackColour(localPlayerId));
                return;
            }

            flipQuietUntil = Time.unscaledTime + FlipQuietSeconds; // the game does not read where the pieces land

            tableSounds?.PlayImpact(TableSoundMaterial.Wood, Feel.ImpactFullSpeed * 1.5f);
            runtimeUi?.ShowTableNews("TABLE FLIPPED! Right-click the table, then Put the table back", new Color(1f, 0.231f, 0.231f, 1f));
        }

        private int FlipViews<TView>(List<TView> views, float unit) where TView : TabletopObjectView
        {
            int flown = 0;
            for (int i = 0; i < views.Count; i++)
            {
                TView view = views[i];
                if (view == null || !view.IsBound || view.BoundState == null || view.PhysicalObject == null) continue;
                TabletopObjectState state = view.BoundState;
                if (!state.ContainerId.IsEmpty || state.IsUserLocked || ActiveCartridge.IsFixedBoardPiece(state.Id)) continue;
                Rigidbody body = view.PhysicalObject.Body;
                if (body == null || body.isKinematic || state.PhysicalState == null || view.PhysicalObject.IsHeld) continue;
                flipSaved[state.Id] = state.PhysicalState;
                body.WakeUp();
                Vector3 outward = new Vector3(UnityEngine.Random.Range(-1f, 1f), 0f, UnityEngine.Random.Range(-1f, 1f));
                body.linearVelocity = (Vector3.up * (FlipUpSpeed * unit * UnityEngine.Random.Range(0.7f, 1.2f)))
                    + (outward * (2.5f * unit));
                body.angularVelocity = UnityEngine.Random.insideUnitSphere * 14f;
                flown++;
            }

            return flown;
        }

        private void PutTableBack()
        {
            ContextMenus.CloseContextMenu();
            int back = 0;
            SessionService.BeginUndoBatch();
            try
            {
                foreach (KeyValuePair<TabletopObjectId, PhysicalObjectState> pair in flipSaved)
                {
                    if (!matchState.ContainsObject(pair.Key) || !TryFindAnyView(pair.Key, out TabletopObjectView view)) continue;
                    TabletopObjectState state = matchState.GetObject(pair.Key);
                    if (!state.ContainerId.IsEmpty || view.PhysicalObject == null || view.PhysicalObject.IsHeld) continue;
                    PhysicalObjectState saved = pair.Value;
                    PhysicalObjectState resting = new PhysicalObjectState(
                        saved.Position, saved.Rotation, new PhysicalVector3(0f, 0f, 0f), new PhysicalVector3(0f, 0f, 0f),
                        PhysicalObjectMode.Sleeping, saved.ControllingPlayerId);
                    Vector3 from = ViewPosition(view);
                    Quaternion fromRotation = view.PhysicalObject.Body != null ? view.PhysicalObject.Body.rotation : view.transform.rotation;
                    CommandResult result = TableAuthority.Submit<CommitPhysicalObjectCommand, CommandResult>(
                        new CommitPhysicalObjectCommand(CreateCommandContext(), pair.Key, resting, state.PhysicalRevision, null,
                            AuthoritativeActionRecordMode.Transaction));
                    if (!result.Succeeded) continue;
                    view.ApplyAcceptedState();
                    view.PhysicalObject.GlideFrom(from, fromRotation, 0.45f, 0.02f);
                    back++;
                }
            }
            finally
            {
                SessionService.EndUndoBatch();
            }

            flipSaved.Clear();
            flipQuietUntil = Time.unscaledTime + FlipQuietSeconds;
            runtimeUi?.ShowTableNews(back == 0 ? "Nothing to put back" : $"Table put back: {back} pieces in place", FeedbackColour(localPlayerId));
        }

        // A new table: nothing carried over.
        private void ClearToys()
        {
            toyVisuals.Clear();
            shuffledPileCounts.Clear();
            diceInShuffleZones.Clear();
            lookedViews.Clear();
            pieceBaseScales.Clear();
            flipSaved.Clear();
            underDropCard = TabletopObjectId.Empty;
            toyCheckPending = true;
            cartridgeHeld = false;
        }

        // ---------- The turn cartridge, passed by hand (doc 57) ----------

        // A press on the turn cartridge picks it up (from TryBeginSelect).
        private bool TryPressTurnCartridge(Vector2 screenPosition)
        {
            if (turnCartridge == null || !turnCartridge.gameObject.activeSelf || targetCamera == null) return false;
            if (!ReferenceEquals(ActiveCartridge, TrapFloorGame)) return false;
            Ray ray = targetCamera.ScreenPointToRay(screenPosition);
            Plane plane = new Plane(Vector3.up, turnCartridge.position);
            if (!plane.Raycast(ray, out float distance)) return false;
            Vector3 hit = ray.GetPoint(distance);
            float reach = 0.85f * WorldPerCardWidth();
            if (FlatDistanceSquared(hit, turnCartridge.position) > reach * reach) return false;
            cartridgeHeld = true;
            cartridgeHeldFrom = turnCartridge.position;
            return true;
        }

        private void UpdateCartridgeCarry(Vector2 screenPosition)
        {
            if (!cartridgeHeld || turnCartridge == null || !TryMarkPoint(screenPosition, out Vector3 point, out _)) return;
            turnCartridge.position = point + (Vector3.up * (0.5f * WorldPerCardWidth()));
        }

        // Let go on another player's Console: the turn passes (END TURN). Anywhere else it hops back.
        private void ReleaseCartridgeCarry(Vector2 screenPosition)
        {
            if (!cartridgeHeld) return;
            cartridgeHeld = false;
            PlayerId turn = ActiveCartridge.TurnPlayerId;
            bool passed = false;
            if (TryFindConsoleUnderPointer(screenPosition, out RuntimeConsoleInstance console) && console != null && console.View != null
                && !turn.IsEmpty && TryTurnConsole(turn, out ConsoleView mine, out _) && !ReferenceEquals(console.View, mine))
            {
                passed = true;
                TrapFloorGame.RequestEndTurn(false);
            }

            if (turnCartridge != null)
            {
                cartridgeFrom = turnCartridge.position;
                cartridgeHopStarted = Time.unscaledTime;
            }

            if (!passed) runtimeUi?.ShowTableNews("Drop the cartridge on the next player's Console to pass the turn", FeedbackColour(localPlayerId));
        }

        private void CancelCartridgeCarry()
        {
            if (!cartridgeHeld) return;
            cartridgeHeld = false;
            if (turnCartridge == null) return;
            cartridgeFrom = turnCartridge.position;
            cartridgeHopStarted = Time.unscaledTime;
        }

        // ---------- The floor's recap (doc 57: "a recap card after each floor") ----------

        private void CountRevealForRecap(string category)
        {
            switch (category)
            {
                case "Trap": recapTraps++; break;
                case "Key": recapKeys++; break;
                case "GrandExit":
                case "SecretExit": recapExits++; break;
                default: recapPrizes++; break;
            }
        }

        /// <summary>What the floor showed ("3 traps · 2 keys · 1 exit · 4 prizes"), then the counts start again.</summary>
        internal string TakeFloorRecap()
        {
            int total = recapTraps + recapKeys + recapExits + recapPrizes;
            string recap = total == 0
                ? null
                : $"Revealed: {Plural(recapTraps, "trap")} · {Plural(recapKeys, "key")} · {Plural(recapExits, "exit")} · {Plural(recapPrizes, "prize")}";
            recapTraps = recapKeys = recapExits = recapPrizes = 0;
            return recap;
        }

        private static string Plural(int count, string word) => count == 1 ? $"1 {word}" : $"{count} {word}s";
    }
}
