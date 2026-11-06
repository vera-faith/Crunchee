using UnityEngine;

namespace Crunchee.CameraRig
{
    /// <summary>
    /// Mobile-friendly bloom + warm color grade + vignette for the Built-in
    /// pipeline. Bloom makes the glowing pillars, sparkles and glossy
    /// highlights read as soft light instead of flat white.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [ExecuteAlways]
    public class CruncheePostFX : MonoBehaviour
    {
        public Shader shader;
        [Range(0f, 2f)] public float threshold = 1.2f;
        [Range(0f, 1f)] public float knee = 0.25f;
        [Range(0f, 3f)] public float bloomIntensity = 0.15f;
        public Color bloomTint = new Color(1f, 0.9f, 0.92f);
        [Range(2, 6)] public int iterations = 5;
        public float exposure = 0.9f;
        public float saturation = 1.08f;
        public float contrast = 1.1f;
        public Color lift = new Color(0.015f, 0.008f, 0.005f);
        public Color gain = new Color(1.02f, 0.99f, 0.97f);
        [Range(0f, 2f)] public float vignette = 0.3f;

        Material mat;
        readonly RenderTexture[] chain = new RenderTexture[8];

        static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
        static readonly int KneeId = Shader.PropertyToID("_Knee");
        static readonly int BloomTexId = Shader.PropertyToID("_BloomTex");
        static readonly int BloomIntensityId = Shader.PropertyToID("_BloomIntensity");
        static readonly int BloomTintId = Shader.PropertyToID("_BloomTint");
        static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        static readonly int SaturationId = Shader.PropertyToID("_Saturation");
        static readonly int ContrastId = Shader.PropertyToID("_Contrast");
        static readonly int LiftId = Shader.PropertyToID("_Lift");
        static readonly int GainId = Shader.PropertyToID("_Gain");
        static readonly int VignetteId = Shader.PropertyToID("_Vignette");

        void OnDisable()
        {
            if (mat != null) DestroyImmediate(mat);
            mat = null;
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (shader == null || !shader.isSupported) { Graphics.Blit(src, dst); return; }
            if (mat == null) mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

            mat.SetFloat(ThresholdId, threshold);
            mat.SetFloat(KneeId, Mathf.Max(knee, 1e-4f));
            var fmt = src.format;
            int w = src.width / 2, h = src.height / 2;
            var cur = RenderTexture.GetTemporary(w, h, 0, fmt);
            Graphics.Blit(src, cur, mat, 0);
            chain[0] = cur;
            int n = 1;
            for (; n < iterations; n++)
            {
                w /= 2; h /= 2;
                if (w < 4 || h < 4) break;
                chain[n] = RenderTexture.GetTemporary(w, h, 0, fmt);
                Graphics.Blit(chain[n - 1], chain[n], mat, 1);
            }
            for (int i = n - 1; i > 0; i--)
                Graphics.Blit(chain[i], chain[i - 1], mat, 2);

            mat.SetTexture(BloomTexId, chain[0]);
            mat.SetFloat(BloomIntensityId, bloomIntensity);
            mat.SetColor(BloomTintId, bloomTint);
            mat.SetFloat(ExposureId, exposure);
            mat.SetFloat(SaturationId, saturation);
            mat.SetFloat(ContrastId, contrast);
            mat.SetColor(LiftId, lift);
            mat.SetColor(GainId, gain);
            mat.SetFloat(VignetteId, vignette);
            Graphics.Blit(src, dst, mat, 3);

            for (int i = 0; i < n; i++) { RenderTexture.ReleaseTemporary(chain[i]); chain[i] = null; }
        }
    }
}
