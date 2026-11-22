using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SpireResources;
using UnityEngine.UI;
using UnityEngine;
namespace SpireAnimation
{
    // Serialized into the actor prefab from Excel binaries. Sprites remain actor-owned assets.
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(SkeletonView))]
    public sealed class BodyAssemblyView : MonoBehaviour
    {
        public BodyPartDefinition[] Parts=Array.Empty<BodyPartDefinition>();
        public BodyNode[] Initial=Array.Empty<BodyNode>();
        public JointPairMotion[] Recipes=Array.Empty<JointPairMotion>();
        public Transform[] Bones=Array.Empty<Transform>();
        BodyAssembly assembly;
        readonly List<AnimationClip> owned=new List<AnimationClip>();
        readonly Dictionary<int,Transform> bones=new Dictionary<int,Transform>();
        public BodyAssembly Assembly=>assembly;
        public Transform Bone(int id)=>bones[id];
        public int OwnedClipCount=>owned.Count;
        void OnEnable() { if(Initial.Length>0 && assembly==null) Initialize(); }
        public async Task ConfigureAsync(BodyCatalog catalog,int owner,ResourceScope scope)
        {
            if(gameObject.activeInHierarchy) throw new InvalidOperationException("Configure an inactive actor");
            var bindings=catalog.Nodes.Where(n=>n.Owner==owner).ToArray();
            if(bindings.Length==0) throw new InvalidOperationException("No body assembly for actor "+owner);
            // Validate topology before mutating the prefab or acquiring images.
            Parts=catalog.Parts; Initial=bindings.Select(b=>b.Node).ToArray(); Recipes=catalog.Motions;
            var check=new BodyAssembly(Parts);
            var pending=Initial.ToList();
            while(pending.Count>0)
            {
                var ready=pending.Where(n=>n.Parent<0 || check.Nodes.Any(p=>p.Id==n.Parent)).ToArray();
                if(ready.Length==0) throw new InvalidOperationException("Invalid assembly topology");
                foreach(var n in ready) { check.Add(n);pending.Remove(n); }
            }
            GetComponent<SkeletonView>().Stop();ClearClips();
            foreach(Transform child in transform) { child.gameObject.SetActive(false);Destroy(child.gameObject); }
            var motion=new GameObject("Body",typeof(RectTransform)).transform;motion.SetParent(transform,false);
            var transforms=new List<Transform>();
            foreach(var binding in bindings)
            {
                var part=Parts.Single(p=>p.Id==binding.Node.Part);
                var sprite=await scope.LoadAsync<Sprite>(part.ArtId);
                if(this==null || scope.IsDisposed) throw new OperationCanceledException();
                var joint=new GameObject(binding.Node.Id.ToString(),typeof(RectTransform)).transform;joint.SetParent(motion,false);
                var image=new GameObject("Image",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).GetComponent<Image>();
                image.transform.SetParent(joint,false);image.sprite=sprite;image.raycastTarget=false;
                image.rectTransform.sizeDelta=part.Size;image.rectTransform.localPosition=part.ImageOffset;
                transforms.Add(joint);
            }
            Bones=transforms.ToArray(); Initialize();
        }
        // The caller supplies a transform whose sprite lifetime is owned by its ResourceScope.
        public void Attach(BodyNode node,Transform joint)
        {
            if(joint==null || joint==transform || transform.IsChildOf(joint) || bones.Values.Contains(joint)) throw new ArgumentException("Invalid joint binding");
            assembly.Add(node);bones.Add(node.Id,joint);Rebuild();
        }
        public BodyNode[] Detach(int id)
        {
            var removed=assembly.Detach(id);
            foreach(var n in removed) { bones[n.Id].gameObject.SetActive(false);bones.Remove(n.Id); }
            if(assembly.Count>0) Rebuild(); else { GetComponent<SkeletonView>().Stop();ClearClips();GetComponent<SkeletonView>().Motions=Array.Empty<MotionEntry>(); }
            return removed;
        }
        public void Initialize()
        {
            if(Initial.Length!=Bones.Length) throw new InvalidOperationException("Body binding count mismatch");
            var next=new BodyAssembly(Parts); var pending=Initial.ToList();
            while(pending.Count>0)
            {
                var ready=pending.Where(n=>n.Parent<0 || next.Nodes.Any(p=>p.Id==n.Parent)).ToArray();
                if(ready.Length==0) throw new InvalidOperationException("Missing parent or cyclic assembly");
                foreach(var n in ready) { next.Add(n); pending.Remove(n); }
            }
            bones.Clear(); for(int i=0;i<Initial.Length;i++) { if(Bones[i]==null) throw new InvalidOperationException("Missing body transform"); bones.Add(Initial[i].Id,Bones[i]); }
            assembly=next; Rebuild();
        }
        public void Reparent(int id,int parent,int parentPort,int port) { assembly.Reparent(id,parent,parentPort,port); Rebuild(); }
        public void MergeGroup(int root,int group) { assembly.MergeGroup(root,group); }
        public void Rebuild(int group=-1)
        {
            var driver=GetComponent<SkeletonView>(); driver.Stop(); ClearClips();
            foreach(var n in assembly.Nodes)
            {
                var t=bones[n.Id];
                if(n.Parent>=0) t.SetParent(bones[n.Parent],false);
                t.localPosition=assembly.LocalPosition(n.Id); t.localRotation=Quaternion.Euler(0,0,assembly.LocalAngle(n.Id));
            }
            var entries=new List<MotionEntry>();
            foreach(MotionId motion in Enum.GetValues(typeof(MotionId)))
            {
                float duration=Recipes.Where(r=>r.Motion==motion).Select(r=>r.Duration).DefaultIfEmpty(.65f).Max();
                var clip=new AnimationClip {name="Body_"+motion,frameRate=30,legacy=true}; owned.Add(clip);
                foreach(var n in assembly.Nodes)
                {
                    var definition=assembly.Definition(n.Id); int parentRole=n.Parent<0?0:assembly.Definition(n.Parent).Role;
                    var recipe=Recipes.FirstOrDefault(r=>r.Motion==motion && r.ChildRole==definition.Role && (r.ParentRole==0 || r.ParentRole==parentRole));
                    float angle=assembly.LocalAngle(n.Id),mid=0,end=0,peak=.45f;
                    if(recipe!=null && (group<0 || group==n.Group)) { mid=recipe.Middle;end=recipe.End;peak=Mathf.Clamp(recipe.Peak,.01f,.99f); }
                    var keys=new AnimationCurve(new Keyframe(0,angle),new Keyframe(duration*peak,angle+mid),new Keyframe(duration,angle+end));
                    for(int k=0;k<3;k++) keys.SmoothTangents(k,0);
                    clip.SetCurve(Path(bones[n.Id]),typeof(Transform),"localEulerAnglesRaw.z",keys);
                }
                clip.legacy=false;
                entries.Add(new MotionEntry {Id=motion,Clip=clip,Loop=motion==MotionId.Idle || motion==MotionId.Walk,Hold=motion==MotionId.Death,Impact=.45f});
            }
            driver.Motions=entries.ToArray(); if(isActiveAndEnabled) driver.Play(MotionId.Idle,0);
            (GetComponent<CombatBody>()??gameObject.AddComponent<CombatBody>()).BindAssembly(this);
        }
        string Path(Transform bone)
        {
            var names=new List<string>();
            while(bone!=transform) { if(bone==null) throw new InvalidOperationException("Bone outside assembly");names.Add(bone.name);bone=bone.parent; }
            names.Reverse();return string.Join("/",names);
        }
        // A merged subtree is a selectable animation unit; switching it regenerates bounded clips.
        public int PlayGroup(int group,MotionId motion,float blend=.12f) { Rebuild(group);return GetComponent<SkeletonView>().Play(motion,blend); }
        void ClearClips() { foreach(var clip in owned) { if(Application.isPlaying) Destroy(clip);else DestroyImmediate(clip); } owned.Clear(); }
        void OnDisable() { Release(); }
        void OnDestroy() { Release(); }
        public void Release() { GetComponent<SkeletonView>().Stop(); ClearClips(); assembly=null; bones.Clear(); GetComponent<SkeletonView>().Motions=Array.Empty<MotionEntry>(); }
    }
}


