using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    public sealed class TrapFloorHudView : ReusableUiView
    {
        [SerializeField] private Text roundLabel;
        [SerializeField] private Text phaseLabel;
        [Tooltip("Turn strip chip tinted with the active player's colour. Optional until the HUD prefab is rebuilt.")]
        [SerializeField] private Image playerChip;
        [SerializeField] private Text searchProgressLabel;
        [SerializeField] private Text detailLabel;
        [SerializeField] private Text containerCountsLabel;
        [SerializeField] private GameObject floorfallPanel;
        [SerializeField] private Text floorfallDiceLabel;
        [SerializeField] private Text floorfallCoordinateLabel;
        [SerializeField] private Text floorfallTargetLabel;
        [SerializeField] private Transform actionsRoot;
        [SerializeField] private GameObject actionsTitle;
        [SerializeField] private Text actionHelpLabel;

        [Header("V5 (Figma): players on the round bar, the player card rows, the HUD's own buttons")]
        [Tooltip("Optional: one slot per player (dot and name) on the round bar. Set by Apply Kit Look.")]
        [SerializeField] private PlayerSlot[] playerSlots = new PlayerSlot[0];
        [SerializeField] private Sprite activeDot;
        [SerializeField] private Sprite idleDot;
        private const float ActiveDotScale = 1.6f;
        [Tooltip("Optional: the player card rows; while shown the old detail lines hide.")]
        [SerializeField] private GameObject cardRows;
        [SerializeField] private Text cardTitle;
        [SerializeField] private Text avatarValue;
        [SerializeField] private Text keysValue;
        [SerializeField] private Image statusChip;
        [SerializeField] private Text statusValue;
        [SerializeField] private Text inHandValue;
        [SerializeField] private Text floorValue;
        [Tooltip("Optional: the HUD's own action button (cloned per action) instead of the shared popup row.")]
        [SerializeField] private PopupActionRowView actionTemplate;
        [SerializeField] private string primaryActionLabel = "End Turn";
        [SerializeField] private Color primaryDot = new Color(0.024f, 0.937f, 0.941f, 1f);
        [SerializeField] private Color otherDot = new Color(0.42f, 0.447f, 0.459f, 1f);
        [SerializeField] private Color statusText = new Color(0.086f, 0.098f, 0.102f, 1f);
        [SerializeField] private Color noStatusText = new Color(0.608f, 0.643f, 0.655f, 1f);

        [Serializable]
        public sealed class PlayerSlot
        {
            public GameObject root;
            public Image dot;
            public Text name;
        }

        private readonly List<PopupActionRowView> ownButtons = new List<PopupActionRowView>();
        private readonly List<Image> ownButtonDots = new List<Image>();
        private readonly List<PopupActionOption> orderedActions = new List<PopupActionOption>();
        private string defaultCardTitle;

        private readonly List<PopupActionRowView> actionRows =
            new List<PopupActionRowView>();
        private IRuntimeUiService uiManager;

        public void Initialize(IRuntimeUiService manager)
        {
            uiManager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        public void ValidateReferences()
        {
            if (roundLabel == null
                || phaseLabel == null
                || searchProgressLabel == null
                || detailLabel == null
                || containerCountsLabel == null
                || floorfallPanel == null
                || floorfallDiceLabel == null
                || floorfallCoordinateLabel == null
                || floorfallTargetLabel == null
                || actionsRoot == null
                || actionsTitle == null
                || actionHelpLabel == null
                || uiManager == null)
            {
                throw new InvalidOperationException(
                    "TrapFloorHudView requires its authored status, Floorfall, action references, and runtime UI manager.");
            }
        }

        public void Show(
            TrapFloorStatusModel status,
            FloorfallStatusModel floorfall,
            IReadOnlyList<PopupActionOption> actions)
        {
            RequireAcquired();
            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            ValidateReferences();
            roundLabel.text = status.Round;
            phaseLabel.text = status.Phase;
            if (playerChip != null)
            {
                // V5: the players' dots on the round bar replace the single chip.
                playerChip.gameObject.SetActive(status.PlayerColor.HasValue && (playerSlots == null || playerSlots.Length == 0));
                if (status.PlayerColor.HasValue) playerChip.color = status.PlayerColor.Value;
            }

            searchProgressLabel.text = status.SearchProgress;
            detailLabel.text = status.Detail;
            containerCountsLabel.text = status.ContainerCounts;
            roundLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(status.Round));
            phaseLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(status.Phase));
            searchProgressLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(status.SearchProgress));
            detailLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(status.Detail));
            containerCountsLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(status.ContainerCounts));
            actionHelpLabel.text = status.ActionHelp;
            actionHelpLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(status.ActionHelp));
            ShowPlayers(status.Players);
            ShowCard(status.Card);

            floorfallPanel.SetActive(floorfall.Visible);
            floorfallDiceLabel.text = floorfall.Dice;
            floorfallCoordinateLabel.text = floorfall.Coordinate;
            floorfallTargetLabel.text = floorfall.Target;
            actionsTitle.SetActive(actions.Count > 0 && (cardRows == null || !cardRows.activeSelf)); // V5: no title
            actionsRoot.gameObject.SetActive(actions.Count > 0);
            BindActions(actions);
            base.Show();
        }

        public void ShowObjective(
            string keyProgress,
            string collapseStatus,
            bool isWon,
            IReadOnlyList<PopupActionOption> actions)
        {
            RequireAcquired();
            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            ValidateReferences();
            roundLabel.gameObject.SetActive(true);
            roundLabel.text = keyProgress ?? string.Empty;
            phaseLabel.gameObject.SetActive(isWon);
            phaseLabel.text = isWon ? "VICTORY" : string.Empty;
            if (playerChip != null) playerChip.gameObject.SetActive(false);
            searchProgressLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(collapseStatus));
            searchProgressLabel.text = collapseStatus ?? string.Empty;
            detailLabel.gameObject.SetActive(false);
            containerCountsLabel.gameObject.SetActive(false);
            floorfallPanel.SetActive(false);
            actionsTitle.SetActive(actions.Count > 0);
            actionsRoot.gameObject.SetActive(actions.Count > 0);
            actionHelpLabel.gameObject.SetActive(false);
            ShowPlayers(null);
            ShowCard(null);
            BindActions(actions);
            base.Show();
        }

        public override void Unbind()
        {
            UnbindActions();
            if (roundLabel != null) roundLabel.text = string.Empty;
            if (phaseLabel != null) phaseLabel.text = string.Empty;
            if (searchProgressLabel != null) searchProgressLabel.text = string.Empty;
            if (detailLabel != null) detailLabel.text = string.Empty;
            if (containerCountsLabel != null) containerCountsLabel.text = string.Empty;
            if (actionHelpLabel != null) actionHelpLabel.text = string.Empty;
            if (floorfallDiceLabel != null) floorfallDiceLabel.text = string.Empty;
            if (floorfallCoordinateLabel != null) floorfallCoordinateLabel.text = string.Empty;
            if (floorfallTargetLabel != null) floorfallTargetLabel.text = string.Empty;
        }

        // V5: the players on the round bar, the turn player's dot ringed.
        private void ShowPlayers(IReadOnlyList<TrapFloorHudPlayer> players)
        {
            if (playerSlots == null) return;
            for (int i = 0; i < playerSlots.Length; i++)
            {
                PlayerSlot slot = playerSlots[i];
                if (slot == null || slot.root == null) continue;
                bool shown = players != null && i < players.Count;
                slot.root.SetActive(shown);
                if (!shown) continue;
                TrapFloorHudPlayer player = players[i];
                if (slot.name != null) slot.name.text = player.Name;
                if (slot.dot != null)
                {
                    slot.dot.color = player.Color;
                    Sprite sprite = player.Active ? activeDot : idleDot;
                    if (sprite != null) slot.dot.sprite = sprite;
                    // Owner, 2026-10-26 (V5 reference): the turn player's ringed dot is larger than the others, and the
                    // row makes room for it so it never runs into the name.
                    SizeDot(slot.dot, player.Active);
                }
            }
        }

        // The dot's own size (its LayoutElement) grows for the turn player; the first size seen is the normal one.
        private void SizeDot(Image dot, bool active)
        {
            LayoutElement element = dot.GetComponent<LayoutElement>();
            if (element == null) return;
            if (!dotSizes.TryGetValue(element, out float normal))
            {
                normal = element.preferredWidth;
                dotSizes[element] = normal;
            }

            float size = active ? normal * ActiveDotScale : normal;
            element.minWidth = size;
            element.preferredWidth = size;
            element.minHeight = size;
            element.preferredHeight = size;
        }

        private readonly Dictionary<LayoutElement, float> dotSizes = new Dictionary<LayoutElement, float>();

        // V5: the turn player's card (Avatar, Keys, Status, In hand, Floor); without one the old lines show.
        private void ShowCard(TrapFloorPlayerCardModel card)
        {
            if (cardRows == null) return;
            bool shown = card != null;
            cardRows.SetActive(shown);
            if (cardTitle != null)
            {
                if (defaultCardTitle == null) defaultCardTitle = cardTitle.text;
                cardTitle.text = shown ? card.Title : defaultCardTitle;
            }

            if (!shown) return;
            searchProgressLabel.gameObject.SetActive(false);
            detailLabel.gameObject.SetActive(false);
            containerCountsLabel.gameObject.SetActive(false);
            actionHelpLabel.gameObject.SetActive(false);
            if (avatarValue != null) avatarValue.text = card.Avatar;
            if (keysValue != null) keysValue.text = card.Keys;
            bool hasStatus = !string.IsNullOrEmpty(card.Status);
            if (statusChip != null) statusChip.enabled = hasStatus;
            if (statusValue != null)
            {
                statusValue.text = hasStatus ? card.Status : "None";
                statusValue.color = hasStatus ? statusText : noStatusText;
            }

            if (inHandValue != null) inHandValue.text = card.InHand;
            if (floorValue != null) floorValue.text = card.Floor;
        }

        private void BindActions(IReadOnlyList<PopupActionOption> actions)
        {
            if (actionTemplate != null)
            {
                BindOwnButtons(actions);
                return;
            }

            while (actionRows.Count < actions.Count)
            {
                PopupActionRowView row =
                    uiManager.AcquirePooled<PopupActionRowView>(
                        UiPrefabIds.PopupActionRow,
                        actionsRoot);
                row.ValidateReferences();
                actionRows.Add(row);
            }

            for (int i = 0; i < actionRows.Count; i++)
            {
                PopupActionRowView row = actionRows[i];
                if (i < actions.Count)
                {
                    row.Bind(actions[i]);
                    row.Show();
                }
                else
                {
                    uiManager.Release(row);
                    actionRows.RemoveAt(i);
                    i--;
                }
            }
        }

        // V5: END TURN first with a cyan dot, the other actions after it with a grey dot (clones of the template,
        // made once and kept).
        private void BindOwnButtons(IReadOnlyList<PopupActionOption> actions)
        {
            orderedActions.Clear();
            for (int i = 0; i < actions.Count; i++)
            {
                if (IsPrimary(actions[i])) orderedActions.Add(actions[i]);
            }

            for (int i = 0; i < actions.Count; i++)
            {
                if (!IsPrimary(actions[i])) orderedActions.Add(actions[i]);
            }

            while (ownButtons.Count < orderedActions.Count)
            {
                PopupActionRowView button = Instantiate(actionTemplate, actionsRoot, false);
                button.name = "Action" + ownButtons.Count;
                button.ValidateReferences();
                button.Acquire();
                Transform dot = button.transform.Find("Dot");
                ownButtons.Add(button);
                ownButtonDots.Add(dot != null ? dot.GetComponent<Image>() : null);
            }

            for (int i = 0; i < ownButtons.Count; i++)
            {
                PopupActionRowView button = ownButtons[i];
                if (i < orderedActions.Count)
                {
                    PopupActionOption action = orderedActions[i];
                    button.Bind(new PopupActionOption(action.Label.ToUpperInvariant(), action.Enabled, action.Selected));
                    if (ownButtonDots[i] != null) ownButtonDots[i].color = IsPrimary(action) ? primaryDot : otherDot;
                    button.Show();
                }
                else
                {
                    button.Hide();
                    button.Unbind();
                }
            }
        }

        private bool IsPrimary(PopupActionOption action) =>
            !string.IsNullOrEmpty(primaryActionLabel)
            && action.Label.StartsWith(primaryActionLabel, StringComparison.OrdinalIgnoreCase);

        private void UnbindActions()
        {
            for (int i = 0; i < ownButtons.Count; i++)
            {
                if (ownButtons[i] == null) continue;
                ownButtons[i].Hide();
                ownButtons[i].Unbind();
            }

            for (int i = actionRows.Count - 1; i >= 0; i--)
            {
                if (actionRows[i] != null)
                {
                    uiManager.Release(actionRows[i]);
                }
            }

            actionRows.Clear();
        }

    }

    public readonly struct TrapFloorStatusModel
    {
        public TrapFloorStatusModel(
            string round,
            string phase,
            string searchProgress,
            string detail,
            string containerCounts,
            string actionHelp,
            Color? playerColor = null,
            IReadOnlyList<TrapFloorHudPlayer> players = null,
            TrapFloorPlayerCardModel card = null)
        {
            Round = round ?? string.Empty;
            Phase = phase ?? string.Empty;
            SearchProgress = searchProgress ?? string.Empty;
            Detail = detail ?? string.Empty;
            ContainerCounts = containerCounts ?? string.Empty;
            ActionHelp = actionHelp ?? string.Empty;
            PlayerColor = playerColor;
            Players = players;
            Card = card;
        }

        public string Round { get; }

        public string Phase { get; }

        public string SearchProgress { get; }

        public string Detail { get; }

        public string ContainerCounts { get; }

        public string ActionHelp { get; }

        /// <summary>The colour of the player whose turn it is (UX-1); null hides the turn strip's colour chip.</summary>
        public Color? PlayerColor { get; }

        /// <summary>V5: the players in turn order for the round bar (null: none shown).</summary>
        public IReadOnlyList<TrapFloorHudPlayer> Players { get; }

        /// <summary>V5: the turn player's card (null: the old detail lines show).</summary>
        public TrapFloorPlayerCardModel Card { get; }
    }

    /// <summary>A player on the round bar: colour name, seat colour, and whether it is their turn.</summary>
    public readonly struct TrapFloorHudPlayer
    {
        public TrapFloorHudPlayer(string name, Color color, bool active)
        {
            Name = name ?? string.Empty;
            Color = color;
            Active = active;
        }

        public string Name { get; }

        public Color Color { get; }

        public bool Active { get; }
    }

    /// <summary>The player card (Figma V5): title and the five rows.</summary>
    public sealed class TrapFloorPlayerCardModel
    {
        public TrapFloorPlayerCardModel(string title, string avatar, string keys, string status, string inHand, string floor)
        {
            Title = title ?? string.Empty;
            Avatar = avatar ?? string.Empty;
            Keys = keys ?? string.Empty;
            Status = status ?? string.Empty;
            InHand = inHand ?? string.Empty;
            Floor = floor ?? string.Empty;
        }

        public string Title { get; }

        public string Avatar { get; }

        public string Keys { get; }

        public string Status { get; }

        public string InHand { get; }

        public string Floor { get; }
    }

    public readonly struct FloorfallStatusModel
    {
        public FloorfallStatusModel(
            bool visible,
            string dice,
            string coordinate,
            string target)
        {
            Visible = visible;
            Dice = dice ?? string.Empty;
            Coordinate = coordinate ?? string.Empty;
            Target = target ?? string.Empty;
        }

        public bool Visible { get; }

        public string Dice { get; }

        public string Coordinate { get; }

        public string Target { get; }
    }
}
