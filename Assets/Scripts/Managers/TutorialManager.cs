using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// Manages the first-time tutorial. Pauses game time while open so gameplay
/// does not run in the background. All DOTween animations use unscaled time
/// so page transitions still work while Time.timeScale == 0.
/// Completion is persisted in PlayerPrefs; the tutorial will never reopen
/// once the player clicks Play on the last page.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    private const string TUTORIAL_COMPLETED_KEY = "TutorialCompleted";

    [Header("Tutorial Panel")]
    [SerializeField] private GameObject tutorialPanel;

    [Header("Pages")]
    [Tooltip("All tutorial pages in order. They should all be children of the tutorial panel.")]
    [SerializeField] private List<GameObject> pages = new List<GameObject>();

    [Header("Navigation Buttons")]
    [SerializeField] private Button nextButton;
    [SerializeField] private Button backButton;
    [Tooltip("The Play button on the last page. Closes tutorial and starts the game.")]
    [SerializeField] private Button playButton;

    [Header("Page Transition")]
    [SerializeField] private float fadeOutDuration = 0.18f;
    [SerializeField] private float fadeInDuration  = 0.32f;

    [Header("Debug")]
    [Tooltip("When enabled, the tutorial shows every time the game starts regardless of saved progress.")]
    [SerializeField] private bool isTest = false;

    private int currentPageIndex  = 0;
    private bool isTransitioning  = false;

    // -------------------------------------------------------------------------
    #region Lifecycle

    private void Start()
    {
        if (isTest || PlayerPrefs.GetInt(TUTORIAL_COMPLETED_KEY, 0) == 0)
        {
            OpenTutorial();
        }
        else
        {
            if (tutorialPanel != null)
                tutorialPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (nextButton != null) nextButton.onClick.RemoveListener(OnNextClicked);
        if (backButton != null) backButton.onClick.RemoveListener(OnBackClicked);
        if (playButton != null) playButton.onClick.RemoveListener(OnPlayClicked);
    }

    #endregion

    // -------------------------------------------------------------------------
    #region Open / Close

    private void OpenTutorial()
    {
        if (tutorialPanel != null)
            tutorialPanel.SetActive(true);

        // Pause gameplay while the tutorial is open.
        Time.timeScale = 0f;

        if (nextButton != null) nextButton.onClick.AddListener(OnNextClicked);
        if (backButton != null) backButton.onClick.AddListener(OnBackClicked);
        if (playButton != null) playButton.onClick.AddListener(OnPlayClicked);

        // Show only the first page immediately (no animation on open).
        currentPageIndex = 0;
        for (int i = 0; i < pages.Count; i++)
        {
            if (pages[i] == null) continue;
            pages[i].SetActive(i == 0);
            GetOrAddCanvasGroup(pages[i]).alpha = (i == 0) ? 1f : 0f;
        }

        RefreshButtonStates();
    }

    private void CloseTutorial()
    {
        PlayerPrefs.SetInt(TUTORIAL_COMPLETED_KEY, 1);
        PlayerPrefs.Save();

        if (tutorialPanel != null)
            tutorialPanel.SetActive(false);

        Time.timeScale = 1f;
    }

    #endregion

    // -------------------------------------------------------------------------
    #region Navigation

    private void OnNextClicked()
    {
        if (isTransitioning || currentPageIndex >= pages.Count - 1) return;
        NavigateToPage(currentPageIndex + 1);
    }

    private void OnBackClicked()
    {
        if (isTransitioning || currentPageIndex <= 0) return;
        NavigateToPage(currentPageIndex - 1);
    }

    private void OnPlayClicked()
    {
        CloseTutorial();
    }

    private void NavigateToPage(int newIndex)
    {
        if (newIndex < 0 || newIndex >= pages.Count) return;

        isTransitioning = true;
        // Immediately disable buttons during transition to prevent spam.
        SetNavigationButtonsInteractable(false);

        int previousIndex = currentPageIndex;
        currentPageIndex  = newIndex;

        GameObject outPage = pages[previousIndex];
        GameObject inPage  = pages[newIndex];

        // Prepare incoming page (invisible, active so it can be animated).
        if (inPage != null)
        {
            inPage.SetActive(true);
            GetOrAddCanvasGroup(inPage).alpha = 0f;
        }

        Sequence seq = DOTween.Sequence().SetUpdate(true); // unscaled — works at timeScale 0

        // Fade out current page.
        if (outPage != null)
        {
            CanvasGroup outCg = GetOrAddCanvasGroup(outPage);
            seq.Append(outCg.DOFade(0f, fadeOutDuration).SetUpdate(true));
            seq.AppendCallback(() => outPage.SetActive(false));
        }

        // Fade in new page.
        if (inPage != null)
        {
            CanvasGroup inCg = GetOrAddCanvasGroup(inPage);
            seq.Append(inCg.DOFade(1f, fadeInDuration).SetUpdate(true));
        }

        seq.OnComplete(() =>
        {
            isTransitioning = false;
            RefreshButtonStates();
        });
    }

    #endregion

    // -------------------------------------------------------------------------
    #region Button State

    private void RefreshButtonStates()
    {
        if (backButton != null)
            backButton.interactable = currentPageIndex > 0;

        if (nextButton != null)
            nextButton.interactable = currentPageIndex < pages.Count - 1;
    }

    private void SetNavigationButtonsInteractable(bool value)
    {
        if (backButton != null) backButton.interactable = value;
        if (nextButton != null) nextButton.interactable = value;
    }

    #endregion

    // -------------------------------------------------------------------------
    #region Helpers

    private static CanvasGroup GetOrAddCanvasGroup(GameObject go)
    {
        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = go.AddComponent<CanvasGroup>();
        return cg;
    }

    #endregion

    // -------------------------------------------------------------------------
    #region Editor Utilities

#if UNITY_EDITOR
    [ContextMenu("DEBUG — Reset Tutorial Flag")]
    private void DebugResetTutorial()
    {
        PlayerPrefs.DeleteKey(TUTORIAL_COMPLETED_KEY);
        Debug.Log("[TutorialManager] Tutorial flag cleared. It will show again on next Play.");
    }
#endif

    #endregion
}
