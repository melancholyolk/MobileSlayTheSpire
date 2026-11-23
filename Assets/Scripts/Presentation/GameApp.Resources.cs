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
    // 角色资源和音频的作用域管理；退出会话时释放。
    public sealed partial class GameApp
    {
        void ReleaseCharacters()
        {
            renderVersion++;
            handMotion?.Clear();
            handRun = null;
            ClearCards();
            foreach (var scope in characterResources.Values)
                scope.Dispose();
            characterResources.Clear();
            characterAudio.Clear();
        }

        void ClearCards()
        {
            foreach (var item in visibleCards)
            {
                if (item.go != null)
                {
                    item.go.GetComponent<CardUIView>().Clear();
                    gameplayView.Unregister(item.go.GetComponent<CardUIView>());
                    item.scope.Return(item.go);
                }
            }

            visibleCards.Clear();
        }

        ResourceScope CharacterScope(int seat)
        {
            if (!characterResources.TryGetValue(seat, out var scope))
            {
                characterResources[seat] = scope = Resources.CreateScope((int)State.Heroes[seat].Class);
                _ = LoadCharacterAudio(seat, scope);
            }

            return scope;
        }

        async Task LoadCharacterAudio(int seat, ResourceScope scope)
        {
            if (audioManager == null || !audioManager.Available)
                return;
            try
            {
                var audio = await audioManager.CreateScopeAsync(scope);
                if (!scope.IsDisposed)
                    characterAudio[seat] = audio;
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception error)
            {
                Debug.LogWarning(error.Message);
            }
        }
    }
}
