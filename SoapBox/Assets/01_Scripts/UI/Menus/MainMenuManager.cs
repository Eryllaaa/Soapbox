using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles all main menu logic directly: button clicks and canvas switching.
/// Attach to a single GameObject in the main menu scene and wire references in Inspector.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("Canvases")]
    [SerializeField] private GameObject mainMenuCanvas;
    [SerializeField] private GameObject gameSelectionCanvas;
    [SerializeField] private GameObject settingsCanvas;

    [Header("Buttons")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    private void Awake()
    {
        playButton.onClick.AddListener(OnPlayButtonPressed);
        settingsButton.onClick.AddListener(OnSettingsButtonPressed);
        quitButton.onClick.AddListener(OnQuitButtonPressed);
    }

    private void OnDestroy()
    {
        playButton.onClick.RemoveListener(OnPlayButtonPressed);
        settingsButton.onClick.RemoveListener(OnSettingsButtonPressed);
        quitButton.onClick.RemoveListener(OnQuitButtonPressed);
    }

    private void Start()
    {
        ShowOnly(mainMenuCanvas);
    }

    private void OnPlayButtonPressed()
    {
        ShowOnly(gameSelectionCanvas);
    }

    private void OnSettingsButtonPressed()
    {
        ShowOnly(settingsCanvas);
    }

    private void OnQuitButtonPressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Add to MainMenuManager, replacing the private OnBackButtonPressed if you no longer need it there
    public void ShowMainMenu()
    {
        ShowOnly(mainMenuCanvas);
    }

    private void ShowOnly(GameObject targetCanvas)
    {
        mainMenuCanvas.SetActive(targetCanvas == mainMenuCanvas);
        gameSelectionCanvas.SetActive(targetCanvas == gameSelectionCanvas);
        settingsCanvas.SetActive(targetCanvas == settingsCanvas);
    }
}