using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace SpireAnimation
{
    // SpriteSkin/IK remains a normal editable world rig. This bounded render target
    // bridges it into the existing overlay UI without replacing gameplay's camera.
    [DefaultExecutionOrder(20000)]
    public sealed class SkinnedActorView : MonoBehaviour
    {
        public Transform RenderSpace;
        public Camera ActorCamera;
        public RawImage Display;
        public const int MaxActiveActors=8;
        static readonly HashSet<int> slots=new HashSet<int>();
        int slot=-1;
        RenderTexture texture;
        public RenderTexture Texture=>texture;
        public static int ActiveTargets=>slots.Count;
        void OnEnable()
        {
            if(!Application.isPlaying) return;
            for(int i=0;i<MaxActiveActors;i++) if(slots.Add(i)) { slot=i; break; }
            if(slot<0) { enabled=false; throw new InvalidOperationException("Skinned actor render capacity exceeded"); }
            try
            {
                texture=new RenderTexture(512,512,16,RenderTextureFormat.ARGB32) {name="ActorSkin",antiAliasing=1};
                texture.Create(); ActorCamera.targetTexture=texture; Display.texture=texture;
                PositionSpace();
            }
            catch { Release(); throw; }
        }
        void PositionSpace()
        {
            RenderSpace.position=new Vector3(10000+slot*100,0,0);
            RenderSpace.rotation=Quaternion.identity;
            var scale=RenderSpace.parent.lossyScale;
            RenderSpace.localScale=new Vector3(1/Mathf.Max(.0001f,Mathf.Abs(scale.x)),1/Mathf.Max(.0001f,Mathf.Abs(scale.y)),1/Mathf.Max(.0001f,Mathf.Abs(scale.z)));
        }
        void LateUpdate()
        {
            if(texture==null) return;
            ActorCamera.Render();
        }
        void Release()
        {
            if(ActorCamera!=null) ActorCamera.targetTexture=null;
            if(Display!=null) Display.texture=null;
            if(texture!=null) { texture.Release(); Destroy(texture); texture=null; }
            if(slot>=0) slots.Remove(slot); slot=-1;
        }
        void OnDisable() { Release(); }
        void OnDestroy() { Release(); }
    }
}
