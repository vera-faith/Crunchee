using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Crunchee.Core;

namespace Crunchee.UI
{
    /// <summary>
    /// Path progress: ten pips across the top that fill gold as each step
    /// cracks. The pips track one stage at a time (Butter Path, then Candy
    /// Path once every butter is cracked). (The finish banner/celebration was removed Nov 3 at Faith's
    /// request - reaching the end is just reaching the end now.)
    /// </summary>
    public class ProgressHud : MonoBehaviour
    {
        public Image[] pips;

        readonly List<Squishy[]> stages = new List<Squishy[]>();

        static readonly Color Filled = new Color(1f, 0.78f, 0.28f, 1f);
        static readonly Color Empty = new Color(1f, 1f, 1f, 0.35f);

        void Start()
        {
            var list = new List<Squishy>(FindObjectsByType<Squishy>(FindObjectsSortMode.None));
            // Course order, not world axis: the path is a spiral.
            list.Sort((a, b) => a.stage != b.stage ? a.stage.CompareTo(b.stage)
                : a.pathOrder != b.pathOrder ? a.pathOrder.CompareTo(b.pathOrder)
                : string.CompareOrdinal(a.name, b.name));
            var cur = new List<Squishy>();
            for (int i = 0; i < list.Count; i++)
            {
                cur.Add(list[i]);
                if (i == list.Count - 1 || list[i + 1].stage != list[i].stage) { stages.Add(cur.ToArray()); cur.Clear(); }
            }
            Refresh();
        }

        void Update() { Refresh(); }

        void Refresh()
        {
            if (stages.Count == 0 || pips == null) return;
            int s = 0;
            while (s < stages.Count - 1 && AllCracked(stages[s])) s++;
            var set = stages[s];
            for (int i = 0; i < pips.Length; i++)
                if (pips[i] != null)
                    pips[i].color = (i < set.Length && set[i].Cracked) ? Filled : Empty;
        }

        static bool AllCracked(Squishy[] set)
        {
            foreach (var q in set) if (!q.Cracked) return false;
            return true;
        }
    }
}
