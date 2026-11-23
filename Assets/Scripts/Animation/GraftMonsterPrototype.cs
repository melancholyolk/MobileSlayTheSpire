using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D.IK;

namespace SpireAnimation
{
    // Self-contained encounter sandbox: no changes to the production encounter table.
    public sealed class GraftMonsterPrototype : MonoBehaviour
    {
        public GraftTemplate[] Library={
            new GraftTemplate {Name="Ripper",Hp=24,Power=9,Color=new Color(.8f,.28f,.2f),Skill=GraftSkill.Slash},
            new GraftTemplate {Name="Bulwark",Hp=40,Power=8,Length=56,IdleLift=10,Color=new Color(.25f,.6f,.75f),Skill=GraftSkill.Guard,DeathDuration=1.1f},
            new GraftTemplate {Name="Suture",Hp=18,Power=7,Length=72,IdleLift=28,Color=new Color(.45f,.75f,.38f),Skill=GraftSkill.Mend}
        };
        public GraftMonsterState State {get; private set;}
        public CombatBody Body {get; private set;}
        public int DetachedCount {get; private set;}
        public bool AutoAct;
        public float ActionInterval=2.5f;
        public string LastEvent {get; private set;}="Empty sockets are ready for grafting.";
        public IReadOnlyDictionary<int,Transform> Bones=>bones;
        sealed class LimbView
        {
            public GraftUnit Unit;public Transform Mount,Target;public SkeletonView Animation;
            public LimbSolver2D Solver;public IKManager2D Manager;
        }
        readonly Dictionary<int,Transform> bones=new Dictionary<int,Transform>();
        readonly Dictionary<int,LimbView> views=new Dictionary<int,LimbView>();
        readonly List<AnimationClip> clips=new List<AnimationClip>();
        readonly List<GameObject> detached=new List<GameObject>();
        SkeletonView baseAnimation;
        float actionClock;
        int selected=-1,turn;
        Vector2 scroll;
        Font font;
        void Start() { if(State==null) Initialize(); }
        public void Initialize()
        {
            if(State!=null) return;
            State=new GraftMonsterState(Library);
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Body=gameObject.AddComponent<CombatBody>();
            baseAnimation=gameObject.AddComponent<SkeletonView>();
            BuildBone(0,transform,new Color(.36f,.31f,.43f));BuildBone(1,bones[0],new Color(.65f,.57f,.68f));
            // The base graph contains only original bones. Grafting never rebuilds it.
            baseAnimation.Motions=CreateMotions(transform,bones[0],false,0,0,.9f);
            baseAnimation.Play(MotionId.Idle,0);
            Label("Face",bones[1],"o   o\n  v",new Vector2(0,5),new Vector2(55,45));
        }
        Transform BuildBone(int node,Transform parent,Color color)
        {
            var definition=State.Assembly.Definition(node);
            var bone=new GameObject("Bone_"+node,typeof(RectTransform)).transform;
            bone.SetParent(parent,false);bone.localPosition=State.Assembly.LocalPosition(node);
            bone.localRotation=Quaternion.Euler(0,0,State.Assembly.LocalAngle(node));bones[node]=bone;
            var image=new GameObject("Shape",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(bone,false);image.color=color;image.raycastTarget=false;
            image.rectTransform.sizeDelta=definition.Size;image.rectTransform.localPosition=definition.ImageOffset;
            var hurt=bone.gameObject.AddComponent<BodyPartHurtbox>();hurt.NodeId=node;hurt.Owner=Body;
            hurt.Collider.size=definition.Size;hurt.Collider.offset=definition.ImageOffset;hurt.Collider.isTrigger=true;
            foreach(var port in definition.Ports.Where(p=>p.Id>0))
            {
                var marker=new GameObject("Socket_"+port.Id,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).GetComponent<Image>();
                marker.transform.SetParent(bone,false);marker.transform.localPosition=port.Position;marker.rectTransform.sizeDelta=Vector2.one*8;
                marker.color=Color.yellow;marker.raycastTarget=false;
            }
            return bone;
        }
        public GraftUnit Graft(int parent=-1)
        {
            var unit=State.Graft(parent);
            if(unit==null) { LastEvent="No compatible free socket (or node budget reached).";return null; }
            var mount=new GameObject("Graft_"+unit.Root,typeof(RectTransform)).transform;
            mount.SetParent(bones[unit.ParentNode],false);
            mount.localPosition=State.Assembly.LocalPosition(unit.Root);
            mount.localRotation=Quaternion.Euler(0,0,State.Assembly.LocalAngle(unit.Root));
            var upper=BuildBone(unit.Root,mount,unit.Template.Color);upper.localPosition=Vector3.zero;upper.localRotation=Quaternion.identity;
            var lower=BuildBone(unit.Root+1,upper,unit.Template.Color*.8f);
            var end=BuildBone(unit.Root+2,lower,unit.Template.Color);
            var target=new GameObject("EndTarget").transform;target.SetParent(mount,false);
            target.localPosition=new Vector3(unit.Template.Length*1.45f,unit.Template.IdleLift,0);
            var solver=mount.gameObject.AddComponent<LimbSolver2D>();
            solver.GetChain(0).effector=end;solver.GetChain(0).target=target;solver.constrainRotation=false;solver.Initialize();
            var manager=mount.gameObject.AddComponent<IKManager2D>();manager.AddSolver(solver);
            var animation=mount.gameObject.AddComponent<SkeletonView>();
            animation.Motions=CreateMotions(mount,target,true,unit.Template.Length*1.45f,unit.Template.IdleLift,unit.Template.DeathDuration);
            animation.Play(MotionId.Spawn,0);
            foreach(int id in unit.Nodes) bones[id].GetComponent<BodyPartHurtbox>().ReactionTarget=target;
            views.Add(unit.Root,new LimbView {Unit=unit,Mount=mount,Target=target,Animation=animation,Solver=solver,Manager=manager});
            selected=unit.Root;baseAnimation.Play(MotionId.Skill);
            LastEvent="Grafted "+unit.Template.Name+" #"+unit.Root+" onto bone "+unit.ParentNode+" socket "+unit.ParentPort;
            return unit;
        }
        public bool UseSkill(int root)
        {
            if(!State.UseSkill(root)) return false;
            var view=views[root];view.Animation.Play(view.Unit.Template.Skill==GraftSkill.Slash?MotionId.Attack:view.Unit.Template.Skill==GraftSkill.Guard?MotionId.Guard:MotionId.Skill);
            LastEvent=view.Unit.Template.Name+" used "+view.Unit.Template.Skill+" ("+view.Unit.Template.Power+")";return true;
        }
        public int Attack(AttackHitbox box,int damage)
        {
            int node=Body.Query(box);if(node<0) { LastEvent="Miss";return -1; }
            ReceiveHit(node,damage);return node;
        }
        public void ReceiveHit(int node,int damage)
        {
            if(!State.Alive || !bones.ContainsKey(node)) return;
            var owner=State.Owner(node);
            var hurt=bones[node].GetComponent<BodyPartHurtbox>();hurt.React(Vector3.right*9,.2f);
            var dead=State.Damage(node,damage);
            LastEvent=owner==null?"Host hit":owner.Template.Name+" hit: "+owner.Hp+" HP (host protected; no overflow)";
            foreach(var limb in dead)
            {
                var view=views[limb.Root];views.Remove(limb.Root);
                // Capture every subtree in world space before its ancestor starts falling.
                view.Mount.SetParent(transform.parent,true);
                foreach(int id in limb.Nodes)
                {
                    bones[id].GetComponent<BodyPartHurtbox>().enabled=false;
                    bones[id].GetComponent<BoxCollider2D>().enabled=false;bones.Remove(id);
                }
                detached.Add(view.Mount.gameObject);DetachedCount++;StartCoroutine(Fall(view));
            }
            if(!State.Alive)
            {
                AutoAct=false;baseAnimation.Play(MotionId.Death);
                foreach(var part in GetComponentsInChildren<BodyPartHurtbox>()) part.Collider.enabled=false;
            }
            else if(owner==null) baseAnimation.Play(MotionId.Hit);
        }
        IEnumerator Fall(LimbView view)
        {
            view.Animation.Play(MotionId.Death,0);
            float time=0,duration=view.Unit.Template.DeathDuration;
            var origin=view.Mount.position;var rotation=view.Mount.rotation;
            var images=view.Mount.GetComponentsInChildren<Graphic>();var colors=images.Select(i=>i.color).ToArray();
            while(time<duration)
            {
                time+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(time/Mathf.Max(.01f,duration));
                view.Mount.position=origin+transform.TransformVector(new Vector3(50*t,-180*t*t,0));
                view.Mount.rotation=rotation*Quaternion.Euler(0,0,-100*t);
                for(int i=0;i<images.Length;i++) { var c=colors[i];c.a*=1-t;images[i].color=c; }
                yield return null;
            }
            view.Animation.Stop();
            foreach(var motion in view.Animation.Motions) { clips.Remove(motion.Clip);Destroy(motion.Clip); }
            detached.Remove(view.Mount.gameObject);Destroy(view.Mount.gameObject);DetachedCount--;
        }
        MotionEntry[] CreateMotions(Transform root,Transform animated,bool limb,float x,float y,float deathDuration)
        {
            var entries=new List<MotionEntry>();
            string path=animated.name;
            foreach(MotionId id in Enum.GetValues(typeof(MotionId)))
            {
                float length=id==MotionId.Idle?1.8f:id==MotionId.Death?deathDuration:.65f;
                var clip=new AnimationClip {name=(limb?"Limb_":"Host_")+id,legacy=true};clips.Add(clip);
                float peak=id==MotionId.Attack?35:id==MotionId.Death?-55:id==MotionId.Guard?25:id==MotionId.Skill?22:8;
                void Curve(string property,float start,float middle,float end)
                { clip.SetCurve(path,typeof(Transform),property,new AnimationCurve(new Keyframe(0,start),new Keyframe(length*.5f,middle),new Keyframe(length,end))); }
                if(limb)
                {
                    Curve("localPosition.x",x,x+(id==MotionId.Attack?25:-8),id==MotionId.Death?x*.35f:x);
                    Curve("localPosition.y",y,y+peak,id==MotionId.Death?-45:y);
                }
                else Curve("localEulerAnglesRaw.z",0,id==MotionId.Death?55:peak*.35f,id==MotionId.Death?85:0);
                clip.legacy=false;entries.Add(new MotionEntry {Id=id,Clip=clip,Loop=id==MotionId.Idle,Hold=id==MotionId.Death});
            }
            return entries.ToArray();
        }
        void Update()
        {
            if(State==null || !State.Alive || !AutoAct || State.OpponentHp<=0) return;
            actionClock+=Time.unscaledDeltaTime;
            if(actionClock<Mathf.Max(.2f,ActionInterval)) return;
            actionClock=0;
            if(turn++%2==0 || State.Limbs.Count==0) Graft();
            else foreach(var limb in State.Limbs.ToArray()) UseSkill(limb.Root);
        }
        void Label(string name,Transform parent,string value,Vector2 position,Vector2 size)
        {
            var label=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(parent,false);label.rectTransform.anchoredPosition=position;label.rectTransform.sizeDelta=size;
            label.font=font;label.fontSize=18;label.text=value;label.alignment=TextAnchor.MiddleCenter;label.color=Color.black;label.raycastTarget=false;
        }
        void OnGUI()
        {
            if(State==null) return;
            GUILayout.BeginArea(new Rect(16,16,360,Screen.height-32),GUI.skin.box);
            GUILayout.Label("THE GRAFTKEEPER / interactive prototype");
            GUILayout.Label("Host "+State.Hp+" / 120    Opponent "+State.OpponentHp+" / 160");
            GUILayout.Label("Yellow squares = sockets. Each colored chain is a separate unit.");
            AutoAct=GUILayout.Toggle(AutoAct,"Auto: graft / each limb uses its skill");
            GUI.enabled=State.Alive;
            if(GUILayout.Button("Graft a compatible limb")) Graft();
            if(GUILayout.Button("Graft onto selected limb (recursive)"))
            { var limb=State.Limbs.FirstOrDefault(l=>l.Root==selected);if(limb!=null) Graft(limb.Root+1); }
            if(GUILayout.Button("Attack host: 15")) HitAt(0,15);
            scroll=GUILayout.BeginScrollView(scroll);
            foreach(var limb in State.Limbs.ToArray())
            {
                if(GUILayout.Button((selected==limb.Root?"> ":"")+limb.Template.Name+" #"+limb.Root+"  HP "+limb.Hp+" / "+limb.Template.Hp+"  Block "+limb.Block)) selected=limb.Root;
                GUILayout.BeginHorizontal();
                if(GUILayout.Button("Hit 12")) HitAt(limb.Root+2,12);
                if(GUILayout.Button(limb.Template.Skill.ToString())) UseSkill(limb.Root);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();GUI.enabled=true;GUILayout.Label(LastEvent);GUILayout.EndArea();
        }
        void HitAt(int node,int damage)
        {
            if(!bones.TryGetValue(node,out var bone)) return;
            Physics2D.SyncTransforms();var collider=bone.GetComponent<BoxCollider2D>();
            Attack(new AttackHitbox(collider.bounds.center,Vector2.one*8),damage);
        }
        void OnDestroy()
        {
            StopAllCoroutines();foreach(var go in detached) if(go!=null) Destroy(go);
            foreach(var clip in clips) if(clip!=null) Destroy(clip);
        }
    }
}
