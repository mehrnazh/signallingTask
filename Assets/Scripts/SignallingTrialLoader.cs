using UnityEngine;
using System.Collections.Generic;
using System.IO;

// Added namespace
namespace SignallingTaskData
{
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

    [System.Serializable]
    public class AttentionTestData : TrialData {
        public string correctAnswer; // "A" or "B"
        
        public AttentionTestData(float aSelf, float aOther, float bSelf, float bOther, string correct) 
            : base(aSelf, aOther, bSelf, bOther) {
            correctAnswer = correct;
        }
    }

    public class SignallingTrialLoader : MonoBehaviour
    {
        public static SignallingTrialLoader Instance { get; private set; }

        public List<TrialData> DeceptionTrials { get; private set; } = new List<TrialData>();
        public List<TrialData> ControlTrials { get; private set; } = new List<TrialData>();
        public List<AttentionTestData> AttentionTests { get; private set; } = new List<AttentionTestData>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LoadTrialData();
                CreateAttentionTests();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void LoadTrialData()
        {
            DeceptionTrials.Clear();
            ControlTrials.Clear();
            string filePath = Path.Combine(Application.streamingAssetsPath, "TrialData.csv");
            if (File.Exists(filePath))
            {
                string[] dataLines = File.ReadAllLines(filePath);
                for (int i = 1; i < dataLines.Length; i++)
                {
                    string line = dataLines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    string[] values = line.Split(',');
                    if (values.Length >= 4)
                    {
                        if (float.TryParse(values[0].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float aSelf) &&
                            float.TryParse(values[1].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float aOther) &&
                            float.TryParse(values[2].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float bSelf) &&
                            float.TryParse(values[3].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float bOther))
                        {
                            TrialData trial = new TrialData(aSelf, aOther, bSelf, bOther);
                            DeceptionTrials.Add(trial);
                            ControlTrials.Add(trial);
                        }
                        else
                        {
                            Debug.LogWarning($"SignallingTrialLoader: Failed to parse line {i + 1} in {filePath}: '{line}'");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"SignallingTrialLoader: Incorrect number of values on line {i + 1} in {filePath}: '{line}'");
                    }
                }
                Debug.Log($"SignallingTrialLoader: Loaded {DeceptionTrials.Count} trials from {filePath}");
            }
            else
            {
                Debug.LogError($"SignallingTrialLoader: TrialData.csv not found at {filePath}!");
            }
        }

        private void CreateAttentionTests()
        {
            AttentionTests.Clear();
            AttentionTests.Add(new AttentionTestData(10f, 5f, 5f, 5f, "B"));
            AttentionTests.Add(new AttentionTestData(5f, 5f, 10f, 5f, "A"));
            AttentionTests.Add(new AttentionTestData(10f, 10f, 5f, 5f, "B"));
            AttentionTests.Add(new AttentionTestData(5f, 5f, 10f, 10f, "A"));
            AttentionTests.Add(new AttentionTestData(5f, 10f, 10f, 5f, "A"));
            Debug.Log($"SignallingTrialLoader: Created {AttentionTests.Count} predefined attention tests.");
        }
    }
}
