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
    /// <summary>Card art, card looks, labels and art materials. (R1a: moved here unchanged from TabletopComposition.cs.)</summary>
    public sealed partial class TabletopComposition
    {
        // Board (floor) card backs change per round: round 1 uses the first back, round 2 the second,
        // and so on, repeating after the last (doc 24). Null when the game has no backs.
        private Texture2D CurrentBoardCardBack()
        {
            if (trapFloorGameDefinition == null)
            {
                return null;
            }

            int count = BoardCardBackCount();
            if (count == 0)
            {
                return null;
            }

            int round = trapFloorRoundState != null ? Math.Max(1, trapFloorRoundState.CurrentRoundNumber) : 1;
            int index = (round - 1) % count;
            IReadOnlyList<Texture2D> backs = trapFloorGameDefinition.BoardCardBacks;
            IReadOnlyList<string> keys = trapFloorGameDefinition.BoardCardBackReferences;
            return Art(
                keys != null && index < keys.Count ? keys[index] : null,
                backs != null && index < backs.Count ? backs[index] : null);
        }

        // A back counts when it has a texture or a key (a pack may supply the picture for a key).
        private int BoardCardBackCount()
        {
            if (trapFloorGameDefinition == null) return 0;
            int textures = trapFloorGameDefinition.BoardCardBacks != null ? trapFloorGameDefinition.BoardCardBacks.Count : 0;
            int keys = trapFloorGameDefinition.BoardCardBackReferences != null
                ? trapFloorGameDefinition.BoardCardBackReferences.Count
                : 0;
            return Math.Max(textures, keys);
        }

        // ---------- A2: art by key ----------

        private Texture2D Art(string key, Texture2D builtIn)
        {
            EnsureContentArt();
            return contentArt.Resolve(key, builtIn);
        }

        private Texture2D FrontArt(CardDefinition definition) =>
            definition == null ? null : Art(definition.FrontArtworkReference, definition.FrontArtwork);

        private Texture2D BackArt(CardDefinition definition) =>
            definition == null ? null : Art(definition.BackArtworkReference, definition.BackArtwork);

        private Texture2D AvatarArt(AvatarDefinition avatar) =>
            avatar == null ? null : Art(avatar.ArtworkReference, avatar.Artwork);

        // The player profile (doc 28): the one used last, or a new "Player 1". Choosing another profile comes with the
        // profile screen; until then the stores are made once for this profile.
        private ProfileService EnsureProfile()
        {
            if (profiles == null) profiles = ProfileService.CreateDefault();
            profiles.EnsureActive();
            return profiles;
        }

        // The content service for this table's game: built-in art, the active pack and your pictures (A2–A4).
        private void EnsureContentArt()
        {
            if (contentArt == null)
            {
                contentArt = new ContentArtService(ContentPackStore.ForProfile(EnsureProfile()));
                contentArt.Library.Changed += HandleArtChanged;
                contentArt.Library.TextureReleased += HandleArtTextureReleased;
            }

            if (!contentArt.HasGame && trapFloorGameDefinition != null)
            {
                contentArt.UseGame(trapFloorGameDefinition);
            }
        }

        // A picture changed: re-apply art on every card in place (no table rebuild).
        private void HandleArtChanged(string key)
        {
            RefreshAllCardArt();
        }

        private void RefreshAllCardArt()
        {
            if (!IsInitialized || matchState == null || artRefreshing) return;
            artRefreshing = true;
            try
            {
                for (int i = 0; i < cardVisualReferences.Count; i++)
                {
                    CardVisualReferences references = cardVisualReferences[i];
                    CardView view = references != null ? references.CardView : null;
                    if (view == null || !view.IsBound || view.CardState == null) continue;
                    ConfigureCardVisuals(references, view.CardState, ResolveCardLabel(view.CardState));
                    references.SetCardContentVisible(ShouldShowCardContent(view.CardState));
                }
            }
            finally
            {
                artRefreshing = false;
            }
        }

        // ---------- Card Art panel (doc 24, A3/A4) ----------

        private void OpenCardArtPanel()
        {
            EnsureContentArt();
            if (!contentArt.HasGame)
            {
                ShowMessage("This table has no card art to change.");
                return;
            }

            ShowCardArtPanel(string.Empty);
        }

        private void ShowCardArtPanel(string status)
        {
            if (runtimeUi == null || contentArt == null) return;
            IReadOnlyList<ContentArtEntry> entries = contentArt.Entries;
            List<ContentArtRowModel> rows = new List<ContentArtRowModel>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                ContentArtEntry entry = entries[i];
                rows.Add(new ContentArtRowModel(
                    entry.Key,
                    entry.Label,
                    contentArt.SourceText(entry.Key),
                    contentArt.Resolve(entry.Key, entry.BuiltIn),
                    contentArt.HasUpload(entry.Key)));
            }

            string text = status;
            if (string.IsNullOrEmpty(text))
            {
                text = entries.Count == 0
                    ? "No pictures have art keys yet: run Console Cards > Content > Fill Missing Art Keys."
                    : PictureFilePicker.IsAvailable
                        ? "Choose a picture for any card. Your pictures are saved in your profile."
                        : $"Choosing pictures is not available here. Packs go in {contentArt.Store.PacksFolder(contentArt.GameId)}.";
            }

            if (cardArtActions == null)
            {
                cardArtActions = new ContentArtPanelActions
                {
                    CyclePack = CycleArtPack,
                    OpenFolder = OpenArtPacksFolder,
                    Reload = ReloadContent,
                    ChoosePicture = ChooseOwnPicture,
                    ResetPicture = ResetOwnPicture,
                    Close = CloseCardArtPanel,
                };
            }

            if (!runtimeUi.ShowContentArtPanel(
                    new ContentArtPanelModel(contentArt.ActivePackName, contentArt.Packs.Count, text, rows,
                        PictureFilePicker.IsAvailable),
                    cardArtActions))
            {
                ShowMessage("The Card Art panel is missing: run Console Cards > UI > Create Missing Gameplay UI Prefabs.");
            }
        }

        private void CycleArtPack(int step)
        {
            contentArt.ActivatePack(contentArt.NeighbourPackId(step), out string message);
            ShowMessage(message);
            ShowCardArtPanel(message);
        }

        private void OpenArtPacksFolder()
        {
            string folder = contentArt.Store.PacksFolder(contentArt.GameId);
            try
            {
                System.IO.Directory.CreateDirectory(folder);
                UnityEngine.Application.OpenURL(new Uri(folder).AbsoluteUri);
                ShowCardArtPanel($"Packs folder: {folder}. Put a pack folder there, then press Reload.");
            }
            catch (Exception exception)
            {
                ShowCardArtPanel($"Could not open {folder}: {exception.Message}");
            }
        }

        private void ReloadContent()
        {
            contentArt.ReloadFromDisk();
            int packs = contentArt.Packs.Count;
            ShowCardArtPanel($"Found {packs} pack{(packs == 1 ? string.Empty : "s")}. Active: {contentArt.ActivePackName}.");
        }

        private void ChooseOwnPicture(string key)
        {
            if (!PictureFilePicker.TryPick("Choose a picture (PNG or JPG)", out string path))
            {
                return;
            }

            contentArt.SetUpload(key, path, out string message);
            ShowMessage(message);
            ShowCardArtPanel(message);
        }

        private void ResetOwnPicture(string key)
        {
            contentArt.RemoveUpload(key, out string message);
            ShowMessage(message);
            ShowCardArtPanel(message);
        }

        // A texture the library loaded is about to be destroyed: drop the card materials built from it.
        private void HandleArtTextureReleased(Texture2D texture)
        {
            List<(Material, Texture2D)> stale = null;
            foreach (KeyValuePair<(Material, Texture2D), Material> pair in cardArtMaterials)
            {
                if (ReferenceEquals(pair.Key.Item2, texture))
                {
                    if (stale == null) stale = new List<(Material, Texture2D)>(2);
                    stale.Add(pair.Key);
                }
            }

            for (int i = 0; stale != null && i < stale.Count; i++)
            {
                Material material = cardArtMaterials[stale[i]];
                cardArtMaterials.Remove(stale[i]);
                if (material == null) continue;
                cardArtBaseMaterials.Remove(material);
                Destroy(material);
            }
        }

        [ContextMenu("Apply Art Test")]
        private void ApplyArtTest()
        {
            if (string.IsNullOrWhiteSpace(artTestKey) || artTestTexture == null)
            {
                Debug.LogWarning("[Art] Set Art Test Key and Art Test Texture first.", this);
                return;
            }

            EnsureContentArt();
            contentArt.Library.Set(ArtLayer.PlayerUpload, artTestKey, artTestTexture, false);
            Debug.Log($"[Art] {ArtLibrary.NormalizeKey(artTestKey)} now shows {artTestTexture.name} (upload layer).", this);
        }

        [ContextMenu("Clear Art Test")]
        private void ClearArtTest()
        {
            EnsureContentArt();
            if (!contentArt.Library.Remove(ArtLayer.PlayerUpload, artTestKey))
            {
                Debug.Log("[Art] Nothing to clear for that key.", this);
            }
        }

        // Only does work on the frame the round number changes; re-applies floor backs then.
        private void RefreshBoardCardBacksIfRoundChanged()
        {
            int round = trapFloorRoundState != null ? trapFloorRoundState.CurrentRoundNumber : 0;
            if (round == boardCardBacksRound)
            {
                return;
            }

            boardCardBacksRound = round;
            if (trapFloorTemplate == null
                || matchState == null
                || trapFloorGameDefinition == null
                || BoardCardBackCount() < 2)
            {
                return;
            }

            foreach (TabletopObjectId floorCardId in trapFloorTemplate.FloorCardIds.Values)
            {
                RefreshTrapFloorFloorCardPresentation(floorCardId);
            }
        }

        private bool TryGetAuthoredAvatar(ObjectDefinitionId definitionId, out AvatarDefinition avatar)
        {
            avatar = null;
            if (trapFloorTemplate == null || trapFloorGameDefinition == null)
            {
                return false;
            }

            IReadOnlyList<AvatarDefinition> avatars = trapFloorGameDefinition.Avatars;
            for (int i = 0; i < avatars.Count; i++)
            {
                AvatarDefinition candidate = avatars[i];
                if (candidate != null
                    && Guid.TryParse(candidate.StableId, out Guid avatarGuid)
                    && new ObjectDefinitionId(avatarGuid) == definitionId)
                {
                    avatar = candidate;
                    return true;
                }
            }

            return false;
        }

        private CardSizeClass CardSizeClassOf(CardInstanceState card)
        {
            if (TryGetCardLook(card.BaseState.DefinitionId, out CardDefinition definition))
            {
                return definition.SizeClass;
            }

            return TryGetAvatarLook(card.BaseState.DefinitionId, out AvatarDefinition avatar)
                ? avatar.CardSize
                : CardSizeClass.Standard;
        }

        // How a card looks (name, art, size): its game's card definition, found in this table's game or in any
        // game box of the library. Presentation only; game rules keep using TryGetAuthoredCardDefinition.
        private bool TryGetCardLook(ObjectDefinitionId definitionId, out CardDefinition definition)
        {
            if (TryGetAuthoredCardDefinition(definitionId, out definition)) return true;
            definition = null;
            if (definitionId.IsEmpty) return false;
            if (trapFloorContent != null && trapFloorContent.TryGetCard(definitionId, out definition)) return true;
            if (trapFloorGameDefinition != null && trapFloorGameDefinition.TryGetCard(definitionId, out definition)) return true;
            IReadOnlyList<ComponentCatalog> boxes = componentLibrary != null ? componentLibrary.GameBoxes : null;
            for (int i = 0; boxes != null && i < boxes.Count; i++)
            {
                GameDefinition game = boxes[i] != null ? boxes[i].Game : null;
                if (game != null && game.TryGetCard(definitionId, out definition)) return true;
            }

            definition = null;
            return false;
        }

        private bool TryGetAvatarLook(ObjectDefinitionId definitionId, out AvatarDefinition avatar)
        {
            if (TryGetAuthoredAvatar(definitionId, out avatar)) return true;
            if (definitionId.IsEmpty) return false;
            if (FindAvatar(trapFloorGameDefinition, definitionId, out avatar)) return true;
            IReadOnlyList<ComponentCatalog> boxes = componentLibrary != null ? componentLibrary.GameBoxes : null;
            for (int i = 0; boxes != null && i < boxes.Count; i++)
            {
                if (boxes[i] != null && FindAvatar(boxes[i].Game, definitionId, out avatar)) return true;
            }

            avatar = null;
            return false;
        }

        private static bool FindAvatar(GameDefinition game, ObjectDefinitionId definitionId, out AvatarDefinition avatar)
        {
            avatar = null;
            IReadOnlyList<AvatarDefinition> avatars = game != null ? game.Avatars : null;
            for (int i = 0; avatars != null && i < avatars.Count; i++)
            {
                if (avatars[i] != null
                    && Guid.TryParse(avatars[i].StableId, out Guid avatarGuid)
                    && new ObjectDefinitionId(avatarGuid) == definitionId)
                {
                    avatar = avatars[i];
                    return true;
                }
            }

            return false;
        }

        private static Color TrapFloorContentColor(TrapFloorFloorContentCategory category)
        {
            switch (category)
            {
                case TrapFloorFloorContentCategory.Trap:
                    return new Color(0.86f, 0.34f, 0.28f);
                case TrapFloorFloorContentCategory.Friend:
                    return new Color(0.40f, 0.76f, 0.70f);
                case TrapFloorFloorContentCategory.Key:
                    return new Color(0.95f, 0.78f, 0.26f);
                case TrapFloorFloorContentCategory.SecretExit:
                    return new Color(0.56f, 0.48f, 0.84f);
                case TrapFloorFloorContentCategory.Entry:
                    return new Color(0.40f, 0.70f, 0.42f);
                case TrapFloorFloorContentCategory.Ability:
                    return new Color(0.42f, 0.66f, 0.90f);
                default:
                    return new Color(0.95f, 0.88f, 0.42f);
            }
        }

        private bool HidesTableVisual(TableVisualHide part)
        {
            return (tableVisualHide & part) != 0;
        }

        private void ApplyFixedContainerVisualHide(FixedContainerVisual visual, bool labelIsDecoration)
        {
            visual.Label.gameObject.SetActive(
                !labelIsDecoration || !HidesTableVisual(TableVisualHide.ContainerLabels));
            visual.SetBasePlateHidden(HidesTableVisual(TableVisualHide.ContainerPlates));
        }

        private void ApplySelectionHighlightHide(TabletopSelectionVisual selectionVisual)
        {
            bool visible = !HidesTableVisual(TableVisualHide.SelectionHighlight);
            Renderer[] renderers = selectionVisual.HighlightRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = visible;
            }
        }

        private void ApplyLabelRendererHide(TextMesh label, TableVisualHide part)
        {
            Renderer labelRenderer = label != null ? label.GetComponent<Renderer>() : null;
            if (labelRenderer != null)
            {
                labelRenderer.enabled = !HidesTableVisual(part);
            }
        }

        private static void ConfigureContainerLabel(TextMesh label, string text)
        {
            ConfigureWorldLabel(
                label,
                text,
                TrapFloorContainerLabelCharacterSize,
                TrapFloorContainerLabelFontSize);
        }

        private static void ConfigureWorldLabel(
            TextMesh label,
            string text,
            float characterSize,
            int fontSize)
        {
            label.text = text;
            label.characterSize = characterSize;
            label.fontSize = fontSize;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.lineSpacing = 0.8f;
            WorldTextDepth.Apply(label);
        }

        private static void ApplyCardColor(Renderer renderer, Color color)
        {
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            properties.SetFloat("_Metallic", 0f);
            properties.SetFloat("_Smoothness", 0.12f);
            renderer.SetPropertyBlock(properties);
        }

        private void ApplyCardArtwork(Renderer renderer, Texture2D artwork)
        {
            Material current = renderer.sharedMaterial;
            if (current == null)
            {
                return;
            }

            Material wanted = CardArtMaterial(CardArtBaseMaterial(current), artwork);
            if (!ReferenceEquals(current, wanted))
            {
                renderer.sharedMaterial = wanted;
            }
        }

        // The supplied card model (Trading Card FBX) has a "back" material slot and a "face" slot.
        // The face slot carries the front art, the back slot the back art; each slot is tinted on its own.
        private void ApplySuppliedModelArtwork(
            CardVisualReferences visualReferences,
            Texture2D frontArtwork,
            Texture2D backArtwork,
            Color frontTint,
            Color backTint)
        {
            if (cardSlotProperties == null)
            {
                cardSlotProperties = new MaterialPropertyBlock();
            }

            IReadOnlyList<Renderer> renderers = visualReferences.SuppliedModelRenderers;
            for (int rendererIndex = 0; rendererIndex < renderers.Count; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer == null)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int slot = 0; slot < materials.Length; slot++)
                {
                    if (materials[slot] == null)
                    {
                        continue;
                    }

                    Material baseMaterial = CardArtBaseMaterial(materials[slot]);
                    bool isBackSlot = IsCardBackMaterial(baseMaterial, slot);
                    Material wanted = CardArtMaterial(baseMaterial, isBackSlot ? backArtwork : frontArtwork);
                    if (!ReferenceEquals(materials[slot], wanted))
                    {
                        materials[slot] = wanted;
                        changed = true;
                    }

                    Color tint = isBackSlot ? backTint : frontTint;
                    renderer.GetPropertyBlock(cardSlotProperties, slot);
                    cardSlotProperties.SetColor("_BaseColor", tint);
                    cardSlotProperties.SetColor("_Color", tint);
                    renderer.SetPropertyBlock(cardSlotProperties, slot);
                    cardSlotProperties.Clear();
                }

                if (changed)
                {
                    renderer.sharedMaterials = materials;
                }
            }
        }

        // Slots are told apart by the model's material names ("back" / "face"); slot 0 is the back otherwise.
        private static bool IsCardBackMaterial(Material baseMaterial, int slot)
        {
            string name = baseMaterial.name;
            if (name.IndexOf("back", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (name.IndexOf("face", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            return slot == 0;
        }

        private Material CardArtBaseMaterial(Material material)
        {
            return cardArtBaseMaterials.TryGetValue(material, out Material knownBase) ? knownBase : material;
        }

        private Material CardArtMaterial(Material baseMaterial, Texture2D artwork)
        {
            if (artwork == null)
            {
                return baseMaterial;
            }

            if (!cardArtMaterials.TryGetValue((baseMaterial, artwork), out Material material))
            {
                material = new Material(baseMaterial) { name = $"{baseMaterial.name} ({artwork.name})" };
                material.SetTexture("_BaseMap", artwork);
                material.SetTexture("_MainTex", artwork);
                cardArtMaterials.Add((baseMaterial, artwork), material);
                cardArtBaseMaterials.Add(material, baseMaterial);
            }

            return material;
        }

        private void ReleaseCardArtMaterials()
        {
            foreach (Material material in cardArtMaterials.Values)
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }

            cardArtMaterials.Clear();
            cardArtBaseMaterials.Clear();
        }

        private bool TryGetAuthoredCardDefinition(
            ObjectDefinitionId definitionId,
            out CardDefinition definition)
        {
            if (trapFloorTemplate != null
                && trapFloorContent != null
                && trapFloorContent.TryGetCard(definitionId, out definition))
            {
                return true;
            }

            if (trapFloorTemplate != null
                && trapFloorGameDefinition != null
                && trapFloorGameDefinition.TryGetCard(definitionId, out definition))
            {
                return true;
            }

            definition = null;
            return false;
        }
    }
}
