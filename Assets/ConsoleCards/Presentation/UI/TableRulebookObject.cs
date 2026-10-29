using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// BOOK-1 (owner, 2026-10-30: "a physical rule book on the table … like Red Dead Redemption: a book there on the
    /// table, you open it up and read the text"): the game's rules as a book lying closed beside the board. Click it (or
    /// the HUD's Rulebook button, or R): it rises in front of the camera and opens.
    /// <para>Open, it is design board "Rulebook" (owner: "everything the same as the reference"): cream pages in the
    /// teal cover, each page "GAME · RULEBOOK", its section as the title and its rules as numbered steps (a rule the
    /// table changed: HOUSE RULE AT THIS TABLE); a tab per section on the right edge (HOUSE lists the changed rules);
    /// under the book ‹ › and the page count, Search the rules (the full screen rulebook), BOOKMARK (the ribbon; the
    /// book opens there), PIN PAGE (the page stays on screen beside the table) and CLOSE (R or Esc). A / D and the
    /// arrows turn the pages too.</para>
    /// <para>Built by Console Cards > UI > Create Missing Table HUD (HUD-B) into Resources/Table/TableRulebook; its
    /// look, sizes and timings are on that prefab. Pages are filled once per Bind and per page turn, nothing is
    /// allocated per frame.</para>
    /// </summary>
    public sealed class TableRulebookObject : MonoBehaviour
    {
        public const string ResourcePath = "Table/TableRulebook";

        /// <summary>One page side of the open book (its words lie on the paper).</summary>
        [Serializable]
        private sealed class Side
        {
            public Text Eyebrow;
            public Text Heading;
            public Text Intro;
            public RectTransform Steps;
            public GameObject RowTemplate;
            public Text Number;
            public GameObject Ribbon;

            public bool IsComplete =>
                Eyebrow != null && Heading != null && Intro != null && Steps != null && RowTemplate != null && Number != null;
        }

        [Header("The book")]
        [SerializeField] private Transform coverHinge;
        [SerializeField] private Transform flipHinge;
        [SerializeField] private GameObject flipPage;
        [SerializeField] private Collider clickCollider;
        [SerializeField] private Text coverTitle;
        [Tooltip("The game's name under RULEBOOK on the cover, in points.")]
        [SerializeField] private int coverGameSize = 72;
        [Tooltip("Where the open book's middle (the spine) sits, in the book's own space.")]
        [SerializeField] private Vector3 openCentre = Vector3.zero;
        [Tooltip("The open book's width in the book's own units (both pages).")]
        [SerializeField] private float openWidth = 2.4f;
        [Tooltip("The open book's height (top to bottom of a page) in the book's own units.")]
        [SerializeField] private float openHeight = 1.64f;

        [Header("Pages")]
        [SerializeField] private Side left = new Side();
        [SerializeField] private Side right = new Side();
        [Tooltip("Canvas pixels a page has for its steps (under the title).")]
        [SerializeField] private float stepRoom = 900f;
        [Tooltip("A step's height without its words (padding), in canvas pixels.")]
        [SerializeField] private float stepPadding = 62f;
        [Tooltip("One line of a step's words, in canvas pixels.")]
        [SerializeField] private float stepLine = 44f;
        [Tooltip("The HOUSE RULE AT THIS TABLE line over a changed rule, in canvas pixels.")]
        [SerializeField] private float houseTagLine = 36f;
        [Tooltip("Characters on one line of a step, to count how many lines a rule needs.")]
        [SerializeField] private int charactersPerLine = 36;

        [Header("Section tabs (right edge)")]
        [SerializeField] private RectTransform tabColumn;
        [SerializeField] private Button[] tabButtons = Array.Empty<Button>();
        [SerializeField] private Text[] tabLabels = Array.Empty<Text>();
        [SerializeField] private Image[] tabFills = Array.Empty<Image>();
        [SerializeField] private Color tabIdle = new Color(0.84f, 0.81f, 0.74f, 1f);
        [SerializeField] private Color tabOn = new Color(0.02f, 0.94f, 0.94f, 1f);
        [SerializeField] private Color tabHouse = new Color(0.96f, 0.79f, 0.65f, 1f);
        [Tooltip("The tabs' top below the book's top edge, in canvas pixels.")]
        [SerializeField] private float tabDrop = 45f;

        [Header("Reading")]
        [SerializeField] private GameObject readingCanvas;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button previousHalf;
        [SerializeField] private Button nextHalf;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button moreButton;
        [SerializeField] private Button bookmarkButton;
        [SerializeField] private Text bookmarkLabel;
        [SerializeField] private Button pinButton;
        [SerializeField] private Text pinLabel;
        [SerializeField] private Text pageCountLabel;
        [Tooltip("How much of the screen's width the open book fills while you read it.")]
        [SerializeField, Range(0.3f, 1f)] private float readingFill = 0.82f;
        [Tooltip("How far the open book tips back from facing the camera, in degrees.")]
        [SerializeField] private float readingTilt = 12f;
        [SerializeField] private float openSeconds = 0.55f;
        [SerializeField] private float flipSeconds = 0.3f;

        [Header("Pinned page (beside the table)")]
        [SerializeField] private GameObject pinnedCanvas;
        [SerializeField] private Text pinnedEyebrow;
        [SerializeField] private Text pinnedHeading;
        [SerializeField] private Text pinnedBody;
        [SerializeField] private Button unpinButton;

        private struct Step
        {
            public string Number;
            public string Text;
            public bool House;
        }

        private sealed class Page
        {
            public string Heading = string.Empty;
            public string Intro = string.Empty;
            public readonly List<Step> Steps = new List<Step>();
            public float Height;
            public int Section = -1;
            public bool IsEmpty => Heading.Length == 0 && Intro.Length == 0 && Steps.Count == 0;
        }

        private sealed class Row
        {
            public GameObject Root;
            public Image Back;
            public GameObject Bar;
            public GameObject Badge;
            public Text BadgeNumber;
            public GameObject Tag;
            public Text Body;
        }

        private readonly List<Page> pages = new List<Page>();
        private readonly List<int> sectionPages = new List<int>();
        private readonly List<bool> sectionHouse = new List<bool>();
        private readonly List<Row> leftRows = new List<Row>();
        private readonly List<Row> rightRows = new List<Row>();
        private readonly StringBuilder pinnedText = new StringBuilder(512);
        private string eyebrow = "RULEBOOK";
        private int stepInSection;
        private Canvas readingCanvasComponent;
        private Vector3 restPosition;
        private Quaternion restRotation = Quaternion.identity;
        private UnityEngine.Camera readingCamera;
        private UnityEngine.Camera clickCamera;
        private Func<bool> blocked;
        private Action requestOpen;
        private Action<bool> readingChanged;
        private Action openFullRulebook;
        private float openAmount; // 0 on the table, 1 open in front of the camera
        private bool wantOpen;
        private int openedFrame = -1;
        private int spread; // the left page's index (even)
        private int bookmark = -1;
        private int pinned = -1;
        private bool tabsShown = true;
        private bool pinnedShown = true;
        private float flipTime = -1f;
        private int flipDirection;
        private bool placed;

        /// <summary>True from the moment it starts to rise until it lies on the table again.</summary>
        public bool IsReading => wantOpen || openAmount > 0f;

        public bool IsComplete =>
            coverHinge != null && flipHinge != null && flipPage != null && clickCollider != null && coverTitle != null
            && left != null && left.IsComplete && right != null && right.IsComplete && readingCanvas != null
            && previousButton != null && nextButton != null && previousHalf != null && nextHalf != null
            && closeButton != null && moreButton != null && bookmarkButton != null && bookmarkLabel != null
            && pinButton != null && pinLabel != null && pageCountLabel != null && tabColumn != null
            && tabButtons.Length > 0 && tabLabels.Length == tabButtons.Length && tabFills.Length == tabButtons.Length
            && pinnedCanvas != null && pinnedEyebrow != null && pinnedHeading != null && pinnedBody != null
            && unpinButton != null;

        /// <param name="isBlocked">True while a panel or menu covers the table (a click then does not open the book).</param>
        /// <param name="open">A click on the closed book.</param>
        /// <param name="reading">Called with true when it starts to rise and false when it lies down again.</param>
        /// <param name="full">Search the rules: the full screen rulebook.</param>
        public void Wire(UnityEngine.Camera tableCamera, Func<bool> isBlocked, Action open, Action<bool> reading, Action full)
        {
            clickCamera = tableCamera;
            blocked = isBlocked;
            requestOpen = open;
            readingChanged = reading;
            openFullRulebook = full;
            readingCanvasComponent = readingCanvas.GetComponent<Canvas>();
            Listen(previousButton, PreviousSpread);
            Listen(previousHalf, PreviousSpread);
            Listen(nextButton, NextSpread);
            Listen(nextHalf, NextSpread);
            Listen(closeButton, Close);
            Listen(moreButton, PressMore);
            Listen(bookmarkButton, ToggleBookmark);
            Listen(pinButton, TogglePin);
            Listen(unpinButton, Unpin);
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int section = i;
                Listen(tabButtons[i], () => GoToSection(section));
            }

            left.RowTemplate.SetActive(false);
            right.RowTemplate.SetActive(false);
            readingCanvas.SetActive(false);
            flipPage.SetActive(false);
            ShowPinned(false);
        }

        /// <summary>Fills the pages from the rules (keeps the open page when it still exists).</summary>
        public void Bind(RulesCardModel rules)
        {
            pages.Clear();
            sectionPages.Clear();
            sectionHouse.Clear();
            string game = rules == null || string.IsNullOrEmpty(rules.GameName) ? string.Empty : rules.GameName.ToUpperInvariant();
            coverTitle.text = game.Length > 0 ? $"RULEBOOK\n<size={coverGameSize}>{game}</size>" : "RULEBOOK";
            eyebrow = game.Length > 0 ? $"{game} · RULEBOOK" : "RULEBOOK";

            Page title = new Page { Heading = game.Length > 0 ? rules.GameName : "Rulebook" };
            stepInSection = 0;
            if (rules != null)
            {
                title.Intro = (rules.IsHouseRules ? $"{rules.SetName} · house rules" : rules.SetName)
                    + "\n\nThe tabs on the right go to each part. A rule this table changed shows as HOUSE RULE AT THIS TABLE."
                    + (string.IsNullOrEmpty(rules.PendingText) ? string.Empty : "\n\n" + rules.PendingText);
            }

            pages.Add(title);
            Page page = null;
            bool anyHouse = false;
            if (rules != null)
            {
                for (int i = 0; i < rules.Lines.Count; i++)
                {
                    RulesCardLine line = rules.Lines[i];
                    if (line.Kind == RulesCardLineKind.Heading)
                    {
                        page = NewSection(SentenceCase(line.Text), false);
                        continue;
                    }

                    anyHouse |= line.Changed;
                    page = AddStep(page, line);
                }

                // HOUSE: every rule this table changed, together.
                if (anyHouse)
                {
                    page = NewSection("House rules", true);
                    page.Intro = "What this table plays differently from the printed rules.";
                    for (int i = 0; i < rules.Lines.Count; i++)
                    {
                        RulesCardLine line = rules.Lines[i];
                        if (line.Kind == RulesCardLineKind.Rule && line.Changed) page = AddStep(page, line);
                    }
                }
            }

            if (pages.Count % 2 == 1) pages.Add(new Page()); // the last spread has two sides
            spread = Mathf.Clamp(spread - (spread % 2), 0, Mathf.Max(0, pages.Count - 2));
            if (bookmark >= pages.Count) bookmark = -1;
            if (pinned >= pages.Count || (pinned >= 0 && pages[pinned].IsEmpty)) pinned = -1;
            FillTabs();
            ShowSpread();
            FillPinned();
        }

        private Page NewSection(string heading, bool house)
        {
            Page page = new Page { Heading = heading, Section = sectionPages.Count };
            stepInSection = 0;
            sectionPages.Add(pages.Count);
            sectionHouse.Add(house);
            pages.Add(page);
            return page;
        }

        private Page AddStep(Page page, RulesCardLine line)
        {
            float height = stepPadding + (houseTagLine * (line.Changed ? 1f : 0f))
                + (stepLine * (1 + ((line.Text.Length + 4) / Mathf.Max(8, charactersPerLine))));
            if (page == null || (page.Steps.Count > 0 && page.Height + height > stepRoom))
            {
                Page next = new Page
                {
                    Heading = page != null ? page.Heading : "Rules",
                    Section = page != null ? page.Section : -1,
                };
                pages.Add(next);
                page = next;
            }

            // A numbered rule keeps its number; a bullet ("•") is numbered in its part, as the board's steps are.
            stepInSection++;
            string number = line.Number.Length > 0 && char.IsDigit(line.Number[0]) ? line.Number : stepInSection.ToString();
            page.Steps.Add(new Step { Number = number, Text = BoldLead(line.Text), House = line.Changed });
            page.Height += height;
            return page;
        }

        /// <summary>Where the closed book lies.</summary>
        public void PlaceOnTable(Vector3 position, Quaternion rotation)
        {
            restPosition = position;
            restRotation = rotation;
            placed = true;
            if (!IsReading)
            {
                transform.SetPositionAndRotation(restPosition, restRotation);
            }
        }

        /// <summary>Lifts the book in front of this camera and opens it (at the bookmark when there is one).</summary>
        public void Open(UnityEngine.Camera camera)
        {
            if (camera == null || pages.Count == 0) return;
            readingCamera = camera;
            if (!wantOpen && openAmount <= 0f)
            {
                if (bookmark >= 0 && bookmark != spread)
                {
                    spread = bookmark;
                    ShowSpread();
                }

                readingChanged?.Invoke(true);
            }

            wantOpen = true;
            openedFrame = Time.frameCount; // the R that opened it does not close it again
            readingCanvas.SetActive(true);
        }

        public void Close()
        {
            if (wantOpen && openAmount <= 0f)
            {
                // Closed before it began to rise: say so now, or the table's input would stay off.
                wantOpen = false;
                readingCanvas.SetActive(false);
                readingChanged?.Invoke(false);
                return;
            }

            wantOpen = false;
            readingCanvas.SetActive(false);
        }

        /// <summary>At once, without the animation (a new table, the game leaving).</summary>
        public void PutDownNow()
        {
            bool wasReading = IsReading;
            wantOpen = false;
            openAmount = 0f;
            readingCanvas.SetActive(false);
            flipPage.SetActive(false);
            flipTime = -1f;
            Pose(0f);
            if (wasReading) readingChanged?.Invoke(false);
        }

        private void Update()
        {
            if (wantOpen) ReadKeys();
            else if (openAmount <= 0f) WatchForClick();

            float target = wantOpen ? 1f : 0f;
            if (!Mathf.Approximately(openAmount, target))
            {
                openAmount = Mathf.MoveTowards(openAmount, target, Time.unscaledDeltaTime / Mathf.Max(0.05f, openSeconds));
                Pose(openAmount);
                if (openAmount <= 0f) readingChanged?.Invoke(false);
            }
            else if (wantOpen)
            {
                Pose(1f); // the camera may still settle
            }

            AnimateFlip();
            PlaceTabs();
            ShowPinned(pinned >= 0 && !IsReading && (blocked == null || !blocked()));
        }

        private void ReadKeys()
        {
            Keyboard keys = Keyboard.current;
            if (keys == null) return;
            if (keys.escapeKey.wasPressedThisFrame || (keys.rKey.wasPressedThisFrame && Time.frameCount != openedFrame))
            {
                Close();
                return;
            }

            if (keys.rightArrowKey.wasPressedThisFrame || keys.dKey.wasPressedThisFrame || keys.pageDownKey.wasPressedThisFrame)
            {
                NextSpread();
            }
            else if (keys.leftArrowKey.wasPressedThisFrame || keys.aKey.wasPressedThisFrame || keys.pageUpKey.wasPressedThisFrame)
            {
                PreviousSpread();
            }
        }

        // A left click on the closed book (not through a UI panel, not while a menu covers the table).
        private void WatchForClick()
        {
            if (!placed || requestOpen == null) return;
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (blocked != null && blocked()) return;
            EventSystem events = EventSystem.current;
            if (events != null && events.IsPointerOverGameObject()) return;
            UnityEngine.Camera eye = clickCamera != null ? clickCamera : UnityEngine.Camera.main;
            if (eye == null) return;
            Ray ray = eye.ScreenPointToRay(mouse.position.ReadValue());
            if (clickCollider.Raycast(ray, out _, 200f)) requestOpen();
        }

        // Between lying on the table (0) and open in front of the camera (1): eased, the cover swinging open on the way.
        private void Pose(float amount)
        {
            float eased = amount * amount * (3f - (2f * amount));
            coverHinge.localRotation = Quaternion.AngleAxis(180f * Mathf.Clamp01((eased - 0.15f) / 0.85f), Vector3.forward);
            if (amount <= 0f || readingCamera == null)
            {
                transform.SetPositionAndRotation(restPosition, restRotation);
                return;
            }

            ReadingPose(out Vector3 position, out Quaternion rotation);
            transform.SetPositionAndRotation(
                Vector3.Lerp(restPosition, position, eased),
                Quaternion.Slerp(restRotation, rotation, eased));
        }

        // Facing the camera with the page tops up, tipped back a little, far enough away that the open book fills
        // readingFill of the screen's width; the spine at the screen's middle.
        private void ReadingPose(out Vector3 position, out Quaternion rotation)
        {
            Transform eye = readingCamera.transform;
            rotation = Quaternion.LookRotation(eye.up, -eye.forward) * Quaternion.AngleAxis(readingTilt, Vector3.right);
            float scale = transform.lossyScale.x;
            float worldWidth = Mathf.Max(0.01f, openWidth * scale);
            float halfFov = readingCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float byWidth = worldWidth / (readingFill * 2f * Mathf.Tan(halfFov) * Mathf.Max(0.1f, readingCamera.aspect));
            float byHeight = Mathf.Max(0.01f, openHeight * scale) / (readingFill * 2f * Mathf.Tan(halfFov));
            float distance = Mathf.Max(byWidth, byHeight); // wide or tall screens: the whole book shows
            Vector3 centre = eye.position + (eye.forward * distance);
            position = centre - (rotation * (openCentre * scale));
        }

        // The tabs hang off the open book's right edge, from just under its top.
        private void PlaceTabs()
        {
            bool show = wantOpen && openAmount >= 0.99f && readingCamera != null && sectionPages.Count > 0;
            if (show != tabsShown)
            {
                tabsShown = show;
                tabColumn.gameObject.SetActive(show);
            }

            if (!show || readingCanvasComponent == null) return;
            Vector3 corner = transform.TransformPoint(openCentre + new Vector3(openWidth * 0.5f, 0f, openHeight * 0.5f));
            Vector3 screen = readingCamera.WorldToScreenPoint(corner);
            float scale = Mathf.Max(0.01f, readingCanvasComponent.scaleFactor);
            tabColumn.anchoredPosition = new Vector2(screen.x / scale, (screen.y / scale) - tabDrop);
        }

        private void NextSpread() => TurnTo(spread + 2, 1);

        private void PreviousSpread() => TurnTo(spread - 2, -1);

        private void GoToSection(int section)
        {
            if (section < 0 || section >= sectionPages.Count) return;
            int page = sectionPages[section];
            int target = page - (page % 2);
            if (target != spread) TurnTo(target, target > spread ? 1 : -1);
        }

        private void TurnTo(int next, int direction)
        {
            if (next < 0 || next >= pages.Count || openAmount < 0.98f) return;
            spread = next;
            ShowSpread();
            flipDirection = direction;
            flipTime = 0f;
            flipHinge.localRotation = Quaternion.AngleAxis(direction > 0 ? 0f : 180f, Vector3.forward);
            flipPage.SetActive(true);
        }

        private void AnimateFlip()
        {
            if (flipTime < 0f) return;
            flipTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(flipTime / Mathf.Max(0.05f, flipSeconds));
            float angle = flipDirection > 0 ? 180f * t : 180f * (1f - t);
            flipHinge.localRotation = Quaternion.AngleAxis(angle, Vector3.forward);
            if (t >= 1f)
            {
                flipTime = -1f;
                flipPage.SetActive(false);
            }
        }

        private void ShowSpread()
        {
            Fill(spread, left, leftRows);
            Fill(spread + 1, right, rightRows);
            int last = Mathf.Max(1, pages.Count);
            pageCountLabel.text = $"Pages {spread + 1}–{Mathf.Min(spread + 2, last)} of {last}";
            previousButton.interactable = spread > 0;
            nextButton.interactable = spread + 2 < pages.Count;
            bookmarkLabel.text = bookmark == spread ? "BOOKMARKED" : "BOOKMARK";
            if (right.Ribbon != null) right.Ribbon.SetActive(bookmark == spread);
            pinLabel.text = pinned == spread || pinned == spread + 1 ? "UNPIN" : "PIN PAGE";
            int section = SectionOf(spread);
            if (section < 0) section = SectionOf(spread + 1);
            for (int i = 0; i < tabFills.Length; i++)
            {
                bool house = i < sectionHouse.Count && sectionHouse[i];
                tabFills[i].color = i == section ? tabOn : house ? tabHouse : tabIdle;
            }
        }

        private int SectionOf(int index) => index >= 0 && index < pages.Count ? pages[index].Section : -1;

        private void FillTabs()
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                bool has = i < sectionPages.Count;
                tabButtons[i].gameObject.SetActive(has);
                if (!has) continue;
                Page page = pages[sectionPages[i]];
                tabLabels[i].text = sectionHouse[i] ? "HOUSE  •" : page.Heading.ToUpperInvariant();
            }
        }

        private void Fill(int index, Side side, List<Row> rows)
        {
            Page page = index >= 0 && index < pages.Count ? pages[index] : null;
            bool has = page != null && !page.IsEmpty;
            side.Eyebrow.text = has ? eyebrow : string.Empty;
            side.Heading.text = has ? page.Heading : string.Empty;
            side.Intro.text = has ? page.Intro : string.Empty;
            side.Intro.gameObject.SetActive(has && page.Intro.Length > 0);
            side.Number.text = has ? (index + 1).ToString() : string.Empty;
            int count = has ? page.Steps.Count : 0;
            while (rows.Count < count) rows.Add(MakeRow(side));
            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                bool shown = i < count;
                row.Root.SetActive(shown);
                if (!shown) continue;
                Step step = page.Steps[i];
                row.Bar.SetActive(step.House);
                row.Tag.SetActive(step.House);
                row.Back.enabled = step.House;
                row.Badge.SetActive(!step.House);
                row.BadgeNumber.text = step.Number;
                row.Body.text = step.Text;
            }
        }

        private static Row MakeRow(Side side)
        {
            GameObject go = Instantiate(side.RowTemplate, side.Steps);
            go.name = "Step";
            Transform root = go.transform;
            return new Row
            {
                Root = go,
                Back = go.GetComponent<Image>(),
                Bar = root.Find("Bar").gameObject,
                Badge = root.Find("Badge").gameObject,
                BadgeNumber = root.Find("Badge/Number").GetComponent<Text>(),
                Tag = root.Find("Words/Tag").gameObject,
                Body = root.Find("Words/Body").GetComponent<Text>(),
            };
        }

        private void ToggleBookmark()
        {
            bookmark = bookmark == spread ? -1 : spread;
            ShowSpread();
        }

        // PIN PAGE: the left page (or the right one when the left is blank) stays beside the table after the book closes.
        private void TogglePin()
        {
            if (pinned == spread || pinned == spread + 1)
            {
                Unpin();
                return;
            }

            pinned = spread < pages.Count && !pages[spread].IsEmpty ? spread : spread + 1;
            if (pinned >= pages.Count || pages[pinned].IsEmpty) pinned = -1;
            FillPinned();
            ShowSpread();
        }

        private void Unpin()
        {
            pinned = -1;
            ShowPinned(false);
            ShowSpread();
        }

        private void FillPinned()
        {
            if (pinned < 0) return;
            Page page = pages[pinned];
            pinnedEyebrow.text = $"PINNED · PAGE {pinned + 1}";
            pinnedHeading.text = page.Heading;
            pinnedText.Clear();
            if (page.Intro.Length > 0 && page.Steps.Count == 0) pinnedText.Append(page.Intro);
            for (int i = 0; i < page.Steps.Count; i++)
            {
                Step step = page.Steps[i];
                if (pinnedText.Length > 0) pinnedText.Append('\n');
                if (step.House) pinnedText.Append("<color=#c0501c><b>HOUSE</b></color>  ");
                else pinnedText.Append("<b>").Append(step.Number).Append("</b>  ");
                pinnedText.Append(step.Text);
            }

            pinnedBody.text = pinnedText.ToString();
        }

        private void ShowPinned(bool show)
        {
            if (show == pinnedShown) return;
            pinnedShown = show;
            pinnedCanvas.SetActive(show);
        }

        private void PressMore()
        {
            PutDownNow();
            openFullRulebook?.Invoke();
        }

        // "DRAW 5 CARDS" stays; "YOUR TURN" reads "Your turn" like the board's titles.
        private static string SentenceCase(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsLower(text[i])) return text; // written in its own case
            }

            return text.Substring(0, 1) + text.Substring(1).ToLowerInvariant();
        }

        // "Draw. Draw 5 cards …" / "Move: up to 2 tiles …": the rule's first word or two in bold, as on the board.
        private static string BoldLead(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            int cut = text.IndexOfAny(LeadEnds);
            if (cut <= 0 || cut > 22 || cut == text.Length - 1) return text;
            return "<b>" + text.Substring(0, cut + 1) + "</b>" + text.Substring(cut + 1);
        }

        private static readonly char[] LeadEnds = { '.', ':' };

        private static void Listen(Button button, UnityEngine.Events.UnityAction action)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
    }
}
