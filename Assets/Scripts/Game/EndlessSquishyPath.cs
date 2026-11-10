using System.Collections.Generic;
using UnityEngine;
using Crunchee.Core;
using Crunchee.CameraRig;

namespace Crunchee.Game
{
    /// <summary>
    /// The endless course (Nov 9, "the map is like a cube box"): the squishies
    /// line the four sides of a giant square tower and the path spirals up
    /// around it. Five slots per side; the spacing alternates walk-together
    /// steps and real JUMP gaps so it plays like parkour - land on a spinner,
    /// walk across, hop the gap. The camera keeps riding just behind the
    /// course like before, only now it rounds the corners with her.
    /// </summary>
    public sealed class EndlessSquishyPath : MonoBehaviour
    {
        public const int StepsPerTurn = ThemePath.StepsPerTurn;
        public const int StepsPerSide = StepsPerTurn / 4;
        public const float BaseY = 0.35f;
        public const float Rise = 0.55f;
        public const float TurnHeight = StepsPerTurn * Rise;
        // One floor of the tower in slot-advance units: two ~4.45 jump gaps,
        // two ~3.15 walk gaps and a 4.0 walk around the corner per side.
        public const float SideLength = 19.2f;
        public const float HalfSide = SideLength * 0.5f;
        public static readonly Vector3 Center = Vector3.zero;

        // Distance from each side's leading corner to its slots, in path
        // order: hop onto the corner slot, JUMP the first gap, walk one, JUMP
        // the second, walk the last slot, then walk around the corner.
        static readonly float[] SideOffsets = { 1.02f, 5.47f, 8.62f, 13.07f, 16.22f };
        static readonly float[] SideYaws = { 90f, 180f, 270f, 0f };

        public Transform player;
        public ThirdPersonFollow cameraFollow;
        public GameObject[] stepTemplates;
        public GameObject[] initialSteps;
        public Transform[] environmentChunks;
        public int environmentFirstIndex = -1;
        public int ahead = 25;
        public int keepBehind = 5;

        sealed class ActiveStep
        {
            public int index;
            public GameObject go;
            public Squishy squishy;
        }

        readonly List<ActiveStep> active = new List<ActiveStep>();
        readonly Dictionary<Squishy, ActiveStep> bySquishy = new Dictionary<Squishy, ActiveStep>();
        readonly List<int> environmentIndices = new List<int>();
        int nextIndex;
        int focusIndex;
        bool initialized;

        public int FocusIndex => focusIndex;
        public int ActiveCount => active.Count;

        static int SideOf(int index)
        {
            int step = ((index % StepsPerTurn) + StepsPerTurn) % StepsPerTurn;
            return step / StepsPerSide;
        }

        /// <summary>Yaw of the course direction at a slot (the travel tangent).</summary>
        public static float SideYawAt(int index) => SideYaws[SideOf(index)];

        public static Vector3 SlotPosition(int index)
        {
            int step = ((index % StepsPerTurn) + StepsPerTurn) % StepsPerTurn;
            float t = SideOffsets[step % StepsPerSide];
            float y = BaseY + index * Rise;
            switch (step / StepsPerSide)
            {
                case 0: return new Vector3(Center.x - HalfSide + t, y, Center.z + HalfSide);
                case 1: return new Vector3(Center.x + HalfSide, y, Center.z + HalfSide - t);
                case 2: return new Vector3(Center.x + HalfSide - t, y, Center.z - HalfSide);
                default: return new Vector3(Center.x - HalfSide, y, Center.z - HalfSide + t);
            }
        }

        public static Quaternion SlotRotation(int index) => Quaternion.Euler(0f, SideYawAt(index), 0f);

        void Awake() => Initialize();

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            nextIndex = 0;
            if (initialSteps != null)
            {
                for (int i = 0; i < initialSteps.Length; i++)
                {
                    if (initialSteps[i] == null) continue;
                    var sq = initialSteps[i].GetComponentInChildren<Squishy>();
                    if (sq == null) continue;
                    var entry = new ActiveStep { index = i, go = initialSteps[i], squishy = sq };
                    active.Add(entry);
                    bySquishy[sq] = entry;
                    sq.pathOrder = i + 1;
                    nextIndex = i + 1;
                }
            }
            if (active.Count == 0)
                for (int i = 0; i <= ahead; i++) Spawn(i);

            if (environmentChunks != null)
                for (int i = 0; i < environmentChunks.Length; i++) environmentIndices.Add(environmentFirstIndex + i);
        }

        void Update()
        {
            if (cameraFollow != null)
            {
                float targetYaw = SideYawAt(focusIndex);
                cameraFollow.fixedYaw = Mathf.LerpAngle(cameraFollow.fixedYaw, targetYaw, 1f - Mathf.Exp(-1.6f * Time.deltaTime));
            }
        }

        public void Touch(Squishy squishy)
        {
            if (squishy != null && bySquishy.TryGetValue(squishy, out var entry)) Touch(entry.index);
        }

        public void Touch(int index)
        {
            if (index < 0) return;
            focusIndex = Mathf.Max(focusIndex, index);
            if (stepTemplates != null && stepTemplates.Length > 0)
                while (nextIndex <= focusIndex + ahead) Spawn(nextIndex);
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var entry = active[i];
                if (entry.index >= focusIndex - keepBehind) continue;
                bySquishy.Remove(entry.squishy);
                active.RemoveAt(i);
                if (entry.go != null)
                {
                    if (Application.isPlaying) Destroy(entry.go);
                    else DestroyImmediate(entry.go);
                }
            }
            UpdateEnvironment();
        }

        void Spawn(int index)
        {
            if (stepTemplates == null || stepTemplates.Length == 0) return;
            int templateIndex = Mathf.Abs(index % stepTemplates.Length);
            var template = stepTemplates[templateIndex];
            if (template == null) { nextIndex = index + 1; return; }
            var go = Instantiate(template, SlotPosition(index), SlotRotation(index), transform);
            go.name = $"{template.name}_{index:0000}";
            go.SetActive(true);
            var sq = go.GetComponentInChildren<Squishy>();
            if (sq == null) { Destroy(go); nextIndex = index + 1; return; }
            sq.pathOrder = index + 1;
            sq.stage = 1;
            var entry = new ActiveStep { index = index, go = go, squishy = sq };
            active.Add(entry);
            bySquishy[sq] = entry;
            nextIndex = index + 1;
        }

        void UpdateEnvironment()
        {
            if (environmentChunks == null || environmentChunks.Length == 0) return;
            int currentTurn = Mathf.FloorToInt((float)focusIndex / StepsPerTurn);
            while (true)
            {
                int oldest = 0;
                for (int i = 1; i < environmentIndices.Count; i++)
                    if (environmentIndices[i] < environmentIndices[oldest]) oldest = i;
                if (environmentIndices[oldest] >= currentTurn - 1) break;
                int newest = environmentIndices[0];
                for (int i = 1; i < environmentIndices.Count; i++) newest = Mathf.Max(newest, environmentIndices[i]);
                int nextTurn = newest + 1;
                if (environmentChunks[oldest] != null)
                    environmentChunks[oldest].position += Vector3.up * ((nextTurn - environmentIndices[oldest]) * TurnHeight);
                environmentIndices[oldest] = nextTurn;
            }
        }
    }
}
