using System;
using System.Collections.Generic;
using UnityEngine;

namespace ConsoleCards.Presentation.UI
{
    public enum UiLayer
    {
        Screen,
        Hud,
        Popup,
        Modal,
        Notification,
    }

    public enum UiRetention
    {
        CachedSingleInstance,
        Pooled,
    }

    [Serializable]
    public sealed class UiPrefabCatalogEntry
    {
        [SerializeField] private string id;
        [SerializeField] private ReusableUiView prefab;
        [SerializeField] private UiLayer layer;
        [SerializeField] private UiRetention retention;

        public string Id => id;

        public ReusableUiView Prefab => prefab;

        public UiLayer Layer => layer;

        public UiRetention Retention => retention;
    }

    [CreateAssetMenu(fileName = "UiPrefabCatalog", menuName = "Console Cards/UI/Prefab Catalog")]
    public sealed class UiPrefabCatalog : ScriptableObject
    {
        [SerializeField] private List<UiPrefabCatalogEntry> entries = new List<UiPrefabCatalogEntry>();

        public IReadOnlyList<UiPrefabCatalogEntry> Entries => entries;

        public void ValidateEntries()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++)
            {
                UiPrefabCatalogEntry entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id))
                {
                    throw new InvalidOperationException($"UI prefab catalog entry {i} requires an ID.");
                }

                if (!ids.Add(entry.Id))
                {
                    throw new InvalidOperationException($"UI prefab catalog contains duplicate ID '{entry.Id}'.");
                }

                if (entry.Prefab == null || entry.Prefab.gameObject.scene.IsValid())
                {
                    throw new InvalidOperationException(
                        $"UI prefab catalog entry '{entry.Id}' requires a prefab asset reference.");
                }
            }
        }

        public UiPrefabCatalogEntry GetRequired(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A UI prefab ID is required.", nameof(id));
            }

            for (int i = 0; i < entries.Count; i++)
            {
                UiPrefabCatalogEntry entry = entries[i];
                if (entry != null && string.Equals(entry.Id, id, StringComparison.Ordinal))
                {
                    return entry;
                }
            }

            throw new InvalidOperationException($"UI prefab catalog does not contain '{id}'.");
        }
    }

    public static class UiPrefabIds
    {
        public const string ComponentToolbox = "platform.component-toolbox";
        public const string SessionBar = "platform.session-bar";
        public const string RulesCard = "platform.rules-card";
        public const string RulesHeading = "platform.rules-heading";
        public const string RulesLine = "platform.rules-line";
        public const string TabletopPopup = "platform.tabletop-popup";
        public const string QuantityPopup = "platform.quantity-popup";
        public const string CardInspect = "platform.card-inspect";
        public const string ActionAbilityPurchase = "platform.action-ability-purchase";
        public const string FocusedCardSelection = "platform.focused-card-selection";
        public const string FocusedCardOption = "platform.focused-card-option";
        public const string InteractionGuide = "platform.interaction-guide";
        public const string PopupActionRow = "platform.popup-action-row";
        public const string GameTemplateRow = "platform.game-template-row";
        public const string TrapFloorHud = "trap-floor.hud";
        public const string FeedbackBanner = "platform.feedback-banner";
        public const string FeedbackMessages = "platform.feedback-messages";
        public const string HotSeatHandover = "platform.hotseat-handover";
        public const string ActivityLog = "platform.activity-log";
        public const string ContentArtPanel = "platform.content-art-panel";
        public const string ContentArtRow = "platform.content-art-row";
        public const string RuleProposalPanel = "platform.rule-proposal-panel";
        public const string HintStrip = "platform.hint-strip";
        public const string ProfilePanel = "platform.profile-panel";
        public const string ProfileRow = "platform.profile-row";
        public const string CardEditorPanel = "platform.card-editor-panel";
        public const string CardEditorRow = "platform.card-editor-row";
        public const string CardEditorDeckRow = "platform.card-editor-deck-row";
        public const string DeckEditorPanel = "platform.deck-editor-panel";
        public const string DeckEditorCardRow = "platform.deck-editor-card-row";
        public const string GameSelectPanel = "platform.game-select-panel";
        public const string GameSavePanel = "platform.game-save-panel";
        public const string TablePanel = "platform.table-panel";
    }
}
