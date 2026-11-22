using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpireAnimation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
public static class SkeletonAssetBuilder
{
    [Serializable] public class Rigs { public Rig[] Items; }
    [Serializable] public class Rig { public string Name,Folder; public int Index,ResourceId,Owner,MotionCount; public Bone[] Bones; }
    [Serializable] public class Bone { public string Name,Parent; public float X,Y; public int Left,Top,Width,Height,Image; }
    const float Scale=.28f;
    [MenuItem("Tools/Spire/Build 2D Skeleton Assets")]
    public static void Build()
    {
        var rigs=JsonUtility.FromJson<Rigs>(File.ReadAllText("GameData/Art/Rigs.json")).Items;
        foreach(var rig in rigs) if(rig.Owner==0) WarriorSkinBuilder.Build(); else Build(rig);
        Directory.CreateDirectory("Assets/GameResources/Animation");
        var config=new ActorVisualCatalog {Actors=rigs.Select(r=>new ActorVisual {Owner=r.Owner,Prefab=r.Owner==0?13000:r.ResourceId}).ToArray()};
        File.WriteAllText("Assets/GameResources/Animation/21.json",JsonUtility.ToJson(config,true));
        AssetDatabase.ImportAsset("Assets/GameResources/Animation/21.json");
        Directory.CreateDirectory("Assets/GameResources/UI/Shared/Effects");
        var ghost=new GameObject("CardEffect",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(CanvasGroup),typeof(BetterAnimation));
        ghost.GetComponent<RectTransform>().sizeDelta=new Vector2(90,130); ghost.GetComponent<Image>().raycastTarget=false;
        ghost.SetActive(false); PrefabUtility.SaveAsPrefabAsset(ghost,"Assets/GameResources/UI/Shared/Effects/22.prefab"); UnityEngine.Object.DestroyImmediate(ghost);
        AssetDatabase.SaveAssets(); ResourceCatalogBuilder.Build();
    }
    static void Build(Rig rig)
    {
        foreach(var bone in rig.Bones)
        {
            string path=rig.Folder+"/"+bone.Image+".png"; AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path); importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Single; importer.alphaIsTransparency=true; importer.mipmapEnabled=false;
            importer.isReadable=false; importer.maxTextureSize=1024; importer.textureCompression=TextureImporterCompression.Compressed; importer.SaveAndReimport();
        }
        var root=new GameObject(rig.Name,typeof(RectTransform),typeof(Animator),typeof(SkeletonView)); root.SetActive(false);
        var rootRect=root.GetComponent<RectTransform>(); rootRect.sizeDelta=Vector2.zero;
        var motion=new GameObject("Motion",typeof(RectTransform)).GetComponent<RectTransform>(); motion.SetParent(root.transform,false); motion.sizeDelta=Vector2.zero;
        var transforms=new Dictionary<string,RectTransform>(); var paths=new Dictionary<string,string>();
        foreach(var bone in rig.Bones)
        {
            var t=new GameObject(bone.Name,typeof(RectTransform)).GetComponent<RectTransform>(); transforms.Add(bone.Name,t);
            var parent=string.IsNullOrEmpty(bone.Parent)?motion:transforms[bone.Parent]; t.SetParent(parent,false); t.sizeDelta=Vector2.zero;
            var p=rig.Bones.FirstOrDefault(b=>b.Name==bone.Parent);
            t.localPosition=new Vector3((bone.X-(p==null?384:p.X))*Scale,((p==null?1000:p.Y)-bone.Y)*Scale,0);
            paths[bone.Name]=AnimationUtility.CalculateTransformPath(t,root.transform);
            var visual=new GameObject("Image",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).GetComponent<Image>();
            visual.transform.SetParent(t,false); visual.raycastTarget=false; visual.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(rig.Folder+"/"+bone.Image+".png");
            var rect=visual.rectTransform; rect.sizeDelta=new Vector2(bone.Width*Scale,bone.Height*Scale);
            rect.localPosition=new Vector3((bone.Left+bone.Width*.5f-bone.X)*Scale,(bone.Y-bone.Top-bone.Height*.5f)*Scale,0);
        }
        // Rear legs first; torso and front arms above. Bone hierarchy keeps sprite ownership explicit.
        transforms["ThighL"].SetAsFirstSibling(); transforms["ThighR"].SetAsFirstSibling();
        var motions=new List<MotionEntry>();
        int count=rig.MotionCount;
        for(int i=0;i<count;i++)
        {
            var id=(MotionId)i; float duration=id==MotionId.Idle?2.4f:id==MotionId.Death?1.0f:id==MotionId.Walk?1f:.65f;
            var clip=new AnimationClip {name=id.ToString(),frameRate=30};
            foreach(var bone in rig.Bones)
                Curve(clip,paths[bone.Name],"localEulerAnglesRaw.z",duration,0,0,0);
            Curve(clip,"Motion","localEulerAnglesRaw.z",duration,0,0,0);
            Curve(clip,"Motion","m_LocalPosition.x",duration,0,0,0);
            Curve(clip,"Motion","m_LocalPosition.y",duration,0,0,0);
            float strength=rig.Owner<1000?1: .75f+(rig.Index%4)*.12f;
            void Rotate(string bone,float mid,float end=0)=>Curve(clip,paths[bone],"localEulerAnglesRaw.z",duration,0,mid*strength,end*strength);
            switch(id)
            {
                case MotionId.Idle: Rotate("Torso",2); Rotate("Head",-3); Rotate("ArmL",-3); Rotate("ArmR",3); Curve(clip,"Motion","m_LocalPosition.y",duration,0,3,0); break;
                case MotionId.Attack:
                    Rotate("Torso",-12); Rotate("Head",9); Rotate("ArmR",rig.Index==1? -80:-65); Rotate("ForearmR",rig.Index==1?60:-70);
                    Rotate("ArmL",rig.Index==1?75:15); Rotate("ForearmL",rig.Index==1?-35:25);
                    Rotate("ThighL",-8); Rotate("ShinR",12); Curve(clip,"Motion","m_LocalPosition.x",duration,0,26,0); break;
                case MotionId.Hit: Rotate("Torso",15); Rotate("Head",14); Rotate("ForearmL",-18); Curve(clip,"Motion","m_LocalPosition.x",duration,0,-18,0); break;
                case MotionId.Death:
                    Rotate("Torso",-20,-25); Rotate("Head",-15,-20); Rotate("ThighR",30,45); Rotate("ShinL",-35,-50);
                    Curve(clip,"Motion","localEulerAnglesRaw.z",duration,0,35,82); Curve(clip,"Motion","m_LocalPosition.y",duration,0,-35,-48); break;
                case MotionId.Guard: Rotate("ForearmL",-75); Rotate("ArmL",-20); Rotate("ForearmR",60); Rotate("Torso",8); break;
                case MotionId.Walk: Rotate("ThighL",22,-22); Rotate("ThighR",-22,22); Rotate("ShinL",-25); Rotate("ShinR",25); Rotate("ArmL",-15,15); Rotate("ArmR",15,-15); break;
                case MotionId.Skill: Rotate("ArmL",65); Rotate("ArmR",-65); Rotate("ForearmL",-45); Rotate("ForearmR",45); Rotate("Head",-10); break;
                case MotionId.Dodge: Rotate("Torso",28); Rotate("ThighL",-25); Rotate("ShinL",45); Curve(clip,"Motion","m_LocalPosition.x",duration,0,-50,0); Curve(clip,"Motion","m_LocalPosition.y",duration,0,-22,0); break;
                case MotionId.Victory: Rotate("ArmR",-140); Rotate("ForearmR",-20); Rotate("Head",-10); Rotate("ArmL",20); break;
                case MotionId.Spawn: Curve(clip,"Motion","m_LocalPosition.y",duration,-40,8,0); Rotate("Torso",-8); break;
            }
            var settings=AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime=id==MotionId.Idle||id==MotionId.Walk; AnimationUtility.SetAnimationClipSettings(clip,settings);
            string path=rig.Folder+"/"+(rig.ResourceId+1+i)+".anim";
            var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(existing==null) AssetDatabase.CreateAsset(clip,path); else { EditorUtility.CopySerialized(clip,existing); UnityEngine.Object.DestroyImmediate(clip); clip=existing; }
            motions.Add(new MotionEntry {Id=id,Clip=clip,Loop=settings.loopTime,Hold=id==MotionId.Death,Impact=id==MotionId.Attack?.42f:.5f});
        }
        root.GetComponent<SkeletonView>().Motions=motions.ToArray();
        PrefabUtility.SaveAsPrefabAsset(root,rig.Folder+"/"+rig.ResourceId+".prefab"); UnityEngine.Object.DestroyImmediate(root);
    }
    static void Curve(AnimationClip clip,string path,string property,float length,float start,float middle,float end)
    {
        var curve=new AnimationCurve(new Keyframe(0,start),new Keyframe(length*.45f,middle),new Keyframe(length,end));
        for(int i=0;i<3;i++) curve.SmoothTangents(i,0);
        clip.SetCurve(path,typeof(Transform),property,curve);
    }
}
