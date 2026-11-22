using UnityEngine;
using UnityEngine.UI;
namespace SpirePresentation
{
    // Layout values live on the prefab; overlap only when the available width is exhausted.
    [AddComponentMenu("Layout/Battle Hand Layout")]
    public sealed class BattleHandLayout : LayoutGroup
    {
        [SerializeField] Vector2 cardSize;
        [SerializeField] float spacing;
        public override void CalculateLayoutInputHorizontal() { base.CalculateLayoutInputHorizontal(); SetLayoutInputForAxis(padding.horizontal, padding.horizontal+rectChildren.Count*(cardSize.x+spacing),1,0); }
        public override void CalculateLayoutInputVertical() { SetLayoutInputForAxis(padding.vertical+cardSize.y,padding.vertical+cardSize.y,0,1); }
        public override void SetLayoutHorizontal() { Arrange(); }
        public override void SetLayoutVertical() { Arrange(); }
        void Arrange()
        {
            int count=rectChildren.Count; if(count==0) return;
            float width=Mathf.Max(0,rectTransform.rect.width-padding.horizontal);
            float height=Mathf.Max(0,rectTransform.rect.height-padding.vertical);
            float scale=Mathf.Min(1,Mathf.Min(width/Mathf.Max(1,cardSize.x),height/Mathf.Max(1,cardSize.y)));
            var size=cardSize*scale;
            float step=count>1?Mathf.Min(size.x+spacing,Mathf.Max(0,width-size.x)/(count-1)):0;
            float x=GetStartOffset(0,size.x+step*(count-1));
            float y=GetStartOffset(1,size.y);
            for(int i=0;i<count;i++) { SetChildAlongAxis(rectChildren[i],0,x+i*step,size.x); SetChildAlongAxis(rectChildren[i],1,y,size.y); }
        }
    }
}
