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
    // 单人和联机会话入口；UI 请求统一通过 Send 进入规则或主机验证。
    public sealed partial class GameApp
    {
        void StopNetwork()
        {
            if (network == null)
                return;
            network.Changed -= Render;
            network.Dispose();
            network = null;
        }

        public void Navigate(StartPage page)
        {
            appAudio?.TryPlay(1);
            MenuPage = page;
            Render();
        }

        public void ReturnToMenu()
        {
            StopNetwork();
            solo = null;
            ReleaseCharacters();
            MenuPage = StartPage.Home;
            status = "";
            Render();
        }

        public void StartSolo(HeroClass cls, uint seed = 42)
        {
            StopNetwork();
            ReleaseCharacters();
            solo = new Run(CardTable.Read(table), seed, cls);
            status = "Choose a route.";
            Render();
        }

        public void Send(string action, int argument = 0)
        {
            if (State != null)
                State.UsePhysicalAttacks = true;
            if (action == "play" && characterAudio.TryGetValue(Player, out var audio))
                audio.TryPlay(2);
            if (network != null)
            {
                network.Submit(action, argument);
                status = network.Status;
            }
            else if (solo != null)
            {
                if (!solo.Apply(0, action, argument, out var error))
                    status = error;
                else
                    status = "";
            }

            Render();
        }

        public void Host(int port = 7777)
        {
            try
            {
                StopNetwork();
                network = new CoopSession(table);
                network.Host(port, 42);
                network.Changed += Render;
                solo = null;
                MenuPage = StartPage.Lobby;
                Render();
            }
            catch (Exception e)
            {
                status = e.Message;
                StopNetwork();
                Render();
            }
        }

        public void Join(bool resume, int port = 7777)
        {
            try
            {
                if (resume && network != null)
                    resumeToken = network.ResumeToken;
                StopNetwork();
                network = new CoopSession(table);
                network.Join(address, port, resume ? resumeToken : null);
                network.Changed += Render;
                solo = null;
                MenuPage = StartPage.Lobby;
                Render();
            }
            catch (Exception e)
            {
                status = e.Message;
                StopNetwork();
                Render();
            }
        }
    }
}
