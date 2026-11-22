using UnityEngine;

namespace SpireAnimation
{
    // Remove last frame's additive pose before animation, apply this frame before IK (-2).
    [DefaultExecutionOrder(-3)]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class BodyPartHurtbox : MonoBehaviour
    {
        public int NodeId;
        public CombatBody Owner;
        public Transform ReactionTarget;
        public BoxCollider2D Collider=>GetComponent<BoxCollider2D>();
        Vector3 applied,impulse;
        float elapsed,duration;
        public void React(Vector3 worldOffset,float seconds)
        {
            Restore();
            var target=ReactionTarget!=null?ReactionTarget:transform;
            impulse=target.parent==null?worldOffset:target.parent.InverseTransformVector(worldOffset);
            elapsed=0; duration=Mathf.Max(.01f,seconds);
        }
        void Restore()
        {
            var target=ReactionTarget!=null?ReactionTarget:transform;
            target.localPosition-=applied; applied=Vector3.zero;
        }
        void Update() { Restore(); }
        void LateUpdate()
        {
            if(duration<=0) return;
            elapsed+=Time.unscaledDeltaTime;
            if(elapsed>=duration) { duration=0; return; }
            applied=impulse*Mathf.Sin(Mathf.PI*elapsed/duration);
            (ReactionTarget!=null?ReactionTarget:transform).localPosition+=applied;
        }
        void OnDisable() { Restore(); duration=0; }
    }
}
