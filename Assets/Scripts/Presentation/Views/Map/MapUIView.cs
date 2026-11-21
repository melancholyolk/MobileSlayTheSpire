using System.Collections.Generic;
using System.Linq;
using Spire;
using SpireUI;
using UnityEngine;
using UnityEngine.UI;
namespace SpirePresentation
{
    public sealed class MapUIView : BaseUIView
    {
        public const int Path=4;
        [SerializeField] RectTransform content;
        [SerializeField] ScrollRect scroll;
        [SerializeField] Text status;
        [SerializeField] Button back;
        [SerializeField] Image background;
        MapTheme theme; GameApp app; Run built;
        readonly Dictionary<int,(Button button,Image icon,Text label)> nodes=new Dictionary<int,(Button,Image,Text)>();
        readonly List<(int from,int to,CurvedRoad road)> roads=new List<(int,int,CurvedRoad)>();
        readonly List<UiElement> dynamicElements=new List<UiElement>();
        public void Bind(GameApp owner,MapTheme style) { app=owner; theme=style; background.sprite=style.Background; }
        protected override void OnInitialize() { back.onClick.AddListener(()=>app.ReturnToMenu()); }
        Vector2 Position(MapNode node)=>new Vector2(node.Kind==RoomKind.Boss?550:300+node.Lane*250+(node.Floor%2==0?-26:26),80+node.Floor*135);
        void Track(GameObject go) { var e=go.AddComponent<UiElement>(); Register(e); dynamicElements.Add(e); }
        public void Refresh(Run run)
        {
            if(built!=run) Build(run);
            var choices=new HashSet<int>(run.Choices); var possible=new HashSet<int>(choices); var queue=new Queue<int>(choices);
            while(queue.Count>0) foreach(int next in run.Map[queue.Dequeue()].Next) if(possible.Add(next)) queue.Enqueue(next);
            foreach(var node in run.Map)
            {
                var item=nodes[node.Id]; bool visited=run.Route.Contains(node.Id),reachable=choices.Contains(node.Id);
                item.button.interactable=reachable && (app.Network==null || app.Network.Connected) && run.Heroes[app.Player].Alive;
                item.icon.color=visited?theme.Visited:reachable?theme.Available:possible.Contains(node.Id)?theme.Future:theme.Blocked;
                item.label.text=(visited?"✓ ":"")+(node.Floor+1)+" · "+RoomName(node.Kind)+(run.Votes.Values.Contains(node.Id)?"  ◉":"");
                item.label.color=item.icon.color;
            }
            foreach(var edge in roads)
            {
                int index=run.Route.IndexOf(edge.from); bool walked=index>=0 && index+1<run.Route.Count && run.Route[index+1]==edge.to;
                edge.road.color=walked?theme.Visited:possible.Contains(edge.to) && (edge.from==run.Current||possible.Contains(edge.from))?theme.Future:theme.Blocked;
                edge.road.Width=walked?9:5; edge.road.SetVerticesDirty();
            }
            status.text="选择下一处房间  ·  铜色：已走过  青色：可前往  灰色：不可到达"+(app.Network!=null?"\n联机路线投票："+run.Votes.Count+" / "+run.Heroes.Count:"");
        }
        static string RoomName(RoomKind kind)=>new[]{"战斗","精英","营火","商店","事件","首领"}[(int)kind];
        void Build(Run run)
        {
            foreach(var e in dynamicElements.ToArray()) if(e!=null) Unregister(e); dynamicElements.Clear();
            foreach(Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            nodes.Clear(); roads.Clear(); built=run;
            foreach(var node in run.Map)
                foreach(int next in node.Next)
                {
                    var road=new GameObject("Road_"+node.Id+"_"+next,typeof(RectTransform),typeof(CanvasRenderer),typeof(CurvedRoad)).GetComponent<CurvedRoad>();
                    road.transform.SetParent(content,false); var r=road.rectTransform; r.anchorMin=r.anchorMax=r.pivot=Vector2.zero; r.anchoredPosition=Vector2.zero; r.sizeDelta=content.sizeDelta;
                    road.Start=Position(node); road.End=Position(run.Map[next]); road.RoadTexture=theme.Road; road.raycastTarget=false; Track(road.gameObject);
                    roads.Add((node.Id,next,road));
                }
            foreach(var node in run.Map)
            {
                int id=node.Id; var go=new GameObject("Node_"+id,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Button)); go.transform.SetParent(content,false);
                var r=(RectTransform)go.transform; r.anchorMin=r.anchorMax=Vector2.zero; r.anchoredPosition=Position(node); r.sizeDelta=new Vector2(76,76);
                var image=go.GetComponent<Image>(); image.sprite=theme.Rooms[(int)node.Kind]; image.preserveAspect=true;
                var button=go.GetComponent<Button>(); button.transition=Selectable.Transition.None; button.onClick.AddListener(()=>app.Send("vote",id)); Track(go);
                var label=new GameObject("Label",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text)).GetComponent<Text>(); label.transform.SetParent(go.transform,false); label.font=UiFonts.Body; label.fontSize=15; label.alignment=TextAnchor.MiddleCenter;
                label.rectTransform.sizeDelta=new Vector2(210,30); label.rectTransform.anchoredPosition=new Vector2(0,-51); label.raycastTarget=false; Track(label.gameObject);
                nodes.Add(id,(button,image,label));
            }
            scroll.verticalNormalizedPosition=0;
        }
    }
}
