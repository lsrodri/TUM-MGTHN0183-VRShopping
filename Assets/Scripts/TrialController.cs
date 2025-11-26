using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.Networking; // Required for Android/Quest Loading
using TMPro;

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

    private Dictionary<int, float> priceDatabase = new Dictionary<int, float>();
    private Dictionary<int, TrialProduct> sceneInventory = new Dictionary<int, TrialProduct>();
    private List<TrialProduct> activeProducts = new List<TrialProduct>();


    void Start()
    {
        // 1. Load Preferences
        currentParticipantID = PlayerPrefs.GetInt("ParticipantID", 1);
        currentTrialNumber = PlayerPrefs.GetInt("TrialNumber", 1);

        Debug.Log($"Initializing TrialController... P:{currentParticipantID}, T:{currentTrialNumber}");

        // 2. Index the physical objects (Synchronous)
        IndexSceneInventory();

        // 3. Start Async Loading for CSVs
        StartCoroutine(InitializeExperiment());
    }

    // --- ASYNC LOADER (Required for Android/Quest) ---
    IEnumerator InitializeExperiment()
    {
        // A. Load Products.csv
        string productsPath = Path.Combine(Application.streamingAssetsPath, productsCsvName);
        string productsContent = "";

        // Check if we need UnityWebRequest (Android/WebGL) or System.IO (Editor/PC)
        if (productsPath.Contains("://") || productsPath.Contains("jar:"))
        {
            UnityWebRequest www = UnityWebRequest.Get(productsPath);
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed to load Products.csv: {www.error}");
            }
            else
            {
                productsContent = www.downloadHandler.text;
            }
        }
        else
        {
            // PC / Editor Fallback
            if (File.Exists(productsPath)) productsContent = File.ReadAllText(productsPath);
            else Debug.LogError($"Products.csv not found at {productsPath}");
        }

        // Parse Products
        ParseProductDatabase(productsContent);


        // B. Load Trials.csv
        string trialsPath = Path.Combine(Application.streamingAssetsPath, trialsCsvName);
        string trialsContent = "";

        if (trialsPath.Contains("://") || trialsPath.Contains("jar:"))
        {
            UnityWebRequest www = UnityWebRequest.Get(trialsPath);
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed to load Trials.csv: {www.error}");
            }
            else
            {
                trialsContent = www.downloadHandler.text;
            }
        }
        else
        {
            if (File.Exists(trialsPath)) trialsContent = File.ReadAllText(trialsPath);
            else Debug.LogError($"Trials.csv not found at {trialsPath}");
        }

        // Parse Trials and Run
        if (ParseTrialsCSV(trialsContent))
        {
            RunTrial(currentParticipantID, currentTrialNumber);
        }
    }


    // --- PARSING LOGIC ---

    void ParseProductDatabase(string csvText)
    {
        if (string.IsNullOrEmpty(csvText)) return;

        // Handle different line endings (Windows \r\n vs Unix \n)
        string[] lines = csvText.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);

        // Skip Header (i=1)
        for (int i = 1; i < lines.Length; i++)
        {
            string[] cols = lines[i].Split(',');
            if (cols.Length < 3) continue;

            // Format: ID, Name, Price
            if (int.TryParse(cols[0], out int id) && float.TryParse(cols[2], out float price))
            {
                if (!priceDatabase.ContainsKey(id))
                    priceDatabase.Add(id, price);
            }
        }
        Debug.Log($"Product DB Loaded: {priceDatabase.Count} entries.");
    }

    bool ParseTrialsCSV(string csvText)
    {
        if (string.IsNullOrEmpty(csvText)) return false;

        string[] lines = csvText.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);

        for (int i = 1; i < lines.Length; i++)
        {
            string[] cols = lines[i].Split(',');
            if (cols.Length < 6) continue;

            TrialData t = new TrialData();
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


    // --- SCENE LOGIC (Unchanged) ---

    void IndexSceneInventory()
    {
        var products = FindObjectsByType<TrialProduct>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var p in products)
        {
            p.gameObject.SetActive(false);
            if (!sceneInventory.ContainsKey(p.productID))
                sceneInventory.Add(p.productID, p);
        }
    }

    public void RunTrial(int pID, int trialNum)
    {
        foreach (var p in activeProducts) p.gameObject.SetActive(false);
        activeProducts.Clear();

        TrialData data = allTrials.Find(t => t.participantID == pID && t.trialNumber == trialNum);

        if (data == null)
        {
            Debug.Log($"No data for P:{pID} T:{trialNum}. Experiment Complete?");
            if (shelfOneLabel) shelfOneLabel.text = "";
            if (shelfTwoLabel) shelfTwoLabel.text = "";
            if (shelfThreeLabel) shelfThreeLabel.text = "";
            if (shelfFourLabel) shelfFourLabel.text = "";
            return;
        }

        Debug.Log($"Running Trial {trialNum}...");

        PlaceProductOnShelf(data.shelfOneProductID, shelfOnePosition, shelfOneLabel);
        PlaceProductOnShelf(data.shelfTwoProductID, shelfTwoPosition, shelfTwoLabel);
        PlaceProductOnShelf(data.shelfThreeProductID, shelfThreePosition, shelfThreeLabel);
        PlaceProductOnShelf(data.shelfFourProductID, shelfFourPosition, shelfFourLabel);
    }

    void PlaceProductOnShelf(int productID, Transform shelfSlot, TextMeshPro shelfLabel)
    {
        if (sceneInventory.TryGetValue(productID, out TrialProduct product))
        {
            product.transform.SetParent(null);
            product.transform.position = shelfSlot.position;
            product.transform.rotation = shelfSlot.rotation;
            product.gameObject.SetActive(true);
            activeProducts.Add(product);

            float price = priceDatabase.ContainsKey(productID) ? priceDatabase[productID] : 0f;

            if (shelfLabel != null) shelfLabel.text = price.ToString("F2");
        }
        else
        {
            Debug.LogError($"Missing Product ID {productID}!");
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
