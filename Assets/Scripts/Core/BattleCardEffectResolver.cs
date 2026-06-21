using System;
using System.Collections.Generic;
using UnityEngine;
using CGM.Data;

namespace CGM.Core
{
    /// <summary>
    /// 单张卡牌结算后的结果摘要。
    /// </summary>
    public sealed class CardPlayResult
    {
        public CardPlayResult(CardInfo card, EntityStats primaryTarget)
        {
            Card = card;
            PrimaryTarget = primaryTarget;
        }

        public CardInfo Card { get; }
        public EntityStats PrimaryTarget { get; }
        public int DamageDealt { get; private set; }
        public int BlockGained { get; private set; }
        public int SelfDamage { get; private set; }
        public int CardsDrawn { get; private set; }
        public float GlucoseDelta { get; private set; }
        public bool FullyBlocked { get; private set; }
        public int TargetStartHp { get; set; }
        public int TargetStartBlock { get; set; }
        public List<string> Messages { get; } = new List<string>();

        /// <summary>多段攻击的每击结果。</summary>
        public List<HitResult> HitResults { get; } = new List<HitResult>();

        public void MarkFullyBlocked()
        {
            FullyBlocked = true;
        }

        public void AddDamage(int value)
        {
            DamageDealt += Mathf.Max(0, value);
        }

        public void AddHitResult(bool blocked, int rawDamage)
        {
            HitResults.Add(new HitResult { blocked = blocked, rawDamage = rawDamage });
        }

        /// <summary>单次攻击命中的结果。</summary>
        public struct HitResult
        {
            public bool blocked;   // 此次攻击是否被完全格挡
            public int rawDamage;  // 原始伤害值
        }

        public void AddBlock(int value)
        {
            BlockGained += Mathf.Max(0, value);
        }

        public void AddSelfDamage(int value)
        {
            SelfDamage += Mathf.Max(0, value);
        }

        public void AddCardsDrawn(int value)
        {
            CardsDrawn += Mathf.Max(0, value);
        }

        public void SetGlucoseDelta(float value)
        {
            GlucoseDelta = value;
        }
    }

    /// <summary>
    /// 卡牌效果执行器，负责把卡牌数据转换成实际的战斗状态变化。
    /// </summary>
    public static class BattleCardEffectResolver
    {
        /// <summary>
        /// 执行一张卡牌的完整结算。
        /// </summary>
        public static CardPlayResult Resolve(CardInfo card, PlayerStats player, EntityStats primaryTarget, Func<int, int> drawCards = null, Action<int> gainEnergy = null)
        {
            CardPlayResult result = new CardPlayResult(card, primaryTarget);
            if (primaryTarget != null)
            {
                result.TargetStartHp = primaryTarget.CurrentHp;
                result.TargetStartBlock = primaryTarget.Block;
            }

            if (card == null || player == null)
            {
                return result;
            }

            bool hasHitEffect = card.HasEffect("hit");

            if (card.finalDamage > 0)
            {
                if (hasHitEffect)
                {
                    List<CardEffect> hitEffects = card.GetEffects("hit");
                    foreach (var effect in hitEffects)
                    {
                        int hitCount = Mathf.Max(1, effect.GetIntValue1());
                        for (int i = 0; i < hitCount; i++)
                        {
                            if (primaryTarget == null)
                            {
                                result.Messages.Add("缺少攻击目标，已跳过伤害结算。");
                                break;
                            }

                            int damage = BattleCalculator.CalculateDamage(card, player, primaryTarget);
                            int preHp = primaryTarget.CurrentHp;
                            primaryTarget.TakeDamage(damage);
                            result.AddDamage(damage);
                            bool blocked = primaryTarget.CurrentHp == preHp && damage > 0;
                            result.AddHitResult(blocked, damage);
                            if (blocked) result.MarkFullyBlocked();
                        }
                    }
                }
                else if (primaryTarget != null)
                {
                    int damage = BattleCalculator.CalculateDamage(card, player, primaryTarget);
                    int preHp = primaryTarget.CurrentHp;
                    primaryTarget.TakeDamage(damage);
                    result.AddDamage(damage);
                    bool blocked = primaryTarget.CurrentHp == preHp && damage > 0;
                    result.AddHitResult(blocked, damage);
                    if (blocked) result.MarkFullyBlocked();
                }
            }

            if (card.finalBlock > 0)
            {
                int block = BattleCalculator.CalculateBlock(card, player);
                player.GainBlock(block);
                result.AddBlock(block);
            }

            if (Mathf.Abs(card.glucoseChange) > Mathf.Epsilon)
            {
                float glucoseDelta = BattleCalculator.CalculateGlucoseChange(card, player);
                player.ChangeGlucose(glucoseDelta);
                result.SetGlucoseDelta(glucoseDelta);
            }

            if (card.effects != null)
            {
                foreach (var effect in card.effects)
                {
                    if (player.IsDead)
                    {
                        break;
                    }

                    switch (effect.GetEffectType())
                    {
                        case EffectType.Hit:
                            break;

                        case EffectType.Draw:
                            if (drawCards != null)
                            {
                                int drawCount = Mathf.Max(0, effect.GetIntValue1());
                                int actualDrawn = drawCards(drawCount);
                                result.AddCardsDrawn(actualDrawn);
                            }
                            break;

                        case EffectType.SelfDamage:
                            int selfDamage = Mathf.Max(0, effect.GetIntValue1());
                            player.LoseHp(selfDamage);
                            result.AddSelfDamage(selfDamage);
                            break;

                        case EffectType.ApplyBuff:
                            player.ApplyBuff(effect.GetBuffId(), effect.GetIntValue2());
                            break;

                        case EffectType.ApplyDebuff:
                            if (primaryTarget != null)
                            {
                                primaryTarget.ApplyBuff(effect.GetBuffId(), effect.GetIntValue2());
                            }
                            else
                            {
                                result.Messages.Add($"效果 {effect.effectType} 需要一个目标，但当前未提供。");
                            }
                            break;

                        case EffectType.GlucoseCap:
                            float cap = effect.GetFloatValue1();
                            player.SetGlucose(cap);
                            result.Messages.Add($"血糖直接复原至 {cap:F1}。");
                            break;

                        case EffectType.GainEnergy:
                            if (gainEnergy != null)
                            {
                                int amount = Mathf.Max(0, effect.GetIntValue1());
                                gainEnergy(amount);
                            }
                            break;
                    }
                }
            }

            return result;
        }
    }
}