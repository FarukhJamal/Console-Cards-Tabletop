using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// Doc 63 H1 (owner, 2026-11-22): a step-by-step guide to making a game, shown over the Make a game screen. It
    /// opens by itself the first time (the game asks, see <see cref="GameSaveModel.ShowGuide"/>) and again from GUIDE in
    /// the top bar. Each card has a title, a few sentences, Step n of N, and BACK, NEXT and SKIP GUIDE; a cyan frame
    /// marks the control a card talks about. All optional: a panel built without it works as before.
    /// </summary>
    public sealed partial class GameSavePanelView
    {
        [Header("Guide (optional, doc 63 H1)")]
        [SerializeField] private Button guideButton;
        [SerializeField] private GameObject guideOverlay;
        [SerializeField] private Text guideCountLabel;
        [SerializeField] private Text guideTitleLabel;
        [SerializeField] private Text guideTextLabel;
        [SerializeField] private Button guideBackButton;
        [SerializeField] private Button guideNextButton;
        [SerializeField] private Text guideNextLabel;
        [SerializeField] private Button guideSkipButton;
        [Tooltip("A frame put around the control the card talks about; stretched over it.")]
        [SerializeField] private RectTransform guideFrame;
        [Tooltip("The control each card talks about, in card order (none: the frame hides).")]
        [SerializeField] private RectTransform[] guideTargets = System.Array.Empty<RectTransform>();

        // Doc 63 H1: the cards, in the screen's own words.
        private static readonly string[] GuideTitles =
        {
            "WELCOME TO MAKE A GAME",
            "START FROM",
            "TEST ON TABLE",
            "1 · DECK",
            "2 · CARDS",
            "3 · DEAL",
            "4 · RULES",
            "5 · SAVE",
            "PLAY IT",
        };

        private static readonly string[] GuideTexts =
        {
            "A game is a table layout plus a deck, a deal and a rules sheet. You set them up here in five steps: DECK, CARDS, DEAL, RULES and SAVE.\n\n"
                + "You can move between the steps at any time. Nothing is saved until you press SAVE.",
            "On the CREATE screen, START FROM chose what this table begins with: an empty table, a built-in game, or one of your games or templates.\n\n"
                + "A game or template is copied, so the one you started from is not changed.",
            "TEST ON TABLE hides this screen so you can lay out pieces with the Toolbox and try the game.\n\n"
                + "BACK TO GAME SETUP comes back here with everything you typed kept. SAVE captures the table as you left it.",
            "Pick the deck your game deals from with ‹ and ›, or No deck to use only what is on the table.\n\n"
                + "New deck… opens the Deck builder. Put on table places the deck where it should lie when the game starts.",
            "Change the words, costs and pictures of your deck's cards. Create a card… opens the Card studio; Pick cards… adds and removes cards.\n\n"
                + "If the deck belongs to another game, changing it changes that game too.",
            "Choose how many cards each player is dealt when the game starts, to their hands or to the table.\n\n"
                + "No dealing means the players take cards themselves.",
            "Edit rules sheet… opens the rules sheet: turns, limits, scoring and more. Fill in only what matters; empty fields mean the players decide.\n\n"
                + "With Assist on (Settings › GAME), the table can remind players of the limits you set.",
            "Give your game a name, the number of players, a line about it and a COVER picture. LAYOUT FOR saves the table for every player count, or for one count.\n\n"
                + "SAVE AS NEW makes a new game, SAVE OVER replaces the one you are editing, SAVE AS TEMPLATE keeps it to start new games from.",
            "Your saved game is in PLAY › YOUR GAMES: press PLAY to start it. Play it here, or online with the friends you invite.\n\n"
                + "Press GUIDE in the top bar to see this guide again.",
        };

        private bool guideOpen;
        private int guideStep;

        private bool HasGuide =>
            guideOverlay != null && guideTitleLabel != null && guideTextLabel != null && guideNextButton != null && guideSkipButton != null;

        // Called by Bind: GUIDE replays it; the game asks for it the first time. A rebind keeps an open guide's card.
        private void BindGuide(GameSaveModel model)
        {
            if (guideButton != null)
            {
                guideButton.onClick.RemoveAllListeners();
                guideButton.gameObject.SetActive(HasGuide);
                if (HasGuide) guideButton.onClick.AddListener(() => OpenGuide(0));
            }

            if (!HasGuide) return;
            guideNextButton.onClick.RemoveAllListeners();
            guideNextButton.onClick.AddListener(GuideNext);
            guideSkipButton.onClick.RemoveAllListeners();
            guideSkipButton.onClick.AddListener(FinishGuide);
            if (guideBackButton != null)
            {
                guideBackButton.onClick.RemoveAllListeners();
                guideBackButton.onClick.AddListener(GuideBack);
            }

            if (model.ShowGuide) OpenGuide(0);
            else if (guideOpen) ShowGuideStep();
            else guideOverlay.SetActive(false);
        }

        private void UnbindGuide()
        {
            Clear(guideButton);
            Clear(guideNextButton);
            Clear(guideSkipButton);
            Clear(guideBackButton);
            guideOpen = false;
            if (guideOverlay != null) guideOverlay.SetActive(false);
            if (guideFrame != null) guideFrame.gameObject.SetActive(false);
        }

        private void OpenGuide(int at)
        {
            if (!HasGuide) return;
            guideOpen = true;
            guideStep = Mathf.Clamp(at, 0, GuideTitles.Length - 1);
            ShowGuideStep();
        }

        private void GuideNext()
        {
            if (guideStep >= GuideTitles.Length - 1)
            {
                FinishGuide();
                return;
            }

            guideStep++;
            ShowGuideStep();
        }

        private void GuideBack()
        {
            if (guideStep <= 0) return;
            guideStep--;
            ShowGuideStep();
        }

        // DONE or SKIP GUIDE: closes it, and the game remembers it was seen (doc 63 H1).
        private void FinishGuide()
        {
            guideOpen = false;
            guideOverlay.SetActive(false);
            if (guideFrame != null) guideFrame.gameObject.SetActive(false);
            actions?.GuideDone?.Invoke();
        }

        private void ShowGuideStep()
        {
            guideOverlay.SetActive(true);
            guideOverlay.transform.SetAsLastSibling(); // over everything on the screen
            int count = GuideTitles.Length;
            if (guideCountLabel != null) guideCountLabel.text = $"STEP {guideStep + 1} OF {count}";
            guideTitleLabel.text = GuideTitles[guideStep];
            guideTextLabel.text = GuideTexts[guideStep];
            if (guideBackButton != null) guideBackButton.gameObject.SetActive(guideStep > 0);
            if (guideNextLabel != null) guideNextLabel.text = guideStep == count - 1 ? "DONE" : "NEXT";
            ShowGuideFrame();
        }

        // The frame goes inside the control it marks (drawn over it), a little larger than it.
        private void ShowGuideFrame()
        {
            if (guideFrame == null) return;
            RectTransform target = guideTargets != null && guideStep < guideTargets.Length ? guideTargets[guideStep] : null;
            bool shown = target != null && target.gameObject.activeInHierarchy;
            if (shown)
            {
                if (guideFrame.parent != target) guideFrame.SetParent(target, false);
                guideFrame.SetAsLastSibling();
                guideFrame.anchorMin = Vector2.zero;
                guideFrame.anchorMax = Vector2.one;
                guideFrame.pivot = new Vector2(0.5f, 0.5f);
                guideFrame.anchoredPosition = Vector2.zero;
                guideFrame.sizeDelta = new Vector2(12f, 12f);
            }

            if (guideFrame.gameObject.activeSelf != shown) guideFrame.gameObject.SetActive(shown);
        }
    }
}
