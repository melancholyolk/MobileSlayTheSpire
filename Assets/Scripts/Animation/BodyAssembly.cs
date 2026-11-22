using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace SpireAnimation
{
    [Serializable] public sealed class BodyPort
    {
        public int Id,Type,Accepts;
        public Vector2 Position;
        public float Angle;
        public bool Compatible(BodyPort other) => Type==other.Accepts && Accepts==other.Type;
    }
    [Serializable] public sealed class BodyPartDefinition
    {
        public int Id,Role,ArtId;
        public Vector2 Size,ImageOffset;
        public BodyPort[] Ports=Array.Empty<BodyPort>();
        public BodyPort Port(int id) => Array.Find(Ports,p=>p.Id==id)??throw new ArgumentException("Unknown port "+id);
    }
    [Serializable] public sealed class BodyNode
    {
        public int Id,Part,Parent=-1,ParentPort,Port,Group;
        public Vector2 RootPosition;
    }
    // A tree has no topology-specific limb/depth limit; the explicit budget bounds memory.
    // Configuration is cloned on entry so outside edits cannot invalidate occupied sockets.
    public sealed class BodyAssembly
    {
        readonly Dictionary<int,BodyPartDefinition> definitions;
        readonly Dictionary<int,BodyNode> nodes=new Dictionary<int,BodyNode>();
        public int Capacity {get;}
        public int Count=>nodes.Count;
        public IEnumerable<BodyNode> Nodes=>nodes.Values.Select(Clone);
        static BodyNode Clone(BodyNode n)=>new BodyNode {Id=n.Id,Part=n.Part,Parent=n.Parent,ParentPort=n.ParentPort,Port=n.Port,Group=n.Group,RootPosition=n.RootPosition};
        public BodyAssembly(IEnumerable<BodyPartDefinition> parts,int capacity=512)
        {
            if(capacity<1 || capacity>4096) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity=capacity; definitions=parts.ToDictionary(p=>p.Id);
            foreach(var p in definitions.Values)
            {
                if(p.Id<=0 || p.Role<=0 || p.Ports==null || p.Ports.Select(s=>s.Id).Distinct().Count()!=p.Ports.Length) throw new ArgumentException("Invalid part definition");
                foreach(var s in p.Ports) if(s.Id<0 || s.Type<=0 || s.Accepts<=0) throw new ArgumentException("Invalid port type");
            }
        }
        public BodyPartDefinition Definition(int id)=>definitions[nodes[id].Part];
        public BodyNode Node(int id)=>Clone(nodes[id]);
        public void Add(BodyNode node)
        {
            if(node==null || node.Id<0 || nodes.ContainsKey(node.Id) || Count>=Capacity || !definitions.ContainsKey(node.Part)) throw new ArgumentException("Invalid/duplicate body node or capacity reached");
            if(node.Parent<0) { if(Count!=0) throw new ArgumentException("Only one root allowed"); }
            else ValidateConnection(node,node.Parent,node.ParentPort,node.Port);
            nodes.Add(node.Id,Clone(node));
        }
        void ValidateConnection(BodyNode child,int parentId,int parentPort,int childPort)
        {
            if(!nodes.TryGetValue(parentId,out var parent)) throw new ArgumentException("Parent missing");
            for(var ancestor=parent;ancestor!=null;ancestor=ancestor.Parent<0?null:nodes[ancestor.Parent])
                if(ancestor.Id==child.Id) throw new ArgumentException("Body cycle");
            var a=definitions[parent.Part].Port(parentPort); var b=definitions[child.Part].Port(childPort);
            if(!a.Compatible(b)) throw new ArgumentException("Incompatible sockets");
            if(parent.Parent>=0 && parent.Port==parentPort) throw new ArgumentException("Parent socket is its incoming joint");
            if(nodes.Values.Any(n=>n.Id!=child.Id && n.Parent==parentId && n.ParentPort==parentPort)) throw new ArgumentException("Socket occupied");
            if(nodes.Values.Any(n=>n.Parent==child.Id && n.ParentPort==childPort)) throw new ArgumentException("Child socket occupied");
        }
        public void Reparent(int id,int parent,int parentPort,int port)
        {
            var node=nodes[id]; if(node.Parent<0) throw new ArgumentException("Cannot move root");
            ValidateConnection(node,parent,parentPort,port);
            node.Parent=parent; node.ParentPort=parentPort; node.Port=port;
        }
        public int[] Subtree(int id)
        {
            if(!nodes.ContainsKey(id)) throw new ArgumentException("Missing body node");
            var result=new List<int>(); var pending=new Queue<int>(); pending.Enqueue(id);
            while(pending.Count>0) { int current=pending.Dequeue();result.Add(current);foreach(var n in nodes.Values) if(n.Parent==current) pending.Enqueue(n.Id); }
            return result.ToArray();
        }
        public BodyNode[] Detach(int id)
        {
            var result=Subtree(id).Select(i=>Clone(nodes[i])).ToArray();
            foreach(var n in result) nodes.Remove(n.Id); result[0].Parent=-1; return result;
        }
        public void MergeGroup(int id,int group)
        {
            if(group<0) throw new ArgumentOutOfRangeException(nameof(group));
            foreach(int child in Subtree(id)) nodes[child].Group=group;
        }
        public Vector2 LocalPosition(int id)
        {
            var n=nodes[id]; if(n.Parent<0) return n.RootPosition;
            var a=Definition(n.Parent).Port(n.ParentPort); var b=Definition(id).Port(n.Port);
            return a.Position-(Vector2)(Quaternion.Euler(0,0,a.Angle-b.Angle)*(Vector3)b.Position);
        }
        public float LocalAngle(int id) { var n=nodes[id]; return n.Parent<0?0:Definition(n.Parent).Port(n.ParentPort).Angle-Definition(id).Port(n.Port).Angle; }
    }
    [Serializable] public sealed class JointPairMotion
    {
        public MotionId Motion;
        public int ParentRole,ChildRole;
        public float Duration=.65f,Peak=.45f,Middle,End;
    }
}
