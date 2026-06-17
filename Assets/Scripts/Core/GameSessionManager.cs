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
        [SerializeField] private GameObject goldIconGo;
        [SerializeField] private GameObject goldValueGo;
        [SerializeField] private Button chooseGoldButton;
        [SerializeField] private GameObject chooseUIConfirmButton;

        [Header("音效配置")]
        [SerializeField] private AudioClip hoverAudioClip;
        [SerializeField] private AudioClip clickAudioClip;

        [Header("商店 UI 引用")]
        [Tooltip("商店退出按钮 (Canvas/ShopPanel/CardListPanel/ExitUI)")]
        [SerializeField] private Button shopExitButton;

        [Header("游戏开始/重新开始按钮")]
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button restartGameButton;

        // 运行时状态
        private int currentGoldReward = 0;
        private bool isGoldChosen = false;
        private UI.RewardCardInteraction selectedCard = null;

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

            // 绑定金币与确认卡牌奖励按钮
            if (chooseGoldButton != null)
            {
                chooseGoldButton.onClick.AddListener(OnChooseGoldClicked);
            }

            if (chooseUIConfirmButton != null)
            {
                var btn = chooseUIConfirmButton.GetComponent<Button>();
                if (btn == null)
                {
                    btn = chooseUIConfirmButton.AddComponent<Button>();
                }
                btn.onClick.AddListener(OnConfirmCardClicked);
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

            // 1. 产生随机金币奖励金额并记录，但不立刻增加玩家金币（等玩家点击时才增加）
            currentGoldReward = Random.Range(30, 51);
            isGoldChosen = false;

            // 显示金币数量
            if (settlementGoldValueText != null)
            {
                settlementGoldValueText.text = $"+{currentGoldReward}";
            }

            // 确保金币奖励相关 GameObject 重新激活显示
            if (goldIconGo != null) goldIconGo.SetActive(true);
            if (goldValueGo != null) goldValueGo.SetActive(true);
            if (chooseGoldButton != null)
            {
                chooseGoldButton.gameObject.SetActive(true);
                chooseGoldButton.interactable = true;

                // 为金币奖励按钮配置 Hover 特效和音效
                var goldHover = chooseGoldButton.gameObject.GetComponent<UI.UIHoverButtonEffects>();
                if (goldHover == null) goldHover = chooseGoldButton.gameObject.AddComponent<UI.UIHoverButtonEffects>();
                goldHover.Setup(hoverAudioClip, 1.05f);
            }

            // 2. 隐藏确认按钮与下一关按钮，直到玩家完成对应的交互
            if (chooseUIConfirmButton != null)
            {
                chooseUIConfirmButton.SetActive(false);
            }

            if (settlementExitButton != null)
            {
                settlementExitButton.gameObject.SetActive(false);
            }

            // 重置选中的卡牌
            selectedCard = null;

            // 3. 配置顶部 UI 可点击项 (设置与卡组) 的 Hover 特效与音效
            var ultop = FindObjectOfType<UI.UltopController>();
            if (ultop != null)
            {
                Transform settingButtonTrans = ultop.transform.Find("Icon_Line/Setting");
                if (settingButtonTrans != null)
                {
                    var settingHover = settingButtonTrans.gameObject.GetComponent<UI.UIHoverButtonEffects>();
                    if (settingHover == null) settingHover = settingButtonTrans.gameObject.AddComponent<UI.UIHoverButtonEffects>();
                    settingHover.Setup(hoverAudioClip, 1.1f);
                }

                Transform cardsButtonTrans = ultop.transform.Find("Icon_Line/Cards");
                if (cardsButtonTrans != null)
                {
                    var cardsHover = cardsButtonTrans.gameObject.GetComponent<UI.UIHoverButtonEffects>();
                    if (cardsHover == null) cardsHover = cardsButtonTrans.gameObject.AddComponent<UI.UIHoverButtonEffects>();
                    cardsHover.Setup(hoverAudioClip, 1.05f);
                }
            }

            // 4. 展示并绑定卡牌奖励 UI (从 pre-placed 寻找 Scroll View/Viewport/Content 里的 3 个 Card)
            if (rewardCardsContainer != null)
            {
                Transform contentTransform = rewardCardsContainer.Find("Scroll View/Viewport/Content");
                if (contentTransform == null)
                {
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

                            // 禁用拖拽脚本以免冲突
                            var dragHandler = cardChild.GetComponent<UI.CardDragHandler>();
                            if (dragHandler != null)
                            {
                                dragHandler.enabled = false;
                            }

                            // 重置 CardUI 缩放与透明度 CanvasGroup
                            cardChild.localScale = Vector3.one;
                            var cg = cardChild.GetComponent<CanvasGroup>();
                            if (cg != null) cg.alpha = 1.0f;

                            // 挂载特有的 Hover 放大/点击固定组件
                            var rewardCardClick = cardChild.GetComponent<UI.RewardCardInteraction>();
                            if (rewardCardClick == null)
                            {
                                rewardCardClick = cardChild.gameObject.AddComponent<UI.RewardCardInteraction>();
                            }
                            rewardCardClick.Initialize(cardInfo, OnRewardCardClicked);

                            // 如果卡牌上原本有旧的 Button 监听，全部移除
                            var btn = cardChild.GetComponent<Button>();
                            if (btn != null) btn.onClick.RemoveAllListeners();
                        }
                        else
                        {
                            cardChild.gameObject.SetActive(false);
                        }
                    }
                }
            }

            // 打开结算面板
            if (settlementPanel != null)
            {
                settlementPanel.SetActive(true);
            }
        }

        private void OnChooseGoldClicked()
        {
            if (isGoldChosen) return;
            isGoldChosen = true;

            // 播放金币点击音效
            if (clickAudioClip != null && Camera.main != null)
            {
                AudioSource.PlayClipAtPoint(clickAudioClip, Camera.main.transform.position);
            }

            // 上方 UI 增加金币并刷新
            if (playerStats != null)
            {
                playerStats.ChangeGold(currentGoldReward);
            }

            // 金币相关的组件（图标和文本，按钮）消失，背景框不消失
            if (goldIconGo != null) goldIconGo.SetActive(false);
            if (goldValueGo != null) goldValueGo.SetActive(false);
            if (chooseGoldButton != null)
            {
                chooseGoldButton.gameObject.SetActive(false);
            }

            Debug.Log($"[GameSessionManager] 玩家领取了金币奖励：+{currentGoldReward} 金币。");
        }

        private void OnRewardCardClicked(UI.RewardCardInteraction clickedInteraction)
        {
            if (clickedInteraction == null || selectedCard == clickedInteraction) return;

            // 播放点击音效
            if (clickAudioClip != null && Camera.main != null)
            {
                AudioSource.PlayClipAtPoint(clickAudioClip, Camera.main.transform.position);
            }

            // 取消之前选中的卡牌特效
            if (selectedCard != null)
            {
                selectedCard.SetSelected(false);
            }

            // 选中新卡牌并保持放大状态
            selectedCard = clickedInteraction;
            selectedCard.SetSelected(true);

            // 选中一张卡牌之后，显示“确认”按钮
            if (chooseUIConfirmButton != null)
            {
                chooseUIConfirmButton.SetActive(true);
            }

            Debug.Log($"[GameSessionManager] 玩家选中了卡牌奖励: {clickedInteraction.CardInfo.name}。");
        }

        private void OnConfirmCardClicked()
        {
            if (selectedCard == null) return;

            // 确认按钮消失
            if (chooseUIConfirmButton != null)
            {
                chooseUIConfirmButton.SetActive(false);
            }

            string chosenCardId = selectedCard.CardInfo.id;
            GameObject selectedCardGo = selectedCard.gameObject;

            // 其余两张牌消失，只保留背景
            Transform contentTransform = rewardCardsContainer.Find("Scroll View/Viewport/Content");
            if (contentTransform == null)
            {
                contentTransform = rewardCardsContainer;
            }

            for (int i = 0; i < contentTransform.childCount; i++)
            {
                Transform child = contentTransform.GetChild(i);
                if (child.gameObject != selectedCardGo)
                {
                    child.gameObject.SetActive(false);
                }
            }

            // 开始播放卡牌飞入顶部牌组的动画，并在飞入后加入卡组并更新 UI
            StartCoroutine(AnimateCardFlyToDeck(selectedCardGo, chosenCardId));
        }

        private IEnumerator AnimateCardFlyToDeck(GameObject cardGo, string cardId)
        {
            // 播放飞入/点击音效
            if (clickAudioClip != null && Camera.main != null)
            {
                AudioSource.PlayClipAtPoint(clickAudioClip, Camera.main.transform.position);
            }

            // 寻找到 UItop 卡组的目标位置
            Transform deckTarget = null;
            var ultop = FindObjectOfType<UI.UltopController>();
            if (ultop != null)
            {
                deckTarget = ultop.transform.Find("Icon_Line/Cards");
            }

            if (deckTarget != null)
            {
                // 创建一个飞行的临时克隆卡牌挂在 Canvas 下
                Canvas canvas = FindObjectOfType<Canvas>();
                GameObject flyClone = Instantiate(cardGo, canvas.transform);
                flyClone.name = "FlyCardClone_" + cardId;

                // 去除克隆体上的事件绑定与交互脚本，以免造成事件穿透
                Destroy(flyClone.GetComponent<UI.RewardCardInteraction>());
                Destroy(flyClone.GetComponent<Button>());
                var cg = flyClone.GetComponent<CanvasGroup>();
                if (cg == null) cg = flyClone.AddComponent<CanvasGroup>();
                cg.blocksRaycasts = false;

                // 初始位置设为原卡牌的位置与大小
                flyClone.transform.position = cardGo.transform.position;
                flyClone.transform.localScale = cardGo.transform.localScale;

                // 隐藏原卡牌
                cardGo.SetActive(false);

                // 平滑插值动画
                Vector3 startPos = flyClone.transform.position;
                Vector3 startScale = flyClone.transform.localScale;
                float elapsed = 0f;
                float duration = 0.75f; // 0.75 秒飞行时间

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    float easedT = t * (2 - t); // Ease Out 算法

                    flyClone.transform.position = Vector3.Lerp(startPos, deckTarget.position, easedT);
                    flyClone.transform.localScale = Vector3.Lerp(startScale, Vector3.one * 0.15f, easedT); // 渐缩至 0.15 倍吸入
                    cg.alpha = Mathf.Lerp(1.0f, 0f, easedT); // 渐隐

                    yield return null;
                }

                Destroy(flyClone);
            }
            else
            {
                cardGo.SetActive(false);
            }

            // 将卡牌加入玩家牌组
            if (battleController != null)
            {
                battleController.AddCardToStartingDeck(cardId);
            }

            // 刷新顶部牌组计数
            if (ultop != null)
            {
                ultop.UpdateCardsCount();
            }

            // 整个卡牌选取与飞入结束后，激活“下一关”退出按钮
            if (settlementExitButton != null)
            {
                settlementExitButton.gameObject.SetActive(true);
                settlementExitButton.interactable = true;
            }

            Debug.Log($"[GameSessionManager] 关卡奖励卡牌 {cardId} 已被成功加入，可以推进到下一关。");
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

                    Transform goldIconTrans = setP.Find("CardListPanel/Golds/Gold/Gold_Icon");
                    if (goldIconTrans != null) goldIconGo = goldIconTrans.gameObject;

                    Transform goldValTrans = setP.Find("CardListPanel/Golds/Gold/Gold_Value");
                    if (goldValTrans != null) goldValueGo = goldValTrans.gameObject;

                    Transform chooseGTrans = setP.Find("CardListPanel/Golds/Gold/Choose_Gold");
                    if (chooseGTrans != null) chooseGoldButton = chooseGTrans.GetComponent<Button>();

                    Transform chooseUI = setP.Find("CardListPanel/Cards/ChooseUI");
                    if (chooseUI != null) chooseUIConfirmButton = chooseUI.gameObject;
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
