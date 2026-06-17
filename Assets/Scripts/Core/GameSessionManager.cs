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
        [Tooltip("设置面板引用 (Canvas/SettingPanel)")]
        [SerializeField] private GameObject settingPanel;

        [Header("卡牌预制体 (用于动态生成)")]
        [SerializeField] private GameObject cardPrefab;

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

        [Header("调试")]
        [Tooltip("设为 >=0 则直接从该关卡索引起始（0=第1关 … 5=商店1 … 6=Boss1 … 7=二层第1关 … 10=商店2 … 11=Boss2）")]
        [SerializeField] private int debugSkipToLevel = -1;

        // 运行时状态
        private int currentGoldReward = 0;
        private bool isGoldChosen = false;
        private UI.RewardCardInteraction selectedCard = null;
        private bool isStartingGame = false; // 防重入标记

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 确保 AudioManager 在 Awake 阶段创建（早于其他组件的 Start）
            EnsureAudioManager();

            // 自动查找设置面板（如果 Inspector 中未赋值）
            if (settingPanel == null)
            {
                var canvas = GameObject.Find("Canvas");
                if (canvas != null)
                {
                    Transform spTrans = canvas.transform.Find("SettingPanel");
                    if (spTrans != null) settingPanel = spTrans.gameObject;
                }
            }
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

            // 开始/重新开始按钮的绑定已由 StartingPanelController / EndingPanelController 各自负责，
            // GameSessionManager 不再重复绑定，避免 StartGame() 被双击触发两次导致手牌翻倍。
            // （若场景中无对应面板控制器，请将按钮引用挂到对应面板控制器上，而非此处。）

            // 绑定顶部 Ultop 牌组按钮点击事件，进入本人牌组界面
            var ultop = FindObjectOfType<UI.UltopController>();
            if (ultop != null)
            {
                Transform cardsButtonTrans = ultop.transform.Find("Icon_Line/Cards");
                if (cardsButtonTrans != null)
                {
                    var btn = cardsButtonTrans.GetComponent<Button>();
                    if (btn == null) btn = cardsButtonTrans.gameObject.AddComponent<Button>();
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => OpenCardsMap(UI.CardsMapMode.PlayerDeck));
                }
            }

            // 确保 SettingPanel 上有 SettingPanelController 组件
            if (settingPanel != null)
            {
                var spController = settingPanel.GetComponent<UI.SettingPanelController>();
                if (spController == null)
                {
                    spController = settingPanel.AddComponent<UI.SettingPanelController>();
                }
            }

            // 绑定战斗面板中抽牌堆与弃牌堆的点击事件
            if (battlePanel != null)
            {
                Transform drawPileTrans = battlePanel.transform.Find("DrawPile_UI");
                if (drawPileTrans != null)
                {
                    var btn = drawPileTrans.GetComponent<Button>();
                    if (btn == null) btn = drawPileTrans.gameObject.AddComponent<Button>();
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => OpenCardsMap(UI.CardsMapMode.DrawPile));
                }

                Transform discardPileTrans = battlePanel.transform.Find("DiscardPile_UI");
                if (discardPileTrans != null)
                {
                    var btn = discardPileTrans.GetComponent<Button>();
                    if (btn == null) btn = discardPileTrans.gameObject.AddComponent<Button>();
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => OpenCardsMap(UI.CardsMapMode.DiscardPile));
                }
            }

            // 根据是否配置了开始面板，决定初始状态
            if (startingPanel != null)
            {
                HideAllPanels();
                startingPanel.SetActive(true);
                if (ultopPanel != null) ultopPanel.SetActive(false);

                // 开始界面 BGM
                EnsureBgmManager();
                if (BgmManager.Instance != null) BgmManager.Instance.PlayBgm("Dance of fireflies");
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

            // startGameButton / restartGameButton 的绑定与解绑已由各面板控制器负责，此处不再处理

            // 清理动态绑定的卡组及抽弃牌堆点击事件
            var ultop = FindObjectOfType<UI.UltopController>();
            if (ultop != null)
            {
                Transform cardsButtonTrans = ultop.transform.Find("Icon_Line/Cards");
                if (cardsButtonTrans != null)
                {
                    var btn = cardsButtonTrans.GetComponent<Button>();
                    if (btn != null) btn.onClick.RemoveAllListeners();
                }
            }

            if (battlePanel != null)
            {
                Transform drawPileTrans = battlePanel.transform.Find("DrawPile_UI");
                if (drawPileTrans != null)
                {
                    var btn = drawPileTrans.GetComponent<Button>();
                    if (btn != null) btn.onClick.RemoveAllListeners();
                }

                Transform discardPileTrans = battlePanel.transform.Find("DiscardPile_UI");
                if (discardPileTrans != null)
                {
                    var btn = discardPileTrans.GetComponent<Button>();
                    if (btn != null) btn.onClick.RemoveAllListeners();
                }
            }
        }

        /// <summary>
        /// 开始游戏（从第一关启动）
        /// </summary>
        public void StartGame()
        {
            // 防止同一帧内重复触发（如多个按钮监听器同时绑定）
            if (isStartingGame) return;
            isStartingGame = true;

            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.ResetGame();

                // Debug：Inspector 中设 debugSkipToLevel >= 0 则直接跳关
                if (debugSkipToLevel >= 0)
                {
                    LevelManager.Instance.SetDebugLevel(debugSkipToLevel);
                }
            }

            // 初始化玩家属性 (血量80，血糖5.7，金币99)
            if (playerStats != null)
            {
                playerStats.Initialize(80, 5.7f, 99);
            }

            LoadCurrentLevel();

            isStartingGame = false;
        }

#if UNITY_EDITOR
        private void Update()
        {
            // 键盘快捷键快速跳关（仅 Editor 下有效）
            if (!Input.anyKeyDown) return;

            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // Shift+数字键 跳 10/11/12（必须先于普通数字键处理）
            if (shift)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0)) { JumpToLevel(10); return; }
                if (Input.GetKeyDown(KeyCode.Alpha1)) { JumpToLevel(11); return; }
                if (Input.GetKeyDown(KeyCode.Alpha2)) { JumpToLevel(12); return; }
            }

            // 普通数字键 0-9（Shift 没按下时才生效，避免冲突）
            for (int i = 0; i <= 9; i++)
            {
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha0 + i)))
                {
                    JumpToLevel(i);
                    return;
                }
            }
        }

        private void JumpToLevel(int index)
        {
            if (LevelManager.Instance == null) return;
            int maxIdx = LevelManager.Instance.LevelSequence.Count - 1;
            if (index < 0 || index > maxIdx) return;

            LevelManager.Instance.SetDebugLevel(index);
            // 不重置 PlayerStats —— 跳关应保持当前数据，仅切换关卡
            LoadCurrentLevel();
            Debug.Log($"[GameSessionManager] 快捷键跳关 → 索引 {index}");
        }
#endif

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
                if (battlePanel != null)
                {
                    battlePanel.SetActive(true);

                    // 按当前层数切换战斗背景图：一层用 background1，二层用 background2
                    int layer = LevelManager.Instance.CurrentLayer;
                    string bgSpriteName = layer == 1 ? "background1" : "background2";
                    Transform bgTrans = battlePanel.transform.Find("Background");
                    if (bgTrans != null)
                    {
                        Image bgImage = bgTrans.GetComponent<Image>();
                        if (bgImage != null)
                        {
                            Sprite bgSprite = Resources.Load<Sprite>($"Sprites/Backgrounds/{bgSpriteName}");
                            if (bgSprite != null)
                            {
                                bgImage.sprite = bgSprite;
                            }
                        }
                    }
                }

                // 装载敌人并启动战斗
                if (battleController != null)
                {
                    battleController.StartingEnemyId = node.enemyId;
                    battleController.StartBattle();
                }

                // 战斗 BGM：普通敌人用 Battle Scars，Boss 用 Advent time
                EnsureBgmManager();
                if (BgmManager.Instance != null)
                {
                    BgmManager.Instance.PlayBgm(node.type == LevelType.Boss ? "Advent time" : "Battle Scars");
                }
            }
            else if (node.type == LevelType.Shop)
            {
                if (shopPanel != null)
                {
                    // 确保 ShopController 组件存在（场景未保存时动态补齐）
                    var shopCtrl = shopPanel.GetComponent<UI.ShopController>();
                    if (shopCtrl == null)
                    {
                        shopCtrl = shopPanel.AddComponent<UI.ShopController>();
                    }
                    // 注入 PlayerStats（此时 BattlePanel 可能已隐藏，FindObjectOfType 找不到 inactive 对象）
                    if (playerStats != null)
                    {
                        shopCtrl.SetPlayerStats(playerStats);
                    }
                    shopPanel.SetActive(true);
                }

                // 商店 BGM
                EnsureBgmManager();
                if (BgmManager.Instance != null) BgmManager.Instance.PlayBgm("Back to yesterday");
            }

            // 通知顶部栏刷新
            var ultop = FindObjectOfType<UI.UltopController>();
            if (ultop != null)
            {
                // 注入 PlayerStats（确保 UltopController 始终持有正确引用，不受面板显隐影响）
                if (playerStats != null)
                {
                    ultop.SetPlayerStats(playerStats);
                }
                ultop.UpdateAllUI();
            }
        }

        /// <summary>
        /// 打开卡牌展示面板 (DrawPile, DiscardPile, PlayerDeck, CardLibrary)
        /// </summary>
        public void OpenCardsMap(UI.CardsMapMode mode)
        {
            if (cardsMapPanel == null) return;

            // 寻找当前正处于打开状态的源面板
            GameObject currentActivePanel = null;
            if (battlePanel != null && battlePanel.activeSelf) currentActivePanel = battlePanel;
            else if (shopPanel != null && shopPanel.activeSelf) currentActivePanel = shopPanel;
            else if (settlementPanel != null && settlementPanel.activeSelf) currentActivePanel = settlementPanel;
            else if (startingPanel != null && startingPanel.activeSelf) currentActivePanel = startingPanel;

            var controller = cardsMapPanel.GetComponent<UI.CardsMapController>();
            if (controller == null)
            {
                controller = cardsMapPanel.AddComponent<UI.CardsMapController>();
            }

            controller.Open(mode, currentActivePanel);
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
            bool isBossBattle = LevelManager.Instance != null &&
                                LevelManager.Instance.CurrentNode != null &&
                                LevelManager.Instance.CurrentNode.type == LevelType.Boss;

            if (isBossBattle)
            {
                currentGoldReward = Random.Range(90, 111);
            }
            else
            {
                currentGoldReward = Random.Range(26, 35);
            }
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

                // 为金币奖励按钮配置 Hover 特效和音效（统一使用 Button_Hover）
                var goldHover = chooseGoldButton.gameObject.GetComponent<UI.UIHoverButtonEffects>();
                if (goldHover == null) goldHover = chooseGoldButton.gameObject.AddComponent<UI.UIHoverButtonEffects>();
                AudioClip buttonHoverClip = Resources.Load<AudioClip>("Audio/Button_Hover");
                goldHover.Setup(buttonHoverClip != null ? buttonHoverClip : hoverAudioClip, 1.05f);
            }

            // 2. 隐藏确认按钮（卡牌可以不选，下一关按钮始终可见）
            if (chooseUIConfirmButton != null)
            {
                chooseUIConfirmButton.SetActive(false);
            }

            if (settlementExitButton != null)
            {
                settlementExitButton.gameObject.SetActive(true);
                settlementExitButton.interactable = true;
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
                    AudioClip btnHover1 = Resources.Load<AudioClip>("Audio/Button_Hover");
                    settingHover.Setup(btnHover1 != null ? btnHover1 : hoverAudioClip, 1.1f);
                }

                Transform cardsButtonTrans = ultop.transform.Find("Icon_Line/Cards");
                if (cardsButtonTrans != null)
                {
                    var cardsHover = cardsButtonTrans.gameObject.GetComponent<UI.UIHoverButtonEffects>();
                    if (cardsHover == null) cardsHover = cardsButtonTrans.gameObject.AddComponent<UI.UIHoverButtonEffects>();
                    AudioClip btnHover2 = Resources.Load<AudioClip>("Audio/Button_Hover");
                    cardsHover.Setup(btnHover2 != null ? btnHover2 : hoverAudioClip, 1.05f);
                }
            }

            // 4. 展示并绑定卡牌奖励 UI
            if (rewardCardsContainer != null)
            {
                Transform contentTransform = rewardCardsContainer.Find("Scroll View/Viewport/Content");
                if (contentTransform == null)
                {
                    contentTransform = rewardCardsContainer;
                }

                // 清除原有的测试/残留卡牌
                foreach (Transform child in contentTransform)
                {
                    Destroy(child.gameObject);
                }

                var pendingRewards = battleController.PendingRewardCards;
                if (pendingRewards != null && pendingRewards.Count > 0)
                {
                    if (cardPrefab == null)
                    {
                        cardPrefab = Resources.Load<GameObject>("Prefabs/Card");
                    }

                    foreach (var cardInfo in pendingRewards)
                    {
                        if (cardPrefab == null) continue;

                        GameObject cardChild = Instantiate(cardPrefab, contentTransform);
                        cardChild.name = $"RewardCard_{cardInfo.id}";
                        cardChild.transform.localScale = Vector3.one;

                        var cg = cardChild.GetComponent<CanvasGroup>();
                        if (cg == null) cg = cardChild.AddComponent<CanvasGroup>();
                        cg.alpha = 1.0f;

                        // 渲染卡牌数据
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

                        // 挂载特有的 Hover 放大/点击固定组件
                        var rewardCardClick = cardChild.GetComponent<UI.RewardCardInteraction>();
                        if (rewardCardClick == null)
                        {
                            rewardCardClick = cardChild.AddComponent<UI.RewardCardInteraction>();
                        }
                        rewardCardClick.Initialize(cardInfo, OnRewardCardClicked);

                        // 如果卡牌上原本有旧的 Button 监听，全部移除
                        var btn = cardChild.GetComponent<Button>();
                        if (btn != null)
                        {
                            btn.onClick.RemoveAllListeners();
                        }
                    }
                }
            }

            // 打开结算面板
            if (settlementPanel != null)
            {
                settlementPanel.SetActive(true);
            }

            // 结算场景属于战斗收尾，不中断战斗 BGM，保持音乐连贯直到下一关
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

            // 飞行动画期间临时禁用下一关按钮，避免误操作
            if (settlementExitButton != null)
            {
                settlementExitButton.interactable = false;
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

            // 结束界面 BGM
            EnsureBgmManager();
            if (BgmManager.Instance != null) BgmManager.Instance.PlayBgm("Dance of fireflies");
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

            // 结束界面 BGM
            EnsureBgmManager();
            if (BgmManager.Instance != null) BgmManager.Instance.PlayBgm("Dance of fireflies");

            Debug.Log("[GameSessionManager] 恭喜你通过全部关卡，达成通关！");
        }

        private void OnSettlementExitClicked()
        {
            // 若未手动领取金币，自动触发领取逻辑
            if (!isGoldChosen)
            {
                OnChooseGoldClicked();
            }

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
            if (settingPanel != null) settingPanel.SetActive(false);
        }

        /// <summary>
        /// 获取当前活跃的游戏面板（战斗/商店/结算之一），供 SettingPanelController 确定来源。
        /// </summary>
        public GameObject GetCurrentActiveGamePanel()
        {
            if (battlePanel != null && battlePanel.activeSelf) return battlePanel;
            if (shopPanel != null && shopPanel.activeSelf) return shopPanel;
            if (settlementPanel != null && settlementPanel.activeSelf) return settlementPanel;
            return null;
        }

        /// <summary>
        /// 返回主菜单（开始界面），从游戏中退出时调用。
        /// </summary>
        public void ReturnToMainMenu()
        {
            // 隐藏所有游戏面板
            HideAllPanels();

            // 停止 BGM
            if (BgmManager.Instance != null) BgmManager.Instance.StopBgm();

            // 重新播放开始界面 BGM
            EnsureBgmManager();
            if (BgmManager.Instance != null) BgmManager.Instance.PlayBgm("Dance of fireflies");

            // 显示开始界面
            if (startingPanel != null) startingPanel.SetActive(true);

            // 隐藏顶部 UI
            if (ultopPanel != null) ultopPanel.SetActive(false);

            // 重置关卡
            if (LevelManager.Instance != null) LevelManager.Instance.ResetGame();
        }

        /// <summary>
        /// 从设置面板中打开卡牌图鉴。
        /// 与 OpenCardsMap 的区别：sourcePanel 为设置面板自身而非游戏面板。
        /// </summary>
        public void OpenCardsMapFromSettings(UI.CardsMapMode mode, GameObject settingsPanel)
        {
            if (cardsMapPanel == null) return;

            var controller = cardsMapPanel.GetComponent<UI.CardsMapController>();
            if (controller == null)
            {
                controller = cardsMapPanel.AddComponent<UI.CardsMapController>();
            }

            controller.Open(mode, settingsPanel);
        }

        /// <summary>
        /// 确保场景中存在 BgmManager 实例，若不存在则自动创建
        /// </summary>
        private void EnsureBgmManager()
        {
            if (BgmManager.Instance == null)
            {
                GameObject bgmGo = new GameObject("[BgmManager]");
                bgmGo.AddComponent<BgmManager>();
            }
        }

        /// <summary>
        /// 确保场景中存在 AudioManager 实例，若不存在则自动创建
        /// </summary>
        private void EnsureAudioManager()
        {
            if (AudioManager.Instance == null)
            {
                GameObject amGo = new GameObject("[AudioManager]");
                amGo.AddComponent<AudioManager>();
            }
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
