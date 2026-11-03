using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Crunchee.Core;

namespace Crunchee.UI
{
    /// <summary>
    /// Path progress: ten pips across the top that fill gold as each butter
    /// cracks. (The finish banner/celebration was removed Nov 3 at Faith's
    /// request - reaching the end is just reaching the end now.)
    /// </summary>
    public class ProgressHud : MonoBehaviour
    {
        public Image[] pips;

        Squishy[] squishies;

        static readonly Color Filled = new Color(1f, 0.78f, 0.28f, 1f);
        static readonly Color Empty = new Color(1f, 1f, 1f, 0.35f);

        void Start()
        {
            var list = new List<Squishy>(FindObjectsOfType<Squishy>());
            list.Sort((a, b) => a.transform.position.z.CompareTo(b.transform.position.z));
            squishies = list.ToArray();
            Refresh();
        }

        void Update() { Refresh(); }

        void Refresh()
        {
            if (squishies == null || pips == null) return;
            for (int i = 0; i < pips.Length; i++)
                if (pips[i] != null)
                    pips[i].color = (i < squishies.Length && squishies[i].Cracked) ? Filled : Empty;
        }
    }
}
