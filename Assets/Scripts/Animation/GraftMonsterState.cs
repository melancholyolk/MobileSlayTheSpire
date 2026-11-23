using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpireAnimation
{
    public enum GraftSkill { Slash, Guard, Mend }
    [Serializable] public sealed class GraftTemplate
    {
        public string Name;
        public int Hp=24,Power=8,SocketType=1;
        public float Length=64,IdleLift=18,DeathDuration=.8f;
        public Color Color=Color.red;
        public GraftSkill Skill;
    }
    public sealed class GraftUnit
    {
        public int Root,ParentNode,ParentPort,Hp,Block;
        public int[] Nodes;
        public GraftTemplate Template;
        public bool Alive=>Hp>0;
    }
    public readonly struct GraftSocket
    {
        public readonly int Node,Port;
        public GraftSocket(int node,int port) { Node=node;Port=port; }
    }
    // Prototype encounter state. Topology uses the same BodyAssembly validation as live actors.
    public sealed class GraftMonsterState
    {
        public readonly BodyAssembly Assembly;
        public readonly GraftTemplate[] Library;
        public readonly List<GraftUnit> Limbs=new List<GraftUnit>();
        public int Hp=120,Block,OpponentHp=160;
        public bool Alive=>Hp>0;
        int nextNode=2,selection;
        readonly Dictionary<int,GraftUnit> owners=new Dictionary<int,GraftUnit>();
        public GraftMonsterState(GraftTemplate[] library,int capacity=512)
        {
            if(library==null || library.Length==0) throw new ArgumentException("A limb library is required");
            Library=library;
            var parts=new List<BodyPartDefinition> {
                new BodyPartDefinition {Id=1,Role=1,Size=new Vector2(100,120),Ports=new[]{Port(0,1,new Vector2(0,65)),Port(1,1,new Vector2(-48,25),160),Port(2,1,new Vector2(48,25),20),Port(3,1,new Vector2(0,-48),-80)}},
                new BodyPartDefinition {Id=2,Role=2,Size=new Vector2(60,55),Ports=new[]{Port(0,1,Vector2.zero)}}
            };
            for(int i=0;i<library.Length;i++)
            {
                var t=library[i];
                if(t==null || t.Length<=0 || t.Hp<=0 || t.Power<0 || t.SocketType<=0) throw new ArgumentException("Invalid limb template");
                parts.Add(new BodyPartDefinition {Id=10+i*3,Role=3,Size=new Vector2(t.Length,20),ImageOffset=new Vector2(t.Length/2,0),Ports=new[]{Port(0,t.SocketType,Vector2.zero),Port(1,t.SocketType,new Vector2(t.Length,0))}});
                parts.Add(new BodyPartDefinition {Id=11+i*3,Role=4,Size=new Vector2(t.Length,16),ImageOffset=new Vector2(t.Length/2,0),Ports=new[]{Port(0,t.SocketType,Vector2.zero),Port(1,t.SocketType,new Vector2(t.Length,0)),Port(2,1,new Vector2(t.Length*.6f,0),55)}});
                parts.Add(new BodyPartDefinition {Id=12+i*3,Role=5,Size=new Vector2(22,22),Ports=new[]{Port(0,t.SocketType,Vector2.zero)}});
            }
            Assembly=new BodyAssembly(parts,capacity);
            Assembly.Add(new BodyNode {Id=0,Part=1});Assembly.Add(new BodyNode {Id=1,Part=2,Parent=0,ParentPort=0});
        }
        static BodyPort Port(int id,int type,Vector2 point,float angle=0)=>new BodyPort {Id=id,Type=type,Accepts=type,Position=point,Angle=angle};
        public GraftUnit Owner(int node)=>owners.TryGetValue(node,out var unit)?unit:null;
        public IEnumerable<GraftSocket> EmptySockets()
        {
            var nodes=Assembly.Nodes.OrderBy(n=>n.Id).ToArray();
            foreach(var n in nodes)
                foreach(var port in Assembly.Definition(n.Id).Ports)
                    if(!(n.Parent>=0 && n.Port==port.Id) && !nodes.Any(c=>c.Parent==n.Id && c.ParentPort==port.Id))
                        yield return new GraftSocket(n.Id,port.Id);
        }
        public GraftUnit Graft(int preferredParent=-1)
        {
            if(!Alive || Assembly.Count+3>Assembly.Capacity) return null;
            foreach(var socket in EmptySockets().Where(s=>preferredParent<0 || s.Node==preferredParent))
                for(int offset=0;offset<Library.Length;offset++)
                {
                    int index=(selection+offset)%Library.Length;
                    var port=Assembly.Definition(socket.Node).Port(socket.Port);
                    var incoming=Port(0,Library[index].SocketType,Vector2.zero);
                    if(!port.Compatible(incoming)) continue;
                    int root=nextNode;nextNode+=3;
                    var unit=new GraftUnit {Root=root,ParentNode=socket.Node,ParentPort=socket.Port,Hp=Library[index].Hp,Template=Library[index],Nodes=new[]{root,root+1,root+2}};
                    for(int k=0;k<3;k++)
                    {
                        Assembly.Add(new BodyNode {Id=root+k,Part=10+index*3+k,Parent=k==0?socket.Node:root+k-1,ParentPort=k==0?socket.Port:1,Group=root});
                        owners.Add(root+k,unit);
                    }
                    Limbs.Add(unit);selection=(index+1)%Library.Length;return unit;
                }
            return null;
        }
        public GraftUnit[] Damage(int node,int amount)
        {
            if(!Alive || amount<=0 || !Assembly.Nodes.Any(n=>n.Id==node)) return Array.Empty<GraftUnit>();
            var owner=Owner(node);
            if(owner!=null)
            {
                int absorbed=Math.Min(owner.Block,amount);owner.Block-=absorbed;
                owner.Hp=Math.Max(0,owner.Hp-(amount-absorbed));
                if(owner.Alive) return Array.Empty<GraftUnit>();
                return Detach(owner.Root);
            }
            int blocked=Math.Min(Block,amount);Block-=blocked;Hp=Math.Max(0,Hp-(amount-blocked));
            if(Alive) return Array.Empty<GraftUnit>();
            var dead=Limbs.ToArray();foreach(var limb in dead) limb.Hp=0;
            // Detach top-level grafts; each removes its nested subtree exactly once.
            foreach(var limb in dead.Where(l=>Owner(l.ParentNode)==null).ToArray()) Assembly.Detach(limb.Root);
            Limbs.Clear();owners.Clear();return dead;
        }
        GraftUnit[] Detach(int root)
        {
            var removed=Assembly.Detach(root).Select(n=>n.Id).ToHashSet();
            var dead=Limbs.Where(l=>removed.Contains(l.Root)).ToArray();
            foreach(var limb in dead) { limb.Hp=0;Limbs.Remove(limb);foreach(int node in limb.Nodes) owners.Remove(node); }
            return dead;
        }
        public bool UseSkill(int root)
        {
            var limb=Limbs.FirstOrDefault(l=>l.Root==root);
            if(!Alive || OpponentHp<=0 || limb==null || !limb.Alive) return false;
            switch(limb.Template.Skill)
            {
                case GraftSkill.Slash: OpponentHp=Math.Max(0,OpponentHp-limb.Template.Power);break;
                case GraftSkill.Guard: limb.Block+=limb.Template.Power;break;
                case GraftSkill.Mend:
                    var parent=Owner(limb.ParentNode);
                    if(parent!=null) parent.Hp=Math.Min(parent.Template.Hp,parent.Hp+limb.Template.Power);
                    else Hp=Math.Min(120,Hp+limb.Template.Power);
                    break;
            }
            return true;
        }
    }
}
