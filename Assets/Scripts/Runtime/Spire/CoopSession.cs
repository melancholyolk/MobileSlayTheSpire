using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Spire
{
    [Serializable] public sealed class CommandRecord
    {
        public int player, revision, argument;
        public string action;
    }
    [Serializable] sealed class Packet
    {
        public string kind, hash, token, error;
        public uint seed;
        public int protocol = 1;
        public CommandRecord command;
        public List<CommandRecord> history;
    }
    // Small bounded, newline-delimited protocol. Pump from the Unity main thread.
    sealed class Peer : IDisposable
    {
        public readonly TcpClient Client;
        readonly List<byte> pending = new List<byte>();
        readonly byte[] buffer = new byte[8192];
        public Peer(TcpClient client) { Client=client; client.NoDelay=true; client.SendTimeout=1000; }
        public bool Closed => Client.Client.Poll(0,SelectMode.SelectRead) && Client.Available==0;
        public void Send(Packet p)
        {
            var bytes=Encoding.UTF8.GetBytes(JsonUtility.ToJson(p)+"\n");
            if(bytes.Length>1048576) throw new IOException("Packet too large");
            Client.GetStream().Write(bytes,0,bytes.Length);
        }
        public IEnumerable<Packet> Read()
        {
            int budget=65536;
            while(Client.Available>0 && budget>0)
            {
                int n=Client.GetStream().Read(buffer,0,Math.Min(buffer.Length,Math.Min(Client.Available,budget))); budget-=n;
                for(int i=0;i<n;i++)
                {
                    if(buffer[i]==10)
                    {
                        var packet=JsonUtility.FromJson<Packet>(Encoding.UTF8.GetString(pending.ToArray())); pending.Clear();
                        if(packet==null || packet.protocol!=1) throw new IOException("Invalid protocol");
                        yield return packet;
                    }
                    else { pending.Add(buffer[i]); if(pending.Count>1048576) throw new IOException("Packet too large"); }
                }
            }
        }
        public void Dispose() { Client.Close(); }
    }

    public sealed class CoopSession : IDisposable
    {
        readonly Card[] cards;
        readonly string hash;
        readonly List<CommandRecord> history = new List<CommandRecord>();
        TcpListener listener;
        Peer peer;
        string seatToken;
        uint seed;
        DateTime handshakeDeadline;
        public Run Run { get; private set; }
        public bool IsHost { get; private set; }
        public bool Connected { get; private set; }
        public int LocalPlayer => IsHost ? 0 : 1;
        public int Port => listener==null ? 0 : ((IPEndPoint)listener.LocalEndpoint).Port;
        public string Status { get; private set; } = "Offline";
        public string ResumeToken => seatToken;
        public CoopSession(byte[] table)
        {
            cards=CardTable.Read(table);
            using(var sha=SHA256.Create()) hash=Convert.ToBase64String(sha.ComputeHash(table));
        }
        public void Host(int port,uint runSeed)
        {
            Dispose(); IsHost=true; seed=runSeed; seatToken=null; history.Clear();
            Run=new Run(cards,seed,HeroClass.Warrior,HeroClass.Hunter);
            listener=new TcpListener(IPAddress.Any,port); listener.Start(2); Status="Waiting for Hunter";
        }
        public void Join(string address,int port,string resumeToken=null)
        {
            Dispose(); IsHost=false; Run=null; seatToken=resumeToken;
            var client=new TcpClient();
            try
            {
                var connect=client.ConnectAsync(address,port);
                if(!connect.Wait(3000)) throw new IOException("Connection timed out");
                peer=new Peer(client); handshakeDeadline=DateTime.UtcNow.AddSeconds(5);
                peer.Send(new Packet {kind="hello",hash=hash,token=seatToken}); Status="Connecting";
            }
            catch { client.Close(); throw; }
        }
        public void Pump()
        {
            try
            {
                if(IsHost && listener!=null && listener.Pending())
                {
                    var client=listener.AcceptTcpClient();
                    if(peer!=null) client.Close();
                    else { peer=new Peer(client); handshakeDeadline=DateTime.UtcNow.AddSeconds(5); }
                }
                if(peer==null) return;
                if(peer.Closed) { Disconnect("Disconnected; reconnect to resume"); return; }
                if(!Connected && DateTime.UtcNow>handshakeDeadline) { Disconnect("Handshake timed out"); return; }
                // Materialize before processing, because rejection may dispose the peer.
                var packets=new List<Packet>(peer.Read());
                foreach(var p in packets) { if(peer==null) break; Receive(p); }
            }
            catch(Exception e) when(e is IOException || e is SocketException || e is ArgumentException || e is InvalidOperationException)
            { Disconnect("Network error: "+e.Message); }
        }
        void Receive(Packet packet)
        {
            if(IsHost)
            {
                if(packet.kind=="hello" && !Connected)
                {
                    if(packet.hash!=hash || (seatToken!=null && packet.token!=seatToken))
                    { peer.Send(new Packet {kind="error",error="Table mismatch or invalid reconnect token"}); Disconnect("Join rejected"); return; }
                    if(seatToken==null) seatToken=Guid.NewGuid().ToString("N");
                    peer.Send(new Packet {kind="welcome",hash=hash,seed=seed,token=seatToken,history=history});
                    Connected=true; Status="Co-op connected";
                }
                else if(packet.kind=="command" && Connected && packet.command!=null)
                {
                    // Bind ownership to the connection; never trust client-supplied player ids.
                    packet.command.player=1;
                    Accept(packet.command);
                }
            }
            else if(packet.kind=="welcome" && !Connected)
            {
                if(packet.hash!=hash || packet.history==null || packet.history.Count>10000) throw new IOException("Invalid session snapshot");
                var restored=new Run(cards,packet.seed,HeroClass.Warrior,HeroClass.Hunter);
                foreach(var c in packet.history)
                    if(c.revision!=restored.Revision || !restored.Apply(c.player,c.action,c.argument,out _)) throw new IOException("Invalid session history");
                Run=restored; seatToken=packet.token; Connected=true; Status="Co-op connected";
            }
            else if(packet.kind=="accepted" && Connected)
            {
                var c=packet.command;
                if(c==null || c.revision!=Run.Revision || !Run.Apply(c.player,c.action,c.argument,out _)) throw new IOException("State mismatch; reconnect required");
                Status="Co-op connected";
            }
            else if(packet.kind=="error") Status=packet.error;
        }
        bool Accept(CommandRecord command)
        {
            string error="Stale command; try again";
            if(history.Count>=10000) { Status="Session command limit reached"; return false; }
            if(command.revision!=Run.Revision || !Run.Apply(command.player,command.action,command.argument,out error))
            {
                Status=error ?? "Stale command; try again";
                if(command.player==1) peer.Send(new Packet {kind="error",error=Status});
                return false;
            }
            history.Add(command);
            peer.Send(new Packet {kind="accepted",command=command}); return true;
        }
        public bool Submit(string action,int argument)
        {
            if(!Connected || Run==null) { Status="Waiting for teammate"; return false; }
            var command=new CommandRecord {player=LocalPlayer,revision=Run.Revision,action=action,argument=argument};
            try
            {
                if(IsHost) return Accept(command);
                peer.Send(new Packet {kind="command",command=command}); return true;
            }
            catch(Exception e) when(e is IOException || e is SocketException) { Disconnect(e.Message); return false; }
        }
        void Disconnect(string message) { peer?.Dispose(); peer=null; Connected=false; Status=message; }
        public void Dispose() { Disconnect("Offline"); listener?.Stop(); listener=null; }
    }
}
