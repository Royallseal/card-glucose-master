using System.Collections.Generic;
using UnityEngine;
using CGM.Data;
using CGM.UI;

namespace CGM.Test
{
    /// <summary>
    /// 卡牌布局与渲染测试脚本。
    /// 挂载到 Canvas 下的某个 GameObject 上（推荐带有 GridLayoutGroup 的容器），即可自动实例化所有卡牌进行排版与渲染测试。
    /// </summary>
    public class CardLayoutTest : MonoBehaviour
    {
        [Header("测试配置")]
        [Tooltip("卡牌预制体资源路径（在 Resources/Prefabs 下）")]
        [SerializeField] private string prefabPath = "Prefabs/Card";
        
        [Tooltip("卡牌生成容器，若为空则默认使用当前 GameObject")]
        [SerializeField] private Transform container;

        [Tooltip("是否在 Start 时自动生成所有卡牌进行测试")]
        [SerializeField] private bool autoGenerateOnStart = true;

        private void Start()
        {
            if (container == null)
            {
                container = transform;
            }

            if (autoGenerateOnStart)
            {
                GenerateAllCards();
            }
        }

        /// <summary>
        /// 生成并渲染数据库中的所有卡牌。
        /// </summary>
        public void GenerateAllCards()
        {
            // 确保 CardDatabase 存在并已初始化
            CardDatabase database = FindObjectOfType<CardDatabase>();
            if (database == null)
            {
                GameObject dbGo = new GameObject("[Temp_CardDatabase]");
                database = dbGo.AddComponent<CardDatabase>();
                
                // 手动触发一次数据加载以确保数据就绪
                var method = typeof(CardDatabase).GetMethod("LoadCardData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method != null)
                {
                    method.Invoke(database, null);
                }
            }

            List<CardInfo> allCards = database.GetAllCards();
            if (allCards == null || allCards.Count == 0)
            {
                Debug.LogError("[CardLayoutTest] 未能在 CardDatabase 中获取到任何卡牌数据，请确认 cards.json 已编译。");
                return;
            }

            GameObject cardPrefab = Resources.Load<GameObject>(prefabPath);
            if (cardPrefab == null)
            {
                Debug.LogError($"[CardLayoutTest] 未能在 Resources 下找到卡牌预制体：{prefabPath}");
                return;
            }

            // 清理旧的测试卡牌
            foreach (Transform child in container)
            {
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            Debug.Log($"[CardLayoutTest] 开始生成卡牌，共 {allCards.Count} 张。");

            foreach (var card in allCards)
            {
                GameObject cardInstance = Instantiate(cardPrefab, container);
                cardInstance.name = $"Card_{card.id}";

                CardUI cardUI = cardInstance.GetComponent<CardUI>();
                if (cardUI == null)
                {
                    Debug.LogError($"[CardLayoutTest] 预制体上未找到 CardUI 组件！");
                    continue;
                }

                // 渲染卡牌
                cardUI.SetCard(card);
            }

            Debug.Log("[CardLayoutTest] 所有卡牌生成并渲染完成。");
        }
    }
}
