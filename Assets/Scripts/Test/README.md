# 卡牌布局与渲染测试 (Card Layout & Rendering Test)

为了方便测试与微调不同卡牌在界面中的渲染效果、文字排版、字体粗细以及卡面着色的呈现，这里提供了一个自动化测试脚本。

## 使用指南

### 1. 在场景中进行配置
1. 打开您的测试场景（或 SampleScene）。
2. 在场景中创建一个 **Canvas**（如果还没有的话）。
3. 在 Canvas 下创建一个空的 GameObject，并重命名为 `CardContainer`（卡牌容器）。
4. 给 `CardContainer` 添加以下组件：
   * **GridLayoutGroup** (网格布局组)
     * 推荐配置：
       * Cell Size: `X: 240, Y: 340` (对应卡牌预制体宽 240，高 340)
       * Spacing: `X: 20, Y: 20` (卡牌间隔)
       * Constraint: `Fixed Column Count` (固定列数，如 5 列)
   * **ContentSizeFitter** (内容尺寸自适应)
     * Horizontal Fit: `Unconstrained`
     * Vertical Fit: `Preferred Size`
5. 将 `CardLayoutTest` 脚本挂载到 `CardContainer` 上。

### 3. 开始测试
* **运行模式下自启动**: 直接运行 Unity，脚本会在 `Start()` 时自动读取 `cards.json` 数据并实例化所有卡牌。
* **双击或调用接口**: 您可以通过该脚本批量渲染生成全部 20 张卡牌，方便您人工视觉审计所有卡牌的外框、名字、耗能位置以及描述文本是否完美契合卡牌位置。
