using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Feedback;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// AVATAR-1 (owner, 2026-10-21: "it's not working out; keep it simple and use avatars, but don't delete the
    /// customizer, just disable it"): each seat shows the player's avatar picture standing just past the table's edge
    /// behind their Console (framed in the seat colour, brighter on their turn) and their name along the rim in front
    /// of the seat. The 3D characters, the leaning tags and the Persona panel below are switched off in the Persona
    /// set and kept for later.
    /// PERSONA (owner, 2026-10-21: "show who plays at each side, like Tabletop Simulator's avatars, but our own";
    /// "think of it as a VR character at the table"; "a proper lite customization"): every seat of the game in play
    /// has the player's character (Universal Base Characters, CC0) sitting just past the table's edge behind their
    /// Console, facing the board, in full colour with an edge light in the seat colour and their name over the head;
    /// a character close to the camera fades. The seat played on this screen shows the profile's character and name
    /// (built in the Persona panel); the other seats a character of their own and "Player n". The glow is brighter,
    /// breathing, on that player's turn. Follows the Console when it moves (the edge, Arrange table). An Empty Table
    /// (no seats) has none. Ticked once a frame; it allocates only when a seat's look or name changes.
    /// </summary>
    public sealed partial class TabletopComposition
    {
        private sealed class SeatPersona
        {
            public PersonaView View;
            public PersonaTagView Tag;
            public PersonaAvatarView Avatar;
            public TextMesh RimName;
            public int AvatarIndex = int.MinValue;
            public SeatId SeatId;
            public PlayerId Occupant;
            public string Name;
            public string NameSource;
            public readonly PersonaLook Look = new PersonaLook();
            public Color Colour;
            // Where the table's edge was found behind this seat's Console (searched again when the Console moves).
            public Vector3 EdgeFrom = new Vector3(float.NaN, 0f, 0f);
            public Vector3 EdgeOutward;
            public float EdgeDistance;
            public float EdgeTop;
        }

        private readonly List<SeatPersona> seatPersonas = new List<SeatPersona>(8);
        private PersonaSet personaSet;
        private bool personaSetUnavailable;
        private Transform personaRoot;

        /// <summary>PERSONA-2: the Persona set for the Persona panel's choices (null when it is not made yet).</summary>
        internal PersonaSet PersonaSetForProfile => EnsurePersonaSet() ? personaSet : null;

        private bool EnsurePersonaSet()
        {
            if (personaSet != null) return true;
            if (personaSetUnavailable) return false;
            personaSet = Resources.Load<PersonaSet>(PersonaSet.ResourcePath);
            if (personaSet == null || (personaSet.AvatarStand == null && personaSet.Character == null))
            {
                personaSet = null;
                personaSetUnavailable = true;
                Debug.LogWarning(
                    "Seat Personas are off: run Console Cards > Feedback > Create Missing Persona Prefabs (Final World).");
                return false;
            }

            return true;
        }

        // Once a frame: one projector per seat, at its place, its look and name current, lit for the turn.
        private void TickPersonas()
        {
            TickPersonaPreview();
            if (!IsInitialized || matchState == null || rebuildingFromUndo)
            {
                HidePersonas();
                return;
            }

            IReadOnlyList<CartridgeSeat> seats = ActiveCartridge.Seats;
            if (seats.Count == 0 || !EnsurePersonaSet())
            {
                HidePersonas();
                return;
            }

            UnityEngine.Camera viewCamera = UnityEngine.Camera.main;
            PlayerId turnPlayer = ActiveCartridge.TurnPlayerId;
            float unit = classicFrame.WorldPerTableUnit > 0f ? classicFrame.WorldPerTableUnit : 1f;
            float pulse = 1f + (personaSet.TurnPulseAmount * Mathf.Sin(Time.unscaledTime * personaSet.TurnPulseSpeed * Mathf.PI * 2f));
            // The middle the seats face: the middle of the Consoles (the table's centre with fewer than two).
            Vector3 middle = classicFrame.Centre;
            int consoles = 0;
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < seats.Count; i++)
            {
                if (matchState.Seats.TryGetValue(seats[i].SeatId, out SeatState counted)
                    && counted.Console != null
                    && counted.Console.SlotContainerIds.Count > 0
                    && TryResolveConsolePlacement(counted.Console.SlotContainerIds[0], out _, out _, out ConsoleView countedView)
                    && countedView != null)
                {
                    sum += countedView.transform.position;
                    consoles++;
                }
            }

            if (consoles >= 2) middle = sum / consoles;
            int shown = 0;
            for (int i = 0; i < seats.Count; i++)
            {
                CartridgeSeat seat = seats[i];
                if (!matchState.Seats.TryGetValue(seat.SeatId, out SeatState seatState)
                    || seatState.Console == null
                    || seatState.Console.SlotContainerIds.Count == 0
                    || !TryResolveConsolePlacement(seatState.Console.SlotContainerIds[0], out _, out _, out ConsoleView consoleView)
                    || consoleView == null)
                {
                    continue;
                }

                SeatPersona persona = PersonaAt(shown++);
                PlayerId occupant = seatState.OccupantPlayerId;
                RefreshPersonaLook(persona, seat, occupant);

                Vector3 console = consoleView.transform.position;
                Vector3 outward = console - middle;
                outward.y = 0f;
                outward = outward.sqrMagnitude > 1e-6f ? outward.normalized : Vector3.back;
                Vector3 inward = -outward;
                float edge = TableEdgeBehind(persona, console, outward, unit);
                bool theirTurn = !turnPlayer.IsEmpty && occupant == turnPlayer;

                // AVATAR-1: the player's picture stands just past the table's edge behind the Console, facing the board.
                if (persona.Avatar != null)
                {
                    Vector3 avatarPlace = console + (outward * (edge + (personaSet.AvatarFromEdge * unit)));
                    avatarPlace.y = persona.EdgeTop + (personaSet.AvatarLift * unit);
                    persona.Avatar.Place(avatarPlace, Quaternion.LookRotation(outward, Vector3.up) * Quaternion.Euler(personaSet.AvatarLean, 0f, 0f), unit);
                    persona.Avatar.SetGlow(theirTurn ? personaSet.AvatarGlowTurn * pulse : personaSet.AvatarGlowIdle);
                    if (!persona.Avatar.gameObject.activeSelf) persona.Avatar.gameObject.SetActive(true);
                }

                // AVATAR-1: the name lies along the rim in front of the seat, letters to the outside, so the players across
                // the table read it.
                if (persona.RimName != null)
                {
                    Vector3 namePlace = console + (outward * (edge - (personaSet.RimNameInset * unit)));
                    namePlace.y = persona.EdgeTop + (0.02f * unit);
                    persona.RimName.transform.SetPositionAndRotation(namePlace, Quaternion.LookRotation(Vector3.down, outward));
                    persona.RimName.characterSize = personaSet.RimNameSize * unit;
                    if (!persona.RimName.gameObject.activeSelf) persona.RimName.gameObject.SetActive(true);
                }

                // The leaning name tag (off unless the set turns it on).
                if (persona.Tag != null)
                {
                    Vector3 tagPlace = console + (outward * (edge + (personaSet.TagFromEdge * unit)));
                    persona.Tag.Place(tagPlace, Quaternion.LookRotation(outward, Vector3.up) * Quaternion.Euler(personaSet.TagLean, 0f, 0f), unit);
                    persona.Tag.SetGlow(theirTurn ? personaSet.TagGlowTurn * pulse : personaSet.TagGlowIdle);
                    if (!persona.Tag.gameObject.activeSelf) persona.Tag.gameObject.SetActive(true);
                }

                // PERSONA-2: the 3D character (off unless the set turns it on), sitting past the edge, facing the board.
                if (persona.View == null) continue;
                Vector3 left = Vector3.Cross(inward, Vector3.up);
                float metre = personaSet.CharacterScale * unit;
                Vector3 place = console
                    + (outward * (edge + (personaSet.PastEdgeMetres * metre)))
                    + (left * (personaSet.SidewaysMetres * metre));
                place.y = console.y - (personaSet.FeetBelowTableMetres * metre);
                float fade = 1f;
                if (viewCamera != null)
                {
                    float distance = Vector3.Distance(viewCamera.transform.position, place);
                    fade = Mathf.Clamp01((distance - personaSet.FadeNear) / (personaSet.FadeFar - personaSet.FadeNear));
                }

                if (fade <= 0.02f)
                {
                    if (persona.View.gameObject.activeSelf) persona.View.gameObject.SetActive(false);
                    continue;
                }

                if (!persona.View.gameObject.activeSelf) persona.View.gameObject.SetActive(true);
                persona.View.Place(place, Quaternion.LookRotation(inward, Vector3.up), metre);
                persona.View.Pose(personaSet);
                persona.View.SetGlow(theirTurn ? personaSet.TurnGlow * pulse : personaSet.IdleGlow, fade);
            }

            for (int i = shown; i < seatPersonas.Count; i++) HidePersona(seatPersonas[i]);
        }

        // How far out from the Console the table's surface ends (world units), found by looking straight down at the
        // table every small step outward until nothing at table height is there. Searched only when the Console has
        // moved (or the seat changed), so a frame normally does no physics queries.
        private float TableEdgeBehind(SeatPersona persona, Vector3 console, Vector3 outward, float unit)
        {
            if (!float.IsNaN(persona.EdgeFrom.x)
                && (persona.EdgeFrom - console).sqrMagnitude < 0.0004f * unit * unit
                && Vector3.Dot(persona.EdgeOutward, outward) > 0.999f)
            {
                return persona.EdgeDistance;
            }

            float step = 0.15f * unit;
            float limit = 40f * unit;
            float distance = 0f;
            for (float d = 0f; d <= limit; d += step)
            {
                Vector3 origin = console + (outward * d) + (Vector3.up * (3f * unit));
                if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 6f * unit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    || hit.point.y < console.y - (0.25f * unit))
                {
                    break;
                }

                distance = d;
                persona.EdgeTop = hit.point.y;
            }

            persona.EdgeFrom = console;
            persona.EdgeOutward = outward;
            persona.EdgeDistance = distance;
            return distance;
        }

        private SeatPersona PersonaAt(int index)
        {
            while (seatPersonas.Count <= index)
            {
                if (personaRoot == null)
                {
                    personaRoot = new GameObject("SeatPersonas").transform;
                    personaRoot.SetParent(transform, false);
                }

                int number = seatPersonas.Count + 1;
                PersonaView view = null;
                if (personaSet.ShowCharacters && personaSet.Character != null)
                {
                    view = Instantiate(personaSet.Character, personaRoot);
                    view.name = $"SeatPersona{number}";
                    if (view.NameLabel != null) WorldTextDepth.Apply(view.NameLabel);
                }

                PersonaAvatarView avatar = null;
                if (personaSet.AvatarStand != null)
                {
                    avatar = Instantiate(personaSet.AvatarStand, personaRoot);
                    avatar.name = $"SeatAvatar{number}";
                }

                TextMesh rimName = null;
                if (personaSet.NameOnRim)
                {
                    GameObject nameObject = new GameObject($"SeatName{number}");
                    nameObject.transform.SetParent(personaRoot, false);
                    rimName = nameObject.AddComponent<TextMesh>();
                    rimName.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    rimName.fontSize = 64;
                    rimName.fontStyle = FontStyle.Bold;
                    rimName.anchor = TextAnchor.MiddleCenter;
                    rimName.alignment = TextAlignment.Center;
                    rimName.richText = false;
                    MeshRenderer nameRenderer = nameObject.GetComponent<MeshRenderer>();
                    if (rimName.font != null) nameRenderer.sharedMaterial = rimName.font.material;
                    nameRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    WorldTextDepth.Apply(rimName);
                }

                PersonaTagView tag = null;
                if (personaSet.ShowSeatTags && personaSet.SeatTag != null)
                {
                    tag = Instantiate(personaSet.SeatTag, personaRoot);
                    tag.name = $"SeatTag{seatPersonas.Count + 1}";
                    if (tag.NameLabel != null) WorldTextDepth.Apply(tag.NameLabel);
                }

                seatPersonas.Add(new SeatPersona { View = view, Tag = tag, Avatar = avatar, RimName = rimName });
            }

            return seatPersonas[index];
        }

        // The look and name change rarely (another seat, a renamed profile, a new Persona): only then is it applied.
        private void RefreshPersonaLook(SeatPersona persona, CartridgeSeat seat, PlayerId occupant)
        {
            bool local = occupant.IsEmpty || occupant == localPlayerId;
            ProfileInfo profile = local ? EnsureProfile().Active : null;
            string nameSource = profile != null ? profile.displayName : null;
            PersonaLook wanted = profile != null && profile.persona != null ? profile.persona : null;
            int avatarIndex = profile != null ? profile.avatar : seat.LayoutSeatIndex;
            bool changed = persona.SeatId != seat.SeatId
                || persona.Occupant != occupant
                || !ReferenceEquals(persona.NameSource, nameSource)
                || persona.AvatarIndex != avatarIndex
                || (wanted != null && !persona.Look.SameAs(wanted));
            if (!changed) return;

            persona.SeatId = seat.SeatId;
            persona.Occupant = occupant;
            persona.NameSource = nameSource;
            persona.Name = !string.IsNullOrWhiteSpace(nameSource) ? nameSource : FormatPlayerName(occupant);
            (wanted ?? PersonaLook.ForSeat(seat.LayoutSeatIndex)).CopyTo(persona.Look);
            persona.Colour = PlayerSeatColor(seat.LayoutSeatIndex);
            persona.AvatarIndex = avatarIndex;
            if (persona.View != null) persona.View.Apply(persona.Look, personaSet, persona.Colour, persona.Name);
            if (persona.Tag != null) persona.Tag.Apply(persona.Name, persona.Colour);
            if (persona.Avatar != null) persona.Avatar.Apply(personaSet.Avatar(avatarIndex), persona.Colour);
            if (persona.RimName != null)
            {
                persona.RimName.text = persona.Name;
                persona.RimName.color = persona.Colour;
            }
        }

        private void HidePersonas()
        {
            for (int i = 0; i < seatPersonas.Count; i++) HidePersona(seatPersonas[i]);
        }

        private static void HidePersona(SeatPersona persona)
        {
            if (persona.View != null && persona.View.gameObject.activeSelf) persona.View.gameObject.SetActive(false);
            if (persona.Tag != null && persona.Tag.gameObject.activeSelf) persona.Tag.gameObject.SetActive(false);
            if (persona.Avatar != null && persona.Avatar.gameObject.activeSelf) persona.Avatar.gameObject.SetActive(false);
            if (persona.RimName != null && persona.RimName.gameObject.activeSelf) persona.RimName.gameObject.SetActive(false);
        }

        // ---------- PERSONA-2: the Persona panel's turning preview ----------

        private PersonaPanelPresenter personaPanelPresenterInstance;
        private Transform personaPreviewRoot;
        private PersonaView personaPreview;
        private UnityEngine.Camera personaPreviewCamera;
        private RenderTexture personaPreviewTexture;
        private float personaPreviewYaw;
        private float personaPreviewTargetYaw;

        internal PersonaPanelPresenter PersonaPanel => personaPanelPresenterInstance ??= new PersonaPanelPresenter(this);

        // Far below the table, a character standing alone and a camera of its own drawing it into a picture for the
        // Persona panel. Made when the panel opens, gone when it closes.
        internal Texture StartPersonaPreview()
        {
            if (personaPreview != null) return personaPreviewTexture;
            if (!EnsurePersonaSet() || personaSet.Character == null) return null;
            personaPreviewRoot = new GameObject("PersonaPreview").transform;
            personaPreviewRoot.SetParent(transform, false);
            personaPreviewRoot.position = new Vector3(0f, -2000f, 0f);
            personaPreview = Instantiate(personaSet.Character, personaPreviewRoot);
            personaPreview.name = "PersonaPreviewCharacter";
            if (personaPreview.NameLabel != null) personaPreview.NameLabel.gameObject.SetActive(false);
            personaPreviewYaw = 20f;
            personaPreviewTargetYaw = 20f;
            personaPreviewTexture = new RenderTexture(512, 640, 24) { name = "PersonaPreview" };
            personaPreviewCamera = new GameObject("PersonaPreviewCamera").AddComponent<UnityEngine.Camera>();
            personaPreviewCamera.transform.SetParent(personaPreviewRoot, false);
            personaPreviewCamera.transform.localPosition = new Vector3(0f, 1.25f, 3.4f);
            personaPreviewCamera.transform.LookAt(personaPreviewRoot.position + new Vector3(0f, 1.0f, 0f));
            personaPreviewCamera.fieldOfView = 32f;
            personaPreviewCamera.nearClipPlane = 0.1f;
            personaPreviewCamera.farClipPlane = 12f;
            personaPreviewCamera.clearFlags = CameraClearFlags.SolidColor;
            personaPreviewCamera.backgroundColor = new Color(0.07f, 0.09f, 0.12f, 1f);
            personaPreviewCamera.targetTexture = personaPreviewTexture;
            return personaPreviewTexture;
        }

        // Shows a look on the preview character (standing), in your seat colour.
        internal void ApplyPersonaPreview(PersonaLook look, string displayName)
        {
            if (personaPreview == null || personaSet == null || look == null) return;
            Color colour = localPlayerLayoutSeatIndex >= 0 ? PlayerSeatColor(localPlayerLayoutSeatIndex) : new Color(0.93f, 0.4f, 0.13f);
            personaPreview.Apply(look, personaSet, colour, displayName);
            personaPreview.Pose(personaSet, false);
            personaPreview.SetGlow(personaSet.IdleGlow, 1f);
        }

        internal void TurnPersonaPreview(int step) => personaPreviewTargetYaw += 45f * step;

        private void TickPersonaPreview()
        {
            if (personaPreview == null) return;
            personaPreviewTargetYaw += 12f * Time.unscaledDeltaTime; // a slow turn, so every side shows
            personaPreviewYaw = Mathf.Lerp(personaPreviewYaw, personaPreviewTargetYaw, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
            personaPreview.Place(personaPreviewRoot.position, Quaternion.Euler(0f, personaPreviewYaw, 0f), 1f);
        }

        internal void StopPersonaPreview()
        {
            if (personaPreviewCamera != null) personaPreviewCamera.targetTexture = null;
            if (personaPreviewTexture != null)
            {
                personaPreviewTexture.Release();
                Destroy(personaPreviewTexture);
                personaPreviewTexture = null;
            }

            if (personaPreviewRoot != null) Destroy(personaPreviewRoot.gameObject);
            personaPreviewRoot = null;
            personaPreview = null;
            personaPreviewCamera = null;
        }

        private void DestroyPersonas()
        {
            StopPersonaPreview();
            seatPersonas.Clear();
            if (personaRoot != null)
            {
                Destroy(personaRoot.gameObject);
                personaRoot = null;
            }
        }
    }
}
