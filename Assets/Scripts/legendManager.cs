using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LegendManager : MonoBehaviour
{
    [Header("Legend Settings")]
    // Container where legend items will be placed.
    public RectTransform legendContainer;
    // Prefab for a legend item (should contain an Image and a child TMP_Text).
    public GameObject legendItemPrefab;
    
    // Legend visual parameters.
    public int legendFontSize = 20;           // Font size for legend label text.
    public float squareSize = 20f;            // Width and height of the square.
    public float spacing = 10f;               // Spacing between legend items.
    
    private void Start()
    {
        CreateLegend();
    }
    
    /// <summary>
    /// Clears the legend container and creates legend items for Receiver and Sender.
    /// </summary>
    public void CreateLegend()
    {
        if (legendContainer == null)
        {
            Debug.LogWarning("Legend Container is not assigned.");
            return;
        }
        
        // Clear any existing items.
        foreach (Transform child in legendContainer)
        {
            Destroy(child.gameObject);
        }
        
        // Ensure the legend container itself has a Horizontal Layout Group for tidy arrangement.
        HorizontalLayoutGroup containerHLG = legendContainer.GetComponent<HorizontalLayoutGroup>();
        if (containerHLG == null)
        {
            containerHLG = legendContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            containerHLG.childForceExpandWidth = false;
            containerHLG.childForceExpandHeight = false;
            containerHLG.spacing = spacing;
            containerHLG.childAlignment = TextAnchor.MiddleCenter;
        }
        // Create a legend item for Sender (using red).
        CreateLegendItem("Sender", Color.red);
        // Create a legend item for Receiver (using blue).
        CreateLegendItem("Receiver", Color.blue);
    }
    
    /// <summary>
    /// Creates a single legend item by instantiating the legend item prefab,
    /// then modifying its Image and TMP_Text components.
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
            // If no sprite is assigned, assign the built-in UI sprite so the square is visible.
            // if (img.sprite == null)
            //     img.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            img.color = squareColor;
            
            // Adjust its size.
            RectTransform rt = legendItem.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(squareSize, squareSize);
            }
        }
        
        // Get the TMP_Text component (which should be a child of the prefab).
        TMP_Text tmpText = legendItem.GetComponentInChildren<TMP_Text>();
        if (tmpText != null)
        {
            tmpText.text = labelText;
            tmpText.fontSize = legendFontSize;
        }
    }
}
