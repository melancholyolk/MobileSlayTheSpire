using System;
using System.Collections.Generic;
using System.Linq;
using Spire;
using UnityEngine;
namespace SpireAnimation
{
    public sealed class BodyCatalog
    {
        public BodyPartDefinition[] Parts;
        public JointPairMotion[] Motions;
        public sealed class Binding { public int Owner; public BodyNode Node; public string Bone; }
        public Binding[] Nodes;
        public static BodyCatalog Read(byte[] parts,byte[] ports,byte[] nodes,byte[] motions)
        {
            var result=new BodyCatalog();
            BusinessTable t=BusinessTable.Read(parts);
            int I(int row,string name)=>t.Int(row,t.Column(name));
            float F(int row,string name)=>(float)t.Float(row,t.Column(name));
            var definitions=new List<BodyPartDefinition>();
            for(int r=0;r<t.RowCount;r++) definitions.Add(new BodyPartDefinition {Id=I(r,"Id"),Role=I(r,"Role"),ArtId=I(r,"ArtId"),Size=new Vector2(F(r,"Width"),F(r,"Height")),ImageOffset=new Vector2(F(r,"OffsetX"),F(r,"OffsetY"))});
            t=BusinessTable.Read(ports);var sockets=new Dictionary<int,List<BodyPort>>();
            for(int r=0;r<t.RowCount;r++) { int id=I(r,"Part");if(!sockets.ContainsKey(id)) sockets[id]=new List<BodyPort>();sockets[id].Add(new BodyPort {Id=I(r,"Port"),Type=I(r,"Type"),Accepts=I(r,"Accepts"),Position=new Vector2(F(r,"X"),F(r,"Y")),Angle=F(r,"Angle")}); }
            foreach(var p in definitions) p.Ports=sockets.TryGetValue(p.Id,out var list)?list.ToArray():Array.Empty<BodyPort>();
            result.Parts=definitions.ToArray();
            t=BusinessTable.Read(nodes); var bindings=new List<Binding>();
            for(int r=0;r<t.RowCount;r++) bindings.Add(new Binding {Owner=I(r,"Owner"),Bone=t.String(r,t.Column("Bone")),Node=new BodyNode {Id=I(r,"Id"),Part=I(r,"Part"),Parent=I(r,"Parent"),ParentPort=I(r,"ParentPort"),Port=I(r,"Port"),Group=I(r,"Group"),RootPosition=new Vector2(F(r,"X"),F(r,"Y"))}});
            result.Nodes=bindings.ToArray();
            t=BusinessTable.Read(motions);var recipes=new List<JointPairMotion>();
            for(int r=0;r<t.RowCount;r++) recipes.Add(new JointPairMotion {Motion=(MotionId)I(r,"Motion"),ParentRole=I(r,"ParentRole"),ChildRole=I(r,"ChildRole"),Duration=F(r,"Duration"),Peak=F(r,"Peak"),Middle=F(r,"Middle"),End=F(r,"End")});
            result.Motions=recipes.ToArray();return result;
        }
    }
}
