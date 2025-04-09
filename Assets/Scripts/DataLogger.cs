using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Linq; // Added for LINQ's string.Join

public static class DataLogger
{
    private static List<string> csvLines = new List<string>();

    // This method is run once at the beginning to add a header.
    public static void Initialize()
    {
        // CSV header: Removed IsAttentionTest, CorrectAttentionResponse. Added BarData.
        csvLines.Clear();
        // Let's put ParticipantID first for easy identification
        csvLines.Add("ParticipantID,EventNumber,TaskTypeOrEvent,MessageChosenOrResponse,ReactionTime,BarData"); // Updated header
    }
    
    // Call this method to log a regular trial.
    // Added barData parameter, removed boolean flags
    public static void LogTrial(int eventNumber, string taskType, string messageChosen, float reactionTime, List<float> barData) 
    {
        // Create a CSV line from the given parameters
        string participantId = GameManager.Instance != null ? GameManager.Instance.GetParticipantId() : "UNKNOWN_ID"; // Get ID
        string barDataString = barData != null ? string.Join(";", barData.Select(b => b.ToString("F2"))) : "N/A"; // Format bar data list
        string line = string.Format("{0},{1},{2},{3},{4:F4},{5}", 
                                    participantId, eventNumber, taskType, messageChosen, reactionTime, 
                                    barDataString // Added bar data
                                   );
        csvLines.Add(line);
    }

    // Call this method to log an attention test.
    // Removed boolean flags
    public static void LogAttentionTest(int eventNumber, string response, float reactionTime) 
    {
        // Create a CSV line for attention test parameters
        string participantId = GameManager.Instance != null ? GameManager.Instance.GetParticipantId() : "UNKNOWN_ID"; // Get ID
        string line = string.Format("{0},{1},{2},{3},{4:F4},{5}",
                                    participantId, eventNumber, "AttentionTest", response, reactionTime,
                                    "N/A" // BarData is N/A for attention tests
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
