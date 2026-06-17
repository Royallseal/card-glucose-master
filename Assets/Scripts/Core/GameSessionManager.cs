using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using CGM.Data;

namespace CGM.Core
{
    /// <summary>
    /// 全局会话控制器，负责游戏核心关卡流转、面板显隐切换、金币结算与卡牌奖励派发。
    /// </summary>
    public class GameSessionManager : MonoBehaviour
    {
        public static GameSessionManager Instance { get; private set; }

        [Header("核心引用")]
        [SerializeField] private BattleSessionController battleController;
        [SerializeField] private PlayerStats playerStats;

        [Header("UI 面板引用")]
        [SerializeField] private GameObject battlePanel;
        [SerializeField] private GameObject shopPanel;
        [SerializeField] private GameObject settlementPanel;
        [SerializeField] private GameObject cardsMapPanel;
        [SerializeField] private GameObject startingPanel;
        [SerializeField] private GameObject endingPanel;
        [SerializeField] private GameObject ultopPanel;

        [Header("结算与奖励 UI 引用")]
        [Tooltip("结算界面卡牌奖励容器 (Canvas/SettlementPanel/CardListPanel/Cards)")]
        [SerializeField] private Transform rewardCardsContainer;
        [Tooltip("结算界面退出按钮 (Canvas/SettlementPanel/CardListPanel/ExitUI)")]
        [SerializeField] private Button settlementExitButton;
        [Tooltip("结算界面金币值文本 (Gold_Value)")]
        [SerializeField] private TextMeshProUGUI settlementGoldValueText;

        [Header("商店 UI 引用")]
        [Tooltip("商店退出按钮 (Canvas/ShopPanel/CardListPanel/ExitUI)")]
        [SerializeField] private Button shopExitButton;

        [Header("游戏开始/重新开始按钮")]
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button restartGameButton;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // 自动加载 LevelManager (如果场景中没有的话)
            if (LevelManager.Instance == null)
            {
                GameObject lmGo = new GameObject("[LevelManager]");
                lmGo.AddComponent<LevelManager>();
            }

            // 绑定战斗结束监听
            if (battleController != null)
            {
                battleController.OnBattleEnded += OnBattleEnded;
            }
            else
            {
                battleController = FindObjectOfType<BattleSessionController>();
                if (battleController != null)
                {
                    battleController.OnBattleEnded += OnBattleEnded;
                }
            }

            if (playerStats == null)
            {
                playerStats = FindObjectOfType<PlayerStats>();
            }

            // 绑定结算与商店退出按钮
            if (settlementExitButton != null)
            {
                settlementExitButton.onClick.AddListener(OnSettlementExitClicked);
            }

            if (shopExitButton != null)
            {
                shopExitButton.onClick.AddListener(OnShopExitClicked);
            }

            // 绑定开始与结束按钮
            if (startGameButton != null)
            {
                startGameButton.onClick.AddListener(StartGame);
            }

            if (restartGameButton != null)
            {
                restartGameButton.onClick.AddListener(RestartGame);
            }

            // 根据是否配置了开始面板，决定初始状态
            if (startingPanel != null)
            {
                HideAllPanels();
                startingPanel.SetActive(true);
                if (ultopPanel != null) ultopPanel.SetActive(false);
            }
            else
            {
                StartGame();
            }
        }

        private void OnDestroy()
        {
            if (battleController != null)
            {
                battleController.OnBattleEnded -= OnBattleEnded;
            }

            if (settlementExitButton != null)
            {
                settlementExitButton.onClick.RemoveListener(OnSettlementExitClicked);
            }

            if (shopExitButton != null)
            {
                shopExitButton.onClick.RemoveListener(OnShopExitClicked);
            }

            if (startGameButton != null)
            {
                startGameButton.onClick.RemoveListener(StartGame);
            }

            if (restartGameButton != null)
            {
                restartGameButton.onClick.RemoveListener(RestartGame);
            }
        }

        /// <summary>
        /// 开始游戏（从第一关启动）
        /// </summary>
        public void StartGame()
        {
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.ResetGame();
            }

            // 初始化玩家属性 (血量80，血糖5.7，金币99)
            if (playerStats != null)
            {
                playerStats.Initialize(80, 5.7f, 99);
            }

            LoadCurrentLevel();
        }

        /// <summary>
        /// 重新开始游戏
        /// </summary>
        public void RestartGame()
        {
            StartGame();
        }

        /// <summary>
        /// 根据 LevelManager 当前索引加载对应的面板与逻辑
        /// </summary>
        public void LoadCurrentLevel()
        {
            if (LevelManager.Instance == null) return;

            LevelNode node = LevelManager.Instance.CurrentNode;
            if (node == null)
            {
                // 通关结局
                ShowEndingPanel();
                return;
            }

            HideAllPanels();

            // 顶部 Ultop 栏在敌人和商店界面都需要显示
            if (ultopPanel != null)
            {
                ultopPanel.SetActive(node.type == LevelType.Enemy || node.type == LevelType.Boss || node.type == LevelType.Shop);
            }

            if (node.type == LevelType.Enemy || node.type == LevelType.Boss)
            {
                if (battlePanel != null) battlePanel.SetActive(true);

                // 装载敌人并启动战斗
                if (battleController != null)
                {
                    battleController.StartingEnemyId = node.enemyId;
                    battleController.StartBattle();
                }
            }
            else if (node.type == LevelType.Shop)
            {
                if (shopPanel != null) shopPanel.SetActive(true);
            }

            // 通知顶部栏刷新
            var ultop = FindObjectOfType<UI.UltopController>();
            if (ultop != null)
            {
                ultop.UpdateAllUI();
            }
        }

        private void OnBattleEnded(BattleOutcome outcome)
        {
            if (outcome == BattleOutcome.Victory)
            {
                StartCoroutine(ShowVictorySettlementDelay());
            }
            else if (outcome == BattleOutcome.Defeat)
            {
                StartCoroutine(ShowDefeatEndingDelay());
            }
        }

        private IEnumerator ShowVictorySettlementDelay()
        {
            yield return new WaitForSeconds(1.5f); // 延迟 1.5 秒展示结算以呈现最后一击动画效果

            // 1. 给玩家奖励随机金币 (30~50)
            int goldReward = Random.Range(30, 51);
            if (playerStats != null)
            {
                playerStats.ChangeGold(goldReward);
            }

            // 2. 显示金币增加额
            if (settlementGoldValueText != null)
            {
                settlementGoldValueText.text = $"+{goldReward}";
            }

            // 3. 展示并绑定卡牌奖励 UI (从 pre-placed 寻找 Scroll View/Viewport/Content 里的 3 个 Card)
            if (rewardCardsContainer != null)
            {
                Transform contentTransform = rewardCardsContainer.Find("Scroll View/Viewport/Content");
                if (contentTransform == null)
                {
                    // 尝试直接作为 content
                    contentTransform = rewardCardsContainer;
                }

                var pendingRewards = battleController.PendingRewardCards;
                if (pendingRewards != null && pendingRewards.Count > 0)
                {
                    for (int i = 0; i < contentTransform.childCount; i++)
                    {
                        Transform cardChild = contentTransform.GetChild(i);
                        if (i < pendingRewards.Count)
                        {
                            cardChild.gameObject.SetActive(true);
                            CardInfo cardInfo = pendingRewards[i];

                            // 渲染卡牌
                            var cardUI = cardChild.GetComponent<UI.CardUI>();
                            if (cardUI != null)
                            {
                                cardUI.SetCard(cardInfo);
                            }

                            // 禁用拖拽与悬停逻辑以免与结算页面冲突
                            var dragHandler = cardChild.GetComponent<UI.CardDragHandler>();
                            if (dragHandler != null)
                            {
                                dragHandler.enabled = false;
                            }

                            // 重置 CardUI 缩放与透明度 CanvasGroup
                            cardChild.localScale = Vector3.one;
                            var cg = cardChild.GetComponent<CanvasGroup>();
                            if (cg != null) cg.alpha = 1.0f;

                            // 绑定点击交互，选中该卡牌奖励
                            var btn = cardChild.GetComponent<Button>();
                            if (btn == null)
                            {
                                btn = cardChild.gameObject.AddComponent<Button>();
                            }
                            btn.interactable = true;
                            btn.onClick.RemoveAllListeners();

                            string cid = cardInfo.id;
                            GameObject cardGo = cardChild.gameObject;
                            Transform container = contentTransform;
                            btn.onClick.AddListener(() =>
                            {
                                OnRewardCardChosen(cid, cardGo, container);
                            });
                        }
                        else
                        {
                            cardChild.gameObject.SetActive(false);
                        }
                    }
                }
            }

            // 默认使退出按钮可以点击（以防玩家跳过/卡组不增加直接走）
            if (settlementExitButton != null)
            {
                settlementExitButton.interactable = true;
            }

            // 打开结算面板
            if (settlementPanel != null)
            {
                settlementPanel.SetActive(true);
            }
        }

        private void OnRewardCardChosen(string cardId, GameObject chosenCardGo, Transform container)
        {
            // 将选中卡牌加入起始卡组
            if (battleController != null)
            {
                battleController.AddCardToStartingDeck(cardId);
            }

            // 选中后将其他卡牌致灰并置为不可交互，选中卡牌略微放大高亮
            for (int i = 0; i < container.childCount; i++)
            {
                Transform child = container.GetChild(i);
                var btn = child.GetComponent<Button>();
                if (btn != null) btn.interactable = false;

                if (child.gameObject != chosenCardGo)
                {
                    var cg = child.GetComponent<CanvasGroup>();
                    if (cg == null) cg = child.gameObject.AddComponent<CanvasGroup>();
                    cg.alpha = 0.4f;
                }
                else
                {
                    child.localScale = Vector3.one * 1.05f;
                }
            }

            // 立即刷新 Ultop 牌组计数显示
            var ultop = FindObjectOfType<UI.UltopController>();
            if (ultop != null)
            {
                ultop.UpdateCardsCount();
            }

            Debug.Log($"[GameSessionManager] 选中奖励卡牌: {cardId}，已成功加入起始卡组！");
        }

        private IEnumerator ShowDefeatEndingDelay()
        {
            yield return new WaitForSeconds(1.5f);
            HideAllPanels();
            if (endingPanel != null)
            {
                endingPanel.SetActive(true);
            }
            else
            {
                // 如果没有结局面板，退回起始面板
                if (startingPanel != null) startingPanel.SetActive(true);
            }
        }

        private void ShowEndingPanel()
        {
            HideAllPanels();
            if (endingPanel != null)
            {
                endingPanel.SetActive(true);
            }
            else
            {
                if (startingPanel != null) startingPanel.SetActive(true);
            }
            Debug.Log("[GameSessionManager] 恭喜你通过全部关卡，达成通关！");
        }

        private void OnSettlementExitClicked()
        {
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.EnterNextLevel();
            }
            LoadCurrentLevel();
        }

        private void OnShopExitClicked()
        {
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.EnterNextLevel();
            }
            LoadCurrentLevel();
        }

        private void ShowPanel(GameObject panel)
        {
            HideAllPanels();
            if (panel != null) panel.SetActive(true);
        }

        private void HideAllPanels()
        {
            if (battlePanel != null) battlePanel.SetActive(false);
            if (shopPanel != null) shopPanel.SetActive(false);
            if (settlementPanel != null) settlementPanel.SetActive(false);
            if (cardsMapPanel != null) cardsMapPanel.SetActive(false);
            if (startingPanel != null) startingPanel.SetActive(false);
            if (endingPanel != null) endingPanel.SetActive(false);
        }

#if UNITY_EDITOR
        private void Reset()
        {
            // Find BattleSessionController and PlayerStats in scene
            battleController = FindObjectOfType<BattleSessionController>();
            playerStats = FindObjectOfType<PlayerStats>();

            // If attached to BattleManager or Canvas, search Canvas for panels
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                Transform bp = canvas.transform.Find("BattlePanel");
                if (bp != null) battlePanel = bp.gameObject;

                Transform sp = canvas.transform.Find("ShopPanel");
                if (sp != null) shopPanel = sp.gameObject;

                Transform setP = canvas.transform.Find("SettlementPanel");
                if (setP != null) settlementPanel = setP.gameObject;

                Transform mp = canvas.transform.Find("Cards_Map");
                if (mp != null) cardsMapPanel = mp.gameObject;

                Transform startP = canvas.transform.Find("StartingPanel");
                if (startP != null) startingPanel = startP.gameObject;

                Transform endP = canvas.transform.Find("EndingPanel");
                if (endP != null) endingPanel = endP.gameObject;

                Transform up = canvas.transform.Find("UItop");
                if (up != null) ultopPanel = up.gameObject;

                // Under SettlementPanel
                if (setP != null)
                {
                    Transform cardsContainer = setP.Find("CardListPanel/Cards");
                    if (cardsContainer != null) rewardCardsContainer = cardsContainer;

                    Transform exitB = setP.Find("CardListPanel/ExitUI");
                    if (exitB != null) settlementExitButton = exitB.GetComponent<Button>();

                    Transform goldV = setP.Find("CardListPanel/Golds/Gold/Gold_Value");
                    if (goldV != null) settlementGoldValueText = goldV.GetComponent<TextMeshProUGUI>();
                }

                // Under ShopPanel
                if (sp != null)
                {
                    Transform exitB = sp.Find("CardListPanel/ExitUI");
                    if (exitB != null) shopExitButton = exitB.GetComponent<Button>();
                }
            }
        }
#endif
    }
}
