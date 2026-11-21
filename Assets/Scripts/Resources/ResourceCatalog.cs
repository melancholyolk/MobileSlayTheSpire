using System;
using System.Collections.Generic;
using UnityEngine;
namespace SpireResources
{
    [Serializable] public sealed class ResourceEntry
    {
        public int Id;
        // -1 is shared. Every character group is a separate bundle.
        public int CharacterId=-1;
        public string Guid;
        public long EstimatedBytes=1048576;
    }
    public sealed class ResourceCatalog : ScriptableObject
    {
        public ResourceEntry[] Entries=Array.Empty<ResourceEntry>();
        Dictionary<int,ResourceEntry> index;
        public ResourceEntry Find(int id)
        {
            if(index==null) { index=new Dictionary<int,ResourceEntry>(); foreach(var e in Entries) index.Add(e.Id,e); }
            if(!index.TryGetValue(id,out var result)) throw new KeyNotFoundException("Unknown resource ID "+id);
            return result;
        }
    }
    public static class ResourceIds
    {
        public const int GameStart=1, Gameplay=2, CardView=3, Cards=10, Localization=11;
    }
}
