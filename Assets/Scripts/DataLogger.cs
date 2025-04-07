using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class DataLogger
{
    private static List<string> csvLines = new List<string>();

    // This method is run once at the beginning to add a header.
    public static void Initialize()
    {
        // CSV header: you can include additional fields if needed (e.g., participant ID, timestamp)
        csvLines.Clear();
        csvLines.Add("Trial,TaskType,MessageChosen,ReactionTime");
    }
    
    // Call this method to log a trial.
    public static void LogTrial(int trialNumber, string taskType, string messageChosen, float reactionTime)
    {
        // Create a CSV line from the given parameters
        string line = string.Format("{0},{1},{2},{3}", trialNumber, taskType, messageChosen, reactionTime);
        csvLines.Add(line);
    }
    
    // Call this method after the experiment to save the CSV to a file.
    public static void SaveData(string filename = "TrialData.csv")
    {
        // Use Unity's persistent data path so that the file is saved in a predictable location.
        string filePath = Path.Combine(Application.persistentDataPath, filename);
        File.WriteAllLines(filePath, csvLines.ToArray());
        Debug.Log("Data saved to: " + filePath);
    }
}
