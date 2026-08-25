using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class PhotoEntry
{
    public Sprite sprite;
    public string title;
    [TextArea] public string caption;
}

public class LindaLeaksPhotoAlbumController : MonoBehaviour
{
    [Header("Album")]
    [SerializeField] private GameObject albumRoot;
    [SerializeField] private Canvas albumCanvas;
    [SerializeField] private Image photoImage;

    [Header("Text")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text captionText;
    [SerializeField] private TMP_Text projectDescriptionText;
    [TextArea]
    [SerializeField] private string projectDescription;

    [Header("Photos")]
    [SerializeField] private List<PhotoEntry> photos = new List<PhotoEntry>();
    [SerializeField] private int startIndex;

    [Header("External Link")]
    [SerializeField] private string externalWebsiteUrl;

    private int currentIndex;

    private void Start()
    {
        currentIndex = Mathf.Clamp(startIndex, 0, Mathf.Max(photos.Count - 1, 0));
        HideAlbum();
    }

    public void OpenAlbum()
    {
        ShowAlbum();
        Refresh();
    }

    public void CloseAlbum()
    {
        HideAlbum();
    }

    public void Next()
    {
        if (photos.Count == 0)
            return;

        currentIndex = (currentIndex + 1) % photos.Count;
        Refresh();
    }

    public void Previous()
    {
        if (photos.Count == 0)
            return;

        currentIndex = (currentIndex - 1 + photos.Count) % photos.Count;
        Refresh();
    }

    public void OpenExternalWebsite()
    {
        if (string.IsNullOrWhiteSpace(externalWebsiteUrl))
            return;

        Debug.Log($"[LindaLeaksPhotoAlbum:{gameObject.name}] Opening external link: {externalWebsiteUrl}");
        Application.OpenURL(externalWebsiteUrl);
    }

    private void Refresh()
    {
        if (projectDescriptionText != null)
            projectDescriptionText.text = projectDescription;

        if (photos.Count == 0)
        {
            if (photoImage != null)
                photoImage.sprite = null;

            if (titleText != null)
                titleText.text = string.Empty;

            if (captionText != null)
                captionText.text = string.Empty;

            return;
        }

        currentIndex = Mathf.Clamp(currentIndex, 0, photos.Count - 1);
        PhotoEntry entry = photos[currentIndex];

        if (photoImage != null)
        {
            photoImage.sprite = entry.sprite;
            photoImage.enabled = entry.sprite != null;
        }

        if (titleText != null)
            titleText.text = entry.title;

        if (captionText != null)
            captionText.text = entry.caption;
    }

    private void ShowAlbum()
    {
        if (albumRoot != null)
            albumRoot.SetActive(true);

        if (albumCanvas != null)
            albumCanvas.enabled = true;
    }

    private void HideAlbum()
    {
        if (albumRoot != null)
            albumRoot.SetActive(false);

        if (albumCanvas != null)
            albumCanvas.enabled = false;

        ClearDisplayedPhoto();
    }

    private void ClearDisplayedPhoto()
    {
        if (photoImage != null)
        {
            photoImage.sprite = null;
            photoImage.enabled = false;
        }
    }
}
