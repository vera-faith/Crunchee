using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Crunchee.Core;

namespace Crunchee.UI
{
    public class ProgressHud : MonoBehaviour
    {
        public Image[] pips;

        readonly HashSet<int> cracked = new HashSet<int>();
        static readonly Color Filled = new Color(1f, 0.78f, 0.28f, 1f);
        static readonly Color Empty = new Color(1f, 1f, 1f, 0.35f);

        void Update()
        {
            foreach (var q in FindObjectsByType<Squishy>())
                if (q.Cracked && q.pathOrder > 0) cracked.Add(q.pathOrder);
            if (pips == null) return;
            int filled = cracked.Count == 0 ? 0 : (cracked.Count - 1) % 10 + 1;
            for (int i = 0; i < pips.Length; i++)
                if (pips[i] != null) pips[i].color = i < filled ? Filled : Empty;
        }
    }
}
