using System.Collections.Generic;
using UnityEngine;
using Crunchee.Core;

namespace Crunchee.Game
{
    /// <summary>
    /// A Crunchee map = one really long path of steps (Faith's design).
    /// The level is DATA: a list of steps laid out along the path direction.
    /// Each step is one ASMR item (material x coating x coating layers).
    /// Building a new map = writing a new step list.
    /// </summary>
    public class LevelPath : MonoBehaviour
    {
        [System.Serializable]
        public class Step
        {
            public BaseMaterial material = BaseMaterial.Butter;
            public Coating coating = Coating.Wax;
            [Range(1, 10)] public int waxLayers = 1;      // progression knob (Butter Path: 1..10)
            public float gap = 2.2f;                       // distance from previous step
            public float sideOffset = 0f;                  // gentle winding
            public float heightOffset = 0.35f;             // slow climb
        }

        public GameObject stepPrefab;          // base step visual; Squishy config is applied per step
        public GameObject startPlatform;       // plain platform the character starts on
        public List<Step> steps = new List<Step>();

        /// <summary>The first authored level: 10 wax butters, one extra wax layer each step.</summary>
        public static List<Step> ButterPath()
        {
            var list = new List<Step>();
            for (int i = 1; i <= 10; i++)
                list.Add(new Step { material = BaseMaterial.Butter, coating = Coating.Wax, waxLayers = i });
            return list;
        }

        void Start()
        {
            if (steps == null || steps.Count == 0) steps = ButterPath();
            Vector3 pos = transform.position;
            if (startPlatform != null) Instantiate(startPlatform, pos, Quaternion.identity, transform);
            foreach (var s in steps)
            {
                pos += new Vector3(s.sideOffset, s.heightOffset, s.gap);
                var go = Instantiate(stepPrefab, pos, Quaternion.identity, transform);
                var sq = go.GetComponent<Squishy>();
                if (sq != null) { sq.baseMaterial = s.material; sq.coating = s.coating; sq.waxLayers = s.waxLayers; }
            }
        }
    }
}
