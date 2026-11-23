using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SpireAnimation
{
    public enum MotionId
    {
        Idle = 0,
        Attack = 1,
        Hit = 2,
        Death = 3,
        Guard = 4,
        Walk = 5,
        Skill = 6,
        Dodge = 7,
        Victory = 8,
        Spawn = 9
    }

    public enum PresentationPhase
    {
        Queued,
        Started,
        Impact,
        Completed,
        Cancelled
    }

    [Serializable]
    public sealed class MotionEntry
    {
        public MotionId Id;
        public AnimationClip Clip;
        public bool Loop;
        public bool Hold;
        [Range(0, 1)]
        public float Impact = .45f;
    }

    public readonly struct MotionSignal
    {
        public readonly int Ticket;
        public readonly MotionId Motion;
        public readonly PresentationPhase Phase;
        public MotionSignal(int ticket, MotionId motion, PresentationPhase phase)
        {
            Ticket = ticket;
            Motion = motion;
            Phase = phase;
        }
    }

    // Plays transform clips for cutout bones or skinned-rig IK controls.
    // No AnimatorController or frame sequence. Graph owns at most two clip playables.
    [RequireComponent(typeof(Animator))]
    public sealed class SkeletonView : MonoBehaviour
    {
        public MotionEntry[] Motions = Array.Empty<MotionEntry>();
        public event Action<MotionSignal> Lifecycle;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable current, previous;
        MotionEntry entry;
        int ticket;
        float elapsed, fade, fadeElapsed;
        bool impacted, finished;
        public MotionId Current => entry == null ? MotionId.Idle : entry.Id;
        public bool GraphValid => graph.IsValid();
        public int ActivePlayableCount => graph.IsValid() ? graph.GetPlayableCount() : 0;

        void EnsureGraph()
        {
            if (graph.IsValid())
                return;
            graph = PlayableGraph.Create("Skeleton2D");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            var animator = GetComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var output = AnimationPlayableOutput.Create(graph, "Bones", animator);
            output.SetSourcePlayable(mixer);
            graph.Play();
        }

        public int Play(MotionId id, float blend = .12f)
        {
            var next = Array.Find(Motions, m => m != null && m.Id == id && m.Clip != null);
            if (next == null)
                throw new ArgumentException("Missing motion " + id);
            EnsureGraph();
            if (entry != null && !finished)
                Signal(PresentationPhase.Cancelled);
            if (previous.IsValid())
            {
                mixer.DisconnectInput(0);
                previous.Destroy();
            }

            if (current.IsValid())
            {
                mixer.DisconnectInput(1);
                previous = current;
                graph.Connect(previous, 0, mixer, 0);
            }

            entry = next;
            current = AnimationClipPlayable.Create(graph, next.Clip);
            current.SetApplyFootIK(false);
            graph.Connect(current, 0, mixer, 1);
            elapsed = fadeElapsed = 0;
            fade = Mathf.Max(0, blend);
            impacted = finished = false;
            ticket++;
            mixer.SetInputWeight(0, previous.IsValid() && fade > 0 ? 1 : 0);
            mixer.SetInputWeight(1, previous.IsValid() && fade > 0 ? 0 : 1);
            Signal(PresentationPhase.Started);
            return ticket;
        }

        void Signal(PresentationPhase phase)
        {
            if (Lifecycle == null)
                return;
            var signal = new MotionSignal(ticket, entry.Id, phase);
            foreach (Action<MotionSignal> handler in Lifecycle.GetInvocationList())
                try
                {
                    handler(signal);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
        }

        void OnEnable()
        {
            if (Motions.Length > 0)
                Play(MotionId.Idle, 0);
        }

        void Update()
        {
            Tick(Time.unscaledDeltaTime);
        }

        public void Tick(float delta)
        {
            if (!graph.IsValid() || entry == null || delta < 0)
                return;
            elapsed += delta;
            fadeElapsed += delta;
            float weight = fade <= 0 ? 1 : Mathf.Clamp01(fadeElapsed / fade);
            mixer.SetInputWeight(0, 1 - weight);
            mixer.SetInputWeight(1, weight);
            if (weight >= 1 && previous.IsValid())
            {
                mixer.DisconnectInput(0);
                previous.Destroy();
            }

            double length = Math.Max(.01, entry.Clip.length);
            current.SetTime(entry.Loop ? elapsed % length : Math.Min(elapsed, length));
            graph.Evaluate(0);
            if (!impacted && elapsed >= length * entry.Impact)
            {
                impacted = true;
                Signal(PresentationPhase.Impact);
            }

            if (!entry.Loop && !finished && elapsed >= length)
            {
                finished = true;
                int completed = ticket;
                Signal(PresentationPhase.Completed);
                if (ticket == completed && !entry.Hold)
                    Play(MotionId.Idle);
            }
        }

        public void Stop()
        {
            if (entry != null && !finished)
            {
                finished = true;
                Signal(PresentationPhase.Cancelled);
            }

            if (graph.IsValid())
                graph.Destroy();
            entry = null;
        }

        void OnDisable()
        {
            Stop();
        }

        void OnDestroy()
        {
            Stop();
            Lifecycle = null;
        }
    }
}
