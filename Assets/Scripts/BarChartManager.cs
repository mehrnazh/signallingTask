using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using System.Threading.Tasks;
using RTLTMPro;

public class BarChartManager : MonoBehaviour
{
    [Header("UI References")]
    // Overall chart container (optional, for layout purposes)
    public RectTransform chartContainer; 
    // Prefab for a single bar (must include an Image component and a child TMP_Text)
    public GameObject barPrefab; 

    [Header("Group Containers")]
    // Containers for the bars (Option A and Option B respectively)
    public Transform groupAContainer;  
    public Transform groupBContainer;  

    [Header("Group Labels (Outside)")]
    // Container for group labels; this should be positioned underneath the group containers.
    public RectTransform groupLabelsContainer;
    // Font asset for RTL text
    public TMP_FontAsset rtlFontAsset;

    [Header("Chart Settings")]
    // Maximum height in pixels corresponding to the highest monetary value.
    public float chartMaxHeight = 300f; 

    // Localization table reference
    private const string UILocalizationTable = "UI";

    /// <summary>
    /// Generates the bar chart using trial monetary allocations with localized text.
    /// </summary>
    public async void CreateBarChart(float optionASelf, float optionAOther, float optionBSelf, float optionBOther)
    {
        // Get localized strings for group labels
        var taskA = GetLocalizedStringAsync(UILocalizationTable, "option_a_label");
        var taskB = GetLocalizedStringAsync(UILocalizationTable, "option_b_label");
        await Task.WhenAll(taskA, taskB);

        // Create or update group labels with localized text
        RTLTextMeshPro groupALabel = GetOrCreateGroupLabelOutside("GroupALabel", taskA.Result);
        RTLTextMeshPro groupBLabel = GetOrCreateGroupLabelOutside("GroupBLabel", taskB.Result);

        // Clear previous bars in the group containers.
        ClearChildren(groupAContainer);
        ClearChildren(groupBContainer);

        // Compute the scaling factor based on the maximum monetary value.
        float maxValue = Mathf.Max(optionASelf, optionAOther, optionBSelf, optionBOther);
        float scaleFactor = chartMaxHeight / maxValue;

        // Create bars for Option A: red for "Self" and blue for "Other".
        CreateBar(groupAContainer, optionASelf, scaleFactor, Color.red);
        CreateBar(groupAContainer, optionAOther, scaleFactor, Color.blue);

        // Create bars for Option B.
        CreateBar(groupBContainer, optionBSelf, scaleFactor, Color.red);
        CreateBar(groupBContainer, optionBOther, scaleFactor, Color.blue);
    }

    /// <summary>
    /// Searches for an existing label with the given name in GroupLabelsContainer.
    /// If not found, creates a new RTLTextMeshPro element and adds it to the container.
    /// </summary>
    private RTLTextMeshPro GetOrCreateGroupLabelOutside(string labelName, string defaultText)
    {
        Transform existingLabel = groupLabelsContainer.Find(labelName);
        RTLTextMeshPro textComp;
        if (existingLabel != null)
        {
            textComp = existingLabel.GetComponent<RTLTextMeshPro>();
            if (textComp != null)
            {
                textComp.text = defaultText;
                return textComp;
            }
            else
            {
                // If the GameObject exists but the component is missing, destroy and recreate
                Destroy(existingLabel.gameObject);
            }
        }
        
        GameObject labelGO = new GameObject(labelName);
        labelGO.transform.SetParent(groupLabelsContainer, false);
        
        textComp = labelGO.AddComponent<RTLTextMeshPro>();
        textComp.text = defaultText;
        textComp.alignment = TextAlignmentOptions.Center;
        textComp.fontSize = 24;
        
        // Set the RTL font asset if provided
        if (rtlFontAsset != null)
        {
            textComp.font = rtlFontAsset;
        }
        
        // Add LayoutElement to ensure proper sizing within a potential layout group
        LayoutElement layoutElement = labelGO.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = 200; // Adjust width as needed
        
        return textComp;
    }

    // Helper function to determine if RTL should be used
    private bool ShouldUseRTL()
    {
        // Check if the selected locale's code is Farsi ('fa')
        return LocalizationSettings.SelectedLocale != null && 
               LocalizationSettings.SelectedLocale.Identifier.Code == "fa";
    }

    /// <summary>
    /// Destroys all children of the given parent.
    /// </summary>
    private void ClearChildren(Transform parent)
    {
        foreach (Transform child in parent)
        {
            Destroy(child.gameObject);
        }
    }

    /// <summary>
    /// Instantiates a bar (from the prefab) into the specified parent container.
    /// Sets its height (using the scale factor and monetary value), color, and positions its internal label.
    /// </summary>
    private void CreateBar(Transform parentContainer, float value, float scaleFactor, Color barColor)
    {
        GameObject newBar = Instantiate(barPrefab, parentContainer, false);
        newBar.transform.localPosition = Vector3.zero;
        RectTransform barRect = newBar.GetComponent<RectTransform>();
        float barHeight = value * scaleFactor;
        barRect.sizeDelta = new Vector2(barRect.sizeDelta.x, barHeight);
        Image barImage = newBar.GetComponent<Image>();
        if(barImage != null)
            barImage.color = barColor;
        TMP_Text barLabel = newBar.GetComponentInChildren<TMP_Text>();
        if(barLabel != null)
        {
            // Format the value with the appropriate currency symbol and format
            barLabel.text = FormatValue(value);
            RectTransform labelRect = barLabel.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.5f, 0f);
            labelRect.anchorMax = new Vector2(0.5f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            float margin = 5f;
            labelRect.anchoredPosition = new Vector2(0f, barHeight + margin);
        }
    }

    private string FormatValue(float value)
    {
        // Get the localized currency format
        var formatTask = GetLocalizedStringAsync(UILocalizationTable, "currency_format");
        formatTask.Wait(); // Wait for the task to complete
        string format = formatTask.Result;
        
        // Format the value according to the localized format
        return string.Format(format, value);
    }

    private async Task<string> GetLocalizedStringAsync(string tableName, string entryName)
    {
        var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(tableName, entryName);
        await operation.Task;
        if (operation.IsDone && operation.Result != null)
        {
            return operation.Result;
        }
        return entryName; // Fallback to the entry name if localization fails
    }

    // /// <summary>
    // /// Dynamically creates a legend guide with two items:
    // /// - A blue circle labeled "Receiver"
    // /// - A red circle labeled "Sender"
    // /// </summary>


    // private void CreateLegend(Transform legendContainer, string legendName, Color legendColor)
    // {
    //     GameObject newRec = Instantiate(legendPrefab, legendContainer, false);
    //     sendRec.transform.localPosition = Vector3.zero;
    //     RectTransform legendRec = newRec.GetComponent<RectTransform>();
    //     float 
    // }
    // private void CreateLegend()
    // {
    //     if (legendContainer == null)
    //         return;
            
    //     // Clear any existing legend items.
    //     foreach (Transform child in legendContainer)
    //         Destroy(child.gameObject);
        
    //     // Ensure the legendContainer has a Horizontal Layout Group.
    //     HorizontalLayoutGroup containerHLG = legendContainer.GetComponent<HorizontalLayoutGroup>();
    //     if (containerHLG == null)
    //     {
    //         containerHLG = legendContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
    //         containerHLG.childForceExpandWidth = false;
    //         containerHLG.childForceExpandHeight = false;
    //         containerHLG.spacing = 20;
    //         containerHLG.childAlignment = TextAnchor.MiddleCenter;
    //     }
        
    //     // ***********************
    //     // Create legend item for Receiver.
    //     // ***********************
    //     GameObject recLegendItem = new GameObject("ReceiverLegend");
    //     recLegendItem.transform.SetParent(legendContainer, false);
    //     HorizontalLayoutGroup recHLG = recLegendItem.AddComponent<HorizontalLayoutGroup>();
    //     recHLG.childForceExpandWidth = false;
    //     recHLG.childForceExpandHeight = false;
    //     recHLG.childAlignment = TextAnchor.MiddleLeft;
    //     recHLG.spacing = 5;


    //     GameObject sendRec = Instantiate(legendPrefab, legendContainer, false);
    //     sendRec
        
    //     // Instantiate your bar prefab as the colored square for the Receiver.
    //     GameObject recSquare = Instantiate(barPrefab, recLegendItem.transform, false);
    //     recSquare.name = "ReceiverSquare";
    //     // Reset its local position to ensure proper placement.
    //     recSquare.transform.localPosition = Vector3.zero;
    //     // Retrieve and adjust the RectTransform for a small size.
    //     RectTransform recSquareRT = recSquare.GetComponent<RectTransform>();
    //     recSquareRT.sizeDelta = new Vector2(20, 20);
    //     // Retrieve the Image component from the prefab and set its color to blue.
    //     Image recImage = recSquare.GetComponent<Image>();
    //     if (recImage != null)
    //     {
    //         // If there is no sprite, assign Unity's built-in sprite.
    //         if(recImage.sprite == null)
    //             recImage.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            
    //         recImage.color = Color.blue;
    //     }
    //     // Disable any internal label from the prefab since it's not needed.
    //     TMP_Text recPrefabLabel = recSquare.GetComponentInChildren<TMP_Text>();
    //     if (recPrefabLabel != null)
    //     {
    //         recPrefabLabel.gameObject.SetActive(false);
    //     }
        
    //     // Create a text object for the Receiver legend.
    //     GameObject recTextGO = new GameObject("ReceiverText");
    //     recTextGO.transform.SetParent(recLegendItem.transform, false);
    //     TMP_Text recText = recTextGO.AddComponent<TextMeshProUGUI>();
    //     recText.text = "Receiver";
    //     recText.fontSize = 20;
        
    //     // ***********************
    //     // Create legend item for Sender.
    //     // ***********************
    //     GameObject sendLegendItem = new GameObject("SenderLegend");
    //     sendLegendItem.transform.SetParent(legendContainer, false);
    //     HorizontalLayoutGroup sendHLG = sendLegendItem.AddComponent<HorizontalLayoutGroup>();
    //     sendHLG.childForceExpandWidth = false;
    //     sendHLG.childForceExpandHeight = false;
    //     sendHLG.childAlignment = TextAnchor.MiddleLeft;
    //     sendHLG.spacing = 5;
        
    //     // Instantiate your bar prefab as the colored square for the Sender.
    //     GameObject sendSquare = Instantiate(barPrefab, sendLegendItem.transform, false);
    //     sendSquare.name = "SenderSquare";
    //     sendSquare.transform.localPosition = Vector3.zero;
    //     RectTransform sendSquareRT = sendSquare.GetComponent<RectTransform>();
    //     sendSquareRT.sizeDelta = new Vector2(20, 20);
    //     Image sendImage = sendSquare.GetComponent<Image>();
    //     if (sendImage != null)
    //     {
    //         sendImage.color = Color.red; // Change this color as desired.
    //     }
    //     TMP_Text sendPrefabLabel = sendSquare.GetComponentInChildren<TMP_Text>();
    //     if (sendPrefabLabel != null)
    //     {
    //         sendPrefabLabel.gameObject.SetActive(false);
    //     }
        
    //     // Create the text object for the Sender legend.
    //     GameObject sendTextGO = new GameObject("SenderText");
    //     sendTextGO.transform.SetParent(sendLegendItem.transform, false);
    //     TMP_Text sendText = sendTextGO.AddComponent<TextMeshProUGUI>();
    //     sendText.text = "Sender";
    //     sendText.fontSize = 20;
    // }
}
