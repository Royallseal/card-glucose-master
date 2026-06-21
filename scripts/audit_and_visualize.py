import os
import csv
import sys
import math

# 尝试导入 matplotlib 绘图库，若未安装则输出引导提示
try:
    import matplotlib.pyplot as plt
    import matplotlib.patches as mpatches
    from matplotlib.lines import Line2D
except ImportError:
    print("\n[错误] 缺少绘图依赖库: matplotlib")
    print("请先在命令行运行以下命令安装它:")
    print("    pip install matplotlib")
    print("\n然后重新运行本脚本。")
    sys.exit(1)

# 配置字体支持，以确保在 Windows 环境下中文字体能正常显示
plt.rcParams['font.sans-serif'] = ['Microsoft YaHei', 'SimHei', 'Arial']
plt.rcParams['axes.unicode_minus'] = False

# 根据脚本文件的位置动态解析项目相对路径
script_dir = os.path.dirname(os.path.abspath(__file__))
root_dir = os.path.dirname(script_dir)
csv_path = os.path.join(root_dir, 'data', 'initial_cards_data.csv')
output_dir = os.path.join(root_dir, 'docs', 'assets')

# 确保图表输出目录已创建
os.makedirs(output_dir, exist_ok=True)

# 颜色配置板定义（选用美观的扁平化 UI 配色）
rarity_colors = {
    'Starter': '#7F8C8D',   # 灰色 (初始)
    'Common': '#3498DB',    # 柔和蓝 (普通)
    'Uncommon': '#E67E22',  # 橙色 (良好)
    'Rare': '#9B59B6'       # 紫色 (优秀/稀有)
}

type_colors = {
    'Starter': '#95A5A6',
    'Diet': '#E74C3C',
    'Exercise': '#2ECC71',
    'Medicine': '#34495E'
}

def calculate_card_vp(row):
    card_id = row['id']
    name = row['name']
    ctype = row['type']
    rarity = row['rarity']
    cost = int(row['energyCost'])
    bg_change = float(row['glucoseChange'])
    dmg_w = float(row['damageWeight'])
    blk_w = float(row['blockWeight'])
    effect_vp = float(row['effectVPCost'])
    final_dmg = int(row['finalDamage'])
    final_blk = int(row['finalBlock'])
    
    # 1. 计算理论价值点数预算 (Target VP)
    base_vp = cost * 6
    bg_compensation = abs(bg_change) * 10
    rarity_bonus = 2 if rarity == "Uncommon" else (4 if rarity == "Rare" else 0)
    
    target_vp = base_vp + bg_compensation + rarity_bonus - effect_vp
    
    # 2. 计算卡牌实际分配的价值点数 (Allocated VP，包含多段攻击逻辑)
    hits = 1
    if card_id == 'diet_apple':
        hits = 2
    elif card_id == 'exe_extreme':
        hits = 3
    
    allocated_vp = (final_dmg * hits * 1.0) + (final_blk * 1.2)
    
    # 3. 计算实际分配与理论预算的偏差值 (Deviation)
    deviation = allocated_vp - target_vp
    
    return {
        'id': card_id,
        'name': name,
        'type': ctype,
        'rarity': rarity,
        'cost': cost,
        'bg_change': bg_change,
        'dmg_weight': dmg_w,
        'blk_weight': blk_w,
        'final_dmg': final_dmg,
        'final_blk': final_blk,
        'hits': hits,
        'total_dmg': final_dmg * hits,
        'target_vp': target_vp,
        'allocated_vp': allocated_vp,
        'deviation': deviation
    }

def get_combat_attribute(c):
    if c['total_dmg'] > 0 and c['final_blk'] == 0:
        return '伤害卡'
    elif c['final_blk'] > 0 and c['total_dmg'] == 0:
        return '格挡卡'
    else:
        return '功能卡'

def main():
    if not os.path.exists(csv_path):
        print(f"[错误] 找不到卡牌数据库文件: {csv_path}")
        return

    cards = []
    with open(csv_path, mode='r', encoding='utf-8') as f:
        reader = csv.DictReader(f)
        for row in reader:
            if not row.get('id'):
                continue
            cards.append(calculate_card_vp(row))
            
    print("\n" + "="*70)
    print("               《卡牌控糖师》数值平衡配平审计账单")
    print("="*70)
    print(f"{'卡牌名称 (ID)':<22} | {'能耗':<4} | {'血糖':<5} | {'理论VP':<6} | {'实际VP':<6} | {'偏差值':<5}")
    print("-"*70)
    
    for c in cards:
        dev_str = f"{c['deviation']:+.1f}" if c['deviation'] != 0 else "0.0"
        name_id = f"{c['name']} ({c['id']})"
        print(f"{name_id:<22} | {c['cost']:<4} | {c['bg_change']:<5} | {c['target_vp']:<6.1f} | {c['allocated_vp']:<6.1f} | {dev_str:<5}")
    print("="*70)
    
    # 分品类计算各战斗属性（伤害、格挡、功能）的平均能耗
    attrs = ['伤害卡', '格挡卡', '功能卡']
    avg_costs_attr = {}
    for a in attrs:
        costs = [c['cost'] for c in cards if get_combat_attribute(c) == a]
        avg_costs_attr[a] = sum(costs)/len(costs) if costs else 0
        
    print("\n" + "="*70)
    print("               《卡牌控糖师》战斗属性平均能耗统计")
    print("="*70)
    for a in attrs:
        card_count = sum(1 for c in cards if get_combat_attribute(c) == a)
        print(f"  {a:<6} (共 {card_count} 张) | 平均能耗: {avg_costs_attr[a]:.2f} 费")
    print("="*70)
    
    # ==========================================
    # 图表 1：能量消耗 vs 血糖变化 散点图
    # ==========================================
    plt.figure(figsize=(7.5, 6))
    plt.title('能量消耗 vs 血糖变化 散点图', fontsize=13, fontweight='bold', pad=12)
    plt.xlabel('血糖变化 (mmol/L)', fontsize=10)
    plt.ylabel('能量消耗 (点)', fontsize=10)
    plt.axvline(0, color='#BDC3C7', linestyle='--', linewidth=1)
    
    # 对相同的坐标进行分组，以便应用径向散开算法
    coord_groups = {}
    for c in cards:
        key = (c['bg_change'], c['cost'])
        coord_groups.setdefault(key, []).append(c)
        
    for (x, y), group in coord_groups.items():
        N = len(group)
        for i, c in enumerate(group):
            if N == 1:
                x_dot, y_dot = x, y
            else:
                # 将完全重叠的卡牌散点以小半径圆形分布在中心周围，确保散点数量可视
                angle = (2 * math.pi * i) / N
                x_dot = x + 0.05 * math.cos(angle)
                y_dot = y + 0.08 * math.sin(angle)
                
            color = rarity_colors.get(c['rarity'], '#34495E')
            size = max(80, int(c['target_vp'] * 15))
            plt.scatter(x_dot, y_dot, color=color, s=size, alpha=0.75, edgecolors='black', linewidth=0.5)
                     
    legend_handles = [Line2D([0], [0], marker='o', color='w', markerfacecolor=v, markersize=10, label=k) for k, v in rarity_colors.items()]
    plt.legend(handles=legend_handles, title='卡牌稀有度', loc='upper right', frameon=True)
    plt.ylim(-0.5, 2.5)
    plt.yticks([0, 1, 2])
    plt.grid(True, linestyle=':', alpha=0.5)
    plt.tight_layout()
    plt.savefig(os.path.join(output_dir, 'card_scatter_energy_vs_glucose.png'), dpi=150)
    plt.close()

    # ==========================================
    # 图表 2：不同战斗属性的平均能耗柱状图
    # ==========================================
    plt.figure(figsize=(6, 5))
    plt.title('战斗属性平均能耗', fontsize=13, fontweight='bold', pad=12)
    
    attr_colors = {
        '伤害卡': '#E74C3C', # 红色
        '格挡卡': '#2ECC71', # 绿色
        '功能卡': '#3498DB'  # 蓝色
    }
    
    bars = plt.bar(attrs, [avg_costs_attr[a] for a in attrs], color=[attr_colors[a] for a in attrs], edgecolor='black', width=0.45, alpha=0.8)
    plt.ylabel('平均费用 (点)', fontsize=10)
    plt.ylim(0, 2.0)
    plt.grid(True, axis='y', linestyle=':', alpha=0.5)
    for bar in bars:
        yval = bar.get_height()
        plt.text(bar.get_x() + bar.get_width()/2.0, yval + 0.05, f"{yval:.2f}", ha='center', va='bottom', fontsize=9.5, weight='bold')
    plt.tight_layout()
    plt.savefig(os.path.join(output_dir, 'card_bar_avg_cost.png'), dpi=150)
    plt.close()

    # ==========================================
    # 图表 3：卡牌价值偏差条形图
    # ==========================================
    plt.figure(figsize=(9, 6.5))
    plt.title('卡牌价值 (VP) 偏差审计', fontsize=13, fontweight='bold', pad=12)
    card_names = [c['name'] for c in cards]
    deviations = [c['deviation'] for c in cards]
    dev_colors = []
    for d in deviations:
        if d > 0.3:
            dev_colors.append('#E74C3C') # 红色 (超模)
        elif d < -0.3:
            dev_colors.append('#3498DB') # 蓝色 (保守/削弱)
        else:
            dev_colors.append('#2ECC71') # 绿色 (完美配平)
            
    y_pos = range(len(card_names))
    plt.barh(y_pos, deviations, color=dev_colors, edgecolor='black', height=0.65, alpha=0.8)
    plt.yticks(y_pos, card_names, fontsize=9)
    plt.xlabel('价值偏差 (VP)', fontsize=10)
    plt.axvline(0, color='black', linewidth=1.2)
    plt.axvline(-1.0, color='#7F8C8D', linestyle=':', linewidth=1)
    plt.grid(True, axis='x', linestyle=':', alpha=0.5)
    plt.tight_layout()
    plt.savefig(os.path.join(output_dir, 'card_bar_vp_deviation.png'), dpi=150)
    plt.close()

    # ==========================================
    # 图表 4：卡牌伤害输出分布柱状图（折算总伤害）
    # ==========================================
    plt.figure(figsize=(8, 5))
    plt.title('卡牌伤害输出分布（含多段）', fontsize=13, fontweight='bold', pad=12)
    damage_cards = [c for c in cards if c['total_dmg'] > 0]
    damage_cards.sort(key=lambda x: x['total_dmg'], reverse=True)
    
    dmg_names = [f"{c['name']}\n({c['final_dmg']}x{c['hits']})" if c['hits'] > 1 else c['name'] for c in damage_cards]
    dmg_values = [c['total_dmg'] for c in damage_cards]
    
    bars = plt.bar(dmg_names, dmg_values, color='#E74C3C', edgecolor='black', width=0.45, alpha=0.8)
    plt.ylabel('总伤害 (点)', fontsize=10)
    plt.grid(True, axis='y', linestyle=':', alpha=0.5)
    for bar in bars:
        yval = bar.get_height()
        plt.text(bar.get_x() + bar.get_width()/2.0, yval + 0.5, f"{int(yval)}", ha='center', va='bottom', fontsize=9.5, weight='bold')
    plt.xticks(fontsize=9)
    plt.tight_layout()
    plt.savefig(os.path.join(output_dir, 'card_bar_damage.png'), dpi=150)
    plt.close()

    # ==========================================
    # 图表 5：卡牌格挡值分布柱状图
    # ==========================================
    plt.figure(figsize=(8, 5))
    plt.title('卡牌格挡值分布', fontsize=13, fontweight='bold', pad=12)
    block_cards = [c for c in cards if c['final_blk'] > 0]
    block_cards.sort(key=lambda x: x['final_blk'], reverse=True)
    
    blk_names = [c['name'] for c in block_cards]
    blk_values = [c['final_blk'] for c in block_cards]
    
    bars = plt.bar(blk_names, blk_values, color='#2ECC71', edgecolor='black', width=0.45, alpha=0.8)
    plt.ylabel('格挡值 (点)', fontsize=10)
    plt.grid(True, axis='y', linestyle=':', alpha=0.5)
    for bar in bars:
        yval = bar.get_height()
        plt.text(bar.get_x() + bar.get_width()/2.0, yval + 0.2, f"{int(yval)}", ha='center', va='bottom', fontsize=9.5, weight='bold')
    plt.xticks(fontsize=9)
    plt.tight_layout()
    plt.savefig(os.path.join(output_dir, 'card_bar_block.png'), dpi=150)
    plt.close()

    # ==========================================
    # 图表 6：卡牌战斗属性纯化占比饼图
    # ==========================================
    plt.figure(figsize=(6.5, 6.5))
    plt.title('卡牌战斗属性纯化占比', fontsize=13, fontweight='bold', pad=12)
    
    pure_attack = sum(1 for c in cards if get_combat_attribute(c) == '伤害卡')
    pure_defense = sum(1 for c in cards if get_combat_attribute(c) == '格挡卡')
    pure_utility = sum(1 for c in cards if get_combat_attribute(c) == '功能卡')
    
    labels = ['伤害卡 (Attack)', '格挡卡 (Defense)', '功能卡 (Utility)']
    sizes = [pure_attack, pure_defense, pure_utility]
    colors = ['#E74C3C', '#2ECC71', '#3498DB']
    
    plt.pie(sizes, labels=labels, colors=colors, autopct='%1.1f%%', startangle=140, 
            wedgeprops={'edgecolor': 'black', 'linewidth': 0.8, 'alpha': 0.8},
            textprops={'fontsize': 10, 'weight': 'bold'})
    plt.tight_layout()
    plt.savefig(os.path.join(output_dir, 'card_pie_attribute_composition.png'), dpi=150)
    plt.close()

    # ==========================================
    # 图表 7：各卡牌血糖变化波动影响柱状图
    # ==========================================
    plt.figure(figsize=(10.5, 5.5))
    plt.title('各卡牌血糖升降波动影响', fontsize=13, fontweight='bold', pad=12)
    
    # 按血糖变化降序排列卡牌数据
    sorted_cards = sorted(cards, key=lambda x: x['bg_change'], reverse=True)
    card_names_bg = [c['name'] for c in sorted_cards]
    bg_changes = [c['bg_change'] for c in sorted_cards]
    
    bar_colors = []
    for c in sorted_cards:
        if c['type'] == 'Starter':
            bar_colors.append('#95A5A6')
        elif c['type'] == 'Diet':
            bar_colors.append('#E74C3C')
        elif c['type'] == 'Exercise':
            bar_colors.append('#2ECC71')
        else:
            bar_colors.append('#34495E')
            
    bars = plt.bar(card_names_bg, bg_changes, color=bar_colors, edgecolor='black', width=0.6, alpha=0.8)
    plt.ylabel('血糖变化值 (mmol/L)', fontsize=10)
    plt.axhline(0, color='black', linewidth=1)
    plt.grid(True, axis='y', linestyle=':', alpha=0.5)
    
    for bar in bars:
        yval = bar.get_height()
        va_dir = 'bottom' if yval >= 0 else 'top'
        offset = 0.05 if yval >= 0 else -0.05
        plt.text(bar.get_x() + bar.get_width()/2.0, yval + offset, f"{yval:+.1f}" if yval != 0 else "0.0", 
                 ha='center', va=va_dir, fontsize=8.5, weight='bold')
                 
    plt.xticks(rotation=45, ha='right', fontsize=9)
    legend_patches = [mpatches.Patch(color=v, label=k) for k, v in {'初始': '#95A5A6', '膳食': '#E74C3C', '运动': '#2ECC71', '药物': '#34495E'}.items()]
    plt.legend(handles=legend_patches, loc='upper right')
    
    plt.tight_layout()
    plt.savefig(os.path.join(output_dir, 'card_bar_glucose_impact.png'), dpi=150)
    plt.close()

    print(f"\n[完成] 7 张配平审计分析图表已成功生成并保存至: {output_dir}")

if __name__ == '__main__':
    main()
