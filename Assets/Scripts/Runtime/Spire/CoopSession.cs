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
    [Serializable] public sealed class CommandRecord
    {
        public int player, revision, argument;
        public string action;
    }
    [Serializable] sealed class Packet
    {
        public string kind, hash, token, error;
        public uint seed;
        public int protocol = 2;
        public int[] characters;
        public bool ready, started;
        public int character;
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
                        if(packet==null || packet.protocol!=2) throw new IOException("Invalid protocol");
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
        TcpClient connecting;
        Task connectTask;
        int[] selections={-1,-1};
        bool guestReady;
        public int LobbyRevision { get; private set; }
        public bool GuestReady=>guestReady;
        public int SelectedCharacter(int seat)=>selections[seat];
        public bool CanStart=>IsHost && Connected && Run==null && guestReady && selections.All(ValidCharacter);
        bool ValidCharacter(int id)=>cards.Any(c=>(int)c.Class==id);
        public event Action Changed;
        void Notify() { LobbyRevision++; Changed?.Invoke(); }

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
            Run=null; selections=new[]{-1,-1}; guestReady=false;
            listener=new TcpListener(IPAddress.Any,port); listener.Start(2); Status="Waiting for player"; Notify();
        }
        public void Join(string address,int port,string resumeToken=null)
        {
            Dispose(); IsHost=false; Run=null; seatToken=resumeToken;
            selections=new[]{-1,-1}; guestReady=false;
            connecting=new TcpClient();
            connectTask=connecting.ConnectAsync(address,port);
            handshakeDeadline=DateTime.UtcNow.AddSeconds(5);
            Status="Connecting"; Notify();
        }
        public void Pump()
        {
            try
            {
                if(connectTask!=null)
                {
                    if(!connectTask.IsCompleted)
                    {
                        if(DateTime.UtcNow>handshakeDeadline) Disconnect("Connection timed out");
                        return;
                    }
                    connectTask.GetAwaiter().GetResult(); connectTask=null;
                    peer=new Peer(connecting); connecting=null;
                    peer.Send(new Packet {kind="hello",hash=hash,token=seatToken});
                    handshakeDeadline=DateTime.UtcNow.AddSeconds(5);
                }
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
                    peer.Send(Snapshot("welcome"));
                    Connected=true; Status="Co-op connected"; Notify();
                }
                else if(packet.kind=="select" && Connected && Run==null)
                {
                    if(ValidCharacter(packet.character)) { selections[1]=packet.character; guestReady=false; BroadcastLobby(); }
                }
                else if(packet.kind=="ready" && Connected && Run==null)
                {
                    guestReady=packet.ready && ValidCharacter(selections[1]); BroadcastLobby();
                }
                else if(packet.kind=="command" && Connected && Run!=null && packet.command!=null)
                {
                    // Bind ownership to the connection; never trust client-supplied player ids.
                    packet.command.player=1;
                    Accept(packet.command);
                }
            }
            else if(packet.kind=="welcome" && !Connected)
            {
                if(packet.hash!=hash || packet.history==null || packet.history.Count>10000) throw new IOException("Invalid session snapshot");
                Restore(packet);
                seatToken=packet.token; Connected=true; Status="Co-op connected"; Notify();
            }
            else if((packet.kind=="lobby" || packet.kind=="start") && Connected)
            {
                if(Run!=null) throw new IOException("Unexpected lobby packet after start");
                Restore(packet); Notify();
            }
            else if(packet.kind=="accepted" && Connected)
            {
                var c=packet.command;
                if(c==null || c.revision!=Run.Revision || !Run.Apply(c.player,c.action,c.argument,out _)) throw new IOException("State mismatch; reconnect required");
                Status="Co-op connected";
            }
            else if(packet.kind=="error") Status=packet.error;
        }
        Packet Snapshot(string kind)=>new Packet {kind=kind,hash=hash,seed=seed,token=seatToken,history=history,characters=selections,ready=guestReady,started=Run!=null};
        void Restore(Packet packet)
        {
            if(packet.characters==null || packet.characters.Length!=2 ||
                packet.characters.Any(c=>c!=-1 && !ValidCharacter(c))) throw new IOException("Invalid character selection");
            selections=(int[])packet.characters.Clone(); guestReady=packet.ready;
            if(packet.started)
            {
                if(!selections.All(ValidCharacter) || packet.history==null || packet.history.Count>10000) throw new IOException("Invalid start snapshot");
                var restored=new Run(cards,packet.seed,selections.Select(c=>(HeroClass)c).ToArray());
                foreach(var c in packet.history)
                    if(c.revision!=restored.Revision || !restored.Apply(c.player,c.action,c.argument,out _)) throw new IOException("Invalid session history");
                Run=restored;
            }
        }
        void BroadcastLobby() { if(Connected) peer.Send(Snapshot("lobby")); Notify(); }
        public bool SelectCharacter(int id)
        {
            if(Run!=null || !ValidCharacter(id) || (!IsHost && !Connected)) return false;
            if(IsHost) { selections[0]=id; guestReady=false; BroadcastLobby(); }
            else peer.Send(new Packet {kind="select",character=id});
            return true;
        }
        public bool SetReady(bool ready)
        {
            if(IsHost || Run!=null || !Connected) return false;
            peer.Send(new Packet {kind="ready",ready=ready}); return true;
        }
        public bool StartGame()
        {
            if(!CanStart) return false;
            Run=new Run(cards,seed,selections.Select(c=>(HeroClass)c).ToArray());
            peer.Send(Snapshot("start")); Status="Co-op connected"; Notify(); return true;
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
        void Disconnect(string message)
        {
            peer?.Dispose(); peer=null; connecting?.Close(); connecting=null; connectTask=null;
            Connected=false; guestReady=false;
            if(Run==null) { seatToken=null; selections[1]=-1; }
            Status=message; Notify();
        }
        public void Dispose() { Disconnect("Offline"); listener?.Stop(); listener=null; }
    }
}
