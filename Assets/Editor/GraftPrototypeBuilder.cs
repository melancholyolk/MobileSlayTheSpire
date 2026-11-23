using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using SpireAnimation;

public static class GraftPrototypeBuilder
{
    [MenuItem("Tools/Spire/Graft Monster/Open Prototype")]
    public static void Open()
    {
        // Additive sandbox leaves the user's existing scene and unsaved objects intact.
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        var canvas=new GameObject("GraftMonsterSandbox",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvas,scene);
        canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        canvas.GetComponent<Canvas>().sortingOrder=100;
        var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);
        var backdrop=new GameObject("Backdrop",typeof(RectTransform),typeof(Image)).GetComponent<Image>();backdrop.transform.SetParent(canvas.transform,false);
        backdrop.rectTransform.anchorMin=Vector2.zero;backdrop.rectTransform.anchorMax=Vector2.one;backdrop.rectTransform.sizeDelta=Vector2.zero;
        backdrop.color=new Color(.055f,.065f,.085f);backdrop.raycastTarget=false;
        var actor=new GameObject("Graftkeeper",typeof(RectTransform),typeof(GraftMonsterPrototype));actor.transform.SetParent(canvas.transform,false);
        ((RectTransform)actor.transform).anchoredPosition=new Vector2(140,20);
        Selection.activeGameObject=actor;
    }
}
