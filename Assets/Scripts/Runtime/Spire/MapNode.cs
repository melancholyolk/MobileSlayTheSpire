using System;
using System.Collections.Generic;
using System.Linq;

namespace Spire
{
    [Serializable]
    public sealed class MapNode
    {
        public int Id, Floor, Lane;
        public RoomKind Kind;
        public List<int> Next = new List<int>();
    }
}
