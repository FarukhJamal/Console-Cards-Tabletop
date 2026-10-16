using System.Collections.Generic;
using UnityEngine;

namespace ConsoleCards.Presentation.TableSurface
{
    /// <summary>
    /// The table models the game can use (doc 30, B8; T1b), loaded from Resources/TableModels. Split Table Model
    /// adds each model it makes; models can also be dragged in by hand. Packs will add their own later (doc 24).
    /// </summary>
    [CreateAssetMenu(menuName = "Console Cards/Table Model Library", fileName = "TableModels")]
    public sealed class TableModelLibrary : ScriptableObject
    {
        public const string ResourcePath = "TableModels";

        [SerializeField] private List<TableModel> models = new List<TableModel>();

        public IReadOnlyList<TableModel> Models => models;

        /// <summary>The usable models in order (empty entries and models without an id are left out).</summary>
        public void CollectUsable(List<TableModel> into)
        {
            into.Clear();
            for (int i = 0; i < models.Count; i++)
            {
                if (models[i] != null && !string.IsNullOrWhiteSpace(models[i].ModelId)) into.Add(models[i]);
            }
        }

        public TableModel Find(string modelId)
        {
            if (string.IsNullOrEmpty(modelId)) return null;
            for (int i = 0; i < models.Count; i++)
            {
                if (models[i] != null && string.Equals(models[i].ModelId, modelId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return models[i];
                }
            }

            return null;
        }

        /// <summary>Adds the model, or replaces the one with the same id. True when the list changed.</summary>
        public bool AddOrReplace(TableModel model)
        {
            if (model == null) return false;
            for (int i = 0; i < models.Count; i++)
            {
                if (models[i] == model) return false;
                if (models[i] != null && string.Equals(models[i].ModelId, model.ModelId, System.StringComparison.OrdinalIgnoreCase))
                {
                    models[i] = model;
                    return true;
                }
            }

            models.Add(model);
            return true;
        }
    }
}
