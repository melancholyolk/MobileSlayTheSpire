using System;
using System.Linq;
using System.Threading.Tasks;
using Spire;
using SpireLocalization;
using UnityEngine;
using UnityEngine.UI;
namespace SpirePresentation
{
    public sealed partial class GameApp
    {
        static string BattleText(string zh,string en) => ExcelLocalization.Language.StartsWith("zh",StringComparison.OrdinalIgnoreCase)?zh:en;
        static void Caption(Button button,string value) => button.GetComponentInChildren<Text>(true).text=value;
        async Task LoadBattlePortrait(Image target,int version)
        {
            try
            {
                var card=Characters.First(c=>c.Class==State.Heroes[Player].Class);
                var sprite=await CharacterScope(Player).LoadAsync<Sprite>(card.PortraitId);
                if(version!=renderVersion || target==null) return;
                target.sprite=sprite;
            }
            catch(OperationCanceledException) { }
            catch(ObjectDisposedException) { }
            catch(Exception error) { Debug.LogException(error); }
        }
        void RenderCombat(Run run,bool ready)
        {
            var v=gameplayView; var hero=run.Heroes[Player];
            v.relics.text=BattleText("遗物 · 暂无","RELICS · None");
            v.potions.text=BattleText("药水 · 暂无","POTIONS · None");
            v.boss.text=BattleText("本章首领 · 灰烬守卫","CHAPTER BOSS · Ash Warden");
            v.mapInfo.text=BattleText($"第 {run.Map[run.Current].Floor+1}/{Run.Floors} 层   金币 {hero.Gold}   牌组 {hero.Deck.Count}",$"Floor {run.Map[run.Current].Floor+1}/{Run.Floors}   Gold {hero.Gold}   Deck {hero.Deck.Count}");
            Caption(v.language,ExcelLocalization.Language); Caption(v.menu,L(LocIds.UiMenu));
            _=LoadBattlePortrait(v.portrait,renderVersion);
            v.characterName.text=ExcelLocalization.Text(Characters.First(c=>c.Class==hero.Class).CharacterNameId);
            v.playerHealth.text=$"{hero.Hp} / {hero.MaxHp}";
            v.playerHealthFill.fillAmount=(float)hero.Hp/Math.Max(1,hero.MaxHp);
            v.energy.text=BattleText($"能量     {hero.Energy}",$"Energy     {hero.Energy}");
            v.handCount.text=BattleText($"手牌     {hero.Hand.Count}",$"Hand       {hero.Hand.Count}");
            v.playerStatus.text=BattleText($"格挡 {hero.Block}   力量 {hero.Strength}",$"Block {hero.Block}   STR {hero.Strength}");
            v.turn.text=BattleText($"回合 {run.Turn}",$"TURN {run.Turn}");
            var enemy=run.Enemies[0];
            v.enemyIntent.text=BattleText($"攻击全体 {enemy.Damage+(run.Turn%3==0?3:0)}",$"Attack all {enemy.Damage+(run.Turn%3==0?3:0)}");
            v.enemyName.text=enemy.Name;
            v.enemyHealth.text=$"{enemy.Hp} / {enemy.MaxHp}";
            v.enemyHealthFill.fillAmount=(float)enemy.Hp/Math.Max(1,enemy.MaxHp);
            v.enemyStatus.text=BattleText($"格挡 {enemy.Block}   中毒 {enemy.Poison}",$"Block {enemy.Block}   Poison {enemy.Poison}");
            v.drawCount.text=BattleText($"抽牌堆\n{hero.Draw.Count}",$"DRAW\n{hero.Draw.Count}");
            v.discardCount.text=BattleText($"弃牌堆\n{hero.Discard.Count}",$"DISCARD\n{hero.Discard.Count}");
            v.exhaustCount.text=BattleText($"消耗牌堆\n{hero.Exhausted.Count}",$"EXHAUST\n{hero.Exhausted.Count}");
            v.handTitle.text=BattleText($"手牌 {hero.Hand.Count}",$"HAND {hero.Hand.Count}");
            Caption(v.endTurn,hero.Ended?L(LocIds.UiWait):L(LocIds.UiEnd));
            v.endTurn.interactable=ready && hero.Alive && !hero.Ended;
            v.status.text=network?.Status??status;
            v.reconnect.gameObject.SetActive(!ready && !network.IsHost);
            Caption(v.reconnect,L(LocIds.UiReconnect));
            CardsReady=RenderCards(hero.Hand.ToArray(),false,ready && hero.Alive && !hero.Ended,renderVersion);
        }
    }
}
