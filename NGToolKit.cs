using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class NgToolBox : EditorWindow
{
    //── Tab navigation ────────────────────────────────────────────────────────//
    private enum Tab { Utilities, BatchRenamer, PrefabBatch }
    private Tab ActiveTab = Tab.Utilities;

    private static readonly string[] TabLabels =
        { "Utilities", "Batch Renamer", "Prefab Batch Edit" };

    //── Utilities : Apply Material ────────────────────────────────────────────//
    private Material SelectedMaterial;
    private int MaterialSlotIndex = 0;

    // Cached slot dropdown — rebuilt whenever the scene selection changes
    private string[] _SlotLabels = { "— no selection —" };
    private int[] _SlotIndices = { 0 };
    private bool _SlotsMixed = false; // true when selected objects have different slot counts

    //── Utilities : Round Transforms ─────────────────────────────────────────//
    private float RoundStep = 0.25f;
    private bool RoundPosition = true;
    private bool RoundRotation = true;
    private bool RoundScale = true;

    //── Batch Renamer ─────────────────────────────────────────────────────────//
    private string RenamePrefix = "";
    private string RenameSuffix = "";
    private string RenameFind = "";
    private string RenameReplace = "";
    private bool RenameAddIndex = false;
    private int RenameIndexStart = 0;

    private enum RenameTarget { Inspector, ProjectFolder }
    private RenameTarget SelectedRenameTarget = RenameTarget.Inspector;

    //── Prefab Batch Edit ─────────────────────────────────────────────────────//
    private DefaultAsset PrefabFolder = null;
    private bool SearchRecursive = true;
    private bool EditSetStatic = false;
    private StaticEditorFlags StaticFlags = StaticEditorFlags.BatchingStatic
                                               | StaticEditorFlags.ContributeGI
                                               | StaticEditorFlags.OccludeeStatic
                                               | StaticEditorFlags.OccluderStatic
                                               | StaticEditorFlags.ReflectionProbeStatic;
    private bool EditResetPosition = false;
    private bool EditResetRotation = false;
    private bool EditResetScale = false;
    private bool EditApplyStaticToChildren = true;

    [MenuItem("NG Tools/Tool box")]
    public static void ShowWindow()
    {
        GetWindow<NgToolBox>("Tool Box");
    }

    // Called by Unity whenever the editor selection changes
    private void OnSelectionChange()
    {
        RebuildSlotDropdown();
        Repaint();
    }

    // Builds _SlotLabels / _SlotIndices from the shared slots across all selected
    // renderers. Uses the MINIMUM slot count so every entry is valid on every object.
    // _SlotsMixed is flagged when objects disagree on total slot count.
    private void RebuildSlotDropdown()
    {
        GameObject[] Selected = Selection.gameObjects;

        List<Renderer> Renderers = new List<Renderer>();
        foreach (GameObject Go in Selected)
        {
            Renderer R = Go.GetComponent<Renderer>();
            if (R != null) Renderers.Add(R);
        }

        if (Renderers.Count == 0)
        {
            _SlotLabels = new[] { "\u2014 no renderer in selection \u2014" };
            _SlotIndices = new[] { 0 };
            _SlotsMixed = false;
            MaterialSlotIndex = 0;
            return;
        }

        int MinSlots = int.MaxValue;
        int MaxSlots = 0;
        foreach (Renderer R in Renderers)
        {
            int C = R.sharedMaterials.Length;
            if (C < MinSlots) MinSlots = C;
            if (C > MaxSlots) MaxSlots = C;
        }

        _SlotsMixed = (MinSlots != MaxSlots);

        if (MinSlots == 0)
        {
            _SlotLabels = new[] { "\u2014 no material slots \u2014" };
            _SlotIndices = new[] { 0 };
            MaterialSlotIndex = 0;
            return;
        }

        // Label each slot using the first renderer as representative.
        // Slots beyond MinSlots are excluded — they don't exist on every object.
        Material[] Mats = Renderers[0].sharedMaterials;
        _SlotLabels = new string[MinSlots];
        _SlotIndices = new int[MinSlots];

        for (int i = 0; i < MinSlots; i++)
        {
            string MatName = (Mats[i] != null) ? Mats[i].name : "None";
            _SlotLabels[i] = $"[{i}]  {MatName}";
            _SlotIndices[i] = i;
        }

        // Clamp remembered slot to the new valid range
        if (MaterialSlotIndex >= MinSlots)
            MaterialSlotIndex = 0;
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);

        // Tab bar
        ActiveTab = (Tab)GUILayout.Toolbar((int)ActiveTab, TabLabels);

        EditorGUILayout.Space(8);

        switch (ActiveTab)
        {
            case Tab.Utilities: DrawUtilitiesTab(); break;
            case Tab.BatchRenamer: DrawRenamerTab(); break;
            case Tab.PrefabBatch: DrawPrefabBatchTab(); break;
        }
    }

    //=========================== UTILITIES TAB GUI ===========================//

    private void DrawUtilitiesTab()
    {
        //───────────────────────── Apply Material ─────────────────────────────//
        GUILayout.Label("Apply Material", EditorStyles.boldLabel);

        SelectedMaterial = (Material)EditorGUILayout.ObjectField(
            "Material", SelectedMaterial, typeof(Material), false);

        //───────────────────────── Material slot dropdown ─────────────────────//
        // Populated from the current scene selection via RebuildSlotDropdown().
        // Shows "[index]  MaterialName" for every slot shared by all selected renderers.
        {
            EditorGUI.BeginDisabledGroup(Selection.gameObjects.Length == 0);

            int PopupIndex = System.Array.IndexOf(_SlotIndices, MaterialSlotIndex);
            if (PopupIndex < 0) PopupIndex = 0;

            PopupIndex = EditorGUILayout.Popup("Material Slot", PopupIndex, _SlotLabels);
            MaterialSlotIndex = _SlotIndices[PopupIndex];

            EditorGUI.EndDisabledGroup();
        }

        if (_SlotsMixed)
            EditorGUILayout.HelpBox(
                "Selected objects have different slot counts. Only common slots are shown. " +
                "Objects with fewer slots than the selection maximum will be skipped.",
                MessageType.Warning);

        if (SelectedMaterial == null)
            EditorGUILayout.HelpBox("Select a material to apply.", MessageType.Info);
        else
        {
            EditorGUI.BeginDisabledGroup(Selection.gameObjects.Length == 0);
            if (GUILayout.Button("Apply Material to Selection"))
                ApplyMaterialToSelection();
            EditorGUI.EndDisabledGroup();

            if (Selection.gameObjects.Length == 0)
                EditorGUILayout.HelpBox("Select objects in the scene.", MessageType.Info);
        }

        EditorGUILayout.Space(12);
        DrawSeparator();
        EditorGUILayout.Space(12);

        //───────────────────────── Round Transforms ─────────────────────────────//
        GUILayout.Label("Round Transform Values", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        RoundPosition = EditorGUILayout.ToggleLeft("Position", RoundPosition, GUILayout.Width(70));
        RoundRotation = EditorGUILayout.ToggleLeft("Rotation", RoundRotation, GUILayout.Width(70));
        RoundScale = EditorGUILayout.ToggleLeft("Scale", RoundScale, GUILayout.Width(70));
        EditorGUILayout.EndHorizontal();

        if (RoundRotation)
            EditorGUILayout.HelpBox(
                "Rounding rotation uses localEulerAngles. Objects near 90°/180° may flip due to gimbal lock.",
                MessageType.Warning);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Round Step", GUILayout.Width(70));
        RoundStep = EditorGUILayout.FloatField(RoundStep, GUILayout.Width(60));

        bool CanRound = RoundStep > 0f && (RoundPosition || RoundRotation || RoundScale)
                        && Selection.gameObjects.Length > 0;

        EditorGUI.BeginDisabledGroup(!CanRound);
        if (GUILayout.Button("Round Transforms"))
            RoundTransformSelection();
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        if (RoundStep <= 0f)
            EditorGUILayout.HelpBox("Round step must be greater than 0.", MessageType.Warning);
        else if (Selection.gameObjects.Length == 0)
            EditorGUILayout.HelpBox("Select objects in the scene.", MessageType.Info);
    }

    // =========================== BATCH RENAMER TAB ===========================//

    private void DrawRenamerTab()
    {
        SelectedRenameTarget = (RenameTarget)GUILayout.Toolbar(
            (int)SelectedRenameTarget,
            new[] { "Inspector Selection", "Project Folder Selection" });

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Prefix", GUILayout.Width(42));
        RenamePrefix = EditorGUILayout.TextField(RenamePrefix);
        EditorGUILayout.LabelField("Suffix", GUILayout.Width(42));
        RenameSuffix = EditorGUILayout.TextField(RenameSuffix);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Find", GUILayout.Width(42));
        RenameFind = EditorGUILayout.TextField(RenameFind);
        EditorGUILayout.LabelField("Replace", GUILayout.Width(50));
        RenameReplace = EditorGUILayout.TextField(RenameReplace);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        RenameAddIndex = EditorGUILayout.ToggleLeft("Add Index", RenameAddIndex, GUILayout.Width(85));
        EditorGUI.BeginDisabledGroup(!RenameAddIndex);
        EditorGUILayout.LabelField("Start at", GUILayout.Width(55));
        RenameIndexStart = EditorGUILayout.IntField(RenameIndexStart, GUILayout.Width(40));
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField($"Preview:  {BuildName("PrefabName", RenameIndexStart)}", EditorStyles.helpBox);
        EditorGUILayout.Space(2);

        if (GUILayout.Button("Rename"))
        {
            if (SelectedRenameTarget == RenameTarget.Inspector)
                BatchRenameInspectorSelection();
            else
                BatchRenameProjectSelection();
        }
    }

    //=========================== PREFAB BATCH EDIT TAB ===========================//

    private void DrawPrefabBatchTab()
    {
        // Folder picker — accepts a DefaultAsset that maps to a folder
        PrefabFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "Prefab Folder", PrefabFolder, typeof(DefaultAsset), false);

        if (PrefabFolder != null)
        {
            string FolderPath = AssetDatabase.GetAssetPath(PrefabFolder);
            if (!AssetDatabase.IsValidFolder(FolderPath))
            {
                EditorGUILayout.HelpBox("The selected asset is not a folder.", MessageType.Warning);
                PrefabFolder = null;
            }
        }

        SearchRecursive = EditorGUILayout.Toggle("Include Sub-folders", SearchRecursive);
        EditorGUILayout.Space(4);

        // Set Static
        EditSetStatic = EditorGUILayout.BeginToggleGroup("Set Static", EditSetStatic);
        StaticFlags = (StaticEditorFlags)EditorGUILayout.EnumFlagsField("Static Flags", StaticFlags);
        EditorGUI.BeginDisabledGroup(!EditSetStatic);
        EditApplyStaticToChildren = EditorGUILayout.Toggle("Apply to Children", EditApplyStaticToChildren);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndToggleGroup();

        EditorGUILayout.Space(4);

        // Reset Transform
        GUILayout.Label("Reset Transform", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Resets position / rotation / scale on the prefab root only.", MessageType.None);
        EditorGUILayout.BeginHorizontal();
        EditResetPosition = EditorGUILayout.ToggleLeft("Position", EditResetPosition, GUILayout.Width(70));
        EditResetRotation = EditorGUILayout.ToggleLeft("Rotation", EditResetRotation, GUILayout.Width(70));
        EditResetScale = EditorGUILayout.ToggleLeft("Scale", EditResetScale, GUILayout.Width(70));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        bool AnyOpEnabled = EditSetStatic || EditResetPosition || EditResetRotation || EditResetScale;
        EditorGUI.BeginDisabledGroup(PrefabFolder == null || !AnyOpEnabled);
        if (GUILayout.Button("Apply to Prefabs"))
            ApplyPrefabBatchEdit();
        EditorGUI.EndDisabledGroup();

        if (PrefabFolder == null)
            EditorGUILayout.HelpBox("Select a folder to enable batch editing.", MessageType.Info);
        else if (!AnyOpEnabled)
            EditorGUILayout.HelpBox("Enable at least one operation above.", MessageType.Info);
    }

    //=========================== LOGIC ===========================//

    void ApplyMaterialToSelection()
    {
        if (SelectedMaterial == null) { Debug.LogWarning("NgToolBox: No material selected."); return; }

        GameObject[] SelectedObjects = Selection.gameObjects;
        if (SelectedObjects.Length == 0) { Debug.LogWarning("NgToolBox: No objects selected."); return; }

        foreach (GameObject Obj in SelectedObjects)
        {
            Renderer ObjRenderer = Obj.GetComponent<Renderer>();
            if (ObjRenderer == null) continue;

            if (MaterialSlotIndex >= ObjRenderer.sharedMaterials.Length)
            {
                Debug.LogWarning($"NgToolBox: '{Obj.name}' only has {ObjRenderer.sharedMaterials.Length} " +
                                 $"material slot(s). Slot {MaterialSlotIndex} does not exist — skipped.");
                continue;
            }

            Undo.RecordObject(ObjRenderer, "Apply Material To Selection");
            Material[] Mats = ObjRenderer.sharedMaterials;
            Mats[MaterialSlotIndex] = SelectedMaterial;
            ObjRenderer.sharedMaterials = Mats;
        }

        Debug.Log($"NgToolBox: Material applied to slot {MaterialSlotIndex} on {SelectedObjects.Length} object(s).");
    }

    void RoundTransformSelection()
    {
        GameObject[] SelectedObjects = Selection.gameObjects;
        if (SelectedObjects.Length == 0) { Debug.LogWarning("NgToolBox: No objects selected."); return; }

        foreach (GameObject Obj in SelectedObjects)
        {
            Transform T = Obj.transform;
            Undo.RecordObject(T, "Round Transform Values");

            if (RoundPosition) T.localPosition = RoundVector3(T.localPosition, RoundStep);
            if (RoundRotation) T.localRotation = Quaternion.Euler(RoundVector3(T.localEulerAngles, RoundStep));
            if (RoundScale) T.localScale = RoundVector3(T.localScale, RoundStep);
        }

        Debug.Log($"NgToolBox: Transforms rounded to nearest {RoundStep} for {SelectedObjects.Length} object(s).");
    }

    void BatchRenameInspectorSelection()
    {
        GameObject[] SelectedObjects = Selection.gameObjects;
        if (SelectedObjects.Length == 0) { Debug.LogWarning("NgToolBox: No objects selected in the Inspector."); return; }

        for (int i = 0; i < SelectedObjects.Length; i++)
        {
            Undo.RecordObject(SelectedObjects[i], "Batch Rename");
            SelectedObjects[i].name = BuildName(SelectedObjects[i].name, RenameIndexStart + i);
        }

        Debug.Log($"NgToolBox: Renamed {SelectedObjects.Length} object(s) in the Inspector.");
    }

    void BatchRenameProjectSelection()
    {
        Object[] SelectedAssets = Selection.GetFiltered<Object>(SelectionMode.Assets);
        if (SelectedAssets.Length == 0) { Debug.LogWarning("NgToolBox: No assets selected in the Project window."); return; }

        int RenameIndex = RenameIndexStart;
        int RenamedCount = 0;

        foreach (Object Asset in SelectedAssets)
        {
            string AssetPath = AssetDatabase.GetAssetPath(Asset);
            if (AssetDatabase.IsValidFolder(AssetPath)) { Debug.LogWarning($"NgToolBox: '{Asset.name}' is a folder — skipped."); continue; }

            string Error = AssetDatabase.RenameAsset(AssetPath, BuildName(Asset.name, RenameIndex));
            if (string.IsNullOrEmpty(Error)) { RenamedCount++; RenameIndex++; }
            else Debug.LogWarning($"NgToolBox: Could not rename '{Asset.name}': {Error}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"NgToolBox: Renamed {RenamedCount} asset(s) in the Project.");
    }

    string BuildName(string BaseName, int Index)
    {
        string Result = string.IsNullOrEmpty(RenameFind) ? BaseName : BaseName.Replace(RenameFind, RenameReplace);
        Result = RenamePrefix + Result + RenameSuffix;
        if (RenameAddIndex) Result += $"_{Index}";
        return Result;
    }

    void ApplyPrefabBatchEdit()
    {
        string FolderPath = AssetDatabase.GetAssetPath(PrefabFolder);

        string[] Guids = AssetDatabase.FindAssets("t:Prefab", new[] { FolderPath });

        int EditedCount = 0;

        foreach (string Guid in Guids)
        {
            string Path = AssetDatabase.GUIDToAssetPath(Guid);

            if (!SearchRecursive)
            {
                string RelativePath = Path.Substring(FolderPath.Length).TrimStart('/');
                if (RelativePath.Contains("/")) continue;
            }

            GameObject PrefabRoot = PrefabUtility.LoadPrefabContents(Path);

            //── Reset root transform ──────────────────────────────────────────//
            Transform RootTransform = PrefabRoot.transform;
            if (EditResetPosition) RootTransform.localPosition = Vector3.zero;
            if (EditResetRotation) RootTransform.localRotation = Quaternion.identity;
            if (EditResetScale) RootTransform.localScale = Vector3.one;

            //── Set static flags ──────────────────────────────────────────────//
            if (EditSetStatic)
            {
                IEnumerable<Transform> FlagTargets = EditApplyStaticToChildren
                    ? PrefabRoot.GetComponentsInChildren<Transform>(true)
                    : new[] { RootTransform };

                foreach (Transform T in FlagTargets)
                    GameObjectUtility.SetStaticEditorFlags(T.gameObject, StaticFlags);
            }

            PrefabUtility.SaveAsPrefabAsset(PrefabRoot, Path);
            PrefabUtility.UnloadPrefabContents(PrefabRoot);
            EditedCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"NgToolBox: Batch edited {EditedCount} prefab(s) in '{FolderPath}'.");
    }

    //─────────────────────────── Helpers ─────────────────────────────────────//

    static Vector3 RoundVector3(Vector3 V, float Step) => new Vector3(
        RoundToStep(V.x, Step),
        RoundToStep(V.y, Step),
        RoundToStep(V.z, Step));

    static float RoundToStep(float Value, float Step) =>
        Mathf.Round(Value / Step) * Step;

    static void DrawSeparator()
    {
        Rect Rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(Rect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
    }
}