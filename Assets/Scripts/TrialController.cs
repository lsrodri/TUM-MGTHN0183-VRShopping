using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro; // Required for TextMeshPro

public class TrialController : MonoBehaviour
{
    [Header("Configuration")]
    public string trialsCsvName = "Trials.csv";
    public string productsCsvName = "Products.csv";

    [Header("Shelf Locations (Where items spawn)")]
    public Transform shelfOnePosition;
    public Transform shelfTwoPosition;
    public Transform shelfThreePosition;
    public Transform shelfFourPosition;

    [Header("Shelf Price Tags (Drag TextMeshPro objects here)")]
    public TextMeshPro shelfOneLabel;
    public TextMeshPro shelfTwoLabel;
    public TextMeshPro shelfThreeLabel;
    public TextMeshPro shelfFourLabel;

    [Header("Status")]
    public int currentParticipantID;
    public int currentTrialNumber;

    // --- Internal Data Structures ---

    // 1. The Experiment Logic (From Trials.csv)
    [System.Serializable]
    public class TrialData
    {
        public int participantID;
        public int trialNumber;

        public int shelfOneProductID;
        public int shelfTwoProductID;
        public int shelfThreeProductID;
        public int shelfFourProductID;
    }
    private List<TrialData> allTrials = new List<TrialData>();

    // 2. The Product Database (From Products.csv)
    // Key: ProductID, Value: Price
    private Dictionary<int, float> priceDatabase = new Dictionary<int, float>();

    // 3. The Scene Inventory (From Hierarchy)
    // Key: ProductID, Value: The Physical GameObject Script
    private Dictionary<int, TrialProduct> sceneInventory = new Dictionary<int, TrialProduct>();

    // Track what is currently showing to hide it later
    private List<TrialProduct> activeProducts = new List<TrialProduct>();


    void Start()
    {
        // 1. Load Preferences (Defaults to 1)
        currentParticipantID = PlayerPrefs.GetInt("ParticipantID", 1);
        currentTrialNumber = PlayerPrefs.GetInt("TrialNumber", 1);

        Debug.Log($"Initializing TrialController... P:{currentParticipantID}, T:{currentTrialNumber}");

        // 2. Index the physical objects in the scene
        IndexSceneInventory();

        // 3. Load the Data Files
        LoadProductDatabase();
        bool trialsLoaded = LoadTrialsCSV();

        // 4. Start Experiment
        if (trialsLoaded)
        {
            RunTrial(currentParticipantID, currentTrialNumber);
        }
    }

    // --- Step A: Indexing ---
    void IndexSceneInventory()
    {
        // Find all TrialProduct scripts, even if disabled
        var products = FindObjectsByType<TrialProduct>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var p in products)
        {
            p.gameObject.SetActive(false); // Hide everything initially

            if (!sceneInventory.ContainsKey(p.productID))
            {
                sceneInventory.Add(p.productID, p);
            }
            else
            {
                Debug.LogWarning($"Duplicate Product ID {p.productID} found on object '{p.name}'. Check your scene!");
            }
        }
        Debug.Log($"Inventory Indexed: Found {sceneInventory.Count} physical products.");
    }

    // --- Step B: Loading Data ---
    void LoadProductDatabase()
    {
        string path = Path.Combine(Application.streamingAssetsPath, productsCsvName);
        if (!File.Exists(path))
        {
            Debug.LogError($"Product Database not found at {path}");
            return;
        }

        string[] lines = File.ReadAllLines(path);
        // Skip Header (Row 0)
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            string[] cols = line.Split(',');

            if (cols.Length < 3) continue;

            // Format: ID, Name, Price
            if (int.TryParse(cols[0], out int id) && float.TryParse(cols[2], out float price))
            {
                if (!priceDatabase.ContainsKey(id))
                    priceDatabase.Add(id, price);
            }
        }
        Debug.Log($"Product DB Loaded: {priceDatabase.Count} prices defined.");
    }

    bool LoadTrialsCSV()
    {
        string path = Path.Combine(Application.streamingAssetsPath, trialsCsvName);
        if (!File.Exists(path))
        {
            Debug.LogError($"Trials file not found at {path}");
            return false;
        }

        string[] lines = File.ReadAllLines(path);
        // Skip Header
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            string[] cols = line.Split(',');

            if (cols.Length < 6) continue;

            TrialData t = new TrialData();
            // Parse Columns
            int.TryParse(cols[0], out t.participantID);
            int.TryParse(cols[1], out t.trialNumber);
            int.TryParse(cols[2], out t.shelfOneProductID);
            int.TryParse(cols[3], out t.shelfTwoProductID);
            int.TryParse(cols[4], out t.shelfThreeProductID);
            int.TryParse(cols[5], out t.shelfFourProductID);

            allTrials.Add(t);
        }
        Debug.Log($"Trials Loaded: {allTrials.Count} trials queued.");
        return true;
    }

    // --- Step C: Running Logic ---
    public void RunTrial(int pID, int trialNum)
    {
        // 1. Clear previous scene state
        foreach (var p in activeProducts) p.gameObject.SetActive(false);
        activeProducts.Clear();

        // 2. Find Trial Data
        TrialData data = allTrials.Find(t => t.participantID == pID && t.trialNumber == trialNum);

        if (data == null)
        {
            Debug.Log($"No data found for Participant {pID}, Trial {trialNum}. Experiment Finished?");
            // Optional: Clear labels if finished
            if (shelfOneLabel) shelfOneLabel.text = "";
            if (shelfTwoLabel) shelfTwoLabel.text = "";
            if (shelfThreeLabel) shelfThreeLabel.text = "";
            if (shelfFourLabel) shelfFourLabel.text = "";
            return;
        }

        Debug.Log($"Running Trial {trialNum}...");

        // 3. Execute Placement (Pass Label with Position)
        PlaceProductOnShelf(data.shelfOneProductID, shelfOnePosition, shelfOneLabel);
        PlaceProductOnShelf(data.shelfTwoProductID, shelfTwoPosition, shelfTwoLabel);
        PlaceProductOnShelf(data.shelfThreeProductID, shelfThreePosition, shelfThreeLabel);
        PlaceProductOnShelf(data.shelfFourProductID, shelfFourPosition, shelfFourLabel);
    }

    void PlaceProductOnShelf(int productID, Transform shelfSlot, TextMeshPro shelfLabel)
    {
        // A. Find the physical object
        if (sceneInventory.TryGetValue(productID, out TrialProduct product))
        {
            // B. Move it
            product.transform.SetParent(null); // Detach to avoid scale issues
            product.transform.position = shelfSlot.position;
            product.transform.rotation = shelfSlot.rotation;
            product.gameObject.SetActive(true);

            activeProducts.Add(product);

            // C. Look up Price
            float price = 0f;
            if (priceDatabase.ContainsKey(productID))
            {
                price = priceDatabase[productID];
            }
            else
            {
                Debug.LogWarning($"Price missing for Product {productID}, defaulting to 0.");
            }

            // D. Update the SHELF Label
            if (shelfLabel != null)
            {
                shelfLabel.text = price.ToString("F2");
            }
        }
        else
        {
            Debug.LogError($"CRITICAL: Trial asks for Product {productID}, but it is not in the Scene Inventory!");
            // Clear label if missing
            if (shelfLabel != null) shelfLabel.text = "---";
        }
    }

    public void NextTrial()
    {
        currentTrialNumber++;
        PlayerPrefs.SetInt("TrialNumber", currentTrialNumber);
        PlayerPrefs.Save();
        RunTrial(currentParticipantID, currentTrialNumber);
    }
}
