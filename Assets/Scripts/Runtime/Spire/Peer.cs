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
    // Small bounded, newline-delimited protocol. Pump from the Unity main thread.
    sealed class Peer : IDisposable
    {
        public readonly TcpClient Client;
        readonly List<byte> pending = new List<byte>();
        readonly byte[] buffer = new byte[8192];
        public Peer(TcpClient client)
        {
            Client = client;
            client.NoDelay = true;
            client.SendTimeout = 1000;
        }

        public bool Closed => Client.Client.Poll(0, SelectMode.SelectRead) && Client.Available == 0;

        public void Send(Packet p)
        {
            var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(p) + "\n");
            if (bytes.Length > 1048576)
                throw new IOException("Packet too large");
            Client.GetStream().Write(bytes, 0, bytes.Length);
        }

        public IEnumerable<Packet> Read()
        {
            int budget = 65536;
            while (Client.Available > 0 && budget > 0)
            {
                int n = Client.GetStream().Read(buffer, 0, Math.Min(buffer.Length, Math.Min(Client.Available, budget)));
                budget -= n;
                for (int i = 0; i < n; i++)
                {
                    if (buffer[i] == 10)
                    {
                        var packet = JsonUtility.FromJson<Packet>(Encoding.UTF8.GetString(pending.ToArray()));
                        pending.Clear();
                        if (packet == null || packet.protocol != 3)
                            throw new IOException("Invalid protocol");
                        yield return packet;
                    }
                    else
                    {
                        pending.Add(buffer[i]);
                        if (pending.Count > 1048576)
                            throw new IOException("Packet too large");
                    }
                }
            }
        }

        public void Dispose()
        {
            Client.Close();
        }
    }
}
