using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// Owner, 2026-11-03 (doc 57, "free hands, live room"): the Assist setting and the table's game feel.
    /// <list type="bullet">
    /// <item><b>Assist</b> (Settings › GAME): FREE (the default) turns the hints and tips off and the game's popups
    /// into a short line at the top; REMINDERS keeps the Hints switch; FULL is the game as before. The rules are still
    /// worked out quietly in every level (keys, falls, floors, turns).</item>
    /// <item><b>The referee's whisper</b>: in FREE and REMINDERS, when something looks off the game says so in one
    /// line (and in the log), and never stops anyone.</item>
    /// <item><b>The turn cartridge</b>: a chunky cartridge that sits by the Console of the player whose turn it is,
    /// showing the round, and hops to the next Console when the turn passes.</item>
    /// <item><b>Reactions</b>: a floor tile turning face up rings in its colour (trap red, key gold, exit green,
    /// prize teal).</item>
    /// </list>
    /// Everything here is Presentation only and allocates nothing per frame (the cartridge's text changes with the
    /// round).
    /// </summary>
    public sealed partial class TabletopComposition
    {
        private static readonly Color WhisperColour = new Color(1f, 0.698f, 0.118f, 1f);
        private static readonly int AssistBaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int AssistColorId = Shader.PropertyToID("_Color");
        private const float CartridgeHopSeconds = 0.7f;
        private const int WhisperShownLetters = 110;

        private int appliedAssist = -1;
        private bool hintsBeforeFree = true;
        private bool tipsBeforeFree = true;

        private Transform turnCartridge;
        private Renderer turnCartridgeBody;
        private TextMesh turnCartridgeLabel;
        private Transform turnCartridgeSticker;
        private Transform turnCartridgeGrip;
        private MaterialPropertyBlock assistBlock;
        private Material assistMaterial;
        private PlayerId cartridgePlayer;
        private Vector3 cartridgeFrom;
        private Vector3 cartridgeTo;
        private float cartridgeHopStarted = -10f;
        private int cartridgeRound = -1;
        private readonly Dictionary<ConsoleView, Vector3> consoleSizes = new Dictionary<ConsoleView, Vector3>();

        // Called every frame from Update.
        private void TickAssist()
        {
            ApplyAssistLevel();
            TickTurnCartridge();
        }

        // ---------- Assist ----------

        private void ApplyAssistLevel()
        {
            int level = (int)PlayAssist.Level;
            bool free = level == (int)PlayAssistLevel.Free;
            if (level == appliedAssist && (!free || (!hintsEnabled && !tipsEnabled))) return;
            if (free && appliedAssist != (int)PlayAssistLevel.Free)
            {
                hintsBeforeFree = hintsEnabled;
                tipsBeforeFree = tipsEnabled;
            }

            if (free)
            {
                if (hintsEnabled) RulesCardPanel.SetHintsEnabled(false);
                if (tipsEnabled) RulesCardPanel.SetTipsEnabled(false);
            }
            else if (appliedAssist == (int)PlayAssistLevel.Free)
            {
                if (hintsBeforeFree != hintsEnabled) RulesCardPanel.SetHintsEnabled(hintsBeforeFree);
                if (tipsBeforeFree != tipsEnabled) RulesCardPanel.SetTipsEnabled(tipsBeforeFree);
            }

            appliedAssist = level;
            if (trapFloorTemplate != null) RefreshTrapFloorBoardFeedback(); // the markers follow the new level
        }

        /// <summary>
        /// The referee's whisper: one line at the top and in the log, only while Assist is FREE or REMINDERS (FULL
        /// asks with its own popups). It never blocks anything.
        /// </summary>
        internal void Whisper(string text)
        {
            if (string.IsNullOrEmpty(text) || !PlayAssist.IsQuiet || TableFlipQuiet) return;
            if (PlayAssist.IsFree)
            {
                // Owner, 2026-11-05 (decision 3): Free shows no game reminders; the line is only kept in the log.
                RecordHandoverLine($"Referee: {text}");
                return;
            }

            // Owner's playtest, 2026-11-04: a long list ran off the screen; the top line keeps the first part, the log all.
            string shown = text;
            if (shown.Length > WhisperShownLetters)
            {
                int cut = shown.LastIndexOf("; ", WhisperShownLetters, StringComparison.Ordinal);
                shown = (cut > 20 ? shown.Substring(0, cut) : shown.Substring(0, WhisperShownLetters)) + " ... (more in the log: L)";
            }

            runtimeUi?.ShowTableNews($"Referee: {shown}", WhisperColour);
            RecordHandoverLine($"Referee: {text}");
        }

        // ---------- Reactions ----------

        /// <summary>A floor tile turned face up: a ring in its colour where it lies, with the ping sound.</summary>
        internal void ReactToRevealedTile(TabletopObjectId tileId, string category)
        {
            CountRevealForRecap(category); // doc 57: the floor's recap
            if (PlayAssist.IsFree) return; // owner, 2026-11-05: no ring in the default mode
            if (!EnsureBoardFeedback() || !Registry.TryGetCardView(tileId, out CardView tile) || tile == null) return;
            Color colour;
            switch (category)
            {
                case "Trap": colour = new Color(0.937f, 0.267f, 0.267f, 1f); break;
                case "Key": colour = new Color(1f, 0.792f, 0.157f, 1f); break;
                case "GrandExit":
                case "SecretExit": colour = new Color(0.184f, 0.831f, 0.420f, 1f); break;
                default: colour = new Color(0.024f, 0.937f, 0.941f, 1f); break;
            }

            boardFeedback.Ping(tile.transform.position, colour, 1.2f);
        }

        // ---------- The turn cartridge ----------

        private void TickTurnCartridge()
        {
            // Owner, 2026-11-05: no turn marker in the default mode (Free).
            PlayerId turn = IsInitialized && matchState != null && !PlayAssist.IsFree ? ActiveCartridge.TurnPlayerId : PlayerId.Empty;
            if (turn.IsEmpty || !TryTurnConsole(turn, out ConsoleView console, out Vector3 inward) || !EnsureTurnCartridge())
            {
                HideTurnCartridge();
                return;
            }

            if (cartridgeHeld) return; // doc 57: a player is passing it by hand
            float unit = WorldPerCardWidth();
            Vector3 size = ConsoleSize(console);
            Vector3 left = Vector3.Cross(Vector3.up, inward).normalized;
            float width = (Mathf.Abs(left.x) * size.x) + (Mathf.Abs(left.z) * size.z);
            Vector3 place = console.transform.position
                - (left * ((0.5f * width) + (0.9f * unit))) // the side away from the hand's row
                + (Vector3.up * (0.09f * unit));
            Quaternion facing = Quaternion.LookRotation(-inward, Vector3.up);
            if (turn != cartridgePlayer || !turnCartridge.gameObject.activeSelf)
            {
                cartridgeFrom = turnCartridge.gameObject.activeSelf ? turnCartridge.position : place;
                cartridgeHopStarted = Time.unscaledTime;
                cartridgePlayer = turn;
                Color seat = FeedbackColour(turn);
                Tint(turnCartridgeBody, seat);
                if (!turnCartridge.gameObject.activeSelf) turnCartridge.gameObject.SetActive(true);
            }

            cartridgeTo = place;
            float t = Mathf.Clamp01((Time.unscaledTime - cartridgeHopStarted) / CartridgeHopSeconds);
            float eased = t * t * (3f - (2f * t));
            Vector3 at = Vector3.Lerp(cartridgeFrom, cartridgeTo, eased) + (Vector3.up * (Mathf.Sin(t * Mathf.PI) * 1.2f * unit));
            turnCartridge.SetPositionAndRotation(at, facing * Quaternion.Euler(0f, (1f - eased) * 360f, 0f));
            turnCartridgeBody.transform.localScale = new Vector3(1.1f * unit, 0.22f * unit, 1.45f * unit);
            if (turnCartridgeLabel != null)
            {
                float letters = unit * 0.3f / LabelPrefabLetterHeight;
                turnCartridgeLabel.transform.localPosition = new Vector3(0f, 0.135f * unit, -0.05f * unit);
                turnCartridgeSticker.localPosition = new Vector3(0f, 0.115f * unit, -0.05f * unit);
                turnCartridgeSticker.localScale = new Vector3(0.86f * unit, 0.02f * unit, 1.0f * unit);
                turnCartridgeGrip.localPosition = new Vector3(0f, 0.04f * unit, 0.62f * unit);
                turnCartridgeGrip.localScale = new Vector3(1.1f * unit, 0.3f * unit, 0.22f * unit);
                turnCartridgeLabel.transform.localScale = new Vector3(letters, letters, letters);
            }

            int round = trapFloorTurnState != null ? trapFloorTurnState.CurrentRound : 0;
            if (round != cartridgeRound && turnCartridgeLabel != null)
            {
                cartridgeRound = round;
                turnCartridgeLabel.text = round > 0 ? $"ROUND\n{round}" : "YOUR\nTURN";
            }

            // (Owner's test, 2026-11-03: a glowing plate under the Console drew over the mat; the turn shows on the
            // cartridge and the player's avatar instead.)
        }

        // P8A (owner, 2026-11-10: the drawn Abilities overlapped the turn cartridge and the Console): where this turn's
        // Ability draw lies, a row of separate cards on the Console's cartridge side, past the cartridge and toward the
        // player's own edge of the table (clear of the Console, the cartridge and the bought-card spot).
        internal bool TryAbilityDealSpots(PlayerId player, int count, List<TableCoordinate> spots)
        {
            spots.Clear();
            if (coordinateConverter == null || !TryTurnConsole(player, out ConsoleView console, out Vector3 inward)) return false;
            float unit = WorldPerCardWidth();
            Vector3 size = ConsoleSize(console);
            Vector3 left = Vector3.Cross(Vector3.up, inward).normalized;
            float width = (Mathf.Abs(left.x) * size.x) + (Mathf.Abs(left.z) * size.z);
            float depth = (Mathf.Abs(inward.x) * size.x) + (Mathf.Abs(inward.z) * size.z);
            for (int i = 0; i < count; i++)
            {
                Vector3 at = console.transform.position
                    - (left * ((0.5f * width) + (2.0f * unit) + (i * 1.15f * unit)))
                    - (inward * (0.35f * depth));
                spots.Add(coordinateConverter.ToTableCoordinate(at));
            }

            return spots.Count == count;
        }

        // The Console of the seat whose player has the turn, and the way from it to the middle of the table.
        private bool TryTurnConsole(PlayerId turn, out ConsoleView console, out Vector3 inward)
        {
            console = null;
            inward = Vector3.forward;
            IReadOnlyList<CartridgeSeat> seats = ActiveCartridge.Seats;
            for (int i = 0; i < seats.Count; i++)
            {
                if (!matchState.Seats.TryGetValue(seats[i].SeatId, out SeatState seat)
                    || seat.OccupantPlayerId != turn
                    || seat.Console == null
                    || seat.Console.SlotContainerIds.Count == 0
                    || !TryResolveConsolePlacement(seat.Console.SlotContainerIds[0], out _, out _, out console)
                    || console == null)
                {
                    continue;
                }

                Vector3 toMiddle = (FindClassicTable() ? classicFrame.Centre : Vector3.zero) - console.transform.position;
                toMiddle.y = 0f;
                inward = toMiddle.sqrMagnitude > 1e-6f ? toMiddle.normalized : Vector3.forward;
                return console.gameObject.activeInHierarchy;
            }

            return false;
        }

        // A Console's footprint on the table, measured once from its renderers.
        private Vector3 ConsoleSize(ConsoleView console)
        {
            if (consoleSizes.TryGetValue(console, out Vector3 size)) return size;
            Renderer[] parts = console.GetComponentsInChildren<Renderer>(false);
            Bounds bounds = new Bounds(console.transform.position, Vector3.zero);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].GetComponent<TextMesh>() != null) continue;
                bounds.Encapsulate(parts[i].bounds);
            }

            size = bounds.size;
            consoleSizes[console] = size;
            return size;
        }

        private bool EnsureTurnCartridge()
        {
            if (turnCartridge != null) return true;
            if (localHandVisual == null || localHandVisual.FeedbackRenderer == null) return false;
            assistMaterial = new Material(localHandVisual.FeedbackRenderer.sharedMaterial) { name = "Turn Cartridge" };
            assistBlock = new MaterialPropertyBlock();

            turnCartridge = new GameObject("Turn Cartridge").transform;
            turnCartridge.SetParent(transform, false);
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(turnCartridge, false);
            turnCartridgeBody = body.GetComponent<Renderer>();
            turnCartridgeBody.sharedMaterial = assistMaterial;
            // A dark label sticker on top and a grip at the back, so it reads as a game cartridge.
            turnCartridgeSticker = NewCartridgePart("Sticker", new Color(0.075f, 0.09f, 0.1f, 1f));
            turnCartridgeGrip = NewCartridgePart("Grip", new Color(0.12f, 0.14f, 0.15f, 1f));
            // The label on its top, upright for the player at that Console.
            if (EnsureMarkSet() && markSet.TableMarkLabel != null)
            {
                turnCartridgeLabel = Instantiate(markSet.TableMarkLabel, turnCartridge, false);
                turnCartridgeLabel.name = "Round";
                turnCartridgeLabel.transform.localRotation = Quaternion.Euler(90f, 180f, 0f); // reads from that seat
                turnCartridgeLabel.anchor = TextAnchor.MiddleCenter;
                turnCartridgeLabel.alignment = TextAlignment.Center;
                turnCartridgeLabel.color = Color.white;
            }

            turnCartridge.gameObject.SetActive(false);
            return true;
        }

        private Transform NewCartridgePart(string name, Color colour)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(turnCartridge, false);
            Renderer renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = assistMaterial;
            Tint(renderer, colour);
            return part.transform;
        }

        private void Tint(Renderer renderer, Color colour)
        {
            if (renderer == null || assistBlock == null) return;
            renderer.GetPropertyBlock(assistBlock);
            assistBlock.SetColor(AssistBaseColorId, colour);
            assistBlock.SetColor(AssistColorId, colour);
            renderer.SetPropertyBlock(assistBlock);
        }

        private void HideTurnCartridge()
        {
            if (turnCartridge != null && turnCartridge.gameObject.activeSelf) turnCartridge.gameObject.SetActive(false);
            cartridgePlayer = PlayerId.Empty;
        }

        // A new table: the cartridge is made again for its Consoles (from ClearPolish).
        private void ClearTurnCartridge()
        {
            HideTurnCartridge();
            consoleSizes.Clear();
            cartridgeRound = -1;
            appliedAssist = -1;
        }
    }
}
