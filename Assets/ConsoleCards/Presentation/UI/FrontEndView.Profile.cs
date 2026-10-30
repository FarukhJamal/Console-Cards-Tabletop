using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// PROFILE-2 (owner, 2026-10-31): board 19 "Player profile v2" (FE_Profile: the players on this computer, the
    /// player playing now with name, colour, avatar and stats, DELETE PLAYER asking again in place, PLAY AS) and board
    /// 19b's switch-player list under the top bar's chip. Built by Console Cards > UI > Build Board Screens.
    /// </summary>
    public sealed partial class FrontEndView
    {
        private const int ProfileBoardRows = 5;
        private const int ProfileBoardAvatars = 8;
        private const int ProfileBoardColours = 4;
        private const int SwitchRows = 5;
        private static readonly Color RowActiveFill = new Color(0.110f, 0.235f, 0.239f, 1f);
        private static readonly Color RowIdleFill = new Color(0.133f, 0.145f, 0.153f, 1f);
        private static readonly Color RowActiveEdge = new Color(0.024f, 0.937f, 0.941f, 1f);
        private static readonly Color RowIdleEdge = new Color(0.235f, 0.255f, 0.263f, 1f);
        private static readonly Color DeleteArmedFill = new Color(0.851f, 0.204f, 0.165f, 1f);
        private static readonly Color DeleteIdleWords = new Color(1f, 0.478f, 0.420f, 1f);

        private bool HasBoardProfile => Find("Pages/Profile/Players") != null;
        private bool HasSwitchList => Find("TopBar/Switch") != null;
        private InputField profileNameField;
        private float profileRowPitch = -1f;

        private void BindProfileBoard()
        {
            if (HasSwitchList)
            {
                On("TopBar/Switch/Outside", () => ShowSwitch(false));
                for (int i = 0; i < SwitchRows; i++)
                {
                    int index = i;
                    On("TopBar/Switch/List/Row" + i, () => PickProfile(index, true));
                }

                On("TopBar/Switch/List/New", () =>
                {
                    ShowSwitch(false);
                    actions?.NewProfile?.Invoke();
                });
                On("TopBar/Switch/List/Edit", () =>
                {
                    ShowSwitch(false);
                    Go(FrontEndPage.Profile);
                });
            }

            if (!HasBoardProfile) return;
            for (int i = 0; i < ProfileBoardRows; i++)
            {
                int index = i;
                On("Pages/Profile/Players/Row" + i, () => PickProfile(index, false));
            }

            for (int i = 0; i < ProfileBoardColours; i++)
            {
                int index = i;
                On("Pages/Profile/Card/Colour" + i, () => actions?.PickProfileColour?.Invoke(index));
            }

            for (int i = 0; i < ProfileBoardAvatars; i++)
            {
                int index = i;
                On("Pages/Profile/Card/Avatar" + i, () => actions?.PickAvatar?.Invoke(index));
            }

            On("Pages/Profile/Players/New", () => actions?.NewProfile?.Invoke());
            On("Pages/Profile/Delete", () => actions?.DeleteProfile?.Invoke());
            On("Pages/Profile/PlayAs", () => Go(FrontEndPage.Library));
            Transform field = Find("Pages/Profile/Card/Name");
            profileNameField = field != null ? field.GetComponent<InputField>() : null;
            if (profileNameField != null)
            {
                profileNameField.onEndEdit.RemoveAllListeners();
                profileNameField.onEndEdit.AddListener(text => actions?.RenameProfile?.Invoke(text));
            }
        }

        // The chip: opens the switch list when the front end has it, else the profile screen.
        private void OpenProfileChip()
        {
            if (HasSwitchList) ShowSwitch(!Find("TopBar/Switch").gameObject.activeSelf);
            else Go(FrontEndPage.Profile);
        }

        // A player in the list or the switch list: the one playing now stays; another one is switched to.
        private void PickProfile(int index, bool fromSwitch)
        {
            if (fromSwitch) ShowSwitch(false);
            IReadOnlyList<FrontEndProfile> cards = model != null ? model.ProfileCards : null;
            if (cards == null || index < 0 || index >= cards.Count || cards[index].Active) return;
            actions?.PlayAs?.Invoke(cards[index].Id);
        }

        private void ShowSwitch(bool shown)
        {
            Transform list = Find("TopBar/Switch");
            if (list == null) return;
            if (list.gameObject.activeSelf != shown) list.gameObject.SetActive(shown);
            if (!shown) return;
            Canvas own = list.GetComponent<Canvas>();
            if (own != null)
            {
                // Over every screen of the front end, whatever their own sorting.
                int top = 0;
                foreach (Canvas canvas in GetComponentsInParent<Canvas>(true)) top = Mathf.Max(top, canvas.sortingOrder);
                foreach (Canvas canvas in GetComponentsInChildren<Canvas>(true))
                {
                    if (canvas != own) top = Mathf.Max(top, canvas.sortingOrder);
                }

                own.overrideSorting = true;
                own.sortingOrder = top + 10;
            }

            RenderSwitch();
        }

        private void RenderSwitch()
        {
            if (model == null) return;
            IReadOnlyList<FrontEndProfile> cards = model.ProfileCards;
            for (int i = 0; i < SwitchRows; i++)
            {
                string path = "TopBar/Switch/List/Row" + i;
                bool shown = i < cards.Count;
                SetActive(path, shown);
                if (!shown) continue;
                FillPlayer(path, cards[i]);
                SetActive(path + "/Tick", cards[i].Active);
            }

            // The list is as tall as its players: the rule, NEW PLAYER and EDIT PROFILE follow the last one.
            RectTransform first = Find("TopBar/Switch/List/Row0") as RectTransform;
            RectTransform second = Find("TopBar/Switch/List/Row1") as RectTransform;
            RectTransform box = Find("TopBar/Switch/List") as RectTransform;
            if (first == null || second == null || box == null) return;
            float pitch = first.anchoredPosition.y - second.anchoredPosition.y;
            float k = pitch / 58f;
            float end = first.anchoredPosition.y - (Mathf.Min(cards.Count, SwitchRows) * pitch);
            Place("TopBar/Switch/List/Rule", end - (4f * k));
            Place("TopBar/Switch/List/New", end - (12f * k));
            Place("TopBar/Switch/List/Edit", end - (54f * k));
            box.sizeDelta = new Vector2(box.sizeDelta.x, -(end - (54f * k)) + (48f * k));
        }

        private void Place(string path, float y)
        {
            if (Find(path) is RectTransform rect) rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
        }

        private void RenderProfileBoard()
        {
            IReadOnlyList<FrontEndProfile> cards = model.ProfileCards;
            for (int i = 0; i < ProfileBoardRows; i++)
            {
                string path = "Pages/Profile/Players/Row" + i;
                bool shown = i < cards.Count;
                SetActive(path, shown);
                if (!shown) continue;
                FrontEndProfile card = cards[i];
                FillPlayer(path, card);
                SetActive(path + "/Tag", card.Active);
                SetActive(path + "/Chevron", !card.Active);
                Paint(path, card.Active ? RowActiveFill : RowIdleFill, card.Active ? RowActiveEdge : RowIdleEdge);
            }

            // NEW PLAYER sits under the last player shown, while there is room.
            Transform add = Find("Pages/Profile/Players/New");
            Transform first = Find("Pages/Profile/Players/Row0");
            Transform second = Find("Pages/Profile/Players/Row1");
            if (add != null && first != null && second != null)
            {
                if (profileRowPitch < 0f) profileRowPitch = ((RectTransform)first).anchoredPosition.y - ((RectTransform)second).anchoredPosition.y;
                bool room = cards.Count < ProfileBoardRows;
                add.gameObject.SetActive(room);
                RectTransform addRect = (RectTransform)add;
                if (room)
                {
                    addRect.anchoredPosition = new Vector2(addRect.anchoredPosition.x,
                        ((RectTransform)first).anchoredPosition.y - (cards.Count * profileRowPitch));
                }
            }

            Color colour = model.ProfileColour >= 0 && model.ProfileColour < model.ProfileColours.Count
                ? model.ProfileColours[model.ProfileColour]
                : RowActiveEdge;
            ShowPicture(Picture("Pages/Profile/Card/Big/Picture"), model.AvatarPicture);
            Paint("Pages/Profile/Card/Big", colour, null);
            if (profileNameField != null && !profileNameField.isFocused) profileNameField.SetTextWithoutNotify(model.ProfileName);
            for (int i = 0; i < ProfileBoardColours; i++)
            {
                bool shown = i < model.ProfileColours.Count;
                SetActive("Pages/Profile/Card/Colour" + i, shown);
                SetActive("Pages/Profile/Card/ColourRing" + i, shown && i == model.ProfileColour);
                if (shown) Paint("Pages/Profile/Card/Colour" + i, model.ProfileColours[i], null);
            }

            for (int i = 0; i < ProfileBoardAvatars; i++)
            {
                bool shown = i < model.AvatarChoices.Count;
                SetActive("Pages/Profile/Card/Avatar" + i, shown);
                SetActive("Pages/Profile/Card/AvatarRing" + i, shown && i == model.AvatarIndex);
                if (shown) ShowPicture(Picture("Pages/Profile/Card/Avatar" + i + "/Picture"), model.AvatarChoices[i]);
            }

            SetText("Pages/Profile/Card/Stat0/Value", model.StatGames);
            SetText("Pages/Profile/Card/Stat1/Value", model.StatWins);
            SetText("Pages/Profile/Card/Stat2/Value", model.StatLastGame);
            SetText("Pages/Profile/Card/Stat3/Value", model.StatYourGames);
            SetActive("Pages/Profile/Delete", model.CanDeleteProfile);
            SetText("Pages/Profile/Delete/Label", model.ProfileDeleteArmed ? "DELETE? PRESS AGAIN" : "DELETE PLAYER");
            Tint("Pages/Profile/Delete/Label", model.ProfileDeleteArmed ? Color.white : DeleteIdleWords);
            Paint("Pages/Profile/Delete", model.ProfileDeleteArmed ? DeleteArmedFill : new Color(0f, 0f, 0f, 0f), null);
            SetActive("Pages/Profile/Saved", model.ProfileSaved);
            SetText("Pages/Profile/PlayAs/Label", "PLAY AS " + Upper(model.ProfileName));
        }

        // A player's avatar (framed in their colour), name and line.
        private void FillPlayer(string path, FrontEndProfile card)
        {
            SetText(path + "/Name", Upper(card.Name));
            SetText(path + "/Line", card.Line);
            ShowPicture(Picture(path + "/Avatar/Picture"), card.Avatar);
            Paint(path + "/Avatar", card.Colour, null);
        }

        // The object's own Image colour and, when given, its Outlines' colour.
        private void Paint(string path, Color fill, Color? edge)
        {
            Transform target = Find(path);
            if (target == null) return;
            Image image = target.GetComponent<Image>();
            if (image != null) image.color = fill;
            if (!edge.HasValue) return;
            target.GetComponents(outlineBuffer);
            for (int i = 0; i < outlineBuffer.Count; i++) outlineBuffer[i].effectColor = edge.Value;
        }

        private readonly List<Outline> outlineBuffer = new List<Outline>(2);
    }
}
