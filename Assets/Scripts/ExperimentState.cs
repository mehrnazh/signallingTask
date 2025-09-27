using System.Collections.Generic;
using SignallingTaskData; // Make sure to include this namespace

[System.Serializable]
public class ExperimentState
{
    public string participantId;
    public string taskType;
    public int series;
    public int lastCompletedEventIndex; // The index of the last event that finished

    // We need to save the exact shuffled order of trials
    public List<TrialData> shuffledTrialOrder;

    // We also need to save the exact placement of attention tests
    public List<int> attentionTestEventIndices;

    // Add this line to save the response history
    public List<string> trialResponses;
}