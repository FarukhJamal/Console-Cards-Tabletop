using System.Collections.Generic;
using System.IO;
using ConsoleCards.Presentation.Feedback;
using UnityEditor;
using UnityEngine;

namespace ConsoleCards.Editor.Feedback
{
    /// <summary>
    /// PERSONA-2 (owner, 2026-10-21): makes the player character prefab (Final/World/SeatPersonaCharacter) from the
    /// Universal Base Characters pack (Quaternius, CC0) found anywhere under Assets: every "*_FullBody.fbx" in a Unity
    /// folder is a body, every "Hair_*.fbx" under "Rigged to Head Bone/FBX (Unity)" a hairstyle (Hair_Beard is the
    /// beard). It sets the bodies to Humanoid (so they can sit), makes a copy of each body mesh with a clothes mask
    /// in its vertex colours (from which bones move each part: head, neck and hands stay skin, feet are shoes, hips
    /// and legs are the lower body), binds every hairstyle to each body's head, puts the emblem on the chest, and
    /// fills Resources/PersonaSet (the character, the skin pictures, the emblems). Like the other Final builders it
    /// only creates what is missing and never overwrites: delete the prefab to make it again.
    /// </summary>
    public static class PersonaBuilder
    {
        private const string ArtPath = "Assets/ConsoleCards/Content/Art/Persona/";
        private const string PartsPath = ArtPath + "Parts/";
        private const string GeneratedPath = ArtPath + "Generated/";
        private const string MaterialPath = "Assets/ConsoleCards/Content/Materials/Persona/";
        private const string PrefabPath = "Assets/ConsoleCards/Content/Prefabs/Final/World/SeatPersonaCharacter.prefab";
        private const string TagPrefabPath = "Assets/ConsoleCards/Content/Prefabs/Final/World/SeatTag.prefab";
        private const string AvatarPrefabPath = "Assets/ConsoleCards/Content/Prefabs/Final/World/SeatAvatar.prefab";
        private const string AvatarsPath = ArtPath + "Avatars/";
        private const string UnlitShaderName = "Universal Render Pipeline/Unlit";
        private const string SetPath = "Assets/ConsoleCards/Content/Resources/PersonaSet.asset";
        private const string CharacterShaderName = "ConsoleCards/Persona Character";
        private const string HologramShaderName = "ConsoleCards/Persona Hologram";
        private const string HairFolderMarker = "Rigged to Head Bone/FBX (Unity)/";

        private sealed class BodySource
        {
            public string Name;
            public string Path;
            public string Key;
        }

        [MenuItem("Console Cards/Feedback/Create Missing Persona Prefabs (Final World)")]
        public static void Build()
        {
            Directory.CreateDirectory(MaterialPath);
            Directory.CreateDirectory(GeneratedPath);
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            ConfigureFolder(PartsPath);

            // AVATAR-1: the avatar pictures and the stand that shows one at each seat.
            ConfigureFolder(AvatarsPath);
            PersonaAvatarView avatarStand = AvatarPrefab();

            // PERSONA-2 (switched off in the set; kept for later): the 3D characters, when their pack is in the project.
            List<BodySource> bodies = FindBodies();
            if (bodies.Count == 0)
            {
                Debug.Log("[Persona] No character bodies found (Universal Base Characters): the 3D characters are left out.");
            }

            List<string> hairs = FindHairs(out string beard);
            Material emblem = HologramMaterial("PersonaEmblem");
            PersonaView character = bodies.Count > 0 ? CharacterPrefab(bodies, hairs, beard, emblem) : null;
            PersonaTagView tag = TagPrefab();

            PersonaSet set = AssetDatabase.LoadAssetAtPath<PersonaSet>(SetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<PersonaSet>();
                AssetDatabase.CreateAsset(set, SetPath);
            }

            SerializedObject so = new SerializedObject(set);
            bool changed = FillMissing(so, "avatarStand", avatarStand)
                | AddPictures(so, "avatars", AvatarsPath, "Avatar_")
                | FillMissing(so, "character", character)
                | FillMissing(so, "seatTag", tag)
                | AddParts(so, "emblems", "Emblem")
                | FillSkins(so, bodies);
            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(set);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Persona] Avatars ready ({set.AvatarCount} pictures); 3D characters (off): {bodies.Count} bodies, {hairs.Count} hairstyles.");
        }

        // ---------- finding the pack ----------

        private static List<BodySource> FindBodies()
        {
            List<BodySource> bodies = new List<BodySource>();
            string[] guids = AssetDatabase.FindAssets("FullBody t:Model");
            List<string> paths = new List<string>();
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.EndsWith("_FullBody.fbx", System.StringComparison.OrdinalIgnoreCase) && path.Contains("/Unity/")) paths.Add(path);
            }

            paths.Sort(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < paths.Count; i++)
            {
                string file = Path.GetFileNameWithoutExtension(paths[i]); // Superhero_Male_FullBody
                string[] words = file.Split('_');
                string key = words.Length >= 2 ? words[words.Length - 2] : file; // Male
                bodies.Add(new BodySource { Name = key, Key = key, Path = paths[i] });
                MakeHumanoid(paths[i]);
            }

            return bodies;
        }

        private static List<string> FindHairs(out string beard)
        {
            beard = null;
            List<string> hairs = new List<string>();
            string[] guids = AssetDatabase.FindAssets("Hair_ t:Model");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!path.Replace('\\', '/').Contains(HairFolderMarker)) continue;
                string file = Path.GetFileNameWithoutExtension(path);
                if (!file.StartsWith("Hair_", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (file.Equals("Hair_Beard", System.StringComparison.OrdinalIgnoreCase)) beard = path;
                else hairs.Add(path);
            }

            hairs.Sort(System.StringComparer.OrdinalIgnoreCase);
            return hairs;
        }

        // The bodies sit through Unity's humanoid muscles, so they are imported as Humanoid.
        private static void MakeHumanoid(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null || importer.animationType == ModelImporterAnimationType.Human) return;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();
            Debug.Log($"[Persona] {path} is now imported as Humanoid.");
        }

        private static Avatar AvatarOf(string path)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Avatar avatar) return avatar;
            }

            return null;
        }

        // ---------- the prefab ----------

        private static PersonaView CharacterPrefab(List<BodySource> bodies, List<string> hairs, string beard, Material emblemMaterial)
        {
            PersonaView existing = AssetDatabase.LoadAssetAtPath<PersonaView>(PrefabPath);
            if (existing != null)
            {
                Debug.Log($"[Persona] Kept {PrefabPath} (already exists; delete it to make it again).");
                return existing;
            }

            GameObject root = new GameObject("SeatPersonaCharacter");
            try
            {
                List<string> hairNames = new List<string>(hairs.Count);
                for (int h = 0; h < hairs.Count; h++) hairNames.Add(Readable(Path.GetFileNameWithoutExtension(hairs[h]).Substring(5)));

                PersonaView.PersonaBody[] made = new PersonaView.PersonaBody[bodies.Count];
                for (int b = 0; b < bodies.Count; b++)
                {
                    made[b] = MakeBody(root.transform, bodies[b], hairs, beard, emblemMaterial);
                    made[b].root.gameObject.SetActive(b == 0);
                }

                GameObject nameObject = new GameObject("Name");
                nameObject.transform.SetParent(root.transform, false);
                nameObject.transform.localPosition = new Vector3(0f, 2.2f, 0f);
                nameObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                TextMesh name = nameObject.AddComponent<TextMesh>();
                Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                name.font = font;
                name.fontSize = 64;
                name.characterSize = 0.015f;
                name.anchor = TextAnchor.MiddleCenter;
                name.alignment = TextAlignment.Center;
                name.richText = false;
                name.text = "Player";
                MeshRenderer nameRenderer = nameObject.GetComponent<MeshRenderer>();
                if (font != null) nameRenderer.sharedMaterial = font.material;
                NoShadows(nameRenderer);

                PersonaView view = root.AddComponent<PersonaView>();
                SerializedObject so = new SerializedObject(view);
                SerializedProperty list = so.FindProperty("bodies");
                list.arraySize = made.Length;
                for (int b = 0; b < made.Length; b++) WriteBody(list.GetArrayElementAtIndex(b), made[b]);
                SerializedProperty names = so.FindProperty("hairNames");
                names.arraySize = hairNames.Count;
                for (int h = 0; h < hairNames.Count; h++) names.GetArrayElementAtIndex(h).stringValue = hairNames[h];
                so.FindProperty("nameLabel").objectReferenceValue = name;
                so.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath).GetComponent<PersonaView>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static PersonaView.PersonaBody MakeBody(Transform parent, BodySource source, List<string> hairs, string beard, Material emblemMaterial)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(source.Path);
            Transform bodyRoot = new GameObject("Body_" + source.Name).transform;
            bodyRoot.SetParent(parent, false);
            GameObject model = Object.Instantiate(asset, bodyRoot);
            model.name = source.Name;
            Animator animator = model.GetComponent<Animator>();
            if (animator != null) animator.enabled = false; // posed once by the table, never animated

            Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                if (!bones.ContainsKey(t.name)) bones.Add(t.name, t);
            }

            SkinnedMeshRenderer bodyRenderer = null;
            SkinnedMeshRenderer eyes = null;
            SkinnedMeshRenderer brows = null;
            foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                string n = renderer.name;
                if (n.IndexOf("Eyebrow", System.StringComparison.OrdinalIgnoreCase) >= 0) brows = renderer;
                else if (n.IndexOf("Eye", System.StringComparison.OrdinalIgnoreCase) >= 0) eyes = renderer;
                else if (bodyRenderer == null || renderer.sharedMesh.vertexCount > bodyRenderer.sharedMesh.vertexCount) bodyRenderer = renderer;
            }

            bones.TryGetValue("Head", out Transform head);
            // Its front is +Z: a body that looks the other way (its toes point to -Z) is turned round.
            if (bones.TryGetValue("foot_l", out Transform foot) && bones.TryGetValue("ball_l", out Transform toe)
                && (toe.position - foot.position).z < 0f)
            {
                model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }

            if (bodyRenderer != null)
            {
                bodyRenderer.sharedMesh = MaskedBodyMesh(source, bodyRenderer);
                bodyRenderer.sharedMaterials = new[] { CharacterMaterial("Persona_Body_" + source.Name, bodyRenderer.sharedMaterial, false) };
                NoShadows(bodyRenderer);
            }

            if (eyes != null)
            {
                eyes.sharedMaterials = new[] { CharacterMaterial("Persona_Eyes", eyes.sharedMaterial, true) };
                NoShadows(eyes);
            }

            if (brows != null)
            {
                brows.sharedMaterials = new[] { CharacterMaterial("Persona_Brows", brows.sharedMaterial, true) };
                NoShadows(brows);
            }

            List<GameObject> hairObjects = new List<GameObject>(hairs.Count);
            for (int h = 0; h < hairs.Count; h++)
            {
                GameObject hair = BindPart(hairs[h], model.transform, bones);
                if (hair != null)
                {
                    hair.SetActive(false);
                    hairObjects.Add(hair);
                }
            }

            GameObject beardObject = beard != null ? BindPart(beard, model.transform, bones) : null;
            if (beardObject != null) beardObject.SetActive(false);

            // The emblem on the chest, facing front.
            Renderer emblem = null;
            if (bones.TryGetValue("spine_03", out Transform chest))
            {
                GameObject quad = new GameObject("Emblem");
                quad.transform.SetParent(chest, true);
                quad.transform.position = chest.position + (Vector3.forward * 0.17f) + (Vector3.up * 0.05f);
                quad.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                quad.transform.localScale = Vector3.one;
                Vector3 lossy = quad.transform.lossyScale;
                quad.transform.localScale = new Vector3(0.13f / Mathf.Max(lossy.x, 1e-4f), 0.13f / Mathf.Max(lossy.y, 1e-4f), 1f / Mathf.Max(lossy.z, 1e-4f));
                quad.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                MeshRenderer quadRenderer = quad.AddComponent<MeshRenderer>();
                quadRenderer.sharedMaterial = emblemMaterial;
                NoShadows(quadRenderer);
                emblem = quadRenderer;
            }

            return new PersonaView.PersonaBody
            {
                name = source.Name,
                root = bodyRoot,
                model = model.transform,
                avatar = AvatarOf(source.Path),
                head = head,
                body = bodyRenderer,
                eyes = eyes,
                brows = brows,
                hairs = hairObjects.ToArray(),
                beard = beardObject,
                emblem = emblem,
            };
        }

        private static void WriteBody(SerializedProperty property, PersonaView.PersonaBody body)
        {
            property.FindPropertyRelative("name").stringValue = body.name;
            property.FindPropertyRelative("root").objectReferenceValue = body.root;
            property.FindPropertyRelative("model").objectReferenceValue = body.model;
            property.FindPropertyRelative("avatar").objectReferenceValue = body.avatar;
            property.FindPropertyRelative("head").objectReferenceValue = body.head;
            property.FindPropertyRelative("body").objectReferenceValue = body.body;
            property.FindPropertyRelative("eyes").objectReferenceValue = body.eyes;
            property.FindPropertyRelative("brows").objectReferenceValue = body.brows;
            SerializedProperty hairs = property.FindPropertyRelative("hairs");
            hairs.arraySize = body.hairs.Length;
            for (int i = 0; i < body.hairs.Length; i++) hairs.GetArrayElementAtIndex(i).objectReferenceValue = body.hairs[i];
            property.FindPropertyRelative("beard").objectReferenceValue = body.beard;
            property.FindPropertyRelative("emblem").objectReferenceValue = body.emblem;
        }

        // A hairstyle (rigged to the head bone in its own file) bound to this body's bones by name, so it follows the head.
        private static GameObject BindPart(string path, Transform model, Dictionary<string, Transform> bones)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return null;
            GameObject temp = Object.Instantiate(asset);
            try
            {
                SkinnedMeshRenderer source = temp.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (source == null) return null;
                GameObject part = new GameObject(Path.GetFileNameWithoutExtension(path));
                part.transform.SetParent(model, false);
                SkinnedMeshRenderer renderer = part.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = source.sharedMesh;
                Transform[] mapped = new Transform[source.bones.Length];
                for (int i = 0; i < mapped.Length; i++)
                {
                    Transform bone = source.bones[i];
                    if (bone != null && bones.TryGetValue(bone.name, out Transform own)) mapped[i] = own;
                }

                renderer.bones = mapped;
                if (source.rootBone != null && bones.TryGetValue(source.rootBone.name, out Transform rootBone)) renderer.rootBone = rootBone;
                renderer.localBounds = source.localBounds;
                renderer.sharedMaterials = new[] { CharacterMaterial("Persona_" + part.name, source.sharedMaterial, true) };
                NoShadows(renderer);
                return part;
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        // ---------- the avatar stand (AVATAR-1) ----------

        // The player's picture standing upright in a frame of the seat colour: pivot at the bottom edge, the picture
        // facing -Z. Sizes in table units (a card is about 2 wide).
        private static PersonaAvatarView AvatarPrefab()
        {
            PersonaAvatarView existing = AssetDatabase.LoadAssetAtPath<PersonaAvatarView>(AvatarPrefabPath);
            if (existing != null) return existing;
            Material pictureMaterial = UnlitMaterial("PersonaAvatarPicture", false);
            Material frameMaterial = UnlitMaterial("PersonaAvatarFrame", true);
            const float size = 4f;
            const float border = 0.25f;
            GameObject root = new GameObject("SeatAvatar");
            try
            {
                Transform frame = Quad("Frame", root.transform, frameMaterial, new Vector3(0f, (size * 0.5f) + border, 0.02f),
                    new Vector2(size + (border * 2f), size + (border * 2f)));
                Transform picture = Quad("Picture", root.transform, pictureMaterial, new Vector3(0f, (size * 0.5f) + border, 0f),
                    new Vector2(size, size));
                PersonaAvatarView view = root.AddComponent<PersonaAvatarView>();
                SerializedObject so = new SerializedObject(view);
                so.FindProperty("picture").objectReferenceValue = picture.GetComponent<Renderer>();
                so.FindProperty("frame").objectReferenceValue = frame.GetComponent<Renderer>();
                so.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, AvatarPrefabPath).GetComponent<PersonaAvatarView>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Material UnlitMaterial(string name, bool bothSides)
        {
            string path = MaterialPath + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find(UnlitShaderName);
            if (shader == null) throw new System.InvalidOperationException($"Shader '{UnlitShaderName}' is missing.");
            Material material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Cull", bothSides ? 0f : 2f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Transform Quad(string name, Transform parent, Material material, Vector3 position, Vector2 size)
        {
            GameObject quad = new GameObject(name);
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = position;
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            quad.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            MeshRenderer renderer = quad.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            NoShadows(renderer);
            return quad.transform;
        }

        // Adds every <prefix>*.png in a folder that the list does not hold yet, in name order.
        private static bool AddPictures(SerializedObject so, string field, string folder, string prefix)
        {
            SerializedProperty list = so.FindProperty(field);
            if (list == null || !Directory.Exists(folder)) return false;
            HashSet<Object> present = new HashSet<Object>();
            for (int i = 0; i < list.arraySize; i++)
            {
                Object existing = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (existing != null) present.Add(existing);
            }

            List<string> files = new List<string>(Directory.GetFiles(folder, prefix + "*.png"));
            files.Sort(System.StringComparer.OrdinalIgnoreCase);
            bool added = false;
            for (int i = 0; i < files.Count; i++)
            {
                Texture2D picture = AssetDatabase.LoadAssetAtPath<Texture2D>(files[i].Replace('\\', '/'));
                if (picture == null || present.Contains(picture)) continue;
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = picture;
                present.Add(picture);
                added = true;
            }

            return added;
        }

        // ---------- the seat tag ----------

        // A name panel (owner's picture: "Player 1" panels along the table's edges): the glowing panel picture and the
        // name in front of it, pivot at the panel's bottom edge, readable from -Z. Sizes in table units.
        private static PersonaTagView TagPrefab()
        {
            PersonaTagView existing = AssetDatabase.LoadAssetAtPath<PersonaTagView>(TagPrefabPath);
            if (existing != null) return existing;
            const string picturePath = ArtPath + "TagPanel.png";
            TextureImporter importer = AssetImporter.GetAtPath(picturePath) as TextureImporter;
            if (importer != null && (!importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp))
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            string materialPath = MaterialPath + "PersonaTag.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = Shader.Find(HologramShaderName);
                if (shader == null) throw new System.InvalidOperationException($"Shader '{HologramShaderName}' is missing.");
                material = new Material(shader) { name = "PersonaTag" };
                material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(picturePath));
                material.SetColor("_Color", Color.white);
                material.SetFloat("_Fill", 1f);
                material.SetFloat("_Rim", 0f);
                material.SetFloat("_ScanDepth", 0.2f);
                AssetDatabase.CreateAsset(material, materialPath);
            }

            const float width = 7f;
            const float height = 1.75f;
            GameObject root = new GameObject("SeatTag");
            try
            {
                GameObject panel = new GameObject("Panel");
                panel.transform.SetParent(root.transform, false);
                panel.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
                panel.transform.localScale = new Vector3(width, height, 1f);
                panel.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                MeshRenderer panelRenderer = panel.AddComponent<MeshRenderer>();
                panelRenderer.sharedMaterial = material;
                NoShadows(panelRenderer);

                GameObject nameObject = new GameObject("Name");
                nameObject.transform.SetParent(root.transform, false);
                nameObject.transform.localPosition = new Vector3(0f, height * 0.5f, -0.02f);
                TextMesh name = nameObject.AddComponent<TextMesh>();
                Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                name.font = font;
                name.fontSize = 96;
                name.characterSize = 0.1f;
                name.fontStyle = FontStyle.Bold;
                name.anchor = TextAnchor.MiddleCenter;
                name.alignment = TextAlignment.Center;
                name.richText = false;
                name.text = "Player 1";
                MeshRenderer nameRenderer = nameObject.GetComponent<MeshRenderer>();
                if (font != null) nameRenderer.sharedMaterial = font.material;
                NoShadows(nameRenderer);

                PersonaTagView view = root.AddComponent<PersonaTagView>();
                SerializedObject so = new SerializedObject(view);
                so.FindProperty("panel").objectReferenceValue = panelRenderer;
                so.FindProperty("nameLabel").objectReferenceValue = name;
                so.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, TagPrefabPath).GetComponent<PersonaTagView>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---------- the clothes mask ----------

        // A copy of the body mesh whose vertex colours say what each part wears: red = clothed, green = lower body,
        // blue = shoes. Worked out from the bone that moves each vertex most (head, neck and hands stay skin).
        private static Mesh MaskedBodyMesh(BodySource source, SkinnedMeshRenderer renderer)
        {
            string path = GeneratedPath + Path.GetFileNameWithoutExtension(source.Path) + "_Clothed.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;

            Mesh mesh = Object.Instantiate(renderer.sharedMesh);
            mesh.name = Path.GetFileNameWithoutExtension(source.Path) + "_Clothed";
            BoneWeight[] weights = mesh.boneWeights;
            Transform[] bones = renderer.bones;
            Color32[] colours = new Color32[mesh.vertexCount];
            for (int v = 0; v < colours.Length; v++)
            {
                string bone = v < weights.Length && weights[v].boneIndex0 < bones.Length && bones[weights[v].boneIndex0] != null
                    ? bones[weights[v].boneIndex0].name
                    : string.Empty;
                colours[v] = MaskFor(bone);
            }

            mesh.colors32 = colours;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Color32 MaskFor(string bone)
        {
            string b = bone.ToLowerInvariant();
            bool skin = b == "head" || b.StartsWith("neck") || b.StartsWith("hand") || b.StartsWith("thumb") || b.StartsWith("index")
                || b.StartsWith("middle") || b.StartsWith("ring") || b.StartsWith("pinky");
            if (skin) return new Color32(0, 0, 0, 255);
            bool shoes = b.StartsWith("foot") || b.StartsWith("ball");
            bool lower = shoes || b == "pelvis" || b.StartsWith("thigh") || b.StartsWith("calf");
            return new Color32(255, (byte)(lower ? 255 : 0), (byte)(shoes ? 255 : 0), 255);
        }

        // ---------- materials and pictures ----------

        // One material per picture the pack's own material used, with the character shader (hair and eyes are
        // recoloured by brightness).
        private static Material CharacterMaterial(string name, Material original, bool recolour)
        {
            Texture picture = original != null ? original.mainTexture : null;
            string path = MaterialPath + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find(CharacterShaderName);
            if (shader == null) throw new System.InvalidOperationException($"Shader '{CharacterShaderName}' is missing.");
            Material material = new Material(shader) { name = name };
            if (picture != null) material.SetTexture("_MainTex", picture);
            material.SetFloat("_ColourMode", recolour ? 1f : 0f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material HologramMaterial(string name)
        {
            string path = MaterialPath + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find(HologramShaderName);
            if (shader == null) throw new System.InvalidOperationException($"Shader '{HologramShaderName}' is missing.");
            Material material = new Material(shader) { name = name };
            material.SetColor("_Color", Color.white);
            material.SetFloat("_Fill", 1f);
            material.SetFloat("_Rim", 0f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // Each body's light and dark skin pictures (T_Superhero_Male_Ligh*, ..._Dark*), in the prefab's body order.
        private static bool FillSkins(SerializedObject so, List<BodySource> bodies)
        {
            SerializedProperty light = so.FindProperty("lightSkin");
            SerializedProperty dark = so.FindProperty("darkSkin");
            if (light == null || dark == null || (light.arraySize == bodies.Count && dark.arraySize == bodies.Count)) return false;
            light.arraySize = bodies.Count;
            dark.arraySize = bodies.Count;
            string[] guids = AssetDatabase.FindAssets("T_ t:Texture2D");
            for (int b = 0; b < bodies.Count; b++)
            {
                string key = "_" + bodies[b].Key + "_";
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    string file = Path.GetFileNameWithoutExtension(path);
                    if (file.IndexOf(key, System.StringComparison.Ordinal) < 0 || path.Contains("Normals")) continue;
                    if (file.IndexOf("Normal", System.StringComparison.Ordinal) >= 0 || file.IndexOf("Roughness", System.StringComparison.Ordinal) >= 0) continue;
                    Texture2D picture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (file.IndexOf("Dark", System.StringComparison.Ordinal) >= 0) dark.GetArrayElementAtIndex(b).objectReferenceValue = picture;
                    else if (file.IndexOf("Ligh", System.StringComparison.Ordinal) >= 0) light.GetArrayElementAtIndex(b).objectReferenceValue = picture;
                }
            }

            return true;
        }

        private static bool FillMissing(SerializedObject so, string field, Object value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null || property.objectReferenceValue != null || value == null) return false;
            property.objectReferenceValue = value;
            return true;
        }

        // Adds every picture named <prefix>n.png in the parts folder that the list does not hold yet, in name order.
        private static bool AddParts(SerializedObject so, string field, string prefix)
        {
            SerializedProperty list = so.FindProperty(field);
            if (list == null || !Directory.Exists(PartsPath)) return false;
            HashSet<Object> present = new HashSet<Object>();
            for (int i = 0; i < list.arraySize; i++)
            {
                Object existing = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (existing != null) present.Add(existing);
            }

            List<string> files = new List<string>(Directory.GetFiles(PartsPath, prefix + "*.png"));
            files.Sort(System.StringComparer.OrdinalIgnoreCase);
            bool added = false;
            for (int i = 0; i < files.Count; i++)
            {
                Texture2D picture = AssetDatabase.LoadAssetAtPath<Texture2D>(files[i].Replace('\\', '/'));
                if (picture == null || present.Contains(picture)) continue;
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = picture;
                present.Add(picture);
                added = true;
            }

            return added;
        }

        private static void ConfigureFolder(string folder)
        {
            if (!Directory.Exists(folder)) return;
            string[] files = Directory.GetFiles(folder, "*.png");
            for (int i = 0; i < files.Length; i++)
            {
                TextureImporter importer = AssetImporter.GetAtPath(files[i].Replace('\\', '/')) as TextureImporter;
                if (importer == null || (importer.alphaIsTransparency && importer.wrapMode == TextureWrapMode.Clamp)) continue;
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
        }

        // "SimpleParted" → "Simple Parted".
        private static string Readable(string name)
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1])) text.Append(' ');
                text.Append(name[i]);
            }

            return text.ToString();
        }

        private static void NoShadows(Renderer renderer)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
