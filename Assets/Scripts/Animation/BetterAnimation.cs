using System;
using System.Threading.Tasks;
using UnityEngine;

namespace SpireAnimation
{
    [Serializable]
    public sealed class UiMotion
    {
        public float Duration = .18f;
        public float FromAlpha = 0, ToAlpha = 1;
        public Vector2 FromOffset, ToOffset;
        public float FromScale = 1, ToScale = 1, FromAngle, ToAngle;
    }

    // Only this adapter touches Unity Animation. Runtime clips are owned and destroyed here.
    [RequireComponent(typeof(Animation), typeof(CanvasGroup))]
    public sealed class BetterAnimation : MonoBehaviour
    {
        public float Speed = 1;
        public bool Paused { get; private set; }
        public bool Running => completion != null;

        public event Action<PresentationPhase> Lifecycle;
        Animation animationPlayer;
        AnimationClip ownedClip;
        bool ownsClip;
        AnimationState state;
        TaskCompletionSource<PresentationPhase> completion;
        float elapsed;
        public Task<PresentationPhase> Play(UiMotion motion)
        {
            if (motion == null)
                throw new ArgumentNullException(nameof(motion));
            Cancel();
            if (animationPlayer == null)
                animationPlayer = GetComponent<Animation>();
            animationPlayer.playAutomatically = false;
            animationPlayer.cullingType = AnimationCullingType.AlwaysAnimate;
            var rect = transform as RectTransform;
            Vector3 position = transform.localPosition, scale = transform.localScale;
            var clip = new AnimationClip
            {
                legacy = true,
                name = "BetterAnimation",
                wrapMode = WrapMode.ClampForever
            };
            float duration = Mathf.Max(.001f, motion.Duration);
            void Curve(Type type, string property, float from, float to) => clip.SetCurve("", type, property, AnimationCurve.EaseInOut(0, from, duration, to));
            Curve(typeof(CanvasGroup), "m_Alpha", motion.FromAlpha, motion.ToAlpha);
            Curve(typeof(Transform), "localPosition.x", position.x + motion.FromOffset.x, position.x + motion.ToOffset.x);
            Curve(typeof(Transform), "localPosition.y", position.y + motion.FromOffset.y, position.y + motion.ToOffset.y);
            Curve(typeof(Transform), "localScale.x", scale.x * motion.FromScale, scale.x * motion.ToScale);
            Curve(typeof(Transform), "localScale.y", scale.y * motion.FromScale, scale.y * motion.ToScale);
            Curve(typeof(Transform), "localEulerAnglesRaw.z", motion.FromAngle, motion.ToAngle);
            ownsClip = true;
            return StartClip(clip);
        }

        public Task<PresentationPhase> PlayClip(AnimationClip clip)
        {
            if (clip == null || !clip.legacy)
                throw new ArgumentException("UI clips must be legacy Animation clips");
            Cancel();
            ownsClip = false;
            return StartClip(clip);
        }

        Task<PresentationPhase> StartClip(AnimationClip clip)
        {
            if (animationPlayer == null)
                animationPlayer = GetComponent<Animation>();
            animationPlayer.playAutomatically = false;
            animationPlayer.cullingType = AnimationCullingType.AlwaysAnimate;
            ownedClip = clip;
            animationPlayer.AddClip(clip, clip.name);
            animationPlayer.Play(clip.name);
            state = animationPlayer[clip.name];
            state.speed = 0;
            state.time = 0;
            animationPlayer.Sample();
            elapsed = 0;
            Paused = false;
            completion = new TaskCompletionSource<PresentationPhase>();
            var task = completion.Task;
            Signal(PresentationPhase.Started);
            return task;
        }

        void Signal(PresentationPhase phase)
        {
            if (Lifecycle == null)
                return;
            foreach (Action<PresentationPhase> callback in Lifecycle.GetInvocationList())
                try
                {
                    callback(phase);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
        }

        public void Pause()
        {
            Paused = true;
        }

        public void Resume()
        {
            Paused = false;
        }

        void Update()
        {
            Tick(Time.unscaledDeltaTime);
        }

        public void Tick(float delta)
        {
            if (completion == null || Paused || state == null)
                return;
            elapsed += Mathf.Max(0, delta) * Mathf.Max(0, Speed);
            state.time = Mathf.Min(elapsed, ownedClip.length);
            animationPlayer.Sample();
            if (elapsed >= ownedClip.length)
                Finish(PresentationPhase.Completed);
        }

        public void Cancel()
        {
            if (completion != null)
                Finish(PresentationPhase.Cancelled);
        }

        void Finish(PresentationPhase phase)
        {
            var pending = completion;
            completion = null;
            state = null;
            if (animationPlayer != null)
            {
                animationPlayer.Stop();
                if (ownedClip != null)
                    animationPlayer.RemoveClip(ownedClip);
            }

            if (ownedClip != null && ownsClip)
                Destroy(ownedClip);
            ownedClip = null;
            Signal(phase);
            pending?.TrySetResult(phase);
        }

        void OnDisable()
        {
            Cancel();
        }

        void OnDestroy()
        {
            Cancel();
            Lifecycle = null;
        }
    }
}
