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
    // 房间和奖励页的临时 UI 构建；战斗 HUD 使用 GameplayView 预制体。
    public sealed partial class GameApp
    {
        RectTransform Box(string name, float x, float y, float w, float h, Color color, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent ?? page, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            go.GetComponent<Image>().color = color;
            var element = go.AddComponent<UiElement>();
            gameplayView.Register(element);
            return rt;
        }

        Text Label(string value, float x, float y, float w, float h, int size = 20, Transform parent = null)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent ?? page, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            gameplayView.Register(go.AddComponent<UiElement>());
            var t = rt.gameObject.AddComponent<Text>();
            t.text = value;
            t.font = font;
            t.fontSize = size;
            t.color = paper;
            t.raycastTarget = false;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }

        void Button(string name, string caption, float x, float y, float w, float h, Action action, bool enabled = true, ButtonIntent intent = ButtonIntent.Secondary)
        {
            var rt = Box(name, x, y, w, h, enabled ? accent : new Color(.22f, .23f, .25f));
            var button = rt.gameObject.AddComponent<Button>();
            button.interactable = enabled;
            uiTheme.Apply(button, intent);
            button.onClick.AddListener(() => action());
            var t = Label(caption, 8, 6, w - 16, h - 12, 18, rt);
            t.alignment = TextAnchor.MiddleCenter;
        }

        async Task RenderCards(string[] cards, bool reward, bool enabled, int version)
        {
            if (cards.Length == 0)
                return;
            var run = State;
            var scope = CharacterScope(Player);
            float width = Math.Min(250, 1160f / cards.Length);
            try
            {
                for (int i = 0; i < cards.Length; i++)
                {
                    if (version != renderVersion || scope.IsDisposed)
                        return;
                    int index = i;
                    var card = run.Cards[cards[i]];
                    var go = await scope.InstantiateAsync(ResourceIds.CardView, reward ? page : gameplayView.hand);
                    if (version != renderVersion || this == null)
                    {
                        scope.Return(go);
                        return;
                    }

                    if (reward)
                    {
                        var rt = go.GetComponent<RectTransform>();
                        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
                        rt.anchoredPosition = new Vector2(40 + i * (width + 4), -310);
                        rt.sizeDelta = new Vector2(width - 4, 220);
                    }

                    go.name = (reward ? "Reward_" : "Card_") + i;
                    go.SetActive(true);
                    var view = go.GetComponent<CardUIView>();
                    gameplayView.Register(view);
                    visibleCards.Add((go, scope));
                    await view.Bind(card, scope, () => Send(reward ? "reward" : "play", index), enabled && (reward || run.Heroes[Player].Energy >= card.Cost));
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
