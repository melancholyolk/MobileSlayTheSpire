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
    // 网络历史记录；字段名参与 JSON 协议，调整命名必须同步协议和重连兼容策略。
    [Serializable]
    public sealed class CommandRecord
    {
        public int player, revision, argument;
        public string action;
        public bool physical;
        public int bodyPart;
    }
}
