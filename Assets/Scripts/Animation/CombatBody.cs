using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.U2D.IK;

namespace SpireAnimation
{
    [DefaultExecutionOrder(10000)]
    public sealed class CombatBody : MonoBehaviour
    {
        public AttackProfile DefaultAttack=AttackProfile.Default;
        public CardAttackProfile[] CardAttacks=Array.Empty<CardAttackProfile>();
        public bool Invulnerable;
        public bool ShowHitboxes=true;
        public BodyPartHurtbox[] Parts {get; private set;}=Array.Empty<BodyPartHurtbox>();
        readonly List<Collider2D> overlaps=new List<Collider2D>(32);
        SkinnedActorView skin;
        AttackHitbox frameBox;
        int drawnFrame=-1;
        AttackHitbox scheduledBox;
        public bool QueryPending {get; private set;}
        public int QueryResult {get; private set;}=-1;
        public void ScheduleQuery(AttackHitbox box) { scheduledBox=box; QueryResult=-1; QueryPending=isActiveAndEnabled; }
        void LateUpdate()
        {
            if(!QueryPending) return;
            QueryResult=Query(scheduledBox); QueryPending=false;
        }
        void OnDisable() { QueryPending=false; QueryResult=-1; drawnFrame=-1; }
        public AttackProfile Profile(string cardId)
        {
            foreach(var entry in CardAttacks) if(entry.CardId==cardId) return entry.Profile;
            return DefaultAttack;
        }
        public void Configure(BodyCatalog catalog,int owner)
        {
            skin=GetComponent<SkinnedActorView>();
            if(skin==null)
            {
                var body=GetComponent<BodyAssemblyView>();
                if(body!=null) BindAssembly(body);
                return;
            }
            var transforms=skin.RenderSpace.GetComponentsInChildren<Transform>(true);
            var parts=new List<BodyPartHurtbox>();
            foreach(var binding in catalog.Nodes.Where(n=>n.Owner==owner))
            {
                var bone=transforms.FirstOrDefault(t=>t.name==binding.Bone);
                if(bone==null) throw new InvalidOperationException("Missing hurtbox bone: "+binding.Bone);
                var definition=catalog.Parts.Single(p=>p.Id==binding.Node.Part);
                // Catalog dimensions are scaled UI units, not original pixels. Derive the
                // conversion from the actual sprite, so PPU and catalog scale can vary.
                var visual=transforms.FirstOrDefault(t=>t.name=="Skin_"+binding.Bone);
                var renderer=visual!=null?visual.GetComponent<SpriteRenderer>():null;
                if(renderer==null || renderer.sprite==null) throw new InvalidOperationException("Missing skin sprite: "+binding.Bone);
                Vector2 size=renderer.sprite.bounds.size;
                var conversion=new Vector2(size.x/definition.Size.x,size.y/definition.Size.y);
                parts.Add(Bind(bone,binding.Node.Id,size,Vector2.Scale(definition.ImageOffset,conversion)));
            }
            Parts=parts.ToArray();
            foreach(var solver in GetComponentsInChildren<Solver2D>(true))
            {
                if(!solver.isValid) solver.Initialize();
                for(int i=0;i<solver.chainCount;i++)
                {
                    var chain=solver.GetChain(i);
                    foreach(var part in Parts)
                        if(chain.transforms.Contains(part.transform)) part.ReactionTarget=chain.target;
                }
            }
        }
        public void BindAssembly(BodyAssemblyView body)
        {
            var result=new List<BodyPartHurtbox>();
            foreach(var node in body.Assembly.Nodes)
            {
                var definition=body.Assembly.Definition(node.Id);
                result.Add(Bind(body.Bone(node.Id),node.Id,definition.Size,definition.ImageOffset));
            }
            Parts=result.ToArray();
        }
        BodyPartHurtbox Bind(Transform bone,int id,Vector2 size,Vector2 offset)
        {
            var part=bone.GetComponent<BodyPartHurtbox>()??bone.gameObject.AddComponent<BodyPartHurtbox>();
            part.Owner=this; part.NodeId=id;
            part.Collider.size=new Vector2(Mathf.Max(.001f,size.x),Mathf.Max(.001f,size.y));
            part.Collider.offset=offset; part.Collider.isTrigger=true;
            return part;
        }
        // Map the offscreen rig through its display. Collision still uses its actual bones.
        public Vector2 ToDisplay(Vector2 world)
        {
            if(skin==null) return world;
            var uv=skin.ActorCamera.WorldToViewportPoint(world);
            var rect=skin.Display.rectTransform.rect;
            return skin.Display.rectTransform.TransformPoint(new Vector3(rect.xMin+uv.x*rect.width,rect.yMin+uv.y*rect.height,0));
        }
        public Vector2 ToPhysics(Vector2 display)
        {
            if(skin==null) return display;
            var local=skin.Display.rectTransform.InverseTransformPoint(display);
            var rect=skin.Display.rectTransform.rect;
            return skin.ActorCamera.ViewportToWorldPoint(new Vector3((local.x-rect.xMin)/rect.width,(local.y-rect.yMin)/rect.height,
                -skin.ActorCamera.transform.position.z));
        }
        public Vector2 AimPoint
        {
            get
            {
                Physics2D.SyncTransforms();
                var active=Parts.Where(p=>p!=null && p.Collider.enabled && p.gameObject.activeInHierarchy).ToArray();
                if(active.Length==0) return transform.position;
                var bounds=active[0].Collider.bounds;
                foreach(var part in active) bounds.Encapsulate(part.Collider.bounds);
                return ToDisplay(bounds.center);
            }
        }
        public int Query(AttackHitbox box)
        {
            frameBox=box; drawnFrame=Time.frameCount;
            if(Invulnerable || !isActiveAndEnabled || box.Size.x<=0 || box.Size.y<=0) return -1;
            var rotation=Quaternion.Euler(0,0,box.Angle);
            var center=ToPhysics(box.Center);
            Vector2 x=ToPhysics(box.Center+(Vector2)(rotation*Vector3.right)*box.Size.x*.5f)-center;
            Vector2 y=ToPhysics(box.Center+(Vector2)(rotation*Vector3.up)*box.Size.y*.5f)-center;
            var filter=new ContactFilter2D {useTriggers=true};
            Physics2D.SyncTransforms();
            overlaps.Clear();
            Physics2D.OverlapBox(center,new Vector2(x.magnitude*2,y.magnitude*2),Mathf.Atan2(x.y,x.x)*Mathf.Rad2Deg,filter,overlaps);
            int node=-1; float distance=float.PositiveInfinity;
            foreach(var collider in overlaps)
            {
                var part=collider.GetComponent<BodyPartHurtbox>();
                if(part==null || !part.isActiveAndEnabled || part.Owner!=this) continue;
                float d=((Vector2)collider.bounds.center-center).sqrMagnitude;
                if(d<distance || (Mathf.Approximately(d,distance) && part.NodeId<node)) { distance=d; node=part.NodeId; }
            }
            return node;
        }
        public void React(int node,AttackProfile profile,float direction)
        {
            var part=Array.Find(Parts,p=>p!=null && p.NodeId==node);
            if(part==null) return;
            var box=part.Collider.bounds;
            float scale=Mathf.Max(box.size.x,box.size.y);
            part.React(new Vector3(profile.Reaction.x*direction,profile.Reaction.y,0)*scale,profile.ReactionDuration);
        }
        void OnDrawGizmos()
        {
            if(ShowHitboxes && drawnFrame==Time.frameCount) frameBox.Draw(Color.red);
        }
    }
}
