using System;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates.Definitions;
using UnityEngine;

namespace ConsoleCards.Presentation.Views
{
    /// <summary>
    /// Links a Console prefab to its authored layout asset. Kept separate from ConsoleView so views
    /// built without a layout (tests, fixtures) are unaffected.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ConsoleLayoutBinding : MonoBehaviour
    {
        [SerializeField] private ConsoleLayoutDefinition layout;

        public ConsoleLayoutDefinition Layout => layout;

        /// <summary>Converts the authored layout to validated data. Call once at initialisation, not per frame.</summary>
        public ConsoleLayoutData ResolveLayoutData()
        {
            if (layout == null)
            {
                throw new InvalidOperationException($"{name} requires a Console layout asset.");
            }

            return layout.ToData();
        }
    }
}
