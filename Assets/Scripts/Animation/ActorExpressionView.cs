using UnityEngine;
using Spire;

namespace SpireAnimation
{
    public enum FacialExpression
    {
        Neutral = 0,
        Pain = 1,
        Happy = 2,
        Focus = 3
    }

    // Sprites carry the same imported skin topology/bind pose as the neutral head.
    // Prefab dependencies are owned and released by the actor's ResourceScope.
    public sealed class ActorExpressionView : MonoBehaviour
    {
        public SpriteRenderer Head;
        public Sprite Neutral, Pain, Happy, Focus;
        public float ReactionDuration = .8f;
        public FacialExpression Current { get; private set; }

        float remaining;
        bool dead;
        public void Apply(BattleCue cue)
        {
            if (cue.Kind == BattleCueKind.Death)
            {
                dead = true;
                Show(FacialExpression.Pain, float.PositiveInfinity);
                return;
            }

            if (dead)
                return;
            if (cue.Kind == BattleCueKind.Hit && cue.Amount > 0)
                Show(FacialExpression.Pain, ReactionDuration);
            else if (cue.Kind == BattleCueKind.Heal && cue.Amount > 0)
                Show(FacialExpression.Happy, ReactionDuration);
            else if (cue.Kind == BattleCueKind.Attack || cue.Kind == BattleCueKind.Guard)
            {
                if (Current != FacialExpression.Pain && Current != FacialExpression.Happy)
                    Show(FacialExpression.Focus, ReactionDuration);
            }
        }

        void Show(FacialExpression expression, float duration)
        {
            Current = expression;
            remaining = duration;
            if (Head != null)
                Head.sprite = expression == FacialExpression.Pain ? Pain ?? Neutral : expression == FacialExpression.Happy ? Happy ?? Neutral : expression == FacialExpression.Focus ? Focus ?? Neutral : Neutral;
        }

        public void Tick(float delta)
        {
            if (delta < 0 || dead || Current == FacialExpression.Neutral)
                return;
            remaining -= delta;
            if (remaining <= 0)
                Show(FacialExpression.Neutral, 0);
        }

        public void ResetExpression()
        {
            dead = false;
            Show(FacialExpression.Neutral, 0);
        }

        void OnEnable() => ResetExpression();
        void OnDisable() => ResetExpression();
        void Update() => Tick(Time.unscaledDeltaTime);
    }
}
