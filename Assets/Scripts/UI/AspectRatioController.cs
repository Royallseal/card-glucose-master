using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasScaler))]
public class AspectRatioController : MonoBehaviour
{
    public static AspectRatioController Instance { get; private set; }

    [SerializeField] private float targetWidth = 1920f;
    [SerializeField] private float targetHeight = 1080f;

    /// <summary>UI 实际渲染区域（屏幕坐标），Tooltip 等边界裁剪用</summary>
    public Rect SafeArea { get; private set; }

    public Vector2 ReferenceResolution => new Vector2(targetWidth, targetHeight);

    private Canvas canvas;
    private CanvasScaler canvasScaler;
    private Camera uiCamera;
    private float targetRatio;

    void Awake()
    {
        Instance = this;
        canvas = GetComponent<Canvas>();
        canvasScaler = GetComponent<CanvasScaler>();
        targetRatio = targetWidth / targetHeight;

        canvasScaler.matchWidthOrHeight = 0.5f;

        SetupUICamera();
        SwitchCanvasToCamera();
        CreateLetterboxBackground();
    }

    void SetupUICamera()
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

    void CreateLetterboxBackground()
    {
        var go = new GameObject("LetterboxBackground", typeof(Image));
        go.transform.SetParent(transform, false);
        go.transform.SetAsFirstSibling();

        var img = go.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;

        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
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

        var pr = uiCamera.pixelRect;
        SafeArea = new Rect(pr.x, pr.y, pr.width, pr.height);
    }
}
