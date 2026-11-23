using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace SpireAnimation
{
    // SpriteSkin/IK remains a normal editable world rig. This bounded render target
    // bridges it into the existing overlay UI without replacing gameplay's camera.
    [ExecuteAlways, DefaultExecutionOrder(20000)]
    public sealed class SkinnedActorView : MonoBehaviour
    {
        public Transform RenderSpace;
        public Camera ActorCamera;
        public RawImage Display;
        public const int MaxActiveActors=8;
        static readonly HashSet<int> slots=new HashSet<int>();
        int slot=-1;
        RenderTexture texture;
#if UNITY_EDITOR
        double nextPreview;
        Texture previousDisplay;
        RenderTexture previousTarget;
        ulong previousCullingMask;
#endif
        public RenderTexture Texture=>texture;
        public static int ActiveTargets=>slots.Count;
        void OnEnable()
        {
            if(!Application.IsPlaying(gameObject))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.update-=PreviewUpdate;
                UnityEditor.EditorApplication.update+=PreviewUpdate;
#endif
                return;
            }
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
            if(!Application.IsPlaying(gameObject) || texture==null) return;
            ActorCamera.Render();
        }
#if UNITY_EDITOR
        void PreviewUpdate()
        {
            if(this==null || !isActiveAndEnabled || Application.IsPlaying(gameObject) ||
                UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode ||
                UnityEditor.EditorUtility.IsPersistent(this) || !gameObject.scene.IsValid() ||
                RenderSpace==null || ActorCamera==null || Display==null) return;
            if(UnityEditor.EditorApplication.timeSinceStartup<nextPreview) return;
            nextPreview=UnityEditor.EditorApplication.timeSinceStartup+1.0/20;
            if(texture==null)
            {
                previousDisplay=Display.texture;
                texture=new RenderTexture(512,512,16,RenderTextureFormat.ARGB32)
                    {name="ActorSkin Preview",hideFlags=HideFlags.HideAndDontSave,antiAliasing=1};
                texture.Create();
            }
            // Prefab Mode has its own scene culling mask. Render the editable rig in
            // place: preview must not serialize runtime relocation or camera settings.
            previousTarget=ActorCamera.targetTexture;
            previousCullingMask=ActorCamera.overrideSceneCullingMask;
            try
            {
                ActorCamera.overrideSceneCullingMask=UnityEditor.SceneManagement.EditorSceneManager.GetSceneCullingMask(gameObject.scene);
                ActorCamera.targetTexture=texture;
                ActorCamera.Render();
                Display.texture=texture;
            }
            finally
            {
                ActorCamera.targetTexture=previousTarget;
                ActorCamera.overrideSceneCullingMask=previousCullingMask;
            }
            UnityEditor.SceneView.RepaintAll();
        }
#endif
        void Release()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update-=PreviewUpdate;
            if(!Application.IsPlaying(gameObject))
            {
                if(Display!=null && Display.texture==texture) Display.texture=previousDisplay;
                if(texture!=null) { texture.Release(); DestroyImmediate(texture); texture=null; }
                if(slot>=0) slots.Remove(slot);slot=-1;
                return;
            }
#endif
            if(ActorCamera!=null) ActorCamera.targetTexture=null;
            if(Display!=null) Display.texture=null;
            if(texture!=null) { texture.Release(); Destroy(texture); texture=null; }
            if(slot>=0) slots.Remove(slot); slot=-1;
        }
        void OnDisable() { Release(); }
        void OnDestroy() { Release(); }
    }
}
