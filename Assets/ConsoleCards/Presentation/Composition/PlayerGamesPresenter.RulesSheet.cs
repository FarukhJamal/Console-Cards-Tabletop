using System;
using System.Collections.Generic;
using ConsoleCards.Definitions;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.UI;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// RULES-SHEET (R2): Make a game's RULES step and the Rules sheet panel it opens. The sheet being made lives here
    /// until Make a game's SAVE saves it with the game; Copy from takes a whole sheet from another of your games.
    /// </summary>
    internal sealed partial class PlayerGamesPresenter
    {
        internal List<SheetValue> saveSheet = new List<SheetValue>();
        internal List<SheetText> saveSheetText = new List<SheetText>();
        private int sheetSection;
        private int sheetCopyIndex;
        private RulesSheetActions rulesSheetActions;

        // Copies, so editing never changes the game it came from until it is saved.
        internal void LoadSaveSheet(PlayerGameFile from)
        {
            saveSheet = new List<SheetValue>();
            saveSheetText = new List<SheetText>();
            sheetSection = RulesSheet.Basics;
            sheetCopyIndex = 0;
            if (from == null) return;
            for (int i = 0; from.sheet != null && i < from.sheet.Count; i++)
            {
                SheetValue value = from.sheet[i];
                if (value != null && !string.IsNullOrWhiteSpace(value.value)) saveSheet.Add(new SheetValue(value.key, value.value));
            }

            for (int i = 0; from.sheetText != null && i < from.sheetText.Count; i++)
            {
                SheetText text = from.sheetText[i];
                if (text != null && !string.IsNullOrWhiteSpace(text.text)) saveSheetText.Add(new SheetText(text.section, text.text));
            }
        }

        internal void FillRulesStep(GameSaveModel model)
        {
            model.StepTitle = "STEP 4 OF 5 · RULES";
            model.ShowPicker = false;
            int filled = FilledFields(out int sections);
            bool any = RulesSheet.HasContent(saveSheet, saveSheetText);
            model.StepText = any
                ? $"Your rules sheet: {CountText(filled, "field")} filled in, rules in {CountText(sections, "section")}.\n\n"
                    + "The rules card and the rulebook show them, section by section. Anything left empty is for the players to decide."
                : "Your game has no rules sheet yet.\n\n"
                    + "Edit rules sheet… opens it: ten sections (Basics, Components, Decks, Setup, Your turn, Limits, Actions, Card effects, "
                    + "Scoring and winning, House rules). Fill in only what matters; anything left empty is for the players to decide.";
            model.ActionAText = "Edit rules sheet…";
            model.ActionBText = any ? "Clear the sheet" : string.Empty;
        }

        private int FilledFields(out int sections)
        {
            int filled = 0;
            for (int i = 0; i < saveSheet.Count; i++)
            {
                if (saveSheet[i] != null && RulesSheet.TryGetField(saveSheet[i].key, out _) && !string.IsNullOrWhiteSpace(saveSheet[i].value)) filled++;
            }

            sections = 0;
            for (int s = 0; s < RulesSheet.SectionHeadings.Length; s++)
            {
                if (SectionHasContent(s)) sections++;
            }

            return filled;
        }

        private bool SectionHasContent(int section)
        {
            for (int i = 0; i < saveSheet.Count; i++)
            {
                if (saveSheet[i] != null && RulesSheet.TryGetField(saveSheet[i].key, out SheetField field) && field.Section == section
                    && !string.IsNullOrWhiteSpace(saveSheet[i].value)) return true;
            }

            return SectionText(section).Length > 0;
        }

        // The sheet as Make a game's preview lines: "# Section", then its rules, as the rules card shows them.
        private List<string> SheetPreviewLines()
        {
            List<string> lines = new List<string>();
            if (!RulesSheet.HasContent(saveSheet, saveSheetText)) return lines;
            RuleSetDefinition set = RulesSheet.Build("sheet:preview", saveName, string.Empty, null, saveSheet, saveSheetText);
            if (set == null) return lines;
            for (int s = 0; s < set.Sections.Count; s++)
            {
                RuleSection section = set.Sections[s];
                lines.Add("# " + section.Heading);
                for (int e = 0; e < section.Entries.Count; e++) lines.Add(section.Entries[e].FormatText());
            }

            UnityEngine.Object.Destroy(set);
            return lines;
        }

        // ---------- the panel ----------

        internal void OpenRulesSheet()
        {
            host.GameSelectPanel.returnToGameSelect = false;
            host.runtimeUi?.CloseGameSave();
            if (!ShowRulesSheet(string.Empty))
            {
                ShowGameSave("The Rules sheet panel is missing: run Console Cards > UI > Create Missing Table HUD (HUD-B).");
            }
        }

        private bool ShowRulesSheet(string status)
        {
            if (host.runtimeUi == null) return false;
            if (rulesSheetActions == null)
            {
                rulesSheetActions = new RulesSheetActions
                {
                    PickSection = PickSheetSection,
                    SetValue = SetSheetValue,
                    StepValue = StepSheetValue,
                    SetSectionText = text => SetSectionText(sheetSection, text),
                    CycleCopySource = CycleSheetCopy,
                    CopyFrom = CopySheet,
                    ClearSection = ClearSheetSection,
                    Done = CloseRulesSheet,
                };
            }

            int section = Mathf.Clamp(sheetSection, 0, RulesSheet.SectionHeadings.Length - 1);
            string[] tabs = new string[RulesSheet.SectionHeadings.Length];
            for (int s = 0; s < tabs.Length; s++) tabs[s] = (s + 1) + "  " + RulesSheet.SectionHeadings[s] + (SectionHasContent(s) ? "  ●" : string.Empty);
            List<RulesSheetRow> rows = new List<RulesSheetRow>();
            for (int f = 0; f < RulesSheet.Fields.Length; f++)
            {
                SheetField field = RulesSheet.Fields[f];
                if (field.Section != section) continue;
                string value = RulesSheet.ValueOf(saveSheet, field.Key);
                rows.Add(new RulesSheetRow(field.Key, field.Label, field.Kind, value,
                    value.Length == 0 ? string.Empty : field.Text.Replace(RuleEntry.ValueToken, value)));
            }

            List<PlayerGameFile> sources = SheetCopySources();
            RulesSheetModel model = new RulesSheetModel
            {
                Title = "RULES SHEET · " + (string.IsNullOrWhiteSpace(saveName) ? "YOUR GAME" : saveName.Trim().ToUpperInvariant()),
                Section = section,
                SectionTabs = tabs,
                SectionHint = SectionHint(section),
                Rows = rows,
                SectionText = SectionText(section),
                CopyText = sources.Count == 0 ? "No other sheets yet" : "Copy from: " + sources[Mod(sheetCopyIndex, sources.Count)].name,
                Status = status ?? string.Empty,
            };
            return host.runtimeUi.ShowRulesSheet(model, rulesSheetActions);
        }

        private static string SectionHint(int section)
        {
            switch (section)
            {
                case RulesSheet.Basics: return "Players and play time come from Make a game's SAVE step. Empty fields: the players decide.";
                case RulesSheet.Components: return "What the pieces on your table are. The table itself is saved from the table.";
                case RulesSheet.Decks: return "Your game's draw pile. Its cards are chosen in the DECK and CARDS steps.";
                case RulesSheet.Setup: return "Dealing is set in the DEAL step; add what else each player starts with.";
                case RulesSheet.Turn: return "Who goes when, and what a turn is made of.";
                case RulesSheet.Limits: return "Numbers the game can check at the stricter assist levels.";
                case RulesSheet.Actions: return "What a player may do on their turn, for example: draw, play, discard, move a pawn, roll, pass.";
                case RulesSheet.CardEffects: return "For example: A = skip the next player; B = reverse the turn order; Start = the next player draws 2.";
                case RulesSheet.Scoring: return "How the score is kept and who wins.";
                default: return "What players may change at the table, and how a change is agreed.";
            }
        }

        private void PickSheetSection(int section, string typedText)
        {
            SetSectionText(sheetSection, typedText, false);
            sheetSection = Mathf.Clamp(section, 0, RulesSheet.SectionHeadings.Length - 1);
            ShowRulesSheet(string.Empty);
        }

        private void SetSheetValue(string key, string value)
        {
            if (!RulesSheet.TryGetField(key, out SheetField field)) return;
            string clean = (value ?? string.Empty).Replace('\n', ' ').Trim();
            if (field.Kind == RuleSettingKind.Number && clean.Length > 0)
            {
                if (!int.TryParse(clean, out int number) || number < 0)
                {
                    ShowRulesSheet($"{field.Label} takes a whole number (0 or more).");
                    return;
                }

                clean = number.ToString();
            }

            if (clean.Length > SheetValueLength) clean = clean.Substring(0, SheetValueLength);
            PutSheetValue(key, clean);
            ShowRulesSheet(string.Empty);
        }

        private const int SheetValueLength = 160;

        // A choice cycles through "players decide" and its choices; a number goes down to empty ("players decide") and up.
        private void StepSheetValue(string key, int step)
        {
            if (!RulesSheet.TryGetField(key, out SheetField field)) return;
            string now = RulesSheet.ValueOf(saveSheet, key);
            if (field.Kind == RuleSettingKind.Choice)
            {
                int count = field.Choices.Count + 1; // 0: players decide
                int at = 0;
                for (int i = 0; i < field.Choices.Count; i++)
                {
                    if (string.Equals(field.Choices[i], now, StringComparison.Ordinal)) at = i + 1;
                }

                at = Mod(at + step, count);
                PutSheetValue(key, at == 0 ? string.Empty : field.Choices[at - 1]);
            }
            else if (field.Kind == RuleSettingKind.Number)
            {
                int number = int.TryParse(now, out int parsed) ? parsed : 0;
                if (now.Length == 0 && step > 0) number = 1;
                else number += step;
                PutSheetValue(key, number < 0 || (now.Length == 0 && step < 0) ? string.Empty : number.ToString());
            }

            ShowRulesSheet(string.Empty);
        }

        private void PutSheetValue(string key, string value)
        {
            for (int i = saveSheet.Count - 1; i >= 0; i--)
            {
                if (saveSheet[i] == null || string.Equals(saveSheet[i].key, key, StringComparison.Ordinal)) saveSheet.RemoveAt(i);
            }

            if (!string.IsNullOrEmpty(value)) saveSheet.Add(new SheetValue(key, value));
        }

        private string SectionText(int section)
        {
            for (int i = 0; i < saveSheetText.Count; i++)
            {
                if (saveSheetText[i] != null && saveSheetText[i].section == section) return saveSheetText[i].text?.Trim() ?? string.Empty;
            }

            return string.Empty;
        }

        private void SetSectionText(int section, string text, bool show = true)
        {
            string clean = (text ?? string.Empty).Trim();
            if (clean.Length > SheetTextLength) clean = clean.Substring(0, SheetTextLength);
            if (string.Equals(clean, SectionText(section), StringComparison.Ordinal)) return;
            for (int i = saveSheetText.Count - 1; i >= 0; i--)
            {
                if (saveSheetText[i] == null || saveSheetText[i].section == section) saveSheetText.RemoveAt(i);
            }

            if (clean.Length > 0) saveSheetText.Add(new SheetText(section, clean));
            if (show) ShowRulesSheet(string.Empty);
        }

        private const int SheetTextLength = 2000;

        private void ClearSheetSection()
        {
            for (int f = 0; f < RulesSheet.Fields.Length; f++)
            {
                if (RulesSheet.Fields[f].Section == sheetSection) PutSheetValue(RulesSheet.Fields[f].Key, string.Empty);
            }

            for (int i = saveSheetText.Count - 1; i >= 0; i--)
            {
                if (saveSheetText[i] == null || saveSheetText[i].section == sheetSection) saveSheetText.RemoveAt(i);
            }

            ShowRulesSheet($"{RulesSheet.SectionHeadings[sheetSection]} cleared: its fields are for the players to decide.");
        }

        // Your other games with a rules sheet (not the one being made).
        private List<PlayerGameFile> SheetCopySources()
        {
            List<PlayerGameFile> sources = new List<PlayerGameFile>();
            List<PlayerGameFile> games = PlayerGames().List();
            for (int i = 0; i < games.Count; i++)
            {
                PlayerGameFile game = games[i];
                if (game == null || (activePlayerGame != null && string.Equals(game.id, activePlayerGame.id, StringComparison.Ordinal))) continue;
                if (RulesSheet.HasContent(game.sheet, game.sheetText)) sources.Add(game);
            }

            return sources;
        }

        private void CycleSheetCopy(int step)
        {
            sheetCopyIndex += step;
            ShowRulesSheet(string.Empty);
        }

        private void CopySheet()
        {
            List<PlayerGameFile> sources = SheetCopySources();
            if (sources.Count == 0)
            {
                ShowRulesSheet("None of your other games has a rules sheet yet.");
                return;
            }

            PlayerGameFile from = sources[Mod(sheetCopyIndex, sources.Count)];
            int section = sheetSection;
            int copyIndex = sheetCopyIndex;
            LoadSaveSheet(from);
            sheetSection = section;
            sheetCopyIndex = copyIndex;
            ShowRulesSheet($"Copied the rules sheet of {from.name}. Change anything; SAVE in Make a game keeps it.");
        }

        private void CloseRulesSheet(string typedText)
        {
            SetSectionText(sheetSection, typedText, false);
            host.runtimeUi?.CloseRulesSheet();
            ShowGameSave(string.Empty);
        }

        internal void ClearSaveSheet()
        {
            saveSheet.Clear();
            saveSheetText.Clear();
        }

        private static int Mod(int value, int count) => count <= 0 ? 0 : ((value % count) + count) % count;
    }
}
