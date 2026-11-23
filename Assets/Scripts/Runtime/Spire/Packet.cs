using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Spire
{
    [Serializable]
    sealed class Packet
    {
        public string kind, hash, token, error;
        public uint seed;
        public int protocol = 3;
        public int[] characters;
        public bool ready, started;
        public int character;
        public CommandRecord command;
        public List<CommandRecord> history;
    }
}
