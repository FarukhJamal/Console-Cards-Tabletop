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

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// POL-0: where the table's tuning comes from, so it can be changed without code: the feel numbers
    /// (Resources/FeelSettings), the cursor pictures (Resources/CursorSet) and the shortcut keys (the TabletopObject
    /// map in TabletopObjectControls.inputactions). Each is loaded once.
    /// </summary>
    public sealed partial class TabletopComposition
    {
        private FeelSettings feelSettings;
        private CursorSet cursorSet;
        private bool cursorSetLoaded;
        private readonly TableKeys tableKeys = new TableKeys();
        private bool tableKeysBound;

        /// <summary>The feel numbers (defaults when the asset is missing).</summary>
        internal FeelSettings Feel
        {
            get
            {
                if (feelSettings == null)
                {
                    feelSettings = Resources.Load<FeelSettings>(FeelSettings.ResourcePath);
                    if (feelSettings == null) feelSettings = ScriptableObject.CreateInstance<FeelSettings>();
                }

                return feelSettings;
            }
        }

        /// <summary>The cursor pictures and hot spots (null: the normal arrow everywhere).</summary>
        internal CursorSet Cursors
        {
            get
            {
                if (!cursorSetLoaded)
                {
                    cursorSetLoaded = true;
                    cursorSet = Resources.Load<CursorSet>(CursorSet.ResourcePath);
                }

                return cursorSet;
            }
        }

        /// <summary>The shortcut keys, bound to the object input map once it exists.</summary>
        internal TableKeys Keys
        {
            get
            {
                if (!tableKeysBound
                    && objectInputAdapter != null
                    && objectInputAdapter.PointAction != null
                    && objectInputAdapter.PointAction.action != null)
                {
                    tableKeys.Bind(objectInputAdapter.PointAction.action.actionMap);
                    tableKeysBound = true;
                }

                return tableKeys;
            }
        }
    }
}
