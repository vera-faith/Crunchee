using System.Collections.Generic;
using UnityEngine;
using Crunchee.Core;
using Crunchee.CameraRig;

namespace Crunchee.Game
{
    public sealed class EndlessSquishyPath : MonoBehaviour
    {
        public const int StepsPerTurn = ThemePath.StepsPerTurn;
        public const float AngleStep = 360f / StepsPerTurn;
        public const float BaseY = 0.35f;
        public const float Rise = 0.55f;
        public const float StartZ = 3.15f;
        public const float TurnHeight = StepsPerTurn * Rise;
        public static readonly float Radius = 3.55f / (2f * Mathf.Sin(AngleStep * 0.5f * Mathf.Deg2Rad));

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

        public static Vector3 SlotPosition(int index)
        {
            float a = index * AngleStep * Mathf.Deg2Rad;
            return new Vector3(Radius * (1f - Mathf.Cos(a)), BaseY + index * Rise, StartZ + Radius * Mathf.Sin(a));
        }

        public static Quaternion SlotRotation(int index) => Quaternion.Euler(0f, index * AngleStep, 0f);

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
                float targetYaw = focusIndex * AngleStep;
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
