using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Base class for secondary menu screens (anything that isn't the main menu).
/// Handles the shared "back to main menu" button wiring.
/// </summary>
public abstract class MenuScreenBase : MonoBehaviour
{
    [Header("Base Menu References")]
    [SerializeField] private Button backButton;
    [SerializeField] private MainMenuManager mainMenuManager;

    protected virtual void Awake()
    {
        if (backButton != null)
        {
            backButton.onClick.AddListener(OnBackButtonPressed);
        }
    }

    protected virtual void OnDestroy()
    {
        if (backButton != null)
        {
            backButton.onClick.RemoveListener(OnBackButtonPressed);
        }
    }

    protected virtual void OnBackButtonPressed()
    {
        mainMenuManager.ShowMainMenu();
    }
}