using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasScaler))]
public class AspectRatioController : MonoBehaviour
{
    [SerializeField] private float targetWidth = 1920f;
    [SerializeField] private float targetHeight = 1080f;

    private Canvas canvas;
    private CanvasScaler canvasScaler;
    private Camera uiCamera;
    private float targetRatio;

    void Awake()
    {
        canvas = GetComponent<Canvas>();
        canvasScaler = GetComponent<CanvasScaler>();
        targetRatio = targetWidth / targetHeight;

        canvasScaler.matchWidthOrHeight = 0.5f;
        CreateLetterboxBackground();
        SetupMainCamera();
        SwitchCanvasToCamera();
    }

    void CreateLetterboxBackground()
    {
        var go = new GameObject("LetterboxBackground", typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.SetAsFirstSibling();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;
    }

    void SetupMainCamera()
    {
        uiCamera = Camera.main;
        if (uiCamera == null)
        {
            var go = new GameObject("Main Camera");
            uiCamera = go.AddComponent<Camera>();
            uiCamera.tag = "MainCamera";
            go.AddComponent<AudioListener>();
        }
        uiCamera.backgroundColor = Color.black;
        uiCamera.allowHDR = false;
        uiCamera.allowMSAA = false;
    }

    void SwitchCanvasToCamera()
    {
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = uiCamera;
        canvas.planeDistance = 100;
    }

    void Update()
    {
        float screenRatio = (float)Screen.width / (float)Screen.height;

        if (screenRatio > targetRatio)
        {
            float w = targetRatio / screenRatio;
            uiCamera.rect = new Rect((1f - w) * 0.5f, 0, w, 1f);
        }
        else
        {
            float h = screenRatio / targetRatio;
            uiCamera.rect = new Rect(0, (1f - h) * 0.5f, 1f, h);
        }
    }
}
