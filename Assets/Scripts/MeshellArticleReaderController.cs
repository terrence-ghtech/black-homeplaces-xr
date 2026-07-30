using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[Serializable]
public class MeshellArticleDocument
{
    public string title;
    public string author;
    public string year;
    public List<Texture2D> pages = new List<Texture2D>();
}

/// <summary>
/// Shared page-image article reader for Meshell Sturgis' research notebooks.
/// The PDFs are DefaultImporter assets, so this reads optimized page images
/// explicitly assigned in the scene instead of using an external browser.
/// </summary>
public class MeshellArticleReaderController : MonoBehaviour
{
    private const string LogTag = "[MeshellArticleReader]";
    private const float OpenDistanceFromCamera = 1.75f;

    [Header("Panel")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private Canvas popupCanvas;
    [SerializeField] private Image pageImage;

    [Header("Text")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text authorYearText;
    [SerializeField] private TMP_Text pageNumberText;

    [Header("Buttons")]
    [SerializeField] private Button previousPageButton;
    [SerializeField] private Button nextPageButton;
    [SerializeField] private Button previousArticleButton;
    [SerializeField] private Button nextArticleButton;
    [SerializeField] private Button closeButton;

    [Header("Documents")]
    [SerializeField] private List<MeshellArticleDocument> articles = new List<MeshellArticleDocument>();

    private readonly List<Behaviour> disabledBehaviours = new List<Behaviour>();
    private Sprite currentSprite;
    private int currentArticleIndex;
    private int currentPageIndex;
    private bool isOpen;
    private bool listenersRegistered;
    private CursorLockMode previousLockMode;
    private bool previousCursorVisible;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        RegisterButtonListeners();

        if (!isOpen)
            Hide();
    }

    private void RegisterButtonListeners()
    {
        if (listenersRegistered)
            return;

        listenersRegistered = true;

        if (previousPageButton != null)
            previousPageButton.onClick.AddListener(PreviousPage);
        if (nextPageButton != null)
            nextPageButton.onClick.AddListener(NextPage);
        if (previousArticleButton != null)
            previousArticleButton.onClick.AddListener(PreviousArticle);
        if (nextArticleButton != null)
            nextArticleButton.onClick.AddListener(NextArticle);
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    private void Update()
    {
        // Focused-modal input reads the central FocusedUiInput helper; opening
        // is owned by the InteractionRouter via the notebook target.
        if (!isOpen)
            return;

        if (BCaT.Production.Interaction.FocusedUiInput.CancelPressed)
            Close();
    }

    public void OpenArticle(int articleIndex)
    {
        Debug.Log($"{LogTag} OpenArticle called with articleIndex={articleIndex}. Article count={articles.Count}.");
        if (articles.Count == 0)
            return;

        currentArticleIndex = Mathf.Clamp(articleIndex, 0, articles.Count - 1);
        currentPageIndex = 0;

        if (!isOpen)
        {
            isOpen = true;
            RegisterButtonListeners();
            previousLockMode = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            DisableWorldInput();
            Show();
            PositionPopupInFrontOfCamera();
            LogVisibilityState();

            // Focused exhibit interface: block background world interaction and
            // give the kiosk reset a close handle.
            BCaT.Production.Interaction.InteractionState.Block(this,
                BCaT.Production.Interaction.InteractionBlockReason.Modal, Close);
        }

        Refresh();
    }

    public void Close()
    {
        if (!isOpen)
            return;

        isOpen = false;
        BCaT.Production.Interaction.InteractionState.Unblock(this);
        ClearCurrentSprite();
        Hide();
        RestoreWorldInput();
        Cursor.lockState = previousLockMode;
        Cursor.visible = previousCursorVisible;
    }

    public void PreviousArticle()
    {
        if (articles.Count == 0)
            return;

        currentArticleIndex = (currentArticleIndex - 1 + articles.Count) % articles.Count;
        currentPageIndex = 0;
        Refresh();
    }

    public void NextArticle()
    {
        if (articles.Count == 0)
            return;

        currentArticleIndex = (currentArticleIndex + 1) % articles.Count;
        currentPageIndex = 0;
        Refresh();
    }

    public void PreviousPage()
    {
        if (currentPageIndex <= 0)
            return;

        currentPageIndex--;
        Refresh();
    }

    public void NextPage()
    {
        MeshellArticleDocument article = CurrentArticle();
        if (article == null || currentPageIndex >= article.pages.Count - 1)
            return;

        currentPageIndex++;
        Refresh();
    }

    private void Refresh()
    {
        MeshellArticleDocument article = CurrentArticle();
        if (article == null)
            return;

        currentPageIndex = Mathf.Clamp(currentPageIndex, 0, Mathf.Max(article.pages.Count - 1, 0));

        if (titleText != null)
            titleText.text = article.title;
        if (authorYearText != null)
            authorYearText.text = $"{article.author} | {article.year}";
        if (pageNumberText != null)
            pageNumberText.text = article.pages.Count > 0 ? $"Page {currentPageIndex + 1} of {article.pages.Count}" : "No pages";

        SetVisiblePage(article.pages.Count > 0 ? article.pages[currentPageIndex] : null);

        if (previousPageButton != null)
            previousPageButton.interactable = currentPageIndex > 0;
        if (nextPageButton != null)
            nextPageButton.interactable = article != null && currentPageIndex < article.pages.Count - 1;
    }

    private MeshellArticleDocument CurrentArticle()
    {
        if (articles.Count == 0)
            return null;

        currentArticleIndex = Mathf.Clamp(currentArticleIndex, 0, articles.Count - 1);
        return articles[currentArticleIndex];
    }

    private void SetVisiblePage(Texture2D texture)
    {
        ClearCurrentSprite();

        if (pageImage == null)
            return;

        if (texture == null)
        {
            pageImage.sprite = null;
            pageImage.enabled = false;
            return;
        }

        currentSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        pageImage.sprite = currentSprite;
        pageImage.preserveAspect = false;
        pageImage.enabled = true;
    }

    private void ClearCurrentSprite()
    {
        if (pageImage != null)
            pageImage.sprite = null;

        if (currentSprite != null)
        {
            Destroy(currentSprite);
            currentSprite = null;
        }
    }

    private void Show()
    {
        if (popupRoot != null)
            popupRoot.SetActive(true);
        if (popupCanvas != null)
        {
            popupCanvas.enabled = true;
            popupCanvas.overrideSorting = true;
            popupCanvas.sortingOrder = 100;
        }
    }

    private void PositionPopupInFrontOfCamera()
    {
        Camera activeCamera = FindActiveCamera();
        if (activeCamera == null)
            return;

        Transform popupTransform = popupRoot != null ? popupRoot.transform : transform;
        Vector3 cameraForward = activeCamera.transform.forward;
        popupTransform.position = activeCamera.transform.position + cameraForward * OpenDistanceFromCamera;

        Vector3 directionAwayFromCamera =
            (popupTransform.position - activeCamera.transform.position).normalized;

        popupTransform.rotation =
            Quaternion.LookRotation(directionAwayFromCamera, Vector3.up);

        EnsureCameraRendersUiLayer(activeCamera);
    }

    private Camera FindActiveCamera()
    {
        if (Camera.main != null && Camera.main.isActiveAndEnabled)
            return Camera.main;

        foreach (Camera camera in FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (camera != null && camera.isActiveAndEnabled)
                return camera;
        }

        return null;
    }

    private void EnsureCameraRendersUiLayer(Camera activeCamera)
    {
        int uiLayer = LayerMask.NameToLayer("UI");
        if (activeCamera == null || uiLayer < 0)
            return;

        int uiMask = 1 << uiLayer;
        if ((activeCamera.cullingMask & uiMask) == 0)
            activeCamera.cullingMask |= uiMask;
    }

    private void LogVisibilityState()
    {
        Camera activeCamera = FindActiveCamera();
        Transform popupTransform = popupRoot != null ? popupRoot.transform : transform;
        int uiLayer = LayerMask.NameToLayer("UI");
        bool cameraRendersUi = activeCamera != null && uiLayer >= 0 && (activeCamera.cullingMask & (1 << uiLayer)) != 0;
        Vector3 directionToCamera = activeCamera != null ? (activeCamera.transform.position - popupTransform.position).normalized : Vector3.zero;
        float facingDot = activeCamera != null ? Vector3.Dot(popupTransform.forward, directionToCamera) : 0f;

        Debug.Log($"{LogTag} Active camera='{(activeCamera != null ? activeCamera.name : "<null>")}', position={(activeCamera != null ? activeCamera.transform.position.ToString("F3") : "<null>")}, forward={(activeCamera != null ? activeCamera.transform.forward.ToString("F3") : "<null>")}.");
        Debug.Log($"{LogTag} Popup position={popupTransform.position.ToString("F3")}, forward={popupTransform.forward.ToString("F3")}, dotForwardTowardCamera={facingDot:0.###}, activeInHierarchy={(popupRoot != null && popupRoot.activeInHierarchy)}.");
        Debug.Log($"{LogTag} Canvas enabled={(popupCanvas != null && popupCanvas.enabled)}, overrideSorting={(popupCanvas != null && popupCanvas.overrideSorting)}, sortingOrder={(popupCanvas != null ? popupCanvas.sortingOrder : 0)}, cameraRendersUILayer={cameraRendersUi}, nearClip={(activeCamera != null ? activeCamera.nearClipPlane : 0f):0.###}.");

        if (popupRoot == null)
            return;

        LogChildActiveState(popupRoot.transform, "Background");
        LogChildActiveState(popupRoot.transform, "Header");
        LogChildActiveState(popupRoot.transform, "PageArea");
        LogChildActiveState(popupRoot.transform, "PageArea/PageImage");
        LogChildActiveState(popupRoot.transform, "Footer");

        foreach (CanvasGroup canvasGroup in popupRoot.GetComponentsInChildren<CanvasGroup>(true))
            Debug.Log($"{LogTag} CanvasGroup '{canvasGroup.name}': alpha={canvasGroup.alpha:0.###}, interactable={canvasGroup.interactable}, blocksRaycasts={canvasGroup.blocksRaycasts}, activeInHierarchy={canvasGroup.gameObject.activeInHierarchy}.");
    }

    private void LogChildActiveState(Transform root, string relativePath)
    {
        Transform child = root.Find(relativePath);
        if (child == null)
        {
            Debug.Log($"{LogTag} Child '{relativePath}' missing.");
            return;
        }

        Image image = child.GetComponent<Image>();
        TMP_Text text = child.GetComponent<TMP_Text>();
        Button button = child.GetComponent<Button>();
        Debug.Log($"{LogTag} Child '{relativePath}': activeSelf={child.gameObject.activeSelf}, activeInHierarchy={child.gameObject.activeInHierarchy}, imageEnabled={(image != null && image.enabled)}, tmpEnabled={(text != null && text.enabled)}, buttonEnabled={(button != null && button.enabled)}.");
    }

    private void Hide()
    {
        if (popupRoot != null)
            popupRoot.SetActive(false);
        if (popupCanvas != null)
            popupCanvas.enabled = false;
    }

    private void DisableWorldInput()
    {
        disabledBehaviours.Clear();
        foreach (Behaviour behaviour in FindObjectsByType<Behaviour>(FindObjectsInactive.Exclude))
        {
            if (behaviour == null || !behaviour.enabled || behaviour.transform.IsChildOf(transform))
                continue;

            if (!ShouldDisableWhileOpen(behaviour))
                continue;

            behaviour.enabled = false;
            disabledBehaviours.Add(behaviour);
        }
    }

    private bool ShouldDisableWhileOpen(Behaviour behaviour)
    {
        string typeName = behaviour.GetType().Name;
        string fullName = behaviour.GetType().FullName ?? typeName;

        return typeName == "FirstPersonController"
            || typeName == "MeshellArticleNotebookInputRouter"
            || typeName == "MeshellArticleNotebookOpener"
            || typeName == "InteractableLinkLauncher"
            || typeName == "LindaLeaksPanelOpener"
            || typeName == "MediaVideoController"
            || fullName.Contains("ContinuousMoveProvider")
            || fullName.Contains("ContinuousTurnProvider")
            || fullName.Contains("SnapTurnProvider")
            || fullName.Contains("TeleportationProvider")
            || fullName.Contains("XRSimpleInteractable");
    }

    private void RestoreWorldInput()
    {
        foreach (Behaviour behaviour in disabledBehaviours)
        {
            if (behaviour != null)
                behaviour.enabled = true;
        }

        disabledBehaviours.Clear();
    }
}
