using System;
using System.Collections.Generic;
using System.Text;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Random;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Boards;
using ConsoleCards.Core.Domain.Cards;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.PlayAreas;
using ConsoleCards.Core.Domain.PlayerLayouts;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Events;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.Presentation.Art;
using ConsoleCards.Presentation.Camera;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.Catalog;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Feedback;
using ConsoleCards.Presentation.Hints;
using ConsoleCards.Presentation.Input;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.Settings;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.UI.Toolbox;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityCamera = UnityEngine.Camera;
using static ConsoleCards.Presentation.Composition.TabletopComposition;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// The rules card panel: house rules, rule proposals, saved rule sets and the hint switches.
    /// (R1d: moved out of TabletopComposition; the table reaches it as <c>RulesCardPanel</c> and it
    /// reaches the table through <c>host</c>.)
    /// </summary>
    internal sealed class RulesCardPresenter
    {
        private readonly TabletopComposition host;

        internal RulesCardPresenter(TabletopComposition host)
        {
            this.host = host;
        }

        // ---------- from TabletopComposition.RulesCard.cs ----------

        // The rules card for this table (doc 23, R1): the rule set in play, filtered to the active mode. Empty Table
        // has no rules. R3: each rule can be proposed for change, saved sets can be picked, and a restart row shows
        // when settings changed since the game was set up.
        internal RulesCardModel BuildRulesCardModel()
        {
            if (host.activeSession != null && host.activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom && host.PlayerGamesPanel.activePlayerGame != null)
            {
                return host.PlayerGamesPanel.PlayerGameRulesCardModel(); // doc 29: the written rules of a game you made
            }

            if (host.activeSession == null
                || host.activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom
                || host.trapFloorGameDefinition == null)
            {
                return null;
            }

            string modeStableId = host.trapFloorTemplate != null ? host.trapFloorTemplate.ActiveMode.StableId : string.Empty;
            RulesCardModel model = RulesCardModel.FromRuleSet(ActiveRuleSet, modeStableId);
            if (model == null) return null;
            if (rulesCardActions == null)
            {
                rulesCardActions = new RulesCardActions
                {
                    ProposeChange = OpenRuleProposal,
                    CycleRuleSet = OpenRuleSetSwitch,
                    Restart = RestartWithActiveRules,
                };
            }

            string pending = host.templatesRuleSet != null && SettingsDiffer(host.templatesRuleSet, ActiveRuleSet)
                ? "Changed settings start when the game restarts."
                : string.Empty;
            return model.WithActions(rulesCardActions, 1 + host.houseRuleSets.Count, pending);
        }

        // ---------- house rules (doc 23, R3) ----------

        internal RuleSetDefinition ActiveRuleSet =>
            activeRuleSet != null ? activeRuleSet : host.trapFloorGameDefinition != null ? host.trapFloorGameDefinition.DefaultRuleSet : null;

        // Index in the pick list: 0 is the default set, then the saved sets oldest first.
        internal int ActiveRuleSetIndex => activeRuleSet == null ? 0 : host.houseRuleSets.IndexOf(activeRuleSet) + 1;

        internal RuleSetDefinition RuleSetAt(int index) =>
            index <= 0 || index > host.houseRuleSets.Count ? host.trapFloorGameDefinition.DefaultRuleSet : host.houseRuleSets[index - 1];

        // Reads the saved house-rule sets once, at start. Sets made from other rules are skipped.
        internal void EnsureHouseRulesLoaded()
        {
            if (houseRulesLoaded || host.trapFloorGameDefinition == null || host.trapFloorGameDefinition.DefaultRuleSet == null) return;
            houseRulesLoaded = true;
            RuleSetDefinition defaults = host.trapFloorGameDefinition.DefaultRuleSet;
            try
            {
                houseRulesStore = new HouseRulesStore(ContentPackStore.ForProfile(host.EnsureProfile()));
                List<HouseRuleSetRecord> records = houseRulesStore.Load(host.trapFloorGameDefinition.StableId);
                for (int i = 0; i < records.Count; i++)
                {
                    HouseRuleSetRecord record = records[i];
                    if (!string.IsNullOrEmpty(record.basedOnStableId)
                        && !string.Equals(record.basedOnStableId, defaults.StableId, StringComparison.Ordinal))
                    {
                        Debug.LogWarning($"[Rules] Skipped house rules '{record.displayName}': made from other rules ({record.basedOnStableId}).");
                        continue;
                    }

                    host.houseRuleSets.Add(RuleSetDefinition.CreateHouseRules(defaults, record.stableId,
                        string.IsNullOrWhiteSpace(record.displayName) ? "House rules" : record.displayName, record.ToChanges()));
                    houseRuleRecords.Add(record);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Rules] Saved house rules could not be read: {exception.Message}");
            }
        }

        // True when a setting the game reads has a different value in the two sets.
        internal static bool SettingsDiffer(RuleSetDefinition a, RuleSetDefinition b)
        {
            if (a == b || a == null || b == null) return false;
            for (int s = 0; s < b.Sections.Count; s++)
            {
                RuleSection section = b.Sections[s];
                if (section == null) continue;
                for (int e = 0; e < section.Entries.Count; e++)
                {
                    RuleEntry entry = section.Entries[e];
                    if (entry == null || !entry.HasSetting) continue;
                    // Settings read live (the Shop) apply at once and need no restart.
                    if (string.Equals(entry.SettingKey, RuleSettingKeys.ShopEnabled, StringComparison.Ordinal)) continue;
                    if (!a.TryGetEntry(entry.StableId, out RuleEntry other)
                        || !string.Equals(other.Value, entry.Value, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        internal void RefreshRulesCard()
        {
            host.runtimeUi?.ShowRulesCard(BuildRulesCardModel(), host.rulesCardEnabled);
        }

        internal void OpenRuleProposal(string entryStableId)
        {
            RuleSetDefinition rules = ActiveRuleSet;
            if (rules == null || !rules.TryGetEntry(entryStableId, out RuleEntry entry)) return;
            ruleProposalEntryId = entryStableId;
            ruleProposalSetIndex = -1;
            ruleProposalValue = entry.Value;
            ruleProposalText = entry.Text;
            BeginRuleProposal();
        }

        internal void OpenRuleSetSwitch(int direction)
        {
            int count = 1 + host.houseRuleSets.Count;
            if (count < 2 || host.trapFloorGameDefinition == null) return;
            ruleProposalEntryId = null;
            ruleProposalSetIndex = (((ActiveRuleSetIndex + direction) % count) + count) % count;
            BeginRuleProposal();
        }

        internal void BeginRuleProposal()
        {
            if (host.runtimeUi == null || host.activeSession == null) return;
            ruleProposalAgreeing = false;
            ruleProposalVoters.Clear();
            ruleProposalAgreed.Clear();
            IReadOnlyList<PlayerId> players = host.activeSession.Request.ActivePlayerIds;
            for (int i = 0; i < players.Count; i++)
            {
                ruleProposalVoters.Add(players[i]);
                ruleProposalAgreed.Add(false);
            }

            if (ruleProposalActions == null)
            {
                ruleProposalActions = new RuleProposalActions
                {
                    Step = StepRuleProposal,
                    TextChanged = TypeRuleProposal,
                    Propose = ProposeRuleChange,
                    Agree = AgreeRuleProposal,
                    Reject = RejectRuleProposal,
                };
            }

            host.ruleProposalOpen = ShowRuleProposalUi();
        }

        // How the proposed change is edited: numbers, on/off and listed choices step; other settings and
        // text-only rules are typed.
        internal RuleProposalEditKind RuleProposalEditKindFor(RuleEntry entry)
        {
            if (entry == null) return RuleProposalEditKind.None;
            switch (entry.SettingKind)
            {
                case RuleSettingKind.Number:
                case RuleSettingKind.Toggle:
                    return RuleProposalEditKind.Step;
                case RuleSettingKind.Choice:
                    return entry.Choices.Count > 0 ? RuleProposalEditKind.Step : RuleProposalEditKind.Text;
                default:
                    return RuleProposalEditKind.Text;
            }
        }

        // Typed text edits the value of a setting, or the words of a text-only rule.
        internal static bool EditsRuleText(RuleEntry entry) => entry != null && !entry.HasSetting;

        internal bool ShowRuleProposalUi()
        {
            RuleProposalModel model = new RuleProposalModel { Agreeing = ruleProposalAgreeing };
            if (ruleProposalEntryId == null)
            {
                model.Title = "SWITCH RULES";
                model.CurrentText = $"In play: {ActiveRuleSet.DisplayName}";
                model.PreviewText = $"Switch to: {RuleSetAt(ruleProposalSetIndex).DisplayName}";
                model.EditKind = RuleProposalEditKind.None;
            }
            else
            {
                if (!ActiveRuleSet.TryGetEntry(ruleProposalEntryId, out RuleEntry entry)) return false;
                RuleProposalEditKind kind = RuleProposalEditKindFor(entry);
                model.CurrentText = entry.FormatText();
                model.EditKind = kind;
                model.ValueText = ruleProposalValue;
                model.EditText = EditsRuleText(entry) ? ruleProposalText : ruleProposalValue;
                model.PreviewText = entry.WithChange(ruleProposalText, ruleProposalValue).FormatText();
            }

            int agreed = 0;
            RuleProposalVoter[] voters = new RuleProposalVoter[ruleProposalVoters.Count];
            for (int i = 0; i < voters.Length; i++)
            {
                voters[i] = new RuleProposalVoter(host.PlayerLabel(ruleProposalVoters[i]), ruleProposalAgreed[i]);
                if (ruleProposalAgreed[i]) agreed++;
            }

            model.Voters = voters;
            model.Status = ruleProposalAgreeing
                ? $"{agreed} of {voters.Length} agreed. Everyone must agree; one \"No\" keeps the rules."
                : "Make the change, then propose it to the table.";
            return host.runtimeUi.ShowRuleProposal(model, ruleProposalActions);
        }

        internal void StepRuleProposal(int direction)
        {
            if (ruleProposalAgreeing || ruleProposalEntryId == null
                || !ActiveRuleSet.TryGetEntry(ruleProposalEntryId, out RuleEntry entry)) return;
            switch (entry.SettingKind)
            {
                case RuleSettingKind.Number:
                    int number = int.TryParse(ruleProposalValue, out int parsed) ? parsed : 0;
                    ruleProposalValue = Mathf.Max(0, number + direction).ToString();
                    break;
                case RuleSettingKind.Toggle:
                    ruleProposalValue = FlipToggle(ruleProposalValue);
                    break;
                case RuleSettingKind.Choice:
                    IReadOnlyList<string> choices = entry.Choices;
                    if (choices.Count == 0) return;
                    int at = -1;
                    for (int i = 0; i < choices.Count; i++)
                    {
                        if (string.Equals(choices[i], ruleProposalValue, StringComparison.OrdinalIgnoreCase)) at = i;
                    }

                    ruleProposalValue = choices[(((at + direction) % choices.Count) + choices.Count) % choices.Count];
                    break;
                default:
                    return;
            }

            ShowRuleProposalUi();
        }

        // On/off settings keep the words they were written with (true/false, on/off, yes/no).
        internal static string FlipToggle(string value)
        {
            string v = (value ?? string.Empty).Trim();
            if (v.Equals("on", StringComparison.OrdinalIgnoreCase)) return "off";
            if (v.Equals("off", StringComparison.OrdinalIgnoreCase)) return "on";
            if (v.Equals("yes", StringComparison.OrdinalIgnoreCase)) return "no";
            if (v.Equals("no", StringComparison.OrdinalIgnoreCase)) return "yes";
            return v.Equals("true", StringComparison.OrdinalIgnoreCase) ? "false" : "true";
        }

        internal void TypeRuleProposal(string typed)
        {
            if (ruleProposalAgreeing || ruleProposalEntryId == null
                || !ActiveRuleSet.TryGetEntry(ruleProposalEntryId, out RuleEntry entry)) return;
            if (EditsRuleText(entry)) ruleProposalText = typed ?? string.Empty;
            else ruleProposalValue = typed ?? string.Empty; // trimmed when proposed, so spaces can be typed
            ShowRuleProposalUi();
        }

        internal void ProposeRuleChange()
        {
            if (ruleProposalAgreeing) return;
            if (ruleProposalEntryId != null)
            {
                if (!ActiveRuleSet.TryGetEntry(ruleProposalEntryId, out RuleEntry entry)) return;
                ruleProposalValue = (ruleProposalValue ?? string.Empty).Trim();
                if (string.Equals(ruleProposalValue, entry.Value, StringComparison.Ordinal)
                    && string.Equals(ruleProposalText, entry.Text, StringComparison.Ordinal))
                {
                    CloseRuleProposal("Nothing changed, so nothing was proposed.");
                    return;
                }

                if (EditsRuleText(entry) && string.IsNullOrWhiteSpace(ruleProposalText))
                {
                    host.FeedbackLine("A rule cannot be empty.", FeedbackSeverity.Warning);
                    return;
                }

                if (entry.HasSetting && !string.Equals(ruleProposalValue, entry.Value, StringComparison.Ordinal)
                    && !RuleChangeCanSetUp(entry, out string problem))
                {
                    host.FeedbackLine($"This change would stop the game from setting up: {problem}", FeedbackSeverity.Warning);
                    return;
                }
            }

            ruleProposalAgreeing = true;
            ShowRuleProposalUi();
        }

        // A changed setting is tried before it is proposed (for example floor counts must still fill the grid):
        // the game is set up once, off the table, with the proposed value.
        internal bool RuleChangeCanSetUp(RuleEntry entry, out string problem)
        {
            problem = string.Empty;
            Dictionary<string, RuleEntryChange> change = new Dictionary<string, RuleEntryChange>(StringComparer.Ordinal)
            {
                [entry.StableId] = new RuleEntryChange(null, ruleProposalValue),
            };
            RuleSetDefinition trial = RuleSetDefinition.CreateHouseRules(ActiveRuleSet, "rules-trial", "Trial", change);
            try
            {
                var gameDefinition = host.trapFloorContent != null
                    ? host.trapFloorContent.ToData(trial)
                    : host.trapFloorGameDefinition.ToData(trial);
                for (int i = 0; i < gameDefinition.Modes.Count; i++)
                {
                    TrapFloorTemplateFactory.CreateStandardFourPlayer(
                        new SystemRandomValueSource(), gameDefinition, gameDefinition.Modes[i].StableId, host.GetConsoleLayout());
                }

                return true;
            }
            catch (Exception exception)
            {
                problem = exception.Message;
                return false;
            }
            finally
            {
                UnityEngine.Object.Destroy(trial);
            }
        }

        internal void AgreeRuleProposal(int voter)
        {
            if (!ruleProposalAgreeing || voter < 0 || voter >= ruleProposalAgreed.Count) return;
            ruleProposalAgreed[voter] = true;
            for (int i = 0; i < ruleProposalAgreed.Count; i++)
            {
                if (!ruleProposalAgreed[i])
                {
                    ShowRuleProposalUi();
                    return;
                }
            }

            CommitRuleProposal();
        }

        internal void RejectRuleProposal()
        {
            CloseRuleProposal(ruleProposalAgreeing ? "Not everyone agreed. The rules stay as they are." : null);
        }

        internal void CloseRuleProposal(string message)
        {
            host.ruleProposalOpen = false;
            ruleProposalAgreeing = false;
            host.runtimeUi?.CloseRuleProposal();
            if (!string.IsNullOrEmpty(message) && host.runtimeUi != null) host.FeedbackLine(message, FeedbackSeverity.Info);
        }

        // Everyone agreed: switch sets, or save a new house-rule set with this change (based on the default rules,
        // carrying the changes already in play) and put it in play. Text applies at once; settings at restart.
        internal void CommitRuleProposal()
        {
            RuleSetDefinition defaults = host.trapFloorGameDefinition.DefaultRuleSet;
            if (ruleProposalEntryId == null)
            {
                activeRuleSet = ruleProposalSetIndex <= 0 ? null : host.houseRuleSets[ruleProposalSetIndex - 1];
                FinishRuleCommit($"Everyone agreed: {ActiveRuleSet.DisplayName} is in play.");
                return;
            }

            int activeIndex = ActiveRuleSetIndex;
            Dictionary<string, RuleEntryChange> changes = activeIndex > 0
                ? houseRuleRecords[activeIndex - 1].ToChanges()
                : new Dictionary<string, RuleEntryChange>(StringComparer.Ordinal);
            if (defaults.TryGetEntry(ruleProposalEntryId, out RuleEntry original))
            {
                string text = string.Equals(ruleProposalText, original.Text, StringComparison.Ordinal) ? null : ruleProposalText;
                string value = !original.HasSetting || string.Equals(ruleProposalValue, original.Value, StringComparison.Ordinal)
                    ? null
                    : ruleProposalValue;
                if (text == null && value == null) changes.Remove(ruleProposalEntryId);
                else changes[ruleProposalEntryId] = new RuleEntryChange(text, value);
            }

            if (changes.Count == 0)
            {
                activeRuleSet = null;
                FinishRuleCommit($"Everyone agreed: back to {defaults.DisplayName}.");
                return;
            }

            string name = $"House rules {host.houseRuleSets.Count + 1}";
            HouseRuleSetRecord record = HouseRuleSetRecord.FromChanges($"house-{Guid.NewGuid():N}", name, defaults, changes);
            string saveNote = string.Empty;
            string error = "no store";
            if (houseRulesStore == null || !houseRulesStore.TrySave(host.trapFloorGameDefinition.StableId, record, out error))
            {
                saveNote = " It could not be saved on this computer, so it lasts until you close the game.";
                Debug.LogWarning($"[Rules] House rules '{name}' were not saved: {error}");
            }

            RuleSetDefinition set = RuleSetDefinition.CreateHouseRules(defaults, record.stableId, name, changes);
            host.houseRuleSets.Add(set);
            houseRuleRecords.Add(record);
            activeRuleSet = set;
            FinishRuleCommit($"Everyone agreed: {name} is in play.{saveNote}");
        }

        internal void FinishRuleCommit(string message)
        {
            RefreshGameHints(); // tips follow the rules in play
            host.ActiveCartridge.RefreshShopEntry(); // the Shop opens or closes at once (owner, 2026-10-15)
            bool restartNeeded = host.templatesRuleSet != null && SettingsDiffer(host.templatesRuleSet, ActiveRuleSet);
            CloseRuleProposal(restartNeeded ? $"{message} Changed settings start when you restart." : message);
            RefreshRulesCard();
        }

        // "Restart" on the rules card: load this game again with the rules in play (asks before replacing the table).
        internal void RestartWithActiveRules()
        {
            if (host.activeSession == null || host.activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom) return;
            host.SessionService.RequestTableReplacement(host.activeSession.Selection);
        }

        internal void SetRulesCardEnabled(bool enabled)
        {
            host.rulesCardEnabled = enabled;
            host.runtimeUi?.ShowRulesCard(BuildRulesCardModel(), enabled);
        }

        // Hints switch: guidance text only (controls strip, Toolbox placing bar, status card help line).
        internal void SetHintsEnabled(bool enabled)
        {
            host.hintsEnabled = enabled;
            host.runtimeUi?.SetHintsVisible(enabled);
            ApplyHintLevel();
            host.ActiveCartridge.RefreshStatusUi();
        }

        internal string HintText(string text) => host.hintsEnabled ? text : string.Empty;

        // ---------- hints (doc 23 §10) ----------

        internal void SetTipsEnabled(bool enabled)
        {
            host.tipsEnabled = enabled;
            ApplyHintLevel();
        }

        internal void EnsureHints()
        {
            if (host.hintService != null) return;
            host.hintService = new HintService(HintSeenStore.ForProfile(host.EnsureProfile()));
            HintLibraryDefinition platform = Resources.Load<HintLibraryDefinition>(HintLibraryDefinition.PlatformResourcePath);
            if (platform != null) host.hintService.AddLibrary(platform);
            else if (!hintLibraryWarned)
            {
                hintLibraryWarned = true;
                Debug.LogWarning("Platform hints are missing: run Console Cards > Hints > Create Missing Platform Hints.");
            }

            // H3: each runs only while a rule in play names it. CART-1a: every code cartridge's helpers, added once.
            for (int i = 0; i < host.Cartridges.CodeCartridges.Count; i++) host.Cartridges.CodeCartridges[i].AddHintHelpers();
            ApplyHintLevel();
        }

        // Hints off: nothing. Hints on, Tips off: reminders only. Both on: tips too.
        internal void ApplyHintLevel()
        {
            if (host.hintService == null) return;
            host.hintService.Level = !host.hintsEnabled ? HintLevel.Off : host.tipsEnabled ? HintLevel.Learning : HintLevel.Reminders;
            if (host.shownHint != null
                && (host.hintService.Level == HintLevel.Off
                    || (host.shownHint.Kind == HintKind.Tip && host.hintService.Level != HintLevel.Learning)))
            {
                HideShownHint();
            }
        }

        // An event happened at the table: show the best hint for it, unless a more important one is showing.
        internal void RaiseHint(string trigger, string context = "")
        {
            if (host.runtimeUi == null || !host.IsInitialized || host.rebuildingFromUndo) return;
            EnsureHints();
            if (!host.hintService.TryFind(trigger, context, out HintEntry hint) || hint == host.shownHint) return;
            // A newer answer from the same helper (a second "why not") replaces the one showing.
            if (host.shownHint != null
                && host.runtimeUi.IsHintShown
                && host.shownHint.Priority >= hint.Priority
                && !string.Equals(host.shownHint.StableId, hint.StableId, StringComparison.Ordinal))
            {
                return;
            }

            if (!host.runtimeUi.ShowHint(hint.Kind == HintKind.Tip ? "TIP" : "RULE", hint.Text, DismissShownHint, HideShownHint))
                return;
            host.shownHint = hint;
            host.hintService.MarkShown(hint);
        }

        // The game's tips come from its rules in play (H2); Empty Table has none.
        internal void RefreshGameHints()
        {
            EnsureHints();
            bool isGame = host.activeSession != null
                && host.activeSession.Selection.Kind != TabletopSessionKind.EmptyCustom
                && host.trapFloorGameDefinition != null;
            string modeStableId = host.trapFloorTemplate != null ? host.trapFloorTemplate.ActiveMode.StableId : string.Empty;
            host.hintService.SetGameHints(isGame
                ? RuleHints.Collect(ActiveRuleSet, modeStableId, host.trapFloorGameDefinition.StableId)
                : null);
            // H3: the game's hint helpers run while a rule in play names them.
            host.hintService.SetHelpersInPlay(isGame ? RuleHints.CollectHelpers(ActiveRuleSet, modeStableId) : null);
        }

        // "Got it": never again on this computer.
        internal void DismissShownHint()
        {
            host.hintService?.MarkDismissed(host.shownHint);
            HideShownHint();
        }

        internal void HideShownHint()
        {
            host.shownHint = null;
            host.runtimeUi?.HideHint();
        }

        // Every accepted table step is a hint event ("action." + its kind); the names are made once.
        internal static string ActionHintTrigger(AuthoritativeActionKind kind)
        {
            if (actionHintTriggers == null)
            {
                Array values = Enum.GetValues(typeof(AuthoritativeActionKind));
                int max = 0;
                foreach (object value in values) max = Mathf.Max(max, Convert.ToInt32(value));
                actionHintTriggers = new string[max + 1];
                foreach (object value in values)
                    actionHintTriggers[Convert.ToInt32(value)] = HintTriggers.ActionPrefix + value;
            }

            int index = (int)kind;
            return index >= 0 && index < actionHintTriggers.Length ? actionHintTriggers[index] : string.Empty;
        }

        // Drag events, checked each frame without allocating; each raises its hint when a drag starts.
        internal void UpdateHintTriggers()
        {
            if (!host.hintsEnabled || host.runtimeUi == null) return;
            bool cardDrag = host.IsCardDragActive();
            if (cardDrag && !hintCardDragSeen) RaiseHint(HintTriggers.CardDragged);
            hintCardDragSeen = cardDrag;
            bool pawnDrag = host.TrapFloorGame.IsAnyPawnHeld(out _);
            if (pawnDrag && !hintPawnDragSeen) RaiseHint(HintTriggers.PawnDragged, host.TrapFloorGame.IsActivePawnHeld() ? "own" : "other");
            hintPawnDragSeen = pawnDrag;
        }
        // ---------- from TabletopComposition.cs ----------

        internal bool hintLibraryWarned;
        internal bool hintCardDragSeen;
        internal bool hintPawnDragSeen;
        internal static string[] actionHintTriggers;
        // House rules (doc 23, R3): the rule set in play (null = the game's default), the sets saved on this
        // computer, the set the current templates were built with, and the open proposal. Kept across Undo and Reset.
        internal RuleSetDefinition activeRuleSet;
        internal readonly List<HouseRuleSetRecord> houseRuleRecords = new List<HouseRuleSetRecord>();
        internal HouseRulesStore houseRulesStore;
        internal bool houseRulesLoaded;
        internal RulesCardActions rulesCardActions;
        internal RuleProposalActions ruleProposalActions;
        internal bool ruleProposalAgreeing;
        internal string ruleProposalEntryId;
        internal int ruleProposalSetIndex = -1;
        internal string ruleProposalValue;
        internal string ruleProposalText;
        internal readonly List<PlayerId> ruleProposalVoters = new List<PlayerId>();
        internal readonly List<bool> ruleProposalAgreed = new List<bool>();
    }
}
