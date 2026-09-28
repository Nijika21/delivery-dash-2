using UnityEngine;
using UnityEngine.InputSystem;

public partial class KidFriendlyHud : MonoBehaviour
{
    private enum Page { Game, Pause, Lobby, Shop, ResetWarning, Daily, FinishWork, ExitWarning, Mode, Settings, About, TutorialPrompt }

    private Page page;
    private Page shopReturn = Page.Lobby;
    private Page exitReturn = Page.Lobby;
    private Driver driver;
    private DeliveryGameManager gameManager;
    private int pendingSkin = -1;
    private bool insufficientCoins;
    private bool dailyPromptShown;

    public bool IsMenuOpen => page != Page.Game;

    // Suasana musik latar 2.0 (DeliveryMusic): hening saat layar muat,
    // lagu lobby di layar-layar lobby, lagu main selama di jalan (termasuk jeda dan tutorial).
    public DeliveryMusic.Mode MusicMode =>
        toolkitScreen == 1 ? DeliveryMusic.Mode.Silent
        : IsLobbyPage(page) ? DeliveryMusic.Mode.Lobby : DeliveryMusic.Mode.Play;

    public void Configure(Driver car, DeliveryGameManager manager)
    {
        driver = car;
        gameManager = manager;
        SetupToolkit();
    }

    public void RequestExit()
    {
        if (page != Page.Lobby && page != Page.Settings) return;
        exitReturn = page;
        SetPage(Page.ExitWarning);
    }

    public void CancelExit()
    {
        SetPage(exitReturn);
    }

    private void ConfirmExit()
    {
        gameManager.Progress.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OpenLobby()
    {
        SetPage(Page.Lobby);
        OpenDailyIfNeeded();
    }

    private void OpenDailyIfNeeded()
    {
        if (dailyPromptShown || gameManager?.Progress?.CanClaimDaily(System.DateTime.Now) != true) return;
        dailyPromptShown = true;
        SetPage(Page.Daily);
    }

    public void ResumeGame()
    {
        SetPage(Page.Game);
    }

    public void OpenFinishWork()
    {
        SetPage(Page.FinishWork);
    }

    public void OpenTutorialPrompt()
    {
        SetPage(Page.TutorialPrompt);
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) PauseForInterruption();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) PauseForInterruption();
    }

    private void PauseForInterruption()
    {
        if (gameManager != null && driver != null && page == Page.Game)
            SetPage(Page.Pause);
    }

    private void SetPage(Page value)
    {
        gameManager.CancelDestinationPreview();
        pendingSkin = -1;
        page = value;
        gameManager.MenuOpen = IsMenuOpen;
        gameManager.SetLobbyView(IsLobbyPage(value));
        Time.timeScale = IsMenuOpen ? 0 : 1;
        driver.StopImmediately();
        ResetJoystick();
        toolkitLeft = toolkitRight = toolkitGas = toolkitBrake = false;
        if (gameManager.Minimap != null) gameManager.Minimap.SetExpanded(false);
        RefreshToolkit(true);
    }

    // Layar yang tampil di atas lobby (latar dunia berbingkai lobby), bukan di tengah permainan.
    private bool IsLobbyPage(Page value) => value == Page.Lobby || value == Page.Daily || value == Page.Mode ||
        value == Page.Settings || value == Page.About || value == Page.ExitWarning || value == Page.ResetWarning ||
        value == Page.Shop && shopReturn != Page.Pause;

    private void Update()
    {
        if (driver == null) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            // Sorotan tujuan: Esc/Back diabaikan (HANDOFF 13 baris 4); kehilangan fokus tetap membuka Jeda.
            if (page == Page.Game && gameManager.IsShowingDestination) { }
            else if (page == Page.ExitWarning) CancelExit();
            else if (page == Page.Lobby) RequestExit();
            else if (page == Page.Game && gameManager.Minimap.Expanded)
            {
                gameManager.Minimap.SetExpanded(false);
                Time.timeScale = 1;
            }
            else if (page == Page.Shop && pendingSkin >= 0)
            {
                pendingSkin = -1;
                RefreshToolkit(true);
            }
            else if (page == Page.FinishWork) gameManager.AnswerFinishWork(false);
            else if (page == Page.TutorialPrompt) gameManager.AnswerTutorialPrompt(false);
            else if (page == Page.Game) SetPage(Page.Pause);
            else if (page == Page.Pause) ResumeGame();
            else if (page == Page.Shop) SetPage(shopReturn);
            else if (page == Page.ResetWarning) SetPage(Page.Settings);
            else if (page == Page.Daily || page == Page.Mode || page == Page.Settings || page == Page.About) SetPage(Page.Lobby);
        }

        if (toolkitActive) UpdateToolkit();
        else driver.SetTouchInput(0, 0);
    }

    public static string SelectDrivingHint(string message, string sideQuest, string bonus, bool fragile)
    {
        if (!string.IsNullOrEmpty(message)) return message;
        if (!string.IsNullOrEmpty(sideQuest)) return sideQuest;
        return fragile ? string.Empty : bonus;
    }

    private void OnDestroy()
    {
        Time.timeScale = 1;
    }
}
