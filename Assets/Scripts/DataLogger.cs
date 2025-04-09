using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class DataLogger
{
    private static List<string> csvLines = new List<string>();

    // This method is run once at the beginning to add a header.
    public static void Initialize()
    {
        // CSV header: Added columns for attention test data
        csvLines.Clear();
        // Added: IsAttentionTest, CorrectAttentionResponse
        csvLines.Add("EventNumber,TaskTypeOrEvent,MessageChosenOrResponse,ReactionTime,IsAttentionTest,CorrectAttentionResponse");
    }
    
    // Call this method to log a regular trial.
    public static void LogTrial(int eventNumber, string taskType, string messageChosen, float reactionTime)
    {
        // Create a CSV line from the given parameters
        // Added placeholders for attention test columns
        string line = string.Format("{0},{1},{2},{3:F4},{4},{5}", 
                                    eventNumber, taskType, messageChosen, reactionTime, 
                                    false, // IsAttentionTest = false
                                    "N/A" // CorrectAttentionResponse = N/A for regular trials
                                   );
        csvLines.Add(line);
    }

    // Call this method to log an attention test.
    public static void LogAttentionTest(int eventNumber, string response, float reactionTime, bool correct)
    {
        // Create a CSV line for attention test parameters
        string line = string.Format("{0},{1},{2},{3:F4},{4},{5}",
                                    eventNumber, "AttentionTest", response, reactionTime,
                                    true, // IsAttentionTest = true
                                    correct // CorrectAttentionResponse = true/false
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
