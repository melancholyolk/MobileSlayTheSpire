using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;
using UnityEngine.U2D.IK;
using UnityEngine.UI;
using SpireAnimation;

// Import-time data is persisted in PNG .meta files and remains editable in Skinning Editor.
public static class WarriorSkinBuilder
{
    public const string Folder="Assets/GameResources/Characters/0/Skin";
    const float Ppu=100;
    [Serializable] public class SideProfile { public SkeletonAssetBuilder.Rig Rig; public Vector2[] EndPoints; public int[] Sorting; }
    [MenuItem("Tools/Spire/Warrior Skin/Rebuild Side View From Source")]
    public static void RebuildSideView() => BuildInternal(true);
    sealed class Joint { public string Name; public int Parent; public Vector2 Point; }
    static Transform Child(string name,Transform parent) { var t=new GameObject(name).transform; t.SetParent(parent,false); return t; }
    [MenuItem("Tools/Spire/Warrior Skin/Build Missing Assets")]
    public static void Build() => BuildInternal(false);
    static void BuildInternal(bool rebuild)
    {
        var profile=JsonUtility.FromJson<SideProfile>(File.ReadAllText("GameData/Art/WarriorSideSkin.json"));
        var rig=profile.Rig;
        Directory.CreateDirectory(Folder);
        var joints=new List<Joint>();
        foreach(var b in rig.Bones) joints.Add(new Joint {Name=b.Name,Parent=Array.FindIndex(rig.Bones,p=>p.Name==b.Parent),Point=new Vector2(b.X,1024-b.Y)});
        int endpoint=0;
        foreach(var name in new[]{"ForearmL","ForearmR","ShinL","ShinR"})
        {
            int parent=joints.FindIndex(j=>j.Name==name); var b=rig.Bones[parent];
            var direction=(joints[parent].Point-joints[joints[parent].Parent].Point).normalized;
            float length=Mathf.Clamp(Mathf.Max(b.Width,b.Height)*.72f,70,210);
            joints.Add(new Joint {Name=name+"End",Parent=parent,Point=new Vector2(profile.EndPoints[endpoint].x,1024-profile.EndPoints[endpoint++].y)});
        }
        for(int i=0;i<rig.Bones.Length;i++)
        {
            string path=Folder+"/"+(13020+i)+".png";
            if(!File.Exists(path)) File.Copy(rig.Folder+"/"+rig.Bones[i].Image+".png",path);
            AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.spritePixelsPerUnit=Ppu; importer.SaveAndReimport();
            var factory=new SpriteDataProviderFactories(); factory.Init();
            var provider=factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var rect=provider.GetSpriteRects().Single();
            var boneProvider=provider.GetDataProvider<ISpriteBoneDataProvider>();
            // Never overwrite hand-painted weights or hand-adjusted bind poses on a routine rebuild.
            if(!rebuild && boneProvider.GetBones(rect.spriteID).Count>0) continue;
            var b=rig.Bones[i];
            var bones=new List<SpriteBone>();
            foreach(var j in joints)
            {
                Vector2 position=j.Parent<0?j.Point-new Vector2(b.Left,1024-b.Top-b.Height):j.Point-joints[j.Parent].Point;
                var child=joints.FirstOrDefault(c=>c.Parent==bones.Count);
                bones.Add(new SpriteBone {name=j.Name,parentId=j.Parent,position=position,rotation=Quaternion.identity,length=child==null?25:Vector2.Distance(j.Point,child.Point)});
            }
            boneProvider.SetBones(rect.spriteID,bones);
            // Sample at <= 16 pixels so the shared elbow/knee transition has several vertex rows.
            int columns=Mathf.Max(4,Mathf.CeilToInt(b.Width/16f)),rows=Mathf.Max(4,Mathf.CeilToInt(b.Height/16f));
            var vertices=new List<Vertex2DMetaData>();var indices=new List<int>();var edges=new List<Vector2Int>();
            for(int y=0;y<=rows;y++) for(int x=0;x<=columns;x++)
            {
                var point=new Vector2(x*b.Width/(float)columns,y*b.Height/(float)rows);
                var global=point+new Vector2(b.Left,1024-b.Top-b.Height);
                vertices.Add(new Vertex2DMetaData {position=point,boneWeight=JointWeight(i,global,joints)});
                if(x<columns && y<rows) {int a=y*(columns+1)+x;indices.AddRange(new[]{a,a+columns+1,a+1,a+1,a+columns+1,a+columns+2});}
                if(x<columns&&(y==0||y==rows))edges.Add(new Vector2Int(y*(columns+1)+x,y*(columns+1)+x+1));
                if(y<rows&&(x==0||x==columns))edges.Add(new Vector2Int(y*(columns+1)+x,(y+1)*(columns+1)+x));
            }
            var mesh=provider.GetDataProvider<ISpriteMeshDataProvider>();mesh.SetVertices(rect.spriteID,vertices.ToArray());mesh.SetIndices(rect.spriteID,indices.ToArray());mesh.SetEdges(rect.spriteID,edges.ToArray());
            provider.Apply();importer.SaveAndReimport();
        }
        // The prefab is authored once; subsequent builds only refresh the resource catalog.
        if(rebuild || !File.Exists(Folder+"/13000.prefab")) BuildPrefab(rig,joints,profile);
        var catalogPath="Assets/GameResources/Animation/21.json";
        var catalog=JsonUtility.FromJson<ActorVisualCatalog>(File.ReadAllText(catalogPath));
        catalog.Actors.Single(a=>a.Owner==0).Prefab=13000;
        File.WriteAllText(catalogPath,JsonUtility.ToJson(catalog,true));AssetDatabase.ImportAsset(catalogPath);
        AssetDatabase.SaveAssets();ResourceCatalogBuilder.Build();
    }
    // Both sprites evaluate the SAME world-space field around a shared joint. At the joint
    // they are 50/50; towards either segment the corresponding bone smoothly takes over.
    static BoneWeight JointWeight(int owner,Vector2 point,List<Joint> joints)
    {
        int joint=-1;
        var name=joints[owner].Name;
        if(name.StartsWith("Forearm")||name.StartsWith("Shin")) joint=owner;
        else if(name.StartsWith("Arm")||name.StartsWith("Thigh"))
            joint=joints.FindIndex(j=>j.Parent==owner);
        if(joint<0) return new BoneWeight {boneIndex0=owner,weight0=1};
        int parent=joints[joint].Parent;
        int tip=joints.FindIndex(j=>j.Parent==joint);
        var incoming=(joints[joint].Point-joints[parent].Point).normalized;
        var outgoing=tip<0?incoming:(joints[tip].Point-joints[joint].Point).normalized;
        var tangent=(incoming+outgoing).normalized;
        if(tangent.sqrMagnitude<.001f)tangent=incoming;
        float radius=Mathf.Min(65,Vector2.Distance(joints[parent].Point,joints[joint].Point)*.4f);
        float t=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-radius,radius,Vector2.Dot(point-joints[joint].Point,tangent)));
        return new BoneWeight {boneIndex0=parent,weight0=1-t,boneIndex1=joint,weight1=t};
    }
    static void BuildPrefab(SkeletonAssetBuilder.Rig rig,List<Joint> joints,SideProfile profile)
    {
        var root=new GameObject("WarriorSkin",typeof(RectTransform),typeof(Animator),typeof(SkeletonView),typeof(SkinnedActorView));root.SetActive(false);
        try
        {
            var space=Child("RenderSpace",root.transform);
            var motion=Child("Motion",space);var controls=Child("Controls",motion);var skeleton=Child("Skeleton",motion);
            var transforms=new Transform[joints.Count];
            for(int i=0;i<joints.Count;i++)
            {
                var j=joints[i];var t=Child(j.Name,j.Parent<0?skeleton:transforms[j.Parent]);
                t.localPosition=(j.Point-(j.Parent<0?new Vector2(384,24):joints[j.Parent].Point))/Ppu;transforms[i]=t;
            }
            var manager=motion.gameObject.AddComponent<IKManager2D>();
            foreach(var name in new[]{"ForearmL","ForearmR","ShinL","ShinR"})
            {
                var end=transforms[joints.FindIndex(j=>j.Name==name+"End")];
                var target=Child(name+"Target",controls);target.position=end.position;
                var solver=Child(name+"IK",controls).gameObject.AddComponent<LimbSolver2D>();
                solver.GetChain(0).effector=end;solver.GetChain(0).target=target;solver.constrainRotation=false;
                // Side view: both elbows bend behind the hands; both knees bend forward.
                solver.flip=name.StartsWith("Forearm",StringComparison.Ordinal);solver.Initialize();manager.AddSolver(solver);
            }
            var materialPath=Folder+"/13080.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null) {material=new Material(Shader.Find("Sprites/Default"));AssetDatabase.CreateAsset(material,materialPath);}
            for(int i=0;i<rig.Bones.Length;i++)
            {
                var b=rig.Bones[i];var visual=Child("Skin_"+b.Name,motion);visual.gameObject.layer=30;
                visual.localPosition=new Vector3((b.Left+b.Width*.5f-384)/Ppu,(1000-b.Top-b.Height*.5f)/Ppu,0);
                var renderer=visual.gameObject.AddComponent<SpriteRenderer>();renderer.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Folder+"/"+(13020+i)+".png");
                renderer.sharedMaterial=material; renderer.sortingOrder=profile.Sorting[i];
                var skin=visual.gameObject.AddComponent<SpriteSkin>();skin.alwaysUpdate=true;
                var serialized=new SerializedObject(skin);serialized.FindProperty("m_RootBone").objectReferenceValue=transforms[0];
                var bindings=serialized.FindProperty("m_BoneTransforms");bindings.arraySize=transforms.Length;
                for(int j=0;j<transforms.Length;j++) bindings.GetArrayElementAtIndex(j).objectReferenceValue=transforms[j];
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var camera=Child("Camera",space).gameObject.AddComponent<Camera>();camera.transform.localPosition=new Vector3(0,5,-20);
            camera.orthographic=true;camera.orthographicSize=6;camera.nearClipPlane=.1f;camera.farClipPlane=40;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.cullingMask=1<<30;camera.enabled=false;camera.allowHDR=false;camera.allowMSAA=false;
            var display=new GameObject("SkinDisplay",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage)).GetComponent<RawImage>();display.transform.SetParent(root.transform,false);display.raycastTarget=false;
            display.rectTransform.sizeDelta=new Vector2(336,336);display.rectTransform.anchoredPosition=new Vector2(0,140);
            var view=root.GetComponent<SkinnedActorView>();view.RenderSpace=space;view.ActorCamera=camera;view.Display=display;
            var entries=new List<MotionEntry>();
            for(int i=0;i<10;i++)
            {
                var id=(MotionId)i;float duration=id==MotionId.Idle?2.4f:id==MotionId.Walk?1f:id==MotionId.Death?1.1f:.7f;
                var clip=new AnimationClip {name=(13001+i).ToString(),frameRate=30};
                string PathOf(Transform t)=>AnimationUtility.CalculateTransformPath(t,root.transform);
                void Curve(Transform t,string property,float start,float peak,float end)=>AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(PathOf(t),typeof(Transform),property),new AnimationCurve(new Keyframe(0,start),new Keyframe(duration*.45f,peak),new Keyframe(duration,end)));
                foreach(Transform target in controls) if(target.name.EndsWith("Target"))
                {
                    var p=target.localPosition;bool hand=target.name.StartsWith("Forearm"),right=target.name.Contains("RTarget");float dx=0,dy=0;
                    if(id==MotionId.Attack&&hand) {dx=right?1.8f:.35f;dy=right?.7f:.2f;}
                    if(id==MotionId.Guard&&hand) {dx=.3f;dy=right?1.1f:.7f;}
                    if((id==MotionId.Skill||id==MotionId.Victory)&&hand) {dx=right?.3f:-.3f;dy=1.4f;}
                    if(id==MotionId.Walk) {dx=right?.45f:-.45f;dy=hand?0:.25f;}
                    if(id==MotionId.Hit&&hand)dx=-.25f;
                    Curve(target,"m_LocalPosition.x",p.x,p.x+dx,p.x);Curve(target,"m_LocalPosition.y",p.y,p.y+dy,p.y);Curve(target,"m_LocalPosition.z",0,0,0);
                }
                float angle=id==MotionId.Hit?10:id==MotionId.Death?80:id==MotionId.Attack?-5:id==MotionId.Dodge?16:0;
                Curve(motion,"localEulerAnglesRaw.z",0,angle,id==MotionId.Death?85:0);
                Curve(motion,"m_LocalPosition.x",0,id==MotionId.Death?5*Mathf.Sin(80*Mathf.Deg2Rad):id==MotionId.Attack?.2f:id==MotionId.Dodge?-.5f:0,id==MotionId.Death?5*Mathf.Sin(85*Mathf.Deg2Rad):0);
                Curve(motion,"m_LocalPosition.y",0,id==MotionId.Death?5-5*Mathf.Cos(80*Mathf.Deg2Rad):id==MotionId.Spawn?.3f:0,id==MotionId.Death?5-5*Mathf.Cos(85*Mathf.Deg2Rad):0);
                var pelvis=transforms[0];float breath=id==MotionId.Idle?.06f:id==MotionId.Walk?.12f:0;
                Curve(pelvis,"m_LocalPosition.y",pelvis.localPosition.y,pelvis.localPosition.y+breath,pelvis.localPosition.y);
                var head=transforms[2];Curve(head,"localEulerAnglesRaw.z",0,id==MotionId.Hit?-8:id==MotionId.Idle?2:0,0);
                var clipPath=Folder+"/"+(13001+i)+".anim";
                var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if(existing!=null) {EditorUtility.CopySerialized(clip,existing);UnityEngine.Object.DestroyImmediate(clip);clip=existing;EditorUtility.SetDirty(clip);}
                else AssetDatabase.CreateAsset(clip,clipPath);
                entries.Add(new MotionEntry {Id=id,Clip=clip,Loop=id==MotionId.Idle||id==MotionId.Walk,Hold=id==MotionId.Death});
            }
            root.GetComponent<SkeletonView>().Motions=entries.ToArray();
            PrefabUtility.SaveAsPrefabAsset(root,Folder+"/13000.prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    [MenuItem("Tools/Spire/Warrior Skin/Open Prefab")]
    static void Open() { AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/13000.prefab")); }
}
