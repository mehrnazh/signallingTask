using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class InstructionManager : MonoBehaviour
{
    [Header("Instruction Settings")]
    public Image instructionImage;
    public Sprite[] instructionSprites;
    public float displayDuration = 10f;  // 10 seconds per image
    public GameObject mainGamePanel;     // Reference to your main game panel

    private int currentImageIndex = 0;
    public bool instructionsComplete = false;

    void Start()
    {
        // Start with the first instruction image
        if (instructionSprites.Length > 0)
        {
            instructionImage.sprite = instructionSprites[0];
            StartCoroutine(CycleInstructions());
        }
        else
        {
            CompleteInstructions();
        }
        
        // Hide the main game panel initially
        if (mainGamePanel != null)
            mainGamePanel.SetActive(false);
    }

    IEnumerator CycleInstructions()
    {
        // Wait for key press or time expiration for each image
        while (currentImageIndex < instructionSprites.Length)
        {
            float timer = 0;
            bool keyPressed = false;
            
            while (timer < displayDuration && !keyPressed)
            {
                if (Input.anyKeyDown)
                {
                    keyPressed = true;
                }
                
                timer += Time.deltaTime;
                yield return null;
            }
            
            // Move to next image
            currentImageIndex++;
            
            // If we have more images to show
            if (currentImageIndex < instructionSprites.Length)
            {
                instructionImage.sprite = instructionSprites[currentImageIndex];
            }
            else
            {
                CompleteInstructions();
                break;
            }
        }
    }
    
    void CompleteInstructions()
    {
        instructionsComplete = true;
        gameObject.SetActive(false);
        
        // Activate the main game panel
        if (mainGamePanel != null)
            mainGamePanel.SetActive(true);
    }
}
