using UnityEngine;
using System.Collections.Generic;
using System.IO;

[System.Serializable]
public class TrialData {
    public float optionA_Self;
    public float optionA_Other;
    public float optionB_Self;
    public float optionB_Other;

    // Constructor for initializing trial data values.
    public TrialData(float aSelf, float aOther, float bSelf, float bOther) {
        optionA_Self = aSelf;
        optionA_Other = aOther;
        optionB_Self = bSelf;
        optionB_Other = bOther;
    }
}

public class TrialDataManager : MonoBehaviour {
    // Two static lists are prepared so that the same monetary allocations are available for both conditions.
    public static List<TrialData> DeceptionTrials = new List<TrialData>();
    public static List<TrialData> ControlTrials = new List<TrialData>();

    void Awake() {
        LoadTrialData();
    }

    void LoadTrialData() {
        // Place your CSV file (e.g., TrialData.csv) in the StreamingAssets folder.
        string filePath = Path.Combine(Application.streamingAssetsPath, "TrialData.csv");
        if (File.Exists(filePath)) {
            string[] dataLines = File.ReadAllLines(filePath);
            // Assumes the first row of CSV is a header; 46 subsequent lines provide trial data.
            for (int i = 1; i < dataLines.Length; i++) {
                string line = dataLines[i];
                string[] values = line.Split(',');
                if (values.Length >= 4) {
                    if (float.TryParse(values[0], out float aSelf) && 
                        float.TryParse(values[1], out float aOther) &&
                        float.TryParse(values[2], out float bSelf) &&
                        float.TryParse(values[3], out float bOther)) {
                        TrialData trial = new TrialData(aSelf, aOther, bSelf, bOther);
                        // Use the same trial order for both tasks.
                        DeceptionTrials.Add(trial);
                        ControlTrials.Add(trial);
                    }
                }
            }
        } else {
            Debug.LogError("TrialData.csv not found in StreamingAssets folder!");
        }
    }
}
