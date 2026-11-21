using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SpireResources;
using UnityEngine;
using F=SpireFmod;
using Studio=SpireFmod.Studio;
namespace SpireAudio
{
    [Serializable] public sealed class AudioEventEntry { public int Id; public string Guid; }
    [Serializable] public sealed class AudioBankEntry { public int ResourceId; public int CharacterId=-1; }
    [Serializable] public sealed class AudioConfig
    {
        public AudioEventEntry[] Events=Array.Empty<AudioEventEntry>();
        public AudioBankEntry[] Banks=Array.Empty<AudioBankEntry>();
    }
    public sealed class FmodAudioManager : MonoBehaviour
    {
        Studio.System system;
        bool initialized;
        AudioConfig config=new AudioConfig();
        readonly Dictionary<int,F.GUID> events=new Dictionary<int,F.GUID>();
        readonly HashSet<FmodAudioScope> scopes=new HashSet<FmodAudioScope>();
        public bool Available=>initialized;
        public string Status { get; private set; }="Not initialized";
        public const int MaxVoices=64;
        public async Task ConfigureAsync(ResourceScope resources)
        {
            var text=await resources.LoadAsync<TextAsset>(20);
            Configure(JsonUtility.FromJson<AudioConfig>(text.text));
        }
        public void Configure(AudioConfig value)
        {
            if(initialized) throw new InvalidOperationException("Configure audio before initialization");
            if(value==null || value.Events==null || value.Banks==null) throw new ArgumentException("Invalid audio config");
            var parsed=new Dictionary<int,F.GUID>();
            foreach(var entry in value.Events)
            {
                if(entry==null || entry.Id<=0 || !Guid.TryParse(entry.Guid,out var guid) || parsed.ContainsKey(entry.Id)) throw new ArgumentException("Invalid audio event ID/GUID");
                var bytes=guid.ToByteArray();
                parsed.Add(entry.Id,new F.GUID {Data1=BitConverter.ToInt32(bytes,0),Data2=BitConverter.ToInt32(bytes,4),Data3=BitConverter.ToInt32(bytes,8),Data4=BitConverter.ToInt32(bytes,12)});
            }
            var bankIds=new HashSet<int>();
            foreach(var bank in value.Banks)
                if(bank==null || bank.ResourceId<=0 || bank.CharacterId < -1 || !bankIds.Add(bank.ResourceId)) throw new ArgumentException("Invalid or duplicate audio bank");
            events.Clear(); foreach(var entry in parsed) events.Add(entry.Key,entry.Value); config=value;
        }
        public bool TryInitialize()
        {
            if(initialized) return true;
            try
            {
                Check(Studio.System.create(out system));
                Check(system.initialize(MaxVoices,Studio.INITFLAGS.NORMAL,F.INITFLAGS.NORMAL,IntPtr.Zero));
                initialized=true;
                Status=config.Banks.Length==0?"FMOD ready; no banks configured":"FMOD ready";
                return true;
            }
            catch(Exception error) when(error is DllNotFoundException || error is EntryPointNotFoundException || error is BadImageFormatException || error is InvalidOperationException)
            {
                if(system.hasHandle()) { try { system.release(); } catch { } system.clearHandle(); }
                Status="FMOD unavailable: "+error.Message; return false;
            }
        }
        public async Task<FmodAudioScope> CreateScopeAsync(ResourceScope owner)
        {
            if(!initialized) throw new InvalidOperationException(Status);
            var scope=new FmodAudioScope(this,owner); scopes.Add(scope);
            // A character's native events/banks stop BEFORE its Addressables leases release.
            owner.Own(scope);
            try
            {
                foreach(var bank in config.Banks.Where(b=>b.CharacterId==owner.CharacterId))
                {
                    var data=await owner.LoadAsync<TextAsset>(bank.ResourceId);
                    if(scope.Disposed || owner.IsDisposed) throw new OperationCanceledException();
                    Check(system.loadBankMemory(data.bytes,Studio.LOAD_BANK_FLAGS.NORMAL,out var handle));
                    scope.AddBank(handle);
                }
                return scope;
            }
            catch { scope.Dispose(); throw; }
        }
        internal Studio.EventInstance CreateEvent(int id)
        {
            if(!initialized || !events.TryGetValue(id,out var guid)) throw new InvalidOperationException("Missing FMOD event "+id);
            Check(system.getEventByID(guid,out var description));
            Check(description.createInstance(out var instance)); return instance;
        }
        internal void Forget(FmodAudioScope scope) { scopes.Remove(scope); }
        internal static void Check(F.RESULT result) { if(result!=F.RESULT.OK) throw new InvalidOperationException("FMOD: "+F.Error.String(result)); }
        void Update() { if(initialized) system.update(); }
        void OnDestroy()
        {
            foreach(var scope in scopes.ToArray()) scope.Dispose();
            if(initialized) { system.unloadAll(); system.release(); system.clearHandle(); initialized=false; }
        }
    }
    public sealed class FmodAudioScope : IDisposable
    {
        readonly FmodAudioManager manager;
        readonly ResourceScope owner;
        readonly List<Studio.Bank> banks=new List<Studio.Bank>();
        readonly List<Studio.EventInstance> voices=new List<Studio.EventInstance>();
        public bool Disposed { get; private set; }
        internal FmodAudioScope(FmodAudioManager manager,ResourceScope owner) { this.manager=manager; this.owner=owner; }
        internal void AddBank(Studio.Bank bank) { banks.Add(bank); }
        public bool TryPlay(int eventId)
        {
            if(Disposed || owner.IsDisposed || !manager.Available) return false;
            for(int i=voices.Count-1;i>=0;i--)
            {
                voices[i].getPlaybackState(out var state);
                if(state==Studio.PLAYBACK_STATE.STOPPED) { voices[i].release(); voices.RemoveAt(i); }
            }
            if(voices.Count>=FmodAudioManager.MaxVoices) return false;
            Studio.EventInstance voice=default;
            try { voice=manager.CreateEvent(eventId); FmodAudioManager.Check(voice.start()); voices.Add(voice); return true; }
            catch(InvalidOperationException) { if(voice.hasHandle()) voice.release(); return false; }
        }
        public void Dispose()
        {
            if(Disposed) return; Disposed=true;
            foreach(var voice in voices) { voice.stop(Studio.STOP_MODE.IMMEDIATE); voice.release(); }
            voices.Clear(); foreach(var bank in banks) bank.unload(); banks.Clear(); manager.Forget(this);
        }
    }
}
