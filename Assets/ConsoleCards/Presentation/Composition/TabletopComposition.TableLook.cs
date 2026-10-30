using System;
using System.Collections.Generic;
using System.IO;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.TableSurface;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>The player's tables saved by name (SAVE AS NEW TABLE, board 12), in the profile.</summary>
    [Serializable]
    internal sealed class SavedTablesFile
    {
        public const string FileName = "saved-tables.json";
        public List<TableSpec> tables = new List<TableSpec>();
    }

    /// <summary>
    /// Table (board 12), the parts added 2026-10-30: TOP colours, Fit the players and SAVE AS NEW TABLE (the saved
    /// tables join the table models under Saved tables ‹ ›).
    /// </summary>
    public sealed partial class TabletopComposition
    {
        private readonly Material[] topMaterials = new Material[TableSpec.TopCount];
        private SavedTablesFile savedTables;

        private string SavedTablesPath => Path.Combine(EnsureProfile().ActiveFolder, SavedTablesFile.FileName);

        // The table's felt with the top's colour on it; made once per colour.
        private Material TopMaterial(int top)
        {
            if (top <= 0 || top >= TableSpec.TopCount || classicPlayingArea == null) return classicPlayingArea;
            if (topMaterials[top] != null) return topMaterials[top];
            Material tinted = new Material(classicPlayingArea) { name = $"Table Top {TableSpec.TopLabel(top)}", hideFlags = HideFlags.DontSave };
            Color tint = TableSpec.TopTint(top);
            if (tinted.HasProperty("_BaseColor")) tinted.SetColor("_BaseColor", tinted.GetColor("_BaseColor") * tint);
            if (tinted.HasProperty("_Color")) tinted.SetColor("_Color", tinted.GetColor("_Color") * tint);
            topMaterials[top] = tinted;
            return tinted;
        }

        private SavedTablesFile SavedTables()
        {
            if (savedTables != null) return savedTables;
            savedTables = SaveFiles.ReadJson<SavedTablesFile>(SavedTablesPath, TableArea) ?? new SavedTablesFile();
            if (savedTables.tables == null) savedTables.tables = new List<TableSpec>();
            savedTables.tables.RemoveAll(t => t == null || string.IsNullOrWhiteSpace(t.savedName));
            return savedTables;
        }

        private void PanelPickTop(int top)
        {
            if (top < 0 || top >= TableSpec.TopCount || tableSpec.top == top) return;
            TableSpec candidate = tableSpec.Copy();
            candidate.top = top;
            ApplyTableModeChange(candidate, $"Top: {TableSpec.TopLabel(top)}.");
        }

        private void PanelToggleFitPlayers()
        {
            TableSpec candidate = tableSpec.Copy();
            candidate.fitPlayers = !candidate.fitPlayers;
            ApplyTableModeChange(candidate, candidate.fitPlayers
                ? "Fit the players: the table grows when a game needs room for more seats."
                : "The table keeps its size for every game.");
        }

        // The table as it is now, saved under a new name; it is then one of the Saved tables.
        private void PanelSaveAsNewTable()
        {
            SavedTablesFile file = SavedTables();
            int number = file.tables.Count + 1;
            string name = $"My table {number}";
            while (file.tables.Exists(t => string.Equals(t.savedName, name, StringComparison.OrdinalIgnoreCase))) name = $"My table {++number}";
            TableSpec saved = tableSpec.Copy();
            saved.savedName = name;
            file.tables.Add(saved);
            if (!SaveFiles.WriteJson(SavedTablesPath, file, TableArea))
            {
                file.tables.Remove(saved);
                RefreshTablePanel("The table could not be saved; the console says why.");
                return;
            }

            tableSpec.savedName = name;
            RefreshTablePanel($"Saved as {name}. Choose it under Saved tables any time.");
        }

        // The choices under Saved tables: the table models, then the tables saved by name.
        private void CollectSavedTableChoices(List<TableSpec> into)
        {
            LoadTableModels();
            for (int i = NextCycleModel(-1); i >= 0; i = NextCycleModel(i)) into.Add(ModelSpec(tableModels[i]));
            List<TableSpec> mine = SavedTables().tables;
            for (int i = 0; i < mine.Count; i++) into.Add(mine[i].Copy());
        }

        private static int IndexOfSavedChoice(List<TableSpec> choices, TableSpec spec)
        {
            for (int i = 0; i < choices.Count; i++)
            {
                TableSpec choice = choices[i];
                bool same = !string.IsNullOrEmpty(spec.savedName)
                    ? string.Equals(choice.savedName, spec.savedName, StringComparison.OrdinalIgnoreCase)
                    : string.IsNullOrEmpty(choice.savedName) && spec.shape == TableShape.Model
                        && string.Equals(choice.modelId, spec.modelId, StringComparison.OrdinalIgnoreCase);
                if (same) return i;
            }

            return -1;
        }
    }
}
