using System;
using ConsoleCards.Presentation.Feedback;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.UI;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// PERSONA-2 (owner, 2026-10-21: "a proper lite customization"): the Persona panel, opened from the Profile panel.
    /// Your character turns in a preview while you choose its body, build, skin, eyes, hair, hair colour, beard,
    /// outfit, top, bottom and shoe colours and chest emblem; Randomize picks everything at random. Every change is
    /// saved in your profile at once, and your character at the table changes with it. Done goes back to the
    /// Profile panel. The choices and their names come from the Persona set and the character prefab.
    /// </summary>
    internal sealed class PersonaPanelPresenter
    {
        private static readonly string[] Titles =
        {
            "BODY", "BUILD", "SKIN", "EYES", "HAIR", "HAIR COLOUR", "BEARD", "OUTFIT", "TOP", "BOTTOM", "SHOES", "EMBLEM",
        };

        private readonly TabletopComposition host;
        private readonly System.Random random = new System.Random();
        private PersonaPanelActions actions;

        internal PersonaPanelPresenter(TabletopComposition host)
        {
            this.host = host;
        }

        internal void Open()
        {
            PersonaSet set = host.PersonaSetForProfile;
            if (set == null || set.Character == null)
            {
                host.ShowMessage("Your Persona needs the characters: run Console Cards > Feedback > Create Missing Persona Prefabs (Final World).");
                return;
            }

            host.runtimeUi?.CloseProfilePanel();
            host.StartPersonaPreview();
            Show("Choose how you look at the table. Every change is saved in your profile.");
        }

        internal void Show(string status)
        {
            if (host.runtimeUi == null) return;
            PersonaSet set = host.PersonaSetForProfile;
            if (set == null || set.Character == null) return;
            ProfileInfo profile = host.EnsureProfile().Active;
            PersonaLook look = profile.persona ?? new PersonaLook();
            host.ApplyPersonaPreview(look, profile.displayName);
            if (actions == null)
            {
                actions = new PersonaPanelActions
                {
                    Cycle = Cycle,
                    Randomize = Randomize,
                    Turn = host.TurnPersonaPreview,
                    Close = Close,
                };
            }

            string[] values = new string[PersonaLook.SlotCount];
            for (int slot = 0; slot < PersonaLook.SlotCount; slot++) values[slot] = ValueName(set, slot, look.Get(slot));
            PersonaPanelModel model = new PersonaPanelModel
            {
                Titles = Titles,
                Values = values,
                Preview = host.StartPersonaPreview(),
                Status = status ?? string.Empty,
            };
            if (!host.runtimeUi.ShowPersonaPanel(model, actions))
            {
                host.StopPersonaPreview();
                host.ShowMessage("The Persona panel is missing: run Console Cards > UI > Create Missing Gameplay UI Prefabs.");
            }
        }

        internal void Close()
        {
            host.runtimeUi?.ClosePersonaPanel();
            host.StopPersonaPreview();
            host.ProfilePanel.ShowProfilePanel(string.Empty, true);
        }

        private void Cycle(int slot, int step)
        {
            PersonaSet set = host.PersonaSetForProfile;
            if (set == null || set.Character == null || slot < 0 || slot >= PersonaLook.SlotCount) return;
            PersonaLook look = (host.EnsureProfile().Active.persona ?? new PersonaLook()).Copy();
            int count = Math.Max(1, ChoiceCount(set, slot));
            int current = PersonaSet.Wrap(look.Get(slot), count);
            look.Set(slot, PersonaSet.Wrap(current + step, count));
            Save(look);
        }

        private void Randomize()
        {
            PersonaSet set = host.PersonaSetForProfile;
            if (set == null || set.Character == null) return;
            PersonaLook look = new PersonaLook();
            for (int slot = 0; slot < PersonaLook.SlotCount; slot++)
            {
                look.Set(slot, random.Next(Math.Max(1, ChoiceCount(set, slot))));
            }

            Save(look);
        }

        private void Save(PersonaLook look)
        {
            string status = host.EnsureProfile().SavePersona(look, out string error)
                ? "Saved in your profile; your character at the table changes too."
                : $"Could not save your Persona: {error}.";
            Show(status);
        }

        private static int ChoiceCount(PersonaSet set, int slot)
        {
            PersonaView view = set.Character;
            switch (slot)
            {
                case PersonaLook.Body: return view.BodyCount;
                case PersonaLook.Build: return set.BuildCount;
                case PersonaLook.Skin: return set.SkinCount;
                case PersonaLook.Eyes: return set.EyeCount;
                case PersonaLook.Hair: return view.HairChoiceCount;
                case PersonaLook.HairColour: return set.HairColourCount;
                case PersonaLook.Beard: return 2;
                case PersonaLook.Outfit: return set.OutfitCount;
                case PersonaLook.Top:
                case PersonaLook.Bottom:
                case PersonaLook.Shoes: return set.ClothColourCount;
                default: return set.EmblemCount;
            }
        }

        private static string ValueName(PersonaSet set, int slot, int value)
        {
            PersonaView view = set.Character;
            switch (slot)
            {
                case PersonaLook.Body: return view.BodyName(value);
                case PersonaLook.Build: return set.BuildName(value);
                case PersonaLook.Skin: return set.Skin(value).name;
                case PersonaLook.Eyes: return set.Eye(value).name;
                case PersonaLook.Hair: return view.HairName(value);
                case PersonaLook.HairColour: return set.HairColour(value).name;
                case PersonaLook.Beard: return PersonaSet.Wrap(value, 2) == 1 ? "Beard" : "None";
                case PersonaLook.Outfit: return set.OutfitName(value);
                case PersonaLook.Top:
                case PersonaLook.Bottom:
                case PersonaLook.Shoes: return set.Cloth(value).name;
                default:
                    Texture2D emblem = set.Emblem(value);
                    return emblem != null ? EmblemName(emblem.name) : "None";
            }
        }

        // Emblem1..4 are the Console's circle, triangle, square and plus.
        private static string EmblemName(string pictureName)
        {
            switch (pictureName)
            {
                case "Emblem1": return "Circle";
                case "Emblem2": return "Triangle";
                case "Emblem3": return "Square";
                case "Emblem4": return "Plus";
                default: return pictureName;
            }
        }
    }
}
