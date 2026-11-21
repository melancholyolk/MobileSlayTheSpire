using System;
using NUnit.Framework;
using SpireAudio;
using UnityEngine;
public class AudioConfigTests
{
    [Test] public void ConfigRejectsInvalidIdsGuidsAndDuplicateBanks()
    {
        var root=new GameObject("AudioTest"); var audio=root.AddComponent<FmodAudioManager>();
        try
        {
            Assert.Throws<ArgumentException>(()=>audio.Configure(new AudioConfig {Events=new[]{new AudioEventEntry {Id=1,Guid="bad"}}}));
            Assert.Throws<ArgumentException>(()=>audio.Configure(new AudioConfig {Banks=new[]{new AudioBankEntry {ResourceId=20},new AudioBankEntry {ResourceId=20}}}));
            Assert.Throws<ArgumentException>(()=>audio.Configure(new AudioConfig {Events=new[]{new AudioEventEntry {Id=0,Guid=Guid.NewGuid().ToString()}}}));
            Assert.DoesNotThrow(()=>audio.Configure(new AudioConfig {Events=new[]{new AudioEventEntry {Id=1,Guid="00112233-4455-6677-8899-aabbccddeeff"}}}));
            Assert.IsFalse(audio.Available);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
