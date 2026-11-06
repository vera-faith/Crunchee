#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Crunchee.Core;

namespace Crunchee.EditorTools
{
    /// <summary>
    /// The butter steps and their crackable wax coating: the step builder,
    /// the Voronoi plate meshes the shell splits into, the seamless pre-crack
    /// glaze sheet, and the butter/dent textures.
    /// </summary>
    public static partial class ButterPathBuilder
    {
        /// <summary>One giant butter step: body + pat top + coating shell + particles + Squishy.
        /// Everything is built under an unscaled, yaw-rotated GROUP in LOCAL
        /// coordinates, so a step can sit at any angle (the spiral staircase).
        /// The Squishy and its decals stay parented under the body exactly as
        /// before, so all of their local-space math is unchanged.</summary>
        static void BuildButter(Vector3 pos, string name, Coating coating, int waxLayers, Material butterMat, Material patMat, float yawDeg = 0f, float depth = 0f, Color polishColor = default)
        {
            if (depth <= 0f) depth = ButterD;
            var group = new GameObject(name);
            group.transform.position = pos;
            group.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);

            var step = ChildBox(group.transform, "Body", Vector3.zero, new Vector3(ButterW, ButterH, depth), butterMat, true);
            Roundify(step, 0.2f); // soft, well-rounded butter block (reference image)

            // Clear plastic serving tray under the block (reference image).
            var trayMat = Mat("TrayClear", new Color(0.95f, 0.96f, 1f), 0.4f, 0.95f);
            trayMat.SetFloat("_RimStrength", 0.45f);
            trayMat.SetFloat("_SpecStrength", 1.4f);
            ChildBox(step.transform, "Tray",
                     new Vector3(0f, (-ButterH / 2f - 0.03f) / ButterH, 0f),
                     new Vector3((ButterW + 0.5f) / ButterW, 0.1f / ButterH, (depth + 0.42f) / depth),
                     trayMat, false);

            // Butter pat: the thinner slab on top of a real butter stick.
            var pat = ChildBox(step.transform, "Pat",
                     new Vector3(0f, (ButterH / 2f + PatH / 2f - 0.02f) / ButterH, 0f),
                     new Vector3(0.96f, PatH / ButterH, 0.95f), patMat, false);
            Roundify(pat, 0.07f);

            Material shellMat; Color particleColor; string shellName; bool crackable; float shellT;
            switch (coating)
            {
                case Coating.Sugar:
                    shellMat = Mat("SugarShell", new Color(1f, 0.93f, 0.95f), 0.88f, 0.9f);
                    particleColor = new Color(1f, 0.8f, 0.88f); shellName = "SugarShell"; crackable = false; shellT = 0.12f; break;
                case Coating.Honey:
                    shellMat = Mat("HoneyShell", new Color(1f, 0.62f, 0.12f), 0.5f, 1f);
                    particleColor = new Color(1f, 0.7f, 0.25f); shellName = "HoneyShell"; crackable = false; shellT = 0.12f; break;
                case Coating.NailPolish:
                    shellMat = Mat("PolishShell", polishColor == default ? new Color(1f, 0.62f, 0.72f) : polishColor, 1f, 1f);
                    particleColor = shellMat.color; shellName = "PolishShell"; crackable = true; shellT = 0.08f; break;
                default: // Wax: a clear glossy coat that thickens and frosts with every layer.
                    float alpha = 0.28f + waxLayers * 0.04f;
                    shellMat = Mat($"Wax{waxLayers}", new Color(1f, 0.985f, 0.95f), alpha, 0.93f);
                    shellMat.SetFloat("_RimStrength", 0.22f);
                    shellMat.SetFloat("_SpecStrength", 1.1f); // soft sheet gloss - plate edges must not glint
                    shellMat.SetFloat("_EnvStrength", 0.15f); // wax glosses, it does not mirror the sky
                    particleColor = Color.white; shellName = "WaxShell"; crackable = true; shellT = 0.1f + waxLayers * 0.012f; break;
            }
            shellMat.SetFloat("_DentAmount", 1f);

            // Collider reaches the shell top so she stands ON the wax.
            var col = step.GetComponent<BoxCollider>();
            col.size = new Vector3(1f, (ButterH + PatH + shellT) / ButterH, 1f);
            col.center = new Vector3(0f, ((PatH + shellT) / 2f) / ButterH, 0f);

            // Shell group sits on the pat. Wax/polish = irregular glassy plates
            // (Voronoi shards, like real cracked wax) that split on landing;
            // sugar/honey = one soft piece. Nov 6 round 2: translucent plates
            // show their side walls through the tops = web seams even flush,
            // so the VISIBLE pre-crack surface is one smooth glaze sheet; the
            // plates sit hidden under it and appear the moment they split.
            var shell = new GameObject(shellName);
            shell.transform.SetParent(step.transform, false);
            shell.transform.localPosition = new Vector3(0f, (ButterH / 2f + PatH - 0.02f) / ButterH, 0f);
            float shellW = ButterW * 1.01f, shellD = depth * 1.01f;
            GameObject seamlessSheet = null;
            if (crackable)
            {
                int nx = coating == Coating.Wax ? 3 + (waxLayers + 1) / 3 : 3;
                int nz = coating == Coating.Wax ? 3 : 2;
                var cells = VoronoiCells(shellW, shellD, nx, nz, 7919 * waxLayers + nx * 31 + (int)coating, 0f);
                var meshes = new List<Mesh>();
                for (int ci = 0; ci < cells.Count; ci++)
                {
                    var cell = cells[ci];
                    var c = Vector2.zero; foreach (var v in cell) c += v; c /= cell.Count;
                    var m = PrismMesh(cell, c, shellT, Mathf.Min(0.008f, shellT * 0.1f));
                    m.name = $"{name}_Shard{ci:00}";
                    meshes.Add(m);
                    var piece = new GameObject("ShellPiece");
                    piece.transform.SetParent(shell.transform, false);
                    piece.transform.localPosition = new Vector3(c.x / ButterW, (shellT / 2f) / ButterH, c.y / depth);
                    piece.transform.localScale = new Vector3(1f / ButterW, 1f / ButterH, 1f / depth);
                    piece.AddComponent<MeshFilter>().sharedMesh = m;
                    piece.AddComponent<MeshRenderer>().sharedMaterial = shellMat;
                    piece.SetActive(false); // hidden until the crack swaps the sheet for the plates
                }
                SaveMeshGroup(meshes, $"WaxShards_{name}");
                // The smooth sheet is a single-sided flat grid, not a box:
                // a translucent box renders its own interior faces through
                // the top (ZWrite off) and reads as camo blotches. A top-only
                // sheet shows nothing but the glaze. It still subdivides so
                // the squish dent can bend it.
                {
                    var sheetGo = new GameObject("WaxSheet");
                    sheetGo.transform.SetParent(step.transform, false);
                    sheetGo.transform.localPosition = new Vector3(0f, (ButterH / 2f + PatH - 0.02f + shellT) / ButterH, 0f);
                    sheetGo.transform.localScale = new Vector3(1f / ButterW, 1f / ButterH, 1f / depth);
                    var sm = FlatSheetMesh(shellW, shellD, 24, 12);
                    sm.name = $"{name}_WaxSheet";
                    SaveMeshGroup(new List<Mesh> { sm }, $"WaxSheet_{name}");
                    sheetGo.AddComponent<MeshFilter>().sharedMesh = sm;
                    sheetGo.AddComponent<MeshRenderer>().sharedMaterial = shellMat;
                    seamlessSheet = sheetGo;
                }
            }
            else
            {
                ChildBox(shell.transform, "Shell", new Vector3(0f, (shellT / 2f) / ButterH, 0f),
                         new Vector3(shellW / ButterW, shellT / ButterH, shellD / depth), shellMat, false);
            }
            float shellTopLocal = (ButterH / 2f + PatH - 0.02f + shellT) / ButterH;

            // Dent decal: a soft shadow disc that follows the character on the
            // squishy, deepening as she sinks. Sits just under the crack plane.
            MeshRenderer dentRend = null;
            {
                var dentMat = Mat("Dent", Color.white, 0.99f, 0.5f);
                dentMat.mainTexture = DentTexture();
                dentMat.renderQueue = 3001;
                NoSheen(dentMat);
                dentMat.SetFloat("_DentAmount", 1f);
                var dent = GameObject.CreatePrimitive(PrimitiveType.Plane);
                dent.name = "DentDecal";
                dent.transform.SetParent(step.transform, false);
                dent.transform.localPosition = new Vector3(0f, shellTopLocal + 0.004f / ButterH, 0f);
                dent.transform.localScale = new Vector3(0.15f / ButterW, 1f, 0.15f / depth);
                dent.GetComponent<MeshRenderer>().material = dentMat;
                Object.DestroyImmediate(dent.GetComponent<Collider>());
                dentRend = dent.GetComponent<MeshRenderer>();
                dentRend.enabled = false; // only shows while she stands on it
            }

            var sq = step.AddComponent<Squishy>();
            sq.baseMaterial = BaseMaterial.Butter; sq.coating = coating; sq.waxLayers = waxLayers;
            sq.coatingShell = shell;
            sq.seamlessShell = seamlessSheet;
            sq.dentDecal = dentRend;

            var ps = new GameObject("StepParticles").AddComponent<ParticleSystem>();
            ps.transform.SetParent(step.transform, false);
            ps.transform.localPosition = new Vector3(0f, ButterH * 0.6f, 0f);
            var main = ps.main; main.startColor = particleColor; main.startSize = 0.09f;
            main.startSpeed = 2.2f; main.startLifetime = 0.7f; main.playOnAwake = false;
            var em = ps.emission; em.enabled = false;
            ps.GetComponent<ParticleSystemRenderer>().material = SparkleParticleMat(false);
            sq.stepParticles = ps;
            sq.pathOrder = ++pathCounter;
            sq.shardMaterial = Mat("WaxChip", new Color(1f, 0.98f, 0.94f), 0.92f, 0.7f);
        }

        /// <summary>Irregular convex plates covering a w x d rectangle: Voronoi
        /// cells of a jittered nx x nz seed grid, each shrunk by a hairline gap.</summary>
        static List<List<Vector2>> VoronoiCells(float w, float d, int nx, int nz, int seed, float gap)
        {
            var rng = new System.Random(seed);
            var seeds = new List<Vector2>();
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                    seeds.Add(new Vector2(-w / 2f + (ix + 0.5f + ((float)rng.NextDouble() - 0.5f) * 0.75f) * w / nx,
                                          -d / 2f + (iz + 0.5f + ((float)rng.NextDouble() - 0.5f) * 0.75f) * d / nz));
            var cells = new List<List<Vector2>>();
            for (int i = 0; i < seeds.Count; i++)
            {
                var poly = new List<Vector2> { new Vector2(-w / 2f, -d / 2f), new Vector2(w / 2f, -d / 2f), new Vector2(w / 2f, d / 2f), new Vector2(-w / 2f, d / 2f) };
                for (int j = 0; j < seeds.Count && poly.Count > 2; j++)
                {
                    if (j == i) continue;
                    var n = seeds[j] - seeds[i]; var mid = (seeds[i] + seeds[j]) * 0.5f;
                    var outPoly = new List<Vector2>();
                    for (int k = 0; k < poly.Count; k++)
                    {
                        var A = poly[k]; var B = poly[(k + 1) % poly.Count];
                        float da = Vector2.Dot(A - mid, n), db = Vector2.Dot(B - mid, n);
                        if (da <= 0f) outPoly.Add(A);
                        if ((da < 0f && db > 0f) || (da > 0f && db < 0f)) outPoly.Add(Vector2.Lerp(A, B, da / (da - db)));
                    }
                    poly = outPoly;
                }
                if (poly.Count < 3) continue;
                var c = Vector2.zero; foreach (var v in poly) c += v; c /= poly.Count;
                for (int k = 0; k < poly.Count; k++) poly[k] += (c - poly[k]).normalized * gap;
                cells.Add(poly);
            }
            return cells;
        }

        /// <summary>A plate mesh: convex polygon extruded to thickness t, with a
        /// small bevel on the top edge so every shard catches a glossy highlight.
        /// Vertices are relative to centre c (world units).</summary>
        static Mesh PrismMesh(List<Vector2> poly, Vector2 c, float t, float bevel)
        {
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            int n = poly.Count; float hy = t / 2f;
            var outer = new Vector3[n]; var inner = new Vector3[n];
            for (int k = 0; k < n; k++)
            {
                var p = poly[k] - c;
                outer[k] = new Vector3(p.x, 0f, p.y);
                var q = p + (-p).normalized * Mathf.Min(bevel, p.magnitude * 0.4f);
                inner[k] = new Vector3(q.x, 0f, q.y);
            }
            void Tri(int a, int b, int d, Vector3 want)
            {
                if (Vector3.Dot(Vector3.Cross(verts[b] - verts[a], verts[d] - verts[a]), want) < 0f) { int tmp = b; b = d; d = tmp; }
                tris.Add(a); tris.Add(b); tris.Add(d);
            }
            int V(Vector3 pos, Vector3 nrm) { verts.Add(pos); norms.Add(nrm); uvs.Add(new Vector2(pos.x * 0.3f + 0.5f, pos.z * 0.3f + 0.5f)); return verts.Count - 1; }
            // top
            int ctop = V(Vector3.up * hy, Vector3.up);
            var top = new int[n];
            for (int k = 0; k < n; k++) top[k] = V(inner[k] + Vector3.up * hy, Vector3.up);
            for (int k = 0; k < n; k++) Tri(ctop, top[k], top[(k + 1) % n], Vector3.up);
            // bottom
            int cbot = V(Vector3.down * hy, Vector3.down);
            var bot = new int[n];
            for (int k = 0; k < n; k++) bot[k] = V(outer[k] + Vector3.down * hy, Vector3.down);
            for (int k = 0; k < n; k++) Tri(cbot, bot[k], bot[(k + 1) % n], Vector3.down);
            // bevel + sides, flat per edge
            for (int k = 0; k < n; k++)
            {
                int k2 = (k + 1) % n;
                var edge = outer[k2] - outer[k];
                var side = Vector3.Cross(edge, Vector3.up).normalized;
                if (Vector3.Dot(side, outer[k] + outer[k2]) < 0f) side = -side;
                var bn = (side + Vector3.up).normalized;
                float yb = hy - bevel;
                int b0 = V(inner[k] + Vector3.up * hy, bn), b1 = V(inner[k2] + Vector3.up * hy, bn);
                int b2 = V(outer[k] + Vector3.up * yb, bn), b3 = V(outer[k2] + Vector3.up * yb, bn);
                Tri(b0, b2, b1, bn); Tri(b1, b2, b3, bn);
                int s0 = V(outer[k] + Vector3.up * yb, side), s1 = V(outer[k2] + Vector3.up * yb, side);
                int s2 = V(outer[k] + Vector3.down * hy, side), s3 = V(outer[k2] + Vector3.down * hy, side);
                Tri(s0, s2, s1, side); Tri(s1, s2, s3, side);
            }
            var m = new Mesh();
            m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
            m.RecalculateBounds(); m.RecalculateTangents();
            return m;
        }

        /// <summary>A single-sided flat top sheet: w x d grid of up-facing
        /// verts (subdivided so the squish dent can bend it).</summary>
        static Mesh FlatSheetMesh(float w, float d, int nx, int nz)
        {
            var verts = new List<Vector3>(); var norms = new List<Vector3>();
            var uvs = new List<Vector2>(); var tris = new List<int>();
            for (int iz = 0; iz <= nz; iz++)
                for (int ix = 0; ix <= nx; ix++)
                {
                    verts.Add(new Vector3(-w / 2f + ix * w / nx, 0f, -d / 2f + iz * d / nz));
                    norms.Add(Vector3.up);
                    uvs.Add(new Vector2(ix / (float)nx, iz / (float)nz));
                }
            for (int iz = 0; iz < nz; iz++)
                for (int ix = 0; ix < nx; ix++)
                {
                    int a = iz * (nx + 1) + ix, b = a + 1, c = a + nx + 1, d2 = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d2);
                }
            var m = new Mesh();
            m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
            m.RecalculateBounds(); m.RecalculateTangents();
            return m;
        }

        /// <summary>Saves several meshes as ONE asset file (regenerated on every build).</summary>
        static void SaveMeshGroup(List<Mesh> meshes, string fileName)
        {
            if (meshes.Count == 0) return;
            string path = $"{GeneratedMeshDir}/{fileName}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(meshes[0], path);
            for (int i = 1; i < meshes.Count; i++) AssetDatabase.AddObjectToAsset(meshes[i], meshes[0]);
        }

        /// <summary>Glossy butter surface: mostly-uniform creamy yellow with a
        /// faint golden marbling (Nov 6 round 2: the strong swirl read as big
        /// blotches under the wax sheet - real butter is subtler than that).</summary>
        static Texture2D ButterSwirlTexture()
        {
            return LoadOrCreateTexture("Assets/Textures/ButterSwirl.png", 512, 256, (x, y, w, h) => Color.white); // DIAGNOSTIC: flat
        }

        /// <summary>Soft radial shadow for the dent she presses into a squishy.</summary>
        static Texture2D DentTexture()
        {
            return LoadOrCreateTexture("Assets/Textures/Dent.png", 128, 128, (x, y, w, h) =>
            {
                float dx = (x - w / 2f) / (w / 2f), dy = (y - h / 2f) / (h / 2f);
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                float a = Mathf.Pow(1f - d, 1.7f) * 0.85f;
                return new Color(0.36f, 0.22f, 0.12f, a);
            });
        }
    }
}
#endif
