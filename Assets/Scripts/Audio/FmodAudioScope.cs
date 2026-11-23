using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SpireResources;
using UnityEngine;
using F = SpireFmod;
using Studio = SpireFmod.Studio;

namespace SpireAudio
{
    public sealed class FmodAudioScope : IDisposable
    {
        readonly FmodAudioManager manager;
        readonly ResourceScope owner;
        readonly List<Studio.Bank> banks = new List<Studio.Bank>();
        readonly List<Studio.EventInstance> voices = new List<Studio.EventInstance>();
        public bool Disposed { get; private set; }

        internal FmodAudioScope(FmodAudioManager manager, ResourceScope owner)
        {
            this.manager = manager;
            this.owner = owner;
        }

        internal void AddBank(Studio.Bank bank)
        {
            banks.Add(bank);
        }

        public bool TryPlay(int eventId)
        {
            if (Disposed || owner.IsDisposed || !manager.Available)
                return false;
            for (int i = voices.Count - 1; i >= 0; i--)
            {
                voices[i].getPlaybackState(out var state);
                if (state == Studio.PLAYBACK_STATE.STOPPED)
                {
                    voices[i].release();
                    voices.RemoveAt(i);
                }
            }

            if (voices.Count >= FmodAudioManager.MaxVoices)
                return false;
            Studio.EventInstance voice = default;
            try
            {
                voice = manager.CreateEvent(eventId);
                FmodAudioManager.Check(voice.start());
                voices.Add(voice);
                return true;
            }
            catch (InvalidOperationException)
            {
                if (voice.hasHandle())
                    voice.release();
                return false;
            }
        }

        public void Dispose()
        {
            if (Disposed)
                return;
            Disposed = true;
            foreach (var voice in voices)
            {
                voice.stop(Studio.STOP_MODE.IMMEDIATE);
                voice.release();
            }

            voices.Clear();
            foreach (var bank in banks)
                bank.unload();
            banks.Clear();
            manager.Forget(this);
        }
    }
}
