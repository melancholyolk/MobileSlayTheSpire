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
    [Serializable]
    public sealed class AudioConfig
    {
        public AudioEventEntry[] Events = Array.Empty<AudioEventEntry>();
        public AudioBankEntry[] Banks = Array.Empty<AudioBankEntry>();
    }
}
