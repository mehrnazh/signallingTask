using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Linq; // Added for LINQ's string.Join

public static class DataLogger
{
    private static List<string> csvLines = new List<string>();
    private static float startTime = 0f; // Time experiment started

    // This method is run once at the beginning to add a header.
    public static void Initialize()
    {
        startTime = Time.realtimeSinceStartup; // Record start time
        // CSV header: Added AbsoluteTime
        csvLines.Clear();
        // Let's put ParticipantID first for easy identification
        csvLines.Add("ParticipantID,EventNumber,AbsoluteTime,TaskTypeOrEvent,MessageChosenOrResponse,ReactionTime,BarData"); // Updated header
    }
    
    // Call this method to log a regular trial.
    // Added barData parameter, removed boolean flags
    public static void LogTrial(int eventNumber, string taskType, string messageChosen, float reactionTime, List<float> barData) 
    {
        float absoluteTime = Time.realtimeSinceStartup - startTime;
        // Create a CSV line from the given parameters
        string participantId = GameManager.Instance != null ? GameManager.Instance.GetParticipantId() : "UNKNOWN_ID"; // Get ID
        string barDataString = barData != null ? string.Join(",", barData.Select(b => b.ToString("F2"))) : "N/A"; // Format bar data list
        string line = string.Format("{0},{1},{2:F4},{3},{4},{5:F4},{6}", 
                                    participantId, eventNumber, absoluteTime, taskType, messageChosen, reactionTime, 
                                    barDataString // Added bar data
                                   );
        csvLines.Add(line);
    }

    // Call this method to log an attention test.
    // Removed boolean flags
    public static void LogAttentionTest(int eventNumber, string response, float reactionTime) 
    {
        float absoluteTime = Time.realtimeSinceStartup - startTime;
        // Create a CSV line for attention test parameters
        string participantId = GameManager.Instance != null ? GameManager.Instance.GetParticipantId() : "UNKNOWN_ID"; // Get ID
        string line = string.Format("{0},{1},{2:F4},{3},{4},{5:F4},{6}",
                                    participantId, eventNumber, absoluteTime, "AttentionTest", response, reactionTime,
                                    "N/A" // BarData is N/A for attention tests
                                   );
        csvLines.Add(line);
    }
    
    // Call this method to log the start of an inter-run break.
    public static void LogInterRunStart(int eventNumberBeforeBreak)
    {
        float absoluteTime = Time.realtimeSinceStartup - startTime;
        string participantId = GameManager.Instance != null ? GameManager.Instance.GetParticipantId() : "UNKNOWN_ID";
        string line = string.Format("{0},{1},{2:F4},{3},{4},{5},{6}",
                                    participantId, eventNumberBeforeBreak, absoluteTime, "InterRunStart", 
                                    "N/A", // MessageChosenOrResponse
                                    "N/A", // ReactionTime
                                    "N/A"  // BarData
                                   );
        csvLines.Add(line);
    }

    // Call this method after the experiment to save the CSV to a file.
    public static void SaveData(string filename = "TrialData.csv")
    {
        // Use Unity's persistent data path so that the file is saved in a predictable location.
        string filePath = Path.Combine(Application.persistentDataPath, filename);
        try 
        {
            File.WriteAllLines(filePath, csvLines.ToArray());
            Debug.Log("Data saved successfully to: " + filePath);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error saving data to {filePath}: {ex.Message}");
        }
        
    }
}
