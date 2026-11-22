using System;
using UnityEngine;

namespace SpireAnimation
{
    [Serializable]
    public struct AttackProfile
    {
        public Vector2 Size,Offset;
        public float Angle,Windup,ActiveTime,Recovery;
        public Vector2 Reaction;
        public float ReactionDuration;
        public static AttackProfile Default=>new AttackProfile {
            Size=new Vector2(90,90),Windup=.18f,ActiveTime=.08f,Recovery=.18f,
            Reaction=new Vector2(.06f,.02f),ReactionDuration=.24f };
    }
    [Serializable]
    public struct CardAttackProfile
    {
        public string CardId;
        public AttackProfile Profile;
    }
    // Value-only oriented box. It never creates an attack GameObject or Collider.
    public readonly struct AttackHitbox
    {
        public readonly Vector2 Center,Size;
        public readonly float Angle;
        public AttackHitbox(Vector2 center,Vector2 size,float angle=0)
        { Center=center; Size=new Vector2(Mathf.Abs(size.x),Mathf.Abs(size.y)); Angle=angle; }
        public void Draw(Color color)
        {
            var matrix=Gizmos.matrix; var previous=Gizmos.color;
            Gizmos.matrix=Matrix4x4.TRS(Center,Quaternion.Euler(0,0,Angle),Vector3.one);
            Gizmos.color=color; Gizmos.DrawWireCube(Vector3.zero,new Vector3(Size.x,Size.y,.01f));
            Gizmos.matrix=matrix; Gizmos.color=previous;
        }
    }
}
