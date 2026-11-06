using System;
using System.Collections.Generic;
using System.Text;
using ConsoleCards.Application.Authority;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.Privacy;
using ConsoleCards.Presentation.Views;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>How the private view drew its cards (PRIVACY-SCENE diagnostics; the four cases stay apart).</summary>
    internal sealed class PrivateViewDiagnostics
    {
        public int HiddenBacks;       // case 1: legitimately hidden, drawn as the game's card back
        public int MissingDefinition; // case 2: an identity was sent that this computer does not know
        public int MissingFrontArt;   // case 3: the card is known, its picture is not
        public int PublicFaces;       // public cards drawn with their own look
        public int MissingViews;      // case 4: a card of the table got no view
    }

    public sealed partial class TabletopComposition
    {
        // PRIVACY-SCENE (s146). Fields exist in every build; only the Editor entry below can turn the view on.
        internal bool privateViewActive;
        internal Texture2D privateViewCardBack;
        internal PrivateViewDiagnostics privateViewDiagnostics = new PrivateViewDiagnostics();

#if UNITY_EDITOR
        /// <summary>
        /// Development only (Editor, Play mode, an offline Trap Floor table): this computer plays the host, builds the
        /// package <paramref name="seatIndex"/>'s player would receive (or a spectator's, for -1), and then rebuilds
        /// this very scene from that package alone — as an Empty Table session, so no game cartridge, no Trap Floor
        /// rules and no seed are involved. The scene is then inspected. Returns a report (no keys, no real hidden ids).
        /// </summary>
        public static string ShowPrivateRecipientViewForDevelopment(TabletopComposition host, int seatIndex)
        {
            if (host == null) return "[PrivateView] No table in the scene.";
            return host.ShowPrivateRecipientView(seatIndex);
        }

        private string ShowPrivateRecipientView(int seatIndex)
        {
            if (!IsInitialized || matchState == null) return "[PrivateView] Start a Trap Floor table first.";
            if (IsOnlineTable) return "[PrivateView] Refused: only on an offline table (it never touches the network).";
            if (trapFloorTemplate == null || trapFloorTurnState == null) return "[PrivateView] Refused: the table is not a Trap Floor game.";
            if (privateViewActive) return "[PrivateView] Already showing a private view: open the main menu to leave it.";

            // ---- host side: the full table is here; build what one recipient may receive ----
            MatchState full = matchState;
            IReadOnlyList<PlayerId> order = trapFloorTurnState.PlayerOrder;
            PlayerId recipient = seatIndex >= 0 && seatIndex < order.Count ? order[seatIndex] : RecipientSnapshotProjection.Spectator;
            string who = recipient.IsEmpty ? "a spectator" : $"seat {seatIndex + 1}";
            RecipientSnapshotProjection.Classify(full, recipient, out HashSet<TabletopObjectId> hidden, out _);
            HashSet<string> hiddenOnlyNames = HiddenOnlyNames(full, hidden, trapFloorTemplate.CardLabels);
            List<Guid> hiddenRealIds = new List<Guid>();
            foreach (TabletopObjectId id in hidden) hiddenRealIds.Add(id.Value);

            RecipientHandleTable handles = new RecipientHandleTable(RecipientSnapshotProjection.NewSessionKey());
            byte[] bytes = TableWire.Write(RecipientBootstrapPackage.Build(full, handles, recipient, trapFloorTemplate.CardLabels), false);
            int idsOnWire = 0;
            foreach (Guid id in hiddenRealIds) if (Contains(bytes, id.ToByteArray())) idsOnWire++;
            int namesOnWire = 0;
            List<string> wireNames = new List<string>();
            foreach (string name in hiddenOnlyNames) if (Contains(bytes, Encoding.UTF8.GetBytes(name))) { namesOnWire++; wireNames.Add(name); }
            HashSet<string> publicLabelTexts = new HashSet<string>();
            foreach (KeyValuePair<TabletopObjectId, string> pair in trapFloorTemplate.CardLabels)
                if (!hidden.Contains(pair.Key) && full.Cards.ContainsKey(pair.Key) && !string.IsNullOrEmpty(pair.Value)) publicLabelTexts.Add(pair.Value);
            Texture2D back = CurrentBoardCardBack(); // the game's public card-back picture

            // ---- client side: everything below uses the received bytes only ----
            RecipientBootstrapPackage received = (RecipientBootstrapPackage)TableWire.Read(bytes, 0, bytes.Length, false);
            RecipientClientTable client = RecipientClientTable.Build(received); // checks the host's digest
            if (!SessionService.TryReplaceTable(TabletopSessionSelection.EmptyCustom))
                return "[PrivateView] The Empty Table session could not be made: " + (gameTemplatesPanelError ?? "unknown");

            privateViewCardBack = back;
            privateViewDiagnostics = new PrivateViewDiagnostics();
            privateViewActive = true;
            GameTemplateInitialSnapshot shown = received.Table.ForSession(activeSession.CurrentMatch.Id, activeSession.CurrentMatch.GameTemplateId);
            if (!RestoreOnlineSnapshot(SessionUndoSnapshot.ForRecipientView(shown), Math.Max(1L, received.Table.Revision), out string error))
            {
                privateViewActive = false;
                return "[PrivateView] The scene could not be built from the package: " + error;
            }

            labelsByCardId.Clear();
            if (received.Labels != null) foreach (RecipientLabel label in received.Labels) labelsByCardId[label.Reference] = label.Text;
            RefreshAllCardArt();
            ShowMessage($"Private view ({who}): built from that player's package only. Development view: nothing here is sent anywhere.");

            // ---- inspect the actual scene ----
            int hiddenCards = 0, viewsWithHiddenFace = 0, namedViews = 0;
            foreach (ClientCardView card in client.Cards)
            {
                if (!Registry.TryGetCardView(card.Reference, out CardView view) || view == null)
                {
                    privateViewDiagnostics.MissingViews++;
                    continue;
                }

                if (!card.IsHidden) continue;
                hiddenCards++;
                if (CardInspectPanel.TryGetCardVisualReferences(card.Reference, out CardVisualReferences visuals))
                {
                    if (!string.IsNullOrEmpty(visuals.FrontLabel?.text) || !string.IsNullOrEmpty(visuals.BackLabel?.text)) namedViews++;
                    Renderer face = visuals.FaceUpRenderer;
                    if (face != null && face.sharedMaterial != null && face.sharedMaterial.mainTexture != null
                        && face.sharedMaterial.mainTexture != back) viewsWithHiddenFace++;
                }
            }

            int realIdsInClient = 0;
            foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in matchState.Cards) if (hidden.Contains(pair.Key)) realIdsInClient++;

            StringBuilder report = new StringBuilder();
            report.Append($"[PrivateView] Scene built for {who} from {bytes.Length} package bytes. ");
            report.Append($"Wire: hidden real ids {idsOnWire}/{hiddenRealIds.Count}, hidden-only names {namesOnWire}/{hiddenOnlyNames.Count}. ");
            report.Append($"Client table: {client.Cards.Count} cards ({hiddenCards} hidden), hidden real ids in the scene's match {realIdsInClient}. ");
            report.Append($"Views: hidden backs {privateViewDiagnostics.HiddenBacks}, public faces {privateViewDiagnostics.PublicFaces}, ");
            report.Append($"missing definition {privateViewDiagnostics.MissingDefinition}, missing front art {privateViewDiagnostics.MissingFrontArt}, ");
            report.Append($"cards without a view {privateViewDiagnostics.MissingViews}, hidden views with a face picture {viewsWithHiddenFace}, hidden views with a label {namedViews}. ");
            report.Append("Scene object scan: two frames later (after the host table's objects are destroyed), appended to the report file. ");
            report.Append($"Card back picture: {(back != null ? "the game's board card back" : "MISSING")}.");
            // Development evidence file (this computer only): which hidden-only names matched the bytes, and whether
            // each one is only a part of a public label (a substring, not the hidden card itself).
            StringBuilder detail = new StringBuilder(report.ToString()).AppendLine();
            foreach (string name in wireNames)
            {
                string inside = null;
                foreach (string text in publicLabelTexts) if (text.IndexOf(name, StringComparison.Ordinal) >= 0) { inside = text; break; }
                detail.AppendLine($"wire match '{name.Replace('\n', ' ')}': {(inside != null ? "inside public label '" + inside.Replace('\n', ' ') + "'" : "NOT inside any public label")}");
            }

            try
            {
                string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "Logs"));
                System.IO.Directory.CreateDirectory(folder);
                System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "PrivateView-report.txt"), detail.ToString());
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[PrivateView] The report file could not be written: " + exception.Message);
            }

            StartCoroutine(ScanPrivateSceneLater(hiddenRealIds, hiddenOnlyNames));
            return report.ToString();
        }

        private System.Collections.IEnumerator ScanPrivateSceneLater(List<Guid> hiddenRealIds, HashSet<string> hiddenOnlyNames)
        {
            yield return null;
            yield return null;
            int leakedNames = 0, leakedIds = 0, objects = 0;
            Dictionary<string, int> where = new Dictionary<string, int>();
            void Note(GameObject go, string hiddenName, string kind)
            {
                Transform root = go.transform;
                while (root.parent != null && root.parent.parent != null) root = root.parent;
                string key = kind + " '" + go.name + "' contains '" + hiddenName.Replace('\n', ' ') + "', under '" + root.name + "'";
                where[key] = where.TryGetValue(key, out int n) ? n + 1 : 1;
            }
            foreach (GameObject go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                objects++;
                string name = go.name;
                foreach (Guid id in hiddenRealIds)
                    if (name.IndexOf(id.ToString("N"), StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf(id.ToString(), StringComparison.OrdinalIgnoreCase) >= 0) leakedIds++;
                foreach (string hiddenName in hiddenOnlyNames)
                    if (name.IndexOf(hiddenName, StringComparison.Ordinal) >= 0) { leakedNames++; Note(go, hiddenName, "name"); }
                TextMesh text = go.GetComponent<TextMesh>();
                if (text != null && !string.IsNullOrEmpty(text.text))
                    foreach (string hiddenName in hiddenOnlyNames) if (text.text.IndexOf(hiddenName, StringComparison.Ordinal) >= 0) { leakedNames++; Note(go, hiddenName, "TextMesh"); }
            }

            string line = $"[PrivateView] Scene scan (2 frames later): {objects} objects; names or texts with a hidden real id {leakedIds}; with a hidden-only card name {leakedNames}.";
            foreach (KeyValuePair<string, int> pair in where) line += System.Environment.NewLine + $"  hidden-only name in {pair.Key}: {pair.Value}";
            Debug.Log(line);
            try
            {
                string file = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "Logs", "PrivateView-report.txt"));
                System.IO.File.AppendAllText(file, line + System.Environment.NewLine);
            }
            catch (Exception)
            {
            }
        }

        private static HashSet<string> HiddenOnlyNames(MatchState full, HashSet<TabletopObjectId> hidden, IReadOnlyDictionary<TabletopObjectId, string> labels)
        {
            HashSet<string> visible = new HashSet<string>(), hiddenNames = new HashSet<string>();
            foreach (KeyValuePair<TabletopObjectId, string> pair in labels)
            {
                if (string.IsNullOrEmpty(pair.Value) || !full.Cards.ContainsKey(pair.Key)) continue;
                if (pair.Value.Length < 3) continue; // one- and two-letter names (A, B, X, Y) match any bytes: not evidence
                if (hidden.Contains(pair.Key)) hiddenNames.Add(pair.Value);
                else visible.Add(pair.Value);
            }

            hiddenNames.ExceptWith(visible);
            return hiddenNames;
        }

        private static bool Contains(byte[] haystack, byte[] needle)
        {
            for (int i = 0; i + needle.Length <= haystack.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && haystack[i + j] == needle[j]) j++;
                if (j == needle.Length) return true;
            }

            return false;
        }
#endif
    }
}
