using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Editor.UI
{
    /// <summary>
    /// UI-1 (owner, 2026-10-24): the designer's frames from the Figma UI Kit (exported by the owner into
    /// Content/UI/Figma/Components at 2x) on the Final UI panels: the grey frame with corner plates on table cards,
    /// the orange frame on popups and menus, the end-capped bars for the round strip and the controls strip, the
    /// hint bar and the Log frame. Part of Console Cards > UI > Apply Kit Look (UI-1); panels already framed are left
    /// alone, so running it again changes nothing.
    /// </summary>
    public static class KitFrames
    {
        public const string FigmaPath = "Assets/ConsoleCards/Content/UI/Figma/Components/";

        // The 2x exports are drawn at 1440 wide; the UI scales from 1920: one export pixel is 2/3 of a UI unit.
        private const float FramePixelsPerUnit = 1.5f;

        private const string GreyFrame = "Rules";
        private const string OrangeFrame = "Player Profile";
        private const string RoundBar = "Top";
        private const string ControlsBar = "Controls";
        private const string HintBar = "Hint";
        private const string LogFrame = "Log";

        // Export file -> 9-slice border (left, bottom, right, top) in export pixels. Bars keep their whole height.
        private static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>
        {
            { GreyFrame, new Vector4(61f, 66f, 61f, 66f) },
            { OrangeFrame, new Vector4(62f, 66f, 62f, 66f) },
            { RoundBar, new Vector4(70f, 0f, 70f, 0f) },
            { ControlsBar, new Vector4(70f, 0f, 70f, 0f) },
            { HintBar, new Vector4(161f, 0f, 50f, 0f) },
            { LogFrame, new Vector4(30f, 32f, 31f, 31f) },
        };

        // Popups and menus take the orange frame (doc 36 §7); the rest are table cards (grey frame).
        private static readonly HashSet<string> OrangePrefabs = new HashSet<string>
        {
            "TabletopPopup", "QuantityPopup", "CardInspect", "AbilityPurchase", "FocusedCardSelection",
            "ProfilePanel", "PersonaPanel", "CardEditorPanel", "DeckEditorPanel", "GameSelectPanel",
            "GameSavePanel", "TablePanel", "RuleProposalPanel", "HotSeatHandover", "CardArtPanel", "SessionBar",
        };

        public static void Prepare()
        {
            foreach (KeyValuePair<string, Vector4> frame in Borders)
            {
                string path = FigmaPath + frame.Key + ".png";
                if (File.Exists(path)) UiKit.ConfigureTexture(path, frame.Value);
            }
        }

        public static void Apply(GameObject root, Action counted)
        {
            if (Frame(GreyFrame) == null) return; // the Figma exports are not in the project yet
            if (V5Hud.Handles(root.name)) return; // the table HUD is laid out and skinned whole by V5Hud

            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                switch (image.gameObject.name)
                {
                    case "TurnStrip":
                        SetBar(image, RoundBar, counted);
                        break;
                    case "ControlsStrip":
                        SetBar(image, ControlsBar, counted);
                        break;
                }
            }

            // The outermost panels of the prefab: the backdrop fills nearest the root.
            List<Image> fills = new List<Image>();
            int shallowest = int.MaxValue;
            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                // Already framed fills count too, so a second run frames the same panels (and nothing nested).
                if (image.gameObject.name != "Fill" || !(IsPanelSprite(image.sprite) || IsFrameSprite(image.sprite))) continue;
                if (image.transform.parent != null && image.transform.parent.name == "OpenButton") continue; // V5Hud
                int depth = Depth(image.transform, root.transform);
                if (depth < shallowest)
                {
                    shallowest = depth;
                    fills.Clear();
                }

                if (depth == shallowest) fills.Add(image);
            }

            foreach (Image fill in fills)
            {
                switch (root.name)
                {
                    case "HintStrip":
                        SetBar(fill, HintBar, counted);
                        break;
                    case "FeedbackBanner":
                        SetBar(fill, RoundBar, counted);
                        break;
                    case "ActivityLog":
                        SetFrame(fill, LogFrame, counted);
                        break;
                    default:
                        SetFrame(fill, OrangePrefabs.Contains(root.name) ? OrangeFrame : GreyFrame, counted);
                        break;
                }

                QuietPanelParts(fill.transform.parent);
            }

            // The Log's tab opens it: the same frame.
            if (root.name == "ActivityLog")
            {
                Transform tab = root.transform.Find("Tab");
                Image tabImage = tab != null ? tab.GetComponent<Image>() : null;
                if (tabImage != null) SetFrame(tabImage, LogFrame, counted);
            }
        }

        // A stretchable frame: corners at their drawn size.
        private static void SetFrame(Image image, string frame, Action counted)
        {
            Sprite sprite = Frame(frame);
            if (sprite == null || image.sprite == sprite) return;
            image.sprite = sprite;
            image.color = Color.white;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = FramePixelsPerUnit;
            counted();
        }

        // A bar of fixed height: its caps scale with the bar's height, so the screws stay round.
        private static void SetBar(Image image, string frame, Action counted)
        {
            Sprite sprite = Frame(frame);
            if (sprite == null || image.sprite == sprite) return;
            float height = ((RectTransform)image.transform).rect.height;
            image.sprite = sprite;
            image.color = Color.white;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = height > 1f ? sprite.rect.height / height : FramePixelsPerUnit;
            counted();
        }

        // The frame draws the panel: its drop shadow goes, and the old orange header band and the cream body
        // turn clear (they keep catching clicks).
        private static void QuietPanelParts(Transform panel)
        {
            if (panel == null) return;
            for (int i = 0; i < panel.childCount; i++)
            {
                Transform part = panel.GetChild(i);
                Image image = part.GetComponent<Image>();
                if (image == null) continue;
                string sprite = image.sprite != null ? image.sprite.name : string.Empty;
                if (part.name == "Shadow")
                {
                    image.enabled = false;
                }
                else if ((part.name == "Header" && sprite == "TopRounded") || (part.name == "Body" && sprite == "BottomRounded"))
                {
                    Color clear = image.color;
                    clear.a = 0f;
                    image.color = clear;
                }
            }
        }

        private static bool IsPanelSprite(Sprite sprite)
        {
            return sprite != null && (sprite.name == "KitPanel" || sprite.name == "PanelOutlined");
        }

        private static bool IsFrameSprite(Sprite sprite)
        {
            return sprite != null && Borders.ContainsKey(sprite.name)
                && UnityEditor.AssetDatabase.GetAssetPath(sprite).StartsWith(FigmaPath, StringComparison.Ordinal);
        }

        private static int Depth(Transform at, Transform root)
        {
            int depth = 0;
            for (; at != null && at != root; at = at.parent) depth++;
            return depth;
        }

        private static Sprite Frame(string name)
        {
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(FigmaPath + name + ".png");
        }
    }
}
