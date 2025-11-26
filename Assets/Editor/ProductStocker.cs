using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class ProductStocker : EditorWindow
{
    // State variables
    string targetID = "";
    const string PREF_LAST_DIR = "VRClass_LastProductDir";

    [MenuItem("Tools/VR Research/Stock Shelf with Product %g")] // Ctrl+G
    public static void ShowWindow()
    {
        // Open a small utility window
        ProductStocker window = GetWindow<ProductStocker>("Stock Shelf");
        window.minSize = new Vector2(300, 150);
        window.maxSize = new Vector2(500, 150);
    }

    void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("Product Assignment Settings", EditorStyles.boldLabel);
        GUILayout.Space(5);

        // 1. The Prompt
        targetID = EditorGUILayout.TextField("Specific ID (Optional):", targetID);
        GUILayout.Label("Leave empty to fill the next available slot.", EditorStyles.miniLabel);

        GUILayout.Space(15);

        if (GUILayout.Button("Select Prefab & Assign", GUILayout.Height(40)))
        {
            ExecuteStocking();
        }
    }

    void ExecuteStocking()
    {
        Transform targetSlot = null;

        // 2. Logic: Determine Target Slot
        if (!string.IsNullOrEmpty(targetID))
        {
            // CASE A: Specific ID requested
            GameObject foundObj = GameObject.Find(targetID);

            // Verify it exists and is a valid slot
            if (foundObj == null || !foundObj.CompareTag("ProductBundle"))
            {
                EditorUtility.DisplayDialog("Error", $"Slot ID '{targetID}' not found (or not tagged ProductBundle).", "OK");
                return;
            }

            // Verify it is empty
            if (foundObj.transform.childCount > 0)
            {
                EditorUtility.DisplayDialog("Slot Occupied",
                    $"Slot '{targetID}' is already full.\n\nPlease manually remove the items if you wish to replace them.",
                    "OK");
                return; // Kill process
            }

            targetSlot = foundObj.transform;
        }
        else
        {
            // CASE B: Auto-fill next empty
            GameObject[] allShelves = GameObject.FindGameObjectsWithTag("ProductBundle");

            // Sort numerically/alphabetically so we fill 1, then 2, then 3...
            // Using a natural sort helper or simple string compare
            System.Array.Sort(allShelves, (a, b) => CompareNatural(a.name, b.name));

            foreach (var shelf in allShelves)
            {
                if (shelf.transform.childCount == 0)
                {
                    targetSlot = shelf.transform;
                    break;
                }
            }

            if (targetSlot == null)
            {
                EditorUtility.DisplayDialog("Warehouse Full", "No empty slots available!", "OK");
                return;
            }
        }

        // 3. File Picker (Memory)
        string lastDir = EditorPrefs.GetString(PREF_LAST_DIR, "Assets");
        if (!System.IO.Directory.Exists(lastDir)) lastDir = "Assets";

        string path = EditorUtility.OpenFilePanel("Select Product Prefab", lastDir, "prefab,fbx,obj");
        if (string.IsNullOrEmpty(path)) return;

        EditorPrefs.SetString(PREF_LAST_DIR, System.IO.Path.GetDirectoryName(path));

        // Load Asset
        path = FileUtil.GetProjectRelativePath(path);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return;

        // 4. Instantiate & Parent
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instance, "Stock Shelf");

        Undo.SetTransformParent(instance.transform, targetSlot, "Parent to Slot");

        // Reset Transform initially
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localScale = Vector3.one; // Ensure scale is 1

        // 5. Z-Alignment Calculation
        ApplyZAlignment(instance);

        // Focus
        Selection.activeGameObject = targetSlot.gameObject; // Select the Parent (ID)
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();

        // Close window after success (optional, remove if you want to spam-add)
        Close();
    }

    // --- Helper: Auto-Align Z ---
    // --- Helper: Auto-Align Z (Negative Direction) ---
    void ApplyZAlignment(GameObject obj)
    {
        // 1. Calculate Bounds
        Bounds combinedBounds = new Bounds(obj.transform.position, Vector3.zero);
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0) return;

        foreach (Renderer r in renderers)
        {
            if (combinedBounds.size == Vector3.zero) combinedBounds = r.bounds;
            else combinedBounds.Encapsulate(r.bounds);
        }

        // 2. Calculate Offset
        // Current Scenario: Shelf edge is at Z=0. Inside of shelf is Negative Z.
        // Problem: Object pivot is center. Object Max Z sticks out to +0.1. Object Min Z is at -0.1.
        // Goal: Move object so its Max Z (the face) is exactly at Z=0.

        // Formula:
        // pivotToMaxZ = combinedBounds.max.z - obj.transform.position.z; (e.g. +0.1)
        // We want to move BACK by this amount.

        float pivotToMaxZ = combinedBounds.max.z - obj.transform.position.z;
        float pushAmount = -pivotToMaxZ;

        // Apply the offset
        obj.transform.localPosition = new Vector3(0, 0, pushAmount);

        Debug.Log($"Aligned {obj.name}: Pushed Z by {pushAmount:F4}m (Negative) to sit inside shelf.");
    }


    // --- Helper: Natural Sort (handles "1", "2", "10" correctly) ---
    int CompareNatural(string a, string b)
    {
        return EditorUtility.NaturalCompare(a, b);
    }
}
