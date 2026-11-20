using System;
using ConsoleCards.Definitions;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// RULES-SHEET (R2): one field in the Rules sheet panel: its name, its value and the rulebook sentence it makes.
    /// A choice shows its value between &lt; and &gt;; a number is typed or changed with – and +; text is typed.
    /// × empties it ("players decide"). Rows are laid out by the panel, one every <c>pitch</c> units.
    /// </summary>
    public sealed class RulesSheetRowView : MonoBehaviour
    {
        [SerializeField] private Text nameLabel;
        [SerializeField] private Text valueLabel;
        [SerializeField] private InputField valueInput;
        [SerializeField] private Button previousButton;
        [SerializeField] private Text previousLabel;
        [SerializeField] private Button nextButton;
        [SerializeField] private Text nextLabel;
        [SerializeField] private Button clearButton;
        [SerializeField] private Text sentenceLabel;

        private string key = string.Empty;
        private Action<string, string> typed;
        private Action<string, int> stepped;

        public void Bind(int index, float pitch, RulesSheetRow row, Action<string, string> onTyped, Action<string, int> onStepped)
        {
            key = row.Key;
            typed = onTyped;
            stepped = onStepped;
            RectTransform rect = (RectTransform)transform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -index * Mathf.Max(1f, pitch));
            bool choice = row.Kind == RuleSettingKind.Choice;
            bool number = row.Kind == RuleSettingKind.Number;
            bool empty = string.IsNullOrEmpty(row.Value);
            if (nameLabel != null) nameLabel.text = row.Label;
            if (sentenceLabel != null) sentenceLabel.text = empty ? "Players decide." : row.Sentence;
            if (valueLabel != null)
            {
                valueLabel.gameObject.SetActive(choice);
                valueLabel.text = empty ? "Players decide" : row.Value;
            }

            if (valueInput != null)
            {
                valueInput.gameObject.SetActive(!choice);
                valueInput.onEndEdit.RemoveAllListeners();
                valueInput.contentType = number ? InputField.ContentType.IntegerNumber : InputField.ContentType.Standard;
                valueInput.SetTextWithoutNotify(row.Value ?? string.Empty);
                valueInput.onEndEdit.AddListener(value => typed?.Invoke(key, value));
            }

            bool steps = choice || number;
            Wire(previousButton, steps, -1);
            Wire(nextButton, steps, 1);
            if (previousLabel != null) previousLabel.text = number ? "–" : "<";
            if (nextLabel != null) nextLabel.text = number ? "+" : ">";
            if (clearButton != null)
            {
                clearButton.onClick.RemoveAllListeners();
                clearButton.gameObject.SetActive(!empty);
                clearButton.onClick.AddListener(() => typed?.Invoke(key, string.Empty));
            }
        }

        public void Unbind()
        {
            typed = null;
            stepped = null;
            if (valueInput != null) valueInput.onEndEdit.RemoveAllListeners();
            if (previousButton != null) previousButton.onClick.RemoveAllListeners();
            if (nextButton != null) nextButton.onClick.RemoveAllListeners();
            if (clearButton != null) clearButton.onClick.RemoveAllListeners();
        }

        private void Wire(Button button, bool shown, int direction)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.gameObject.SetActive(shown);
            if (shown) button.onClick.AddListener(() => stepped?.Invoke(key, direction));
        }
    }
}
