using System;
using ConsoleCards.Presentation.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// "What happened" log (doc 25, F4): a foldable list of the latest table events, newest first, so nothing a
    /// player missed is lost. A tab (or L) opens and closes it. Layout and styling are authored on the Final UI
    /// prefab; the game only supplies the text when the log changes.
    /// CHAT-1, designer note 03 (2026-11-17: "Chat beside Log in the Activity / Chat panel"): when the prefab has them
    /// (Console Cards > UI > Add Chat to the Log), the open panel has LOG and CHAT tabs; CHAT shows the players'
    /// messages and a box to write one (Enter or SEND). A message from someone else marks the CHAT tab while it is not
    /// shown. Folded, the panel always shows the log.
    /// </summary>
    public sealed class ActivityLogView : ReusableUiView
    {
        [SerializeField] private Button toggleButton;
        [SerializeField] private Text toggleLabel;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text body;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private string collapsedText = "WHAT HAPPENED  (L)";
        [SerializeField] private string expandedText = "HIDE  (L)";
        [Header("V5: always on, the newest lines (set by Apply Kit Look)")]
        [Tooltip("Above 0 the panel stays open at this height while collapsed and L or a click grows it.")]
        [SerializeField] private float peekHeight;
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private Button panelButton;
        [SerializeField] private GameObject header;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private float peekInset = 16f;
        [Header("CHAT-1: Chat beside Log (optional; Console Cards > UI > Add Chat to the Log)")]
        [SerializeField] private Button logTab;
        [SerializeField] private Text logTabLabel;
        [SerializeField] private Button chatTab;
        [SerializeField] private Text chatTabLabel;
        [SerializeField] private Text chatBody;
        [SerializeField] private GameObject chatRow;
        [SerializeField] private InputField chatField;
        [SerializeField] private Button chatSendButton;
        [Tooltip("How much the list gets shorter for the message box under it.")]
        [SerializeField] private float chatRowHeight = 52f;
        [SerializeField] private Color tabOn = Color.white;
        [SerializeField] private Color tabOff = new Color(1f, 1f, 1f, 0.45f);

        private bool expanded;
        private bool fullSizeKnown;
        private float fullHeight;
        private Vector2 fullViewportMin;
        private Vector2 fullViewportMax;
        private bool chatMode;
        private bool chatUnread;
        private bool chatBaseKnown;
        private Vector2 chatBaseMin;
        private Action<string> chatSender;

        private bool Peeks => peekHeight > 0f && panelRect != null && viewport != null;

        public bool IsExpanded => expanded;

        /// <summary>CHAT-1: the prefab has the Chat tab, its list and its message box.</summary>
        public bool HasChat => chatTab != null && chatBody != null && chatRow != null && chatField != null;

        public void ValidateReferences()
        {
            if (toggleButton == null || toggleLabel == null || panel == null || body == null)
            {
                throw new InvalidOperationException("ActivityLogView requires its authored references.");
            }
        }

        protected override void OnAcquire()
        {
            ValidateReferences();
            toggleButton.onClick.RemoveListener(Toggle);
            toggleButton.onClick.AddListener(Toggle);
            if (panelButton != null)
            {
                panelButton.onClick.RemoveListener(Toggle);
                panelButton.onClick.AddListener(Toggle);
            }

            if (HasChat)
            {
                chatTab.onClick.RemoveListener(OpenChat);
                chatTab.onClick.AddListener(OpenChat);
                if (logTab != null)
                {
                    logTab.onClick.RemoveListener(OpenLogTab);
                    logTab.onClick.AddListener(OpenLogTab);
                }

                if (chatSendButton != null)
                {
                    chatSendButton.onClick.RemoveListener(SubmitChat);
                    chatSendButton.onClick.AddListener(SubmitChat);
                }

                chatField.onEndEdit.RemoveListener(EndChatEdit);
                chatField.onEndEdit.AddListener(EndChatEdit);
                chatField.characterLimit = 200;
            }

            chatMode = false;
            SetExpanded(false);
        }

        /// <summary>CHAT-1: where a message written in the Chat tab goes (null: nowhere).</summary>
        public void BindChat(Action<string> send) => chatSender = send;

        /// <summary>CHAT-1: the messages, oldest first. <paramref name="fromOthers"/> marks the tab while it is not shown.</summary>
        public void SetChat(string text, bool fromOthers)
        {
            if (!HasChat) return;
            chatBody.text = string.IsNullOrEmpty(text) ? "No messages yet. Say hello!" : text;
            if (fromOthers && !(chatMode && expanded)) chatUnread = true;
            RefreshTabs();
            if (chatMode && expanded && scroll != null)
            {
                Canvas.ForceUpdateCanvases();
                scroll.verticalNormalizedPosition = 0f; // the newest message is at the bottom
            }
        }

        /// <summary>CHAT-1: opens the panel on its Chat tab, ready to write.</summary>
        public void OpenChat()
        {
            if (!HasChat) return;
            chatMode = true;
            if (!expanded) SetExpanded(true);
            else ApplyChatLayout();
            if (chatField.isActiveAndEnabled) chatField.ActivateInputField();
        }

        private void OpenLogTab()
        {
            chatMode = false;
            ApplyChatLayout();
        }

        private void EndChatEdit(string text)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)) SubmitChat();
        }

        private void SubmitChat()
        {
            if (!HasChat) return;
            string text = chatField.text;
            if (string.IsNullOrWhiteSpace(text)) return;
            chatField.text = string.Empty;
            chatSender?.Invoke(text);
            if (chatField.isActiveAndEnabled) chatField.ActivateInputField();
        }

        // The Chat tab shows its list and the message box (the list gets shorter for it); the Log tab, the log.
        private void ApplyChatLayout()
        {
            if (!HasChat) return;
            bool chat = chatMode && expanded;
            if (chat) chatUnread = false;
            if (chatRow.activeSelf != chat) chatRow.SetActive(chat);
            if (body.gameObject.activeSelf == chat) body.gameObject.SetActive(!chat);
            if (chatBody.gameObject.activeSelf != chat) chatBody.gameObject.SetActive(chat);
            RectTransform list = viewport != null ? viewport : (scroll != null ? scroll.transform as RectTransform : null);
            if (list != null && expanded)
            {
                if (!chatBaseKnown)
                {
                    chatBaseKnown = true;
                    chatBaseMin = list.offsetMin;
                }

                list.offsetMin = chat ? chatBaseMin + new Vector2(0f, chatRowHeight) : chatBaseMin;
            }

            RefreshTabs();
            if (scroll != null && expanded)
            {
                Canvas.ForceUpdateCanvases();
                scroll.verticalNormalizedPosition = chat ? 0f : 1f;
            }
        }

        private void RefreshTabs()
        {
            if (logTabLabel != null) logTabLabel.color = chatMode && expanded ? tabOff : tabOn;
            if (chatTabLabel != null)
            {
                chatTabLabel.color = chatMode && expanded ? tabOn : tabOff;
                chatTabLabel.text = chatUnread ? "CHAT  NEW" : "CHAT";
            }
        }

        /// <summary>Replaces the log text (newest first). Call when the log changes, not every frame.</summary>
        public void SetText(string text)
        {
            RequireAcquired();
            body.text = text ?? string.Empty;
            if ((expanded || Peeks) && scroll != null)
            {
                scroll.verticalNormalizedPosition = 1f;
            }
        }

        public void SetExpanded(bool open)
        {
            expanded = open;
            panel.SetActive(open || Peeks);
            toggleLabel.text = open ? expandedText : collapsedText;
            if (Peeks)
            {
                if (!fullSizeKnown)
                {
                    fullSizeKnown = true;
                    fullHeight = panelRect.sizeDelta.y;
                    fullViewportMin = viewport.offsetMin;
                    fullViewportMax = viewport.offsetMax;
                }

                panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, open ? fullHeight : peekHeight);
                if (header != null) header.SetActive(open);
                viewport.offsetMin = open ? fullViewportMin : new Vector2(peekInset, peekInset);
                viewport.offsetMax = open ? fullViewportMax : new Vector2(-peekInset, -peekInset);
                if (scroll != null)
                {
                    Canvas.ForceUpdateCanvases();
                    scroll.verticalNormalizedPosition = 1f;
                }
            }

            if (open && scroll != null)
            {
                Canvas.ForceUpdateCanvases();
                scroll.verticalNormalizedPosition = 1f;
            }

            if (!open) chatMode = false; // CHAT-1: folded, it shows the log
            ApplyChatLayout();
        }

        public void Toggle() => SetExpanded(!expanded);

        public override void Unbind()
        {
            if (toggleButton != null) toggleButton.onClick.RemoveListener(Toggle);
            if (panelButton != null) panelButton.onClick.RemoveListener(Toggle);
            if (chatTab != null) chatTab.onClick.RemoveListener(OpenChat);
            if (logTab != null) logTab.onClick.RemoveListener(OpenLogTab);
            if (chatSendButton != null) chatSendButton.onClick.RemoveListener(SubmitChat);
            if (chatField != null) chatField.onEndEdit.RemoveListener(EndChatEdit);
            chatSender = null;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null
                && !TableKeys.TypingInUi // CHAT-1: an L typed in the message box is a letter
                && keyboard.lKey.wasPressedThisFrame
                && !keyboard.leftCtrlKey.isPressed
                && !keyboard.rightCtrlKey.isPressed)
            {
                Toggle();
            }
        }
    }
}
