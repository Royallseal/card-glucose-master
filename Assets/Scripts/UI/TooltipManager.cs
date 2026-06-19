using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CGM.UI
{
    /// <summary>
    /// Global auto-adapting Tooltip manager.
    /// Attached to a GameObject under the UI Canvas, referencing the CustomTooltip prefab.
    /// </summary>
    public class TooltipManager : MonoBehaviour
    {
        public static TooltipManager Instance { get; private set; }

        [Header("Tooltip Prefab Settings")]
        [Tooltip("CustomTooltip Prefab")]
        [SerializeField] private GameObject tooltipPrefab;
        
        [Tooltip("TextMeshProUGUI component inside the prefab")]
        [SerializeField] private TextMeshProUGUI textComponent;

        [Tooltip("Safety padding between tooltip and hovered UI")]
        [SerializeField] private float padding = 15f;

        private GameObject _tooltipInstance;
        private RectTransform _tooltipRect;
        private CanvasGroup _tooltipCanvasGroup;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializeTooltip();
        }

        private void InitializeTooltip()
        {
            if (tooltipPrefab == null)
            {
                Debug.LogError("[TooltipManager] Error: Tooltip Prefab is not assigned in the Inspector!");
                return;
            }

            // Instantiate the tooltip prefab as a child of TooltipManager
            _tooltipInstance = Instantiate(tooltipPrefab, transform);
            _tooltipRect = _tooltipInstance.GetComponent<RectTransform>();
            _tooltipCanvasGroup = _tooltipInstance.GetComponent<CanvasGroup>();

            // Automatically find TMPro text component if not assigned
            if (textComponent == null)
            {
                textComponent = _tooltipInstance.GetComponentInChildren<TextMeshProUGUI>();
                if (textComponent == null)
                {
                    Debug.LogError("[TooltipManager] Error: Cannot find TextMeshProUGUI in Tooltip Prefab children!");
                }
                else
                {
                    Debug.Log($"[TooltipManager] Auto-retrieved and bound text component: {textComponent.name}");
                }
            }

            // Force anchor and pivot to (0.5, 0.5) to simplify math calculations
            if (_tooltipRect != null)
            {
                _tooltipRect.anchorMin = new Vector2(0.5f, 0.5f);
                _tooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
                _tooltipRect.pivot = new Vector2(0.5f, 0.5f);
            }

            _tooltipInstance.SetActive(false);
            Debug.Log("[TooltipManager] Initialization completed successfully.");
        }

        /// <summary>
        /// Show the tooltip with automatic positioning and screen clamping.
        /// </summary>
        /// <param name="content">Rich text content for the tooltip</param>
        /// <param name="targetRect">RectTransform of the hovered UI element</param>
        public void ShowTooltip(string content, RectTransform targetRect)
        {
            Debug.Log($"[TooltipManager] ShowTooltip requested. Target: {(targetRect != null ? targetRect.name : "null")}, Content: {content}");

            // Move TooltipManager to the bottom of the Canvas hierarchy so it renders on top of everything else
            transform.SetAsLastSibling();

            if (_tooltipInstance == null)
            {
                Debug.LogError("[TooltipManager] Error: _tooltipInstance is null!");
                return;
            }
            if (textComponent == null)
            {
                Debug.LogError("[TooltipManager] Error: textComponent is null!");
                return;
            }
            if (targetRect == null)
            {
                Debug.LogError("[TooltipManager] Error: targetRect is null!");
                return;
            }

            // Set text and activate instance
            textComponent.text = content;
            _tooltipInstance.SetActive(true);

            // Print scale and group settings
            if (_tooltipCanvasGroup != null)
            {
                Debug.Log($"[TooltipManager] Tooltip CanvasGroup found. Alpha: {_tooltipCanvasGroup.alpha}, Interactable: {_tooltipCanvasGroup.interactable}");
            }
            else
            {
                Debug.Log("[TooltipManager] No CanvasGroup found on tooltip instance.");
            }

            // Force layout rebuild to get correct dimensions
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_tooltipRect);

            // Check Canvas render mode and associated camera
            Canvas canvas = targetRect.GetComponentInParent<Canvas>();
            Camera uiCamera = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                uiCamera = canvas.worldCamera;
            }
            Debug.Log($"[TooltipManager] Canvas: {(canvas != null ? canvas.name : "null")}, RenderMode: {(canvas != null ? canvas.renderMode.ToString() : "null")}, Camera: {(uiCamera != null ? uiCamera.name : "null")}");

            // Get target corners in world coordinates
            Vector3[] targetWorldCorners = new Vector3[4];
            targetRect.GetWorldCorners(targetWorldCorners);
            Debug.Log($"[TooltipManager] Target world corners: BL={targetWorldCorners[0]}, TL={targetWorldCorners[1]}, TR={targetWorldCorners[2]}, BR={targetWorldCorners[3]}");

            // Convert world corners to screen space coordinates
            Vector2[] targetScreenCorners = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                targetScreenCorners[i] = RectTransformUtility.WorldToScreenPoint(uiCamera, targetWorldCorners[i]);
            }
            Debug.Log($"[TooltipManager] Target screen corners: BL={targetScreenCorners[0]}, TL={targetScreenCorners[1]}, TR={targetScreenCorners[2]}, BR={targetScreenCorners[3]}");

            // Target center in screen space
            Vector2 targetCenterScreen = (targetScreenCorners[0] + targetScreenCorners[2]) / 2f;

            // Calculate tooltip dimensions in screen pixels
            float tooltipWidth = _tooltipRect.rect.width * _tooltipRect.lossyScale.x;
            float tooltipHeight = _tooltipRect.rect.height * _tooltipRect.lossyScale.y;
            Debug.Log($"[TooltipManager] Tooltip dimensions: Scale={_tooltipRect.lossyScale}, RectSize={_tooltipRect.rect.width}x{_tooltipRect.rect.height}, ScreenSize={tooltipWidth}x{tooltipHeight}");

            // Position to the right of target by default
            Vector2 screenPos = targetCenterScreen;
            screenPos.x = targetScreenCorners[2].x + (tooltipWidth / 2f) + padding;

            // Check if it overflows the right edge of the screen
            if (screenPos.x + (tooltipWidth / 2f) > Screen.width)
            {
                // Flip to the left side
                screenPos.x = targetScreenCorners[0].x - (tooltipWidth / 2f) - padding;
                Debug.Log($"[TooltipManager] Tooltip overflows right edge. Flipped to left side.");
            }

            // Clamp vertical position within screen bounds
            float minY = (tooltipHeight / 2f) + padding;
            float maxY = Screen.height - (tooltipHeight / 2f) - padding;
            screenPos.y = Mathf.Clamp(screenPos.y, minY, maxY);

            // Clamp horizontal position as a fallback safety measure
            float minX = (tooltipWidth / 2f) + padding;
            float maxX = Screen.width - (tooltipWidth / 2f) - padding;
            screenPos.x = Mathf.Clamp(screenPos.x, minX, maxX);
            Debug.Log($"[TooltipManager] Final calculated screen position: {screenPos}. Resolution: {Screen.width}x{Screen.height}");

            // Convert screen position back to world space and apply
            Vector3 worldPos;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(_tooltipRect, screenPos, uiCamera, out worldPos))
            {
                _tooltipRect.position = worldPos;
                Debug.Log($"[TooltipManager] Tooltip positioned successfully! World Position: {worldPos}, ActiveInHierarchy: {_tooltipInstance.activeInHierarchy}");
            }
            else
            {
                Debug.LogError("[TooltipManager] Error: ScreenPointToWorldPointInRectangle failed to convert screen position to world position!");
            }
        }

        /// <summary>
        /// Hide the tooltip.
        /// </summary>
        public void HideTooltip()
        {
            Debug.Log("[TooltipManager] HideTooltip called.");
            if (_tooltipInstance != null)
            {
                _tooltipInstance.SetActive(false);
            }
        }
    }
}
