using System;
using System.Threading.Tasks;
using SpireUI;
using SpireResources;
using SpireAudio;
using SpireAnimation;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using Spire;
using SpireLocalization;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SpirePresentation
{
    // 按当前阶段切换视图，等待战斗表现结束后再显示结算。
    public sealed partial class GameApp
    {
        public void Render()
        {
            if (destroying || startView == null || gameplayView == null)
                return;
            if (State != null && State.Phase == Phase.Combat && handRun != State)
            {
                handMotion.Clear();
                handRun = State;
                handMotion.Configure(this, gameplayView, CharacterScope(Player));
            }

            BattleVisuals.Observe(State, Player);
            if (State != null && State.Phase != Phase.Combat && handRun == State && BattleVisuals.PendingCount > 0)
            {
                CardsReady = FinishBattlePresentation(State);
                return;
            }

            if (State == null || State.Phase != Phase.Combat)
            {
                handMotion.Clear();
                handRun = null;
            }

            gameplayView.backdrop.Observe(State);
            gameplayView.ShowCombat(State != null && State.Phase == Phase.Combat);
            lastRevision = State?.Revision ?? -1;
            lastNetworkStatus = network?.Status;
            if (State == null)
            {
                ui.Hide(MapUIView.Path);
                ui.Hide(GameplayView.Path);
                ui.Show(GameStartView.Path);
                startView.Refresh();
                return;
            }

            ui.Hide(GameStartView.Path);
            if (State.Phase == Phase.Map)
            {
                renderVersion++;
                ClearCards();
                ui.Hide(GameplayView.Path);
                ui.Show(MapUIView.Path);
                mapView.Refresh(State);
                return;
            }

            ui.Hide(MapUIView.Path);
            ui.Show(GameplayView.Path);
            renderVersion++;
            ClearCards();
            foreach (Transform child in page)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            if (State.Phase == Phase.Combat)
            {
                RenderCombat(State, network == null || network.Connected);
                return;
            }

            var background = Box("Background", 0, 0, 1280, 720, new Color(ink.r, ink.g, ink.b, .60f));
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.offsetMin = background.offsetMax = Vector2.zero;
            Label(L(LocIds.UiTitle), 32, 20, 780, 55, 36);
            Label(L(LocIds.UiSubtitle), 34, 70, 800, 30, 17);
            var run = State;
            lastRevision = run?.Revision ?? -1;
            lastNetworkStatus = network?.Status;
            Label(network?.Status ?? status, 32, 672, 1040, 40, 18);
            Button("Menu", L(LocIds.UiMenu), 1140, 25, 108, 42, ReturnToMenu, intent: ButtonIntent.Destructive);
            Button("Language", ExcelLocalization.Language, 990, 25, 135, 42, CycleLanguage);
            for (int p = 0; p < run.Heroes.Count; p++)
            {
                var h = run.Heroes[p];
                Label($"{(p == Player ? "> " : "")}{ExcelLocalization.Text(Characters.First(c => c.Class == h.Class).CharacterNameId)}  HP {h.Hp}/{h.MaxHp}  Gold {h.Gold}  Deck {h.Deck.Count}", 32 + p * 610, 115, 610, 35, 22);
            }

            Label($"{run.Phase}   /   Floor {(run.Current < 0 ? 0 : run.Map[run.Current].Floor + 1)} of {Run.Floors}", 32, 160, 900, 35, 24);
            bool ready = network == null || network.Connected;
            if (!ready && !network.IsHost)
                Button("Reconnect", L(LocIds.UiReconnect), 1080, 665, 160, 40, () => Join(true), intent: ButtonIntent.Primary);
            var hero = run.Heroes[Player];
            switch (run.Phase)
            {
                case Phase.Reward:
                    Label(L(LocIds.UiReward), 60, 240, 1100, 50, 27);
                    CardsReady = RenderCards(run.Rewards[Player].ToArray(), true, ready && !run.Resolved.Contains(Player) && hero.Alive, renderVersion);
                    Button("SkipReward", L(LocIds.UiSkip), 500, 540, 280, 60, () => Send("reward", -1), ready && !run.Resolved.Contains(Player) && hero.Alive);
                    break;
                case Phase.Camp:
                    Room("Rest: heal 33%", "Train: +5 max HP");
                    break;
                case Phase.Shop:
                    Room("Heal 25 HP: 30 gold", "Leave shop");
                    break;
                case Phase.Event:
                    Room("Pay 6 HP: gain 35 gold", "Walk away");
                    break;
                case Phase.Won:
                case Phase.Lost:
                    Label(run.Phase == Phase.Won ? L(LocIds.UiWin) : L(LocIds.UiLose), 220, 300, 900, 90, 48);
                    Label(L(LocIds.UiRestart), 300, 420, 800, 60, 25);
                    break;
            }

            void Room(string a, string b)
            {
                bool enabled = ready && hero.Alive && !run.Resolved.Contains(Player);
                Button("Room_0", a, 160, 320, 440, 150, () => Send("room", 0), enabled);
                Button("Room_1", b, 660, 320, 440, 150, () => Send("room", 1), enabled);
            }
        }

        async Task FinishBattlePresentation(Run run)
        {
            while (this != null && State == run && BattleVisuals.PendingCount > 0)
                await Task.Yield();
            if (this != null && State == run)
                Render();
        }
    }
}
