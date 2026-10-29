using System;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI.Toolbox
{
    /// <summary>One size or variant chip on a tile (for example a die size). Authored in the prefab.</summary>
    [Serializable]
    public sealed class ToolboxChoiceChip
    {
        [SerializeField] private Button button;
        [SerializeField] private Image fill;
        [SerializeField] private Text label;
        [SerializeField] private int value;

        public Button Button => button;

        public Image Fill => fill;

        public Text Label => label;

        public int Value => value;
    }

    /// <summary>
    /// One authored Toolbox tile for one catalog entry (doc 22). Built at edit time by the Toolbox builder;
    /// at runtime it is only bound to its click handlers. Optional parts: a quantity counter (Cards) and
    /// choice chips (die sizes). Picking reports the entry ID and a value: the count, the chosen size, or 0.
    /// </summary>
    public sealed class ToolboxEntryTile : MonoBehaviour
    {
        [SerializeField] private string entryId;
        [SerializeField] private Button button;
        [SerializeField] private Image fill;
        [SerializeField] private Image shadow;
        [SerializeField] private Color fillColor = Color.white;
        [SerializeField] private Color selectedFillColor = Color.white;
        [SerializeField] private Color shadowColor = Color.black;
        [SerializeField] private Color selectedShadowColor = Color.black;

        [Header("Board 8 (optional): one tile per size, and the RECENT copies")]
        [Tooltip("Not 0: this tile always places this value (a d6 tile: 6). Several tiles may then share one entry.")]
        [SerializeField] private int fixedValue;
        [Tooltip("The detail line's first words for this tile (d6 · six-sided die); empty: the entry's name.")]
        [SerializeField] private string detailTitle = string.Empty;
        [Tooltip("A copy on the RECENT shelf (shown only while its piece is among the recent ones).")]
        [SerializeField] private bool recentCopy;

        [Header("Optional quantity counter")]
        [SerializeField] private Button minusButton;
        [SerializeField] private Button plusButton;
        [SerializeField] private Text countLabel;
        [SerializeField] private Text counterHint;
        [SerializeField] private string singleFormat = "Places 1";
        [SerializeField] private string pluralFormat = "Places {0}";
        [SerializeField] private int minimumCount = 1;
        [SerializeField] private int maximumCount = 99;
        [SerializeField] private int defaultCount = 1;

        [Header("Optional choice chips")]
        [SerializeField] private ToolboxChoiceChip[] choices = Array.Empty<ToolboxChoiceChip>();
        [SerializeField] private int defaultChoiceIndex;
        [SerializeField] private Color choiceFillColor = Color.white;
        [SerializeField] private Color choiceTextColor = Color.black;
        [SerializeField] private Color choiceSelectedFillColor = Color.black;
        [SerializeField] private Color choiceSelectedTextColor = Color.white;

        private Action<ToolboxEntryTile, int> picked;
        private int count;
        private int choiceIndex;

        public string EntryId => entryId;

        public bool HasCounter => countLabel != null;

        public bool HasChoices => choices != null && choices.Length > 0;

        public int Count => count;

        public int FixedValue => fixedValue;

        public string DetailTitle => detailTitle;

        public bool IsRecentCopy => recentCopy;

        /// <summary>The piece this tile places, for the RECENT list: the entry and its fixed value.</summary>
        public string RecentKey => fixedValue != 0 ? entryId + "|" + fixedValue : entryId;

        public void ValidateReferences()
        {
            if (string.IsNullOrWhiteSpace(entryId) || button == null || fill == null)
            {
                throw new InvalidOperationException($"Toolbox tile {name} requires its entry ID, button and fill.");
            }

            if (countLabel != null && (minusButton == null || plusButton == null || counterHint == null))
            {
                throw new InvalidOperationException($"Toolbox tile {name} has a counter without its buttons and hint.");
            }

            for (int i = 0; i < choices.Length; i++)
            {
                if (choices[i] == null || choices[i].Button == null || choices[i].Fill == null || choices[i].Label == null)
                {
                    throw new InvalidOperationException($"Toolbox tile {name} choice {i} is incomplete.");
                }
            }
        }

        public void Bind(Action<ToolboxEntryTile, int> onPicked)
        {
            ValidateReferences();
            Unbind();
            picked = onPicked ?? throw new ArgumentNullException(nameof(onPicked));
            count = Mathf.Clamp(defaultCount, minimumCount, maximumCount);
            choiceIndex = HasChoices ? Mathf.Clamp(defaultChoiceIndex, 0, choices.Length - 1) : -1;
            button.onClick.AddListener(Pick);
            if (HasCounter)
            {
                minusButton.onClick.AddListener(() => ChangeCount(-1));
                plusButton.onClick.AddListener(() => ChangeCount(1));
            }

            for (int i = 0; i < choices.Length; i++)
            {
                int index = i;
                choices[i].Button.onClick.AddListener(() => PickChoice(index));
            }

            RefreshCounter();
            RefreshChoices();
            SetSelected(false);
        }

        public void Unbind()
        {
            picked = null;
            Clear(button);
            Clear(minusButton);
            Clear(plusButton);
            if (choices != null)
            {
                for (int i = 0; i < choices.Length; i++)
                {
                    Clear(choices[i]?.Button);
                }
            }
        }

        private bool isSelected;

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            fill.color = selected ? selectedFillColor : fillColor;
            if (shadow != null)
            {
                shadow.color = selected ? selectedShadowColor : shadowColor;
            }
        }

        /// <summary>Board 8: a tile under the pointer looks chosen (teal with a cyan edge) until the pointer leaves.</summary>
        public void SetHovered(bool hovered)
        {
            if (isSelected) return;
            fill.color = hovered ? selectedFillColor : fillColor;
            if (shadow != null) shadow.color = hovered ? selectedShadowColor : shadowColor;
        }

        private void Pick()
        {
            int value = fixedValue != 0 ? fixedValue : HasCounter ? count : HasChoices ? choices[choiceIndex].Value : 0;
            picked?.Invoke(this, value);
        }

        private void PickChoice(int index)
        {
            choiceIndex = index;
            RefreshChoices();
            Pick();
        }

        private void ChangeCount(int delta)
        {
            count = Mathf.Clamp(count + delta, minimumCount, maximumCount);
            RefreshCounter();
        }

        private void RefreshCounter()
        {
            if (!HasCounter)
            {
                return;
            }

            countLabel.text = count.ToString();
            counterHint.text = count == 1 ? singleFormat : string.Format(pluralFormat, count);
            minusButton.interactable = count > minimumCount;
            plusButton.interactable = count < maximumCount;
        }

        private void RefreshChoices()
        {
            for (int i = 0; i < choices.Length; i++)
            {
                bool selected = i == choiceIndex;
                choices[i].Fill.color = selected ? choiceSelectedFillColor : choiceFillColor;
                choices[i].Label.color = selected ? choiceSelectedTextColor : choiceTextColor;
            }
        }

        private static void Clear(Button target)
        {
            if (target != null)
            {
                target.onClick.RemoveAllListeners();
            }
        }
    }
}
