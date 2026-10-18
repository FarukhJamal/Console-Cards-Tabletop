using System;
using System.Collections.Generic;
using System.IO;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.TableSurface;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// The table (doc 30, B2 and B8; T1, T1b). Everything starts from the <b>base table</b>: a table model with three
    /// parts — playing area, sides and lower frame.
    /// <list type="bullet">
    /// <item>The base table is the table model in the scene (set up with Console Cards > Table > Use Table Model As
    /// Scene Table). Without one, the "classic" model from Resources/TableModels is placed where the scene's old
    /// table is, and the old table is turned off for good.</item>
    /// <item>Classic shows the whole base table. Rectangle, Square, Round and Oval are built on the base table: they
    /// replace its playing area and sides, keep its lower frame (legs), use its playing-area material and reach
    /// down to its legs.</item>
    /// <item>After Oval comes Star (T2); then the Table menu row goes through the other table models, which replace
    /// the base table.</item>
    /// <item>The "Edge: …" row (T2) cycles the built tables' edge: None, Lip, Rounded rail, Wooden rail, Spikes,
    /// Neon.</item>
    /// <item>"Table tools…" (T3) opens Table mode (TabletopComposition.TableMode.cs).</item>
    /// </list>
    /// The choice is saved in the profile (table.json). Sizes, the shape cards and Undo come in T3.
    /// </summary>
    public sealed partial class TabletopComposition
    {
        private const string TableSpecFileName = "table.json";
        private const string TableArea = "Table";
        private const string ClassicModelId = "classic";

        internal enum BaseTableView
        {
            Whole,
            LegsOnly,
            Hidden,
        }

        private bool classicTableLooked;
        private PhysicalTabletopSurface classicSurface;
        private TableFrame classicFrame;
        private Material classicPlayingArea;
        private Material classicBody;
        private GeneratedTable generatedTable;
        private TableSpec tableSpec = new TableSpec();
        private Func<string> cycleTableShapeAction;
        private Func<string> cycleTableEdgeAction;

        // The base table (T1b) and, when there is none, the scene's old one-piece table.
        private TableModel baseTable;
        private float baseBodyDepth;
        private Vector3[] baseLegHome = Array.Empty<Vector3>();
        private Vector3[] baseLegHomeScale = Array.Empty<Vector3>();
        private float[] baseLegHeight = Array.Empty<float>();
        private PhysicalTabletopSurface sceneSurface;
        private Renderer sceneBodyRenderer;
        private bool sceneBodyWasEnabled;

        // The table models, loaded once, and another model on the table in place of the base table.
        private readonly List<TableModel> tableModels = new List<TableModel>();
        private bool tableModelsLoaded;
        private TableModel modelTable;
        private string modelTableId;

        private string TableSpecPath => Path.Combine(EnsureProfile().ActiveFolder, TableSpecFileName);

        private string TableShapeText() => $"Table: {TableShapeName(tableSpec)}";

        private string TableShapeName(TableSpec spec)
        {
            if (spec.shape != TableShape.Model) return TableSpec.Label(spec.shape);
            TableModel model = FindTableModel(spec.modelId);
            return model != null ? model.DisplayName : spec.modelId;
        }

        // At start, before the first table: puts back the shape saved in the profile. Never stops the game starting;
        // anything wrong keeps the Classic table with a warning.
        private void ApplySavedTable()
        {
            try
            {
                if (!FindClassicTable()) return;
                TableSpec saved = SaveFiles.ReadJson<TableSpec>(TableSpecPath, TableArea);
                if (saved == null || saved.shape == TableShape.Classic)
                {
                    ShowClassicTable();
                    return;
                }

                if (saved.shape != TableShape.Model && (saved.width <= 0f || saved.depth <= 0f))
                {
                    TableSpec sized = TableSpec.DefaultFor(saved.shape, classicFrame.ClassicSize.x, classicFrame.ClassicSize.y);
                    sized.edge = saved.edge;
                    saved = sized;
                }

                saved.Clamp();
                if (ShowTable(saved, out string error))
                {
                    tableSpec = saved;
                }
                else
                {
                    Debug.LogWarning($"[{TableArea}] The saved {TableShapeName(saved)} table could not be built ({error}); using the Classic table.", this);
                    ShowClassicTable();
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[{TableArea}] The saved table could not be used ({exception.Message}); using the Classic table.", this);
                ShowClassicTable();
            }
        }

        // The Table menu row, on every table. Hidden when the scene has no table to start from.
        internal void RefreshTableShapeEntry()
        {
            if (runtimeUi == null) return;
            if (cycleTableShapeAction == null) cycleTableShapeAction = CycleTableShape;
            if (cycleTableEdgeAction == null) cycleTableEdgeAction = CycleTableEdge;
            bool canChange = FindClassicTable();
            runtimeUi.ShowTableShapeEntry(TableShapeText(), canChange ? cycleTableShapeAction : null);
            runtimeUi.ShowTableEdgeEntry(TableEdgeText(), canChange ? cycleTableEdgeAction : null);
            runtimeUi.ShowTableToolsEntry(canChange ? OpenTableModeAction : null);
        }

        private string TableEdgeText() => $"Edge: {TableSpec.EdgeLabel(tableSpec.edge)}";

        private static bool IsBuiltShape(TableShape shape) => shape != TableShape.Classic && shape != TableShape.Model;

        // The next edge (T2). A built table is rebuilt with it at once; Classic and models keep their own edge, and
        // the choice waits for the next built shape. Returns the row's new text; the menu stays open.
        private string CycleTableEdge()
        {
            if (!FindClassicTable()) return TableEdgeText();
            TableSpec candidate = tableSpec.Copy();
            candidate.edge = TableSpec.NextEdge(tableSpec.edge);
            string note = string.Empty;
            if (IsBuiltShape(candidate.shape) && !TryChangeTable(candidate, out note))
            {
                ShowMessage(note);
                return TableEdgeText();
            }

            tableSpec = candidate;
            SaveFiles.WriteJson(TableSpecPath, tableSpec, TableArea);
            ShowMessage(IsBuiltShape(tableSpec.shape)
                ? $"Edge: {TableSpec.EdgeLabel(tableSpec.edge)}.{note}"
                : $"Edge: {TableSpec.EdgeLabel(tableSpec.edge)}. It shows on the built tables (Rectangle, Square, Round, Oval, Star).");
            return TableEdgeText();
        }

        // The next shape at its default size, keeping the edge. Returns the row's new text; the menu stays open.
        private string CycleTableShape()
        {
            if (!FindClassicTable()) return TableShapeText();
            TableSpec candidate = NextTableSpec();
            TableShape shape = candidate.shape;
            TableSpec previous = tableSpec;
            if (!TryChangeTable(candidate, out string note))
            {
                ShowMessage(note);
                return TableShapeText();
            }

            tableSpec = candidate;
            GrowFrom(previous, candidate);
            SaveFiles.WriteJson(TableSpecPath, tableSpec, TableArea);
            ShowMessage((shape == TableShape.Classic
                ? "Classic table."
                : shape == TableShape.Model
                    ? $"{TableShapeName(candidate)} table.{ModelFitNote()}"
                    : $"{TableSpec.Label(shape)} table. Change its size in Table tools.") + note);
            return TableShapeText();
        }

        // Shows the table for the spec. Only one playing area is ever the placement surface: the others are off first.
        private bool ShowTable(TableSpec spec, out string error)
        {
            error = null;
            if (spec.shape == TableShape.Classic || (spec.shape == TableShape.Model && IsBaseModel(spec.modelId)))
            {
                ShowClassicTable();
                return true;
            }

            if (spec.shape == TableShape.Model) return ShowModelTable(spec.modelId, out error);
            HideModelTable();
            if (generatedTable == null)
            {
                generatedTable = GeneratedTable.Create(
                    null,
                    classicSurface.gameObject.layer,
                    classicSurface.gameObject.layer);
                SceneManager.MoveGameObjectToScene(generatedTable.gameObject, gameObject.scene);
            }

            // Built on the base table. With legs, the legs are fitted to the new top (below); a lower frame without
            // separate legs cannot move, so the new top reaches down to it instead.
            TableSpec build = spec;
            if (baseLegHome.Length == 0 && baseBodyDepth > 0f)
            {
                build = spec.Copy();
                build.thickness = Mathf.Max(spec.thickness, baseBodyDepth / classicFrame.WorldPerTableUnit);
            }

            SetBaseTable(BaseTableView.LegsOnly);
            if (generatedTable.TryBuild(build, classicFrame, classicPlayingArea, classicBody, out error))
            {
                FitLegsUnder(generatedTable, build.thickness * classicFrame.WorldPerTableUnit);
                Physics.SyncTransforms();
                return true;
            }

            // The last good table stays; with none built yet, that is the Classic table.
            if (!generatedTable.gameObject.activeSelf) SetBaseTable(BaseTableView.Whole);
            return false;
        }

        // Classic: the whole base table.
        private void ShowClassicTable()
        {
            if (generatedTable != null) generatedTable.Hide();
            HideModelTable();
            SetBaseTable(BaseTableView.Whole);
            Physics.SyncTransforms();
        }

        private void SetBaseTable(BaseTableView view)
        {
            if (baseTable != null)
            {
                SetPartActive(baseTable.PlayingArea, view == BaseTableView.Whole);
                SetPartActive(baseTable.Sides, view == BaseTableView.Whole);
                SetPartActive(baseTable.LowerFrame, view != BaseTableView.Hidden);

                // Built shapes keep the legs only (the rest of the lower frame belongs to the classic outline).
                bool hasLegs = baseLegHome.Length > 0;
                if (baseTable.LowerFrame != null) baseTable.LowerFrame.enabled = view == BaseTableView.Whole || !hasLegs;
                if (view == BaseTableView.Whole) PutLegsHome();
                return;
            }

            if (sceneSurface == null) return;
            bool visible = view == BaseTableView.Whole;
            if (sceneSurface.gameObject.activeSelf != visible) sceneSurface.gameObject.SetActive(visible);
            if (sceneBodyRenderer != null) sceneBodyRenderer.enabled = visible && sceneBodyWasEnabled;
        }

        // Fits each leg to the built top. In: along the line from the centre until it stands inside the new edge
        // with a little room (legs already inside stay where they are). Up: its top is set halfway into the new top
        // and the leg stretches to keep its foot on the floor, so it always meets the top and its cut end is hidden.
        // The built table shares the base table's centre and facing, so the legs and the outline share flat space.
        private void FitLegsUnder(GeneratedTable table, float topThickness)
        {
            if (baseTable == null) return;
            Transform[] legs = baseTable.Legs;
            float legTop = -0.5f * topThickness;
            for (int i = 0; i < legs.Length && i < baseLegHome.Length; i++)
            {
                if (legs[i] == null) continue;
                Vector3 home = baseLegHome[i];
                Vector2 flat = new Vector2(home.x, home.z);
                float distance = flat.magnitude;
                float edge = table.EdgeDistance(flat);
                Renderer legRenderer = legs[i].GetComponent<Renderer>();
                float halfWidth = legRenderer != null ? Mathf.Max(legRenderer.bounds.extents.x, legRenderer.bounds.extents.z) : 0f;
                float allowed = edge - halfWidth - (0.05f * edge);
                Vector2 fitted = distance > allowed && distance > 1e-4f && allowed > 0f ? flat * (allowed / distance) : flat;

                float height = baseLegHeight[i];
                Vector3 scale = baseLegHomeScale[i];
                float foot = home.y - height;
                if (height > 1e-4f && legTop > foot) scale.y *= (legTop - foot) / height;
                legs[i].localScale = scale;
                legs[i].localPosition = new Vector3(fitted.x, height > 1e-4f && legTop > foot ? legTop : home.y, fitted.y);
            }
        }

        private void PutLegsHome()
        {
            Transform[] legs = baseTable.Legs;
            for (int i = 0; i < legs.Length && i < baseLegHome.Length; i++)
            {
                if (legs[i] == null) continue;
                legs[i].localPosition = baseLegHome[i];
                legs[i].localScale = baseLegHomeScale[i];
            }
        }

        private static void SetPartActive(Renderer part, bool active)
        {
            if (part != null && part.gameObject.activeSelf != active) part.gameObject.SetActive(active);
        }

        private bool IsBaseModel(string modelId) =>
            baseTable != null && string.Equals(baseTable.ModelId, modelId, StringComparison.OrdinalIgnoreCase);

        // Once per scene: finds the base table and measures it. The active surface marked as the Template Layout
        // Origin gives the frame, the centre of the playing area at its top and the playing-area size.
        private bool FindClassicTable()
        {
            if (classicTableLooked) return classicSurface != null;
            classicTableLooked = true;
            Physics.SyncTransforms();
            PhysicsScene physicsScene = targetCamera.gameObject.scene.GetPhysicsScene();
            PhysicalTabletopSurface origin = null;
            Collider surfaceCollider = null;
            foreach (PhysicalTabletopSurface surface in PhysicalTabletopSurface.Registered)
            {
                if (surface != null && surface.ParticipatesIn(physicsScene) && surface.IsTemplateLayoutOrigin
                    && surface.TryGetCollider(out Collider found, out _))
                {
                    origin = surface;
                    surfaceCollider = found;
                    break;
                }
            }

            if (origin == null)
            {
                Debug.LogWarning($"[{TableArea}] No table surface is marked as the Template Layout Origin, so the table cannot change.", this);
                return false;
            }

            if (!TryMeasureFrame(origin, surfaceCollider, out classicFrame))
            {
                return false;
            }

            baseTable = origin.GetComponentInParent<TableModel>();
            if (baseTable == null)
            {
                // The scene still has its old one-piece table: the classic model takes its place when there is one.
                sceneSurface = origin;
                sceneBodyRenderer = origin.TemplateLayoutFrame.GetComponent<Renderer>();
                sceneBodyWasEnabled = sceneBodyRenderer != null && sceneBodyRenderer.enabled;
                baseTable = PlaceClassicModel();
            }

            classicSurface = baseTable != null ? baseTable.PlayingArea.GetComponent<PhysicalTabletopSurface>() : origin;
            if (baseTable != null)
            {
                classicPlayingArea = baseTable.PlayingArea.sharedMaterial;
                classicBody = baseTable.Sides != null ? baseTable.Sides.sharedMaterial
                    : baseTable.LowerFrame != null ? baseTable.LowerFrame.sharedMaterial : classicPlayingArea;
                Transform[] legs = baseTable.Legs;
                baseLegHome = new Vector3[legs.Length];
                baseLegHomeScale = new Vector3[legs.Length];
                baseLegHeight = new float[legs.Length];
                float lowestLegTop = float.PositiveInfinity;
                for (int i = 0; i < legs.Length; i++)
                {
                    if (legs[i] == null) continue;
                    baseLegHome[i] = legs[i].localPosition;
                    baseLegHomeScale[i] = legs[i].localScale;
                    Renderer legRenderer = legs[i].GetComponent<Renderer>();
                    baseLegHeight[i] = legRenderer != null ? legRenderer.bounds.size.y : 0f;
                    lowestLegTop = Mathf.Min(lowestLegTop, legs[i].position.y);
                }

                // How far below the playing area the legs (or the lower frame) begin: built tops reach that far.
                float frameTop = legs.Length > 0 && !float.IsPositiveInfinity(lowestLegTop) ? lowestLegTop
                    : baseTable.LowerFrame != null ? baseTable.LowerFrame.bounds.max.y : classicFrame.Centre.y;
                baseBodyDepth = Mathf.Clamp(classicFrame.Centre.y - frameTop, 0f, 3f * classicFrame.WorldPerTableUnit);
                if (baseBodyDepth < 0.05f) baseBodyDepth = 0f;
            }
            else
            {
                Renderer playingAreaRenderer = origin.GetComponent<Renderer>();
                classicPlayingArea = playingAreaRenderer != null ? playingAreaRenderer.sharedMaterial : null;
                classicBody = sceneBodyRenderer != null ? sceneBodyRenderer.sharedMaterial : null;
                if (classicPlayingArea == null) classicPlayingArea = classicBody;
            }

            if (classicSurface == null || classicPlayingArea == null)
            {
                Debug.LogWarning($"[{TableArea}] The table has no playing area or material to use, so the table cannot change.", this);
                classicSurface = null;
                return false;
            }

            return true;
        }

        // The frame and the playing-area centre and size, from the surface that is the Template Layout Origin.
        private bool TryMeasureFrame(PhysicalTabletopSurface origin, Collider surfaceCollider, out TableFrame frameOut)
        {
            frameOut = default;
            Transform frame = origin.TemplateLayoutFrame;
            Vector3 up = frame.up.normalized;
            Bounds bounds = surfaceCollider.bounds;
            float reach = bounds.extents.magnitude + 1f;
            if (!surfaceCollider.Raycast(new Ray(bounds.center + (up * reach), -up), out RaycastHit top, reach * 2f))
            {
                Debug.LogWarning($"[{TableArea}] The table's top could not be found from above, so the table cannot change.", this);
                return false;
            }

            Vector3 right = Vector3.ProjectOnPlane(frame.right, up).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(frame.forward, up).normalized;
            float localUnits = origin.LocalUnitsPerTableUnit;
            float worldPerUnit = frame.TransformVector(Vector3.right * localUnits).magnitude * worldUnitsPerTableUnit;
            if (worldPerUnit <= 0f || float.IsNaN(worldPerUnit) || forward.sqrMagnitude < 0.5f)
            {
                Debug.LogWarning($"[{TableArea}] The table's frame has no usable size, so the table cannot change.", this);
                return false;
            }

            Vector3 extents = bounds.extents;
            float width = 2f * Vector3.Dot(TableAbs(right), extents);
            float depth = 2f * Vector3.Dot(TableAbs(forward), extents);
            frameOut = new TableFrame(
                top.point,
                Quaternion.LookRotation(forward, up),
                frame,
                localUnits,
                worldPerUnit,
                new Vector2(width / worldPerUnit, depth / worldPerUnit));
            return true;
        }

        // The scene has only its old table: places the "classic" model there as the base table and turns the old
        // table off for good. Null (the old table stays) when there is no classic model or it cannot be used.
        private TableModel PlaceClassicModel()
        {
            TableModel prefab = FindTableModel(ClassicModelId);
            if (prefab == null) return null;
            TableModel placed = Instantiate(prefab, classicFrame.Centre, classicFrame.Rotation);
            placed.name = $"Base Table ({prefab.DisplayName})";
            SceneManager.MoveGameObjectToScene(placed.gameObject, gameObject.scene);
            SetBaseTable(BaseTableView.Hidden); // baseTable is still null here: this turns the old table off
            if (placed.TryMakeSurface(classicFrame.Frame, classicFrame.LocalUnitsPerTableUnit, out string error))
            {
                return placed;
            }

            Debug.LogWarning($"[{TableArea}] The classic table model could not be used ({error}); using the scene's table.", this);
            Destroy(placed.gameObject);
            SetBaseTable(BaseTableView.Whole);
            return null;
        }

        // Built shapes in order (Star last), then every other table model, then Classic again.
        private TableSpec NextTableSpec()
        {
            LoadTableModels();
            TableSpec model = null;
            if (tableSpec.shape == TableShape.Star)
            {
                int first = NextCycleModel(-1);
                if (first >= 0) model = ModelSpec(tableModels[first]);
            }
            else if (tableSpec.shape == TableShape.Model)
            {
                int nextModel = NextCycleModel(tableModels.IndexOf(FindTableModel(tableSpec.modelId)));
                if (nextModel >= 0) model = ModelSpec(tableModels[nextModel]);
                else return new TableSpec { shape = TableShape.Classic, edge = tableSpec.edge };
            }

            if (model != null) return model;
            TableSpec built = TableSpec.DefaultFor(TableSpec.Next(tableSpec.shape), classicFrame.ClassicSize.x, classicFrame.ClassicSize.y);
            built.edge = tableSpec.edge;
            return built;
        }

        private TableSpec ModelSpec(TableModel model) =>
            new TableSpec { shape = TableShape.Model, modelId = model.ModelId, edge = tableSpec.edge };

        private void LoadTableModels()
        {
            if (tableModelsLoaded) return;
            tableModelsLoaded = true;
            TableModelLibrary library = Resources.Load<TableModelLibrary>(TableModelLibrary.ResourcePath);
            if (library != null) library.CollectUsable(tableModels);
        }

        // The models the Table row steps through after Oval: all but the base table (that one is Classic).
        private int NextCycleModel(int after)
        {
            for (int i = after + 1; i < tableModels.Count; i++)
            {
                string id = tableModels[i].ModelId;
                if (!IsBaseModel(id) && !string.Equals(id, ClassicModelId, StringComparison.OrdinalIgnoreCase)) return i;
            }

            return -1;
        }

        private TableModel FindTableModel(string modelId)
        {
            LoadTableModels();
            for (int i = 0; i < tableModels.Count; i++)
            {
                if (string.Equals(tableModels[i].ModelId, modelId, StringComparison.OrdinalIgnoreCase)) return tableModels[i];
            }

            return null;
        }

        // Places another model in place of the base table: its root (the centre of its playing area, at its top)
        // where the base playing area is, facing the same way. The base table and any built table are off first.
        private bool ShowModelTable(string modelId, out string error)
        {
            error = null;
            TableModel prefab = FindTableModel(modelId);
            if (prefab == null)
            {
                error = $"there is no table model called '{modelId}' in Resources/{TableModelLibrary.ResourcePath}";
                return false;
            }

            if (modelTable != null && !string.Equals(modelTableId, prefab.ModelId, StringComparison.OrdinalIgnoreCase))
            {
                Destroy(modelTable.gameObject);
                modelTable = null;
            }

            if (generatedTable != null) generatedTable.Hide();
            SetBaseTable(BaseTableView.Hidden);
            if (modelTable == null)
            {
                modelTable = Instantiate(prefab, classicFrame.Centre, classicFrame.Rotation);
                modelTable.name = $"Table Model ({prefab.DisplayName})";
                modelTableId = prefab.ModelId;
                SceneManager.MoveGameObjectToScene(modelTable.gameObject, gameObject.scene);
                if (!modelTable.TryMakeSurface(classicFrame.Frame, classicFrame.LocalUnitsPerTableUnit, out error))
                {
                    Destroy(modelTable.gameObject);
                    modelTable = null;
                    SetBaseTable(BaseTableView.Whole);
                    return false;
                }
            }
            else if (!modelTable.gameObject.activeSelf)
            {
                modelTable.gameObject.SetActive(true);
            }

            Physics.SyncTransforms();
            return true;
        }

        private void HideModelTable()
        {
            if (modelTable != null && modelTable.gameObject.activeSelf) modelTable.gameObject.SetActive(false);
        }

        // A warning when the model's playing area is smaller than the base one, where game layouts may not fit.
        private string ModelFitNote()
        {
            if (modelTable == null || modelTable.PlayingArea == null) return string.Empty;
            Bounds area = modelTable.PlayingArea.bounds;
            Vector2 classic = classicFrame.ClassicSize * classicFrame.WorldPerTableUnit;
            Vector3 right = classicFrame.Rotation * Vector3.right;
            Vector3 forward = classicFrame.Rotation * Vector3.forward;
            float width = 2f * Vector3.Dot(TableAbs(right), area.extents);
            float depth = 2f * Vector3.Dot(TableAbs(forward), area.extents);
            return width + 0.01f < classic.x || depth + 0.01f < classic.y
                ? " Its playing area is smaller than the classic one, so some game layouts may not fit."
                : string.Empty;
        }

        private static Vector3 TableAbs(Vector3 value) => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }
}
