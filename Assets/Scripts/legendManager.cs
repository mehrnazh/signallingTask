using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using System.Threading.Tasks;
using RTLTMPro;

public class LegendManager : MonoBehaviour
{
    [Header("Legend Settings")]
    // Container where legend items will be placed.
    public RectTransform legendContainer;
    // Prefab for a legend item (should contain an Image and a child TMP_Text).
    public GameObject legendItemPrefab;
    // Font asset for RTL text
    public TMP_FontAsset rtlFontAsset;
    
    // Legend visual parameters.
    public int legendFontSize = 30;           // Font size for legend label text.
    public float squareSize = 20f;            // Width and height of the square.
    public float spacing = 15f;               // Spacing between legend items.

    // Localization table reference
    private const string UILocalizationTable = "UI";

    private bool isLegendCreated = false;
    
    /// <summary>
    /// Clears the legend container and creates legend items using localized text.
    /// </summary>
    public async Task CreateLegend()
    {
        if (legendContainer == null)
        {
            Debug.LogWarning("Legend Container is not assigned.");
            return;
        }

        // Prevent duplicate creation
        if (isLegendCreated)
        {
            return;
        }
        
        // Clear existing items
        foreach (Transform child in legendContainer)
        {
            Destroy(child.gameObject);
        }
        
        // Setup layout group
        HorizontalLayoutGroup containerHLG = legendContainer.GetComponent<HorizontalLayoutGroup>();
        if (containerHLG == null)
        {
            containerHLG = legendContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            containerHLG.childForceExpandWidth = false;
            containerHLG.childForceExpandHeight = false;
            containerHLG.spacing = spacing;
            containerHLG.childAlignment = TextAnchor.MiddleCenter;
        }

        // Get localized strings
        var senderTask = GetLocalizedStringAsync(UILocalizationTable, "sender_label");
        var receiverTask = GetLocalizedStringAsync(UILocalizationTable, "receiver_label");
        await Task.WhenAll(senderTask, receiverTask);

        // Create legend items with localized text
        CreateLegendItem(senderTask.Result, Color.red);
        CreateLegendItem(receiverTask.Result, Color.blue);

        isLegendCreated = true;
    }
    
    /// <summary>
    /// Creates a single legend item by instantiating the legend item prefab,
    /// then modifying its Image and RTLTextMeshPro components.
    /// </summary>
    /// <param name="labelText">The label text (e.g., "Receiver" or "Sender").</param>
    /// <param name="squareColor">The color to display in the square.</param>
    private void CreateLegendItem(string labelText, Color squareColor)
    {
        if (legendItemPrefab == null)
        {
            Debug.LogWarning("Legend Item Prefab is not assigned.");
            return;
        }
        
        // Instantiate the prefab as a child of the legend container.
        GameObject legendItem = Instantiate(legendItemPrefab, legendContainer, false);
        legendItem.name = labelText + "LegendItem";
        
        // Get the Image component from the legend item.
        Image img = legendItem.GetComponent<Image>();
        if (img != null)
        {
            img.color = squareColor;
            
            // Adjust its size.
            RectTransform rt = legendItem.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(squareSize, squareSize);
            }
        }
        
        // Get the text component (either TMP_Text or RTLTextMeshPro)
        RTLTextMeshPro rtlText = legendItem.GetComponentInChildren<RTLTextMeshPro>();
        if (rtlText == null)
        {
            // If no RTLTextMeshPro exists, try to get TMP_Text and convert it
            TMP_Text existingText = legendItem.GetComponentInChildren<TMP_Text>();
            if (existingText != null)
            {
                GameObject textGO = existingText.gameObject;
                Destroy(existingText);
                rtlText = textGO.AddComponent<RTLTextMeshPro>();
            }
        }

        if (rtlText != null)
        {
            rtlText.text = labelText;
            rtlText.fontSize = legendFontSize;
            
            // Set the RTL font asset if provided
            if (rtlFontAsset != null)
            {
                rtlText.font = rtlFontAsset;
            }
        }
        else
        {
            Debug.LogWarning("No text component found in legend item prefab!");
        }
    }

    private bool ShouldUseRTL()
    {
        if (LocalizationSettings.SelectedLocale == null)
            return false;

        string languageCode = LocalizationSettings.SelectedLocale.Identifier.Code;
        
        // List of RTL language codes
        string[] rtlLanguages = { "fa", "ar", "he", "ur", "ps", "sd" };
        
        return System.Array.Exists(rtlLanguages, lang => lang == languageCode);
    }

    private async Task<string> GetLocalizedStringAsync(string tableName, string entryName)
    {
        var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(tableName, entryName);
        await operation.Task;
        if (operation.IsDone && operation.Result != null)
        {
            return operation.Result;
        }
        Debug.LogWarning($"Could not find localized string for key '{entryName}' in table '{tableName}'. Returning key.");
        return entryName; // Fallback
    }

    /// <summary>
    /// Refreshes the legend by recreating it. Call this after language changes.
    /// </summary>
    public async Task RefreshLegend()
    {
        isLegendCreated = false; // Reset the flag to allow recreation
        await CreateLegend();
    }
}
