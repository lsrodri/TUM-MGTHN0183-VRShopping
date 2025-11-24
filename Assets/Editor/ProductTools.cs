using UnityEngine;
using UnityEditor;

public class ProductTools : Editor
{
    // Key used to store the path in Unity's Editor Preferences
    private const string PREF_LAST_DIR = "VRClass_LastProductDir";

    [MenuItem("Tools/VR Research/Create Product from Prefab %g")]
    public static void CreateProductFromPrefab()
    {
        // 1. Find Shelf (Same as before)
        GameObject[] allShelves = GameObject.FindGameObjectsWithTag("WarehouseShelf");
        if (allShelves.Length == 0) return; // (Add your error dialogs back here)

        System.Array.Sort(allShelves, (a, b) => string.Compare(a.name, b.name));
        Transform targetShelf = null;
        foreach (GameObject shelf in allShelves)
        {
            if (shelf.transform.childCount == 0)
            {
                targetShelf = shelf.transform;
                break;
            }
        }
        if (targetShelf == null)
        {
            EditorUtility.DisplayDialog("Shelves Full", "No empty shelves available!", "OK");
            return;
        }

        // 2. Open File Picker with Memory
        // Retrieve last path, default to "Assets" if empty
        string lastDir = EditorPrefs.GetString(PREF_LAST_DIR, "Assets");

        // If the saved path no longer exists, fallback to Assets
        if (!System.IO.Directory.Exists(lastDir))
        {
            lastDir = "Assets";
        }

        string path = EditorUtility.OpenFilePanel("Select Product Prefab", lastDir, "prefab,fbx,obj");

        if (string.IsNullOrEmpty(path)) return; // User cancelled

        // SAVE the directory of the selected file for next time
        string selectedDir = System.IO.Path.GetDirectoryName(path);
        EditorPrefs.SetString(PREF_LAST_DIR, selectedDir);

        // ... Rest of logic (GetProjectRelativePath, Instantiate, etc.) ...
        path = FileUtil.GetProjectRelativePath(path);
        GameObject selectedAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (selectedAsset == null)
        {
            Debug.LogError("Could not load asset. Is it outside the project folder?");
            return;
        }

        // 3. Create Container & Parent (Same as before)
        GameObject container = new GameObject(selectedAsset.name);
        Undo.RegisterCreatedObjectUndo(container, "Create Product");
        Undo.SetTransformParent(container.transform, targetShelf, "Parent");
        container.transform.localPosition = Vector3.zero;
        container.transform.localRotation = Quaternion.identity;
        container.AddComponent<TrialProduct>();

        GameObject art = (GameObject)PrefabUtility.InstantiatePrefab(selectedAsset);
        Undo.SetTransformParent(art.transform, container.transform, "Parent Art");
        art.transform.localPosition = Vector3.zero;
        art.transform.localRotation = Quaternion.identity;

        Selection.activeGameObject = container;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
    }
}
