# 《卡牌控糖师》Unity C# 开发与编码规范

本规范定义了《卡牌控糖师》项目的 C# 代码编写格式、注释习惯、资源命名以及 Unity 开发最佳实践，旨在提高代码可读性，确保各模块之间的低耦合度。

---

## 一、 命名规范

各代码元素的命名应具备清晰的语义，严禁使用无意义的单字符（循环变量除外）。

### 1. 大驼峰命名法
适用于类名、结构体名、接口名、枚举名、方法名、属性名及公共常量：
* **类与结构体**：`CardManager`、`GlucoseSystem`
* **接口**：以 `I` 开头，如 `ICardEffect`、`IDamageable`
* **方法**：`ModifyGlucose()`、`DrawCards()`
* **属性**：`CurrentGlucose`、`ActiveBuffs`
* **常量/公共只读字段**：`MaxGlucoseValue`、`BaseEnergyCost`

### 2. 小驼峰命名法
适用于局部变量、方法参数：
* **局部变量**：`cardCount`、`targetEnemy`
* **方法参数**：`glucoseDelta`、`cardId`

### 3. 带下划线的小驼峰命名法
适用于类的私有（`private`）及保护（`protected`）成员变量：
* **私有成员**：`_currentGlucose`、`_handCards`
* **序列化私有成员**（面板显示）：`_cardPrefab`、`_glucoseSlider`

---

## 二、 代码结构与排版

### 1. 命名空间
所有代码必须包裹在以 `CGM`（Card Glucose Master）开头的命名空间中：
* **核心业务逻辑**：`namespace CGM.Core`
* **配置与数据结构**：`namespace CGM.Data`
* **界面表现与交互**：`namespace CGM.UI`
* **编辑器扩展工具**：`namespace CGM.Editor`

### 2. 类内部成员排列顺序
为了保持类文件的整洁，成员声明须按以下顺序排列：
1. **序列化私有字段**：`[SerializeField] private`
2. **普通私有字段**：`private`
3. **公共属性与字段**：`public`
4. **Unity 生命周期方法**：`Awake()` -> `Start()` -> `OnEnable()` -> `Update()` -> `OnDisable()` -> `OnDestroy()`
5. **公共方法 (Public Methods)**
6. **私有方法 (Private Methods)**

---

## 三、 注释规范

代码注释必须全部使用准确、简洁的**中文**。

### 1. XML 文档注释
所有公共类、接口、属性及公共方法，**必须**编写 XML 格式的三斜杠注释：
```csharp
/// <summary>
/// 修改玩家当前血糖值，并触发状态变更回调。
/// </summary>
/// <param name="delta">血糖变化量</param>
/// <param name="isCardEffect">是否由卡牌打出引起</param>
public void ModifyGlucose(float delta, bool isCardEffect)
{
    // 逻辑实现
}
```

### 2. 单行与行尾注释
用于解释代码的“设计意图”（为什么这么做），而不是描述代码的“执行动作”（在做什么）：
* **推荐**：`// 触发重合散点算法，防止多张同能耗同血糖卡牌在图标中重合`
* **避免**：`// 循环遍历 group 列表`

---

## 四、 Unity 开发最佳实践

### 1. 引用与序列化
* 尽量减少使用 `GameObject.Find` 或 `FindObjectOfType`。所有的预制体和跨组件引用，须使用 `[SerializeField] private` 并在 Unity 编辑器中拖拽指定，或者在 `Awake()` / `Start()` 中通过 `GetComponent` 进行缓存。
* 严禁在 `Update()` 内部调用 `GetComponent` 或进行高开销的垃圾回收操作（如频繁的字符串拼接）。

### 2. 逻辑与表现分离
* 核心逻辑组件（如 `GlucoseSystem`、`BattleManager`）**不应**直接持有 UI 控件的引用。
* 核心逻辑组件在状态变更时，须通过 C# 事件（`System.Action` 或 `delegate`）向外播报；UI 控件通过监听对应事件来更新显示。

### 3. 内存与性能
* 重复生成的 UI 节点（如手牌卡牌、Buff 图标）须建立对象池或在删除时显式销毁，防止内存泄漏。
* UI 文本显示数值时，若无必要，避免每帧执行 `ToString()`。仅在数值实际发生变化时，再行触发 UI 刷新。

---

## 五、 战斗数值计算与状态封装规范

为了支持复杂状态组合叠加时的正确运算与可维护性，战斗数值计算必须严格遵守以下工程规范：

### 1. 魔法数字与颜色代码集中化
* 严禁在逻辑代码（如卡牌描述渲染、伤害计算）中硬编码富文本颜色（如 `"<color=#FF6B6B>"`）。
* 所有的富文本颜色（增益、减益、警示）、血糖区间判定阈值、Buff/Debuff 计算比率，必须统一提取在 `CGM.Core.BattleConstants` 静态常量类中集中管理。

### 2. 状态影响因子的方法级封装
* 不允许在计算公式中直接解算 inline 属性（例如直接在伤害计算方法里写死 `* 0.75f` 判定是否乏力）。
* 必须为每个状态的数值修正提供独立且高可读性的封装获取方法（如 `GetLethargyDamageMultiplier(EntityStats source)`），提高代码重用性与逻辑严密性。

### 3. 结算优先级与安全边界
* 所有的百分比倍率加成（如脆弱 +50%、健康血糖 +25%）必须先以 `float` 乘数进行合并演算，在最终结算时再进行向上取整（`Mathf.CeilToInt`），避免多次取整引入的累积误差。
* 任何战斗数值在经过所有增减益计算后，最终必须使用零值夹逼保底判定（`Mathf.Max(0, ...)`），确保最终结算值绝不为负数。

### 4. 规范代码架构骨架示例

为了确保各逻辑模块一致遵循此规范，以下列出核心计算逻辑与常数定义类的标准参考实现骨架。

#### (1) 常量集中管理类（BattleConstants.cs）
```csharp
namespace CGM.Core
{
    /// <summary>
    /// 全局战斗与数值计算常量类。
    /// 集中管理数值缩减率、血糖区间阈值及 UI 高亮颜色代码，严禁硬编码。
    /// </summary>
    public static class BattleConstants
    {
        // 状态影响因子
        public const float FragilityDamageIncrease = 0.50f;   // 脆弱状态增加受击伤害比率 (50%)
        public const float LethargyDamageReduction = 0.25f;    // 乏力状态降低输出伤害比率 (25%)
        public const float StiffnessBlockReduction = 0.25f;    // 僵硬状态降低获得格挡比率 (25%)

        // 血糖控制阈值
        public const float GlucoseMin = 1.0f;
        public const float GlucoseMax = 15.0f;
        public const float HealthyGlucoseMin = 4.4f;
        public const float HealthyGlucoseMax = 7.0f;
        public const float HyperGlucoseThreshold = 7.1f;

        // 血糖修正乘数
        public const float HealthyModifierMultiplier = 1.25f;       // 健康区间卡牌效果加成 (1.25)
        public const float HyperModifierMultiplier = 0.75f;         // 高血糖区间卡牌效果削减 (0.75)
        public const float HyperGlucoseFluctuationMultiplier = 2.0f; // 高血糖区间血糖波动倍数 (2.0)

        // UI 文本富文本高亮颜色代码
        public const string ColorGreen = "#4EC9B0";   // 属性增益/提升高亮色
        public const string ColorRed = "#FF6B6B";     // 属性减益/警告高亮色
        public const string ColorDefault = "#FFFFFF"; // 默认白/基础色
    }
}
```

#### (2) 数值结算计算器（BattleCalculator.cs）
```csharp
using UnityEngine;
using CGM.Data;

namespace CGM.Core
{
    /// <summary>
    /// 核心战斗数值结算与血糖修正计算器。
    /// 统一承载卡牌与敌方动作意图计算，防止多头维护。
    /// </summary>
    public static class BattleCalculator
    {
        // 独立状态比率封装
        public static float GetLethargyDamageMultiplier(EntityStats source)
        {
            if (source == null) return 1.0f;
            return source.GetBuffCount(BuffId.Lethargy) > 0 
                ? (1.0f - BattleConstants.LethargyDamageReduction) 
                : 1.0f;
        }

        // 通用受击计算（复用逻辑）
        public static int CalculateDamage(int baseDamageValue, EntityStats source, EntityStats target)
        {
            if (baseDamageValue <= 0) return 0;

            // 1. 基础伤害与攻击者活力叠加
            int baseDamage = baseDamageValue + source.GetBuffCount(BuffId.Vitality);
            baseDamage = Mathf.Max(0, baseDamage);

            // 2. 乘法叠加百分比状态 (乏力、脆弱、自身血糖区间)
            float multiplier = GetLethargyDamageMultiplier(source) * GetFragilityDamageMultiplier(target);
            multiplier *= GetGlucoseMultiplier(source);

            // 3. 向上取整与最低零值保底
            int finalDamage = Mathf.CeilToInt(baseDamage * multiplier);
            return Mathf.Max(0, finalDamage);
        }
    }
}
```
