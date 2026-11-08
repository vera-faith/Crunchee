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

            // Soft white plastic serving tray under the block (reference
            // image). Opaque and low-rim: the old clear tray glowed white.
            var trayMat = Mat("Tray", new Color(0.96f, 0.95f, 0.93f), 1f, 0.6f);
            trayMat.SetFloat("_RimStrength", 0.1f);
            trayMat.SetFloat("_SpecStrength", 0.9f);
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
                default: // Wax: a clear glossy coat, thicker + milkier with every layer.
                    shellMat = WaxCoatMaterial($"Wax{waxLayers}", (waxLayers - 1) / 9f);
                    particleColor = Color.white; shellName = "WaxShell"; crackable = true; shellT = 0f; break;
            }
            shellMat.SetFloat("_DentAmount", 1f);

            // The wax coat (crackable coatings): a clear rounded clamshell
            // that stands coatT OFF the butter on every side and is itself
            // coatT thick, so no face is ever coplanar with butter, pat or
            // tray. Layer n: coatT = 0.012 + 0.009*(n-1) m.
            float coatT = 0.012f + 0.009f * (waxLayers - 1);
            float patTop = ButterH / 2f + PatH - 0.02f; // world units above step centre
            float waxTop = crackable ? patTop + 2f * coatT : patTop + shellT;

            // Collider reaches the coat top so she stands ON the wax.
            var col = step.GetComponent<BoxCollider>();
            col.size = new Vector3(1f, (waxTop + ButterH / 2f) / ButterH, 1f);
            col.center = new Vector3(0f, (waxTop - ButterH / 2f) / (2f * ButterH), 0f);

            // Shell group sits on the pat; its children are the hidden
            // crackable plates ONLY (the skirt must not be counted as a
            // piece by Squishy.InitShellPieces). Wax/polish = irregular
            // glassy plates (Voronoi shards, like real cracked wax) that
            // split on landing; sugar/honey = one soft piece.
            var shell = new GameObject(shellName);
            shell.transform.SetParent(step.transform, false);
            shell.transform.localPosition = new Vector3(0f, patTop / ButterH, 0f);
            float shellW = ButterW * 1.01f, shellD = depth * 1.01f;
            GameObject seamlessSheet = null;
            Material crackedShellMat = null;
            if (crackable)
            {
                // Coat footprint: rounded rect standing coatT outside the
                // butter silhouette (corner radius = body round + the gap).
                float hx = ButterW / 2f + 2f * coatT, hz = depth / 2f + 2f * coatT;
                float cornerR = 0.2f + 2f * coatT;
                float layerT = Mathf.Clamp01((waxLayers - 1) / 9f);
                int nx = coating == Coating.Wax ? Mathf.RoundToInt(Mathf.Lerp(6f, 3f, layerT)) : 3;
                int nz = coating == Coating.Wax ? Mathf.RoundToInt(Mathf.Lerp(4f, 2f, layerT)) : 2;
                float seamGap = coating == Coating.Wax ? Mathf.Lerp(0.005f, 0.014f, layerT) : 0f;
                var cells = VoronoiCells(hx * 2f, hz * 2f, nx, nz, 7919 * waxLayers + nx * 31 + (int)coating, seamGap);
                ClipToRoundedRect(cells, hx, hz, cornerR);
                // Plates only appear post-crack now, so they can have a REAL
                // bevel again - the bevelled top edges catch the sun glint and
                // read as broken glass. (The old invisible bevel was only a
                // problem when the plates showed pre-crack.)
                float plateBevel = Mathf.Min(0.04f, coatT * 0.5f + 0.004f);
                crackedShellMat = WaxCoatMaterial($"{shellMat.name}Cracked", layerT);
                crackedShellMat.SetFloat("_Frost", 0.55f); // stress-whitened, contrasts the butter in the gaps
                var meshes = new List<Mesh>();
                for (int ci = 0; ci < cells.Count; ci++)
                {
                    var cell = cells[ci];
                    var c = Vector2.zero; foreach (var v in cell) c += v; c /= cell.Count;
                    var m = PrismMesh(cell, c, coatT, plateBevel);
                    m.name = $"{name}_Shard{ci:00}";
                    meshes.Add(m);
                    var piece = new GameObject("ShellPiece");
                    piece.transform.SetParent(shell.transform, false);
                    piece.transform.localPosition = new Vector3(c.x / ButterW, (1.5f * coatT) / ButterH, c.y / depth);
                    piece.transform.localScale = new Vector3(1f / ButterW, 1f / ButterH, 1f / depth);
                    piece.AddComponent<MeshFilter>().sharedMesh = m;
                    piece.AddComponent<MeshRenderer>().sharedMaterial = shellMat;
                    piece.SetActive(false); // hidden until the crack swaps the top slab for the plates
                }
                SaveMeshGroup(meshes, $"WaxShards_{name}");

                // The TOP of the coat (the part that cracks): one seamless
                // rounded slab exactly where the plates sit, tessellated
                // radially so the squish dent can dish it. Its bottom face
                // only renders from below (Cull Back), never through the top.
                {
                    var topGo = new GameObject("WaxTop");
                    topGo.transform.SetParent(step.transform, false);
                    topGo.transform.localPosition = new Vector3(0f, (patTop + 1.5f * coatT) / ButterH, 0f);
                    topGo.transform.localScale = new Vector3(1f / ButterW, 1f / ButterH, 1f / depth);
                    var sm = WaxTopMesh(hx, hz, cornerR, coatT);
                    sm.name = $"{name}_WaxTop";
                    SaveMeshGroup(new List<Mesh> { sm }, $"WaxTop_{name}");
                    topGo.AddComponent<MeshFilter>().sharedMesh = sm;
                    topGo.AddComponent<MeshRenderer>().sharedMaterial = shellMat;
                    seamlessSheet = topGo;
                }

                // The SIDE skirt: a single-walled clear band hanging from the
                // slab's outer bottom edge down to just above the tray top.
                // Separate object - it does NOT swap out on crack.
                {
                    float trayTop = -ButterH / 2f + 0.02f;
                    var skirt = new GameObject("WaxSkirt");
                    skirt.transform.SetParent(step.transform, false);
                    skirt.transform.localPosition = Vector3.zero;
                    skirt.transform.localScale = new Vector3(1f / ButterW, 1f / ButterH, 1f / depth);
                    // Bottom ring tapers inward by coatT so the skirt hugs
                    // the butter like a coat instead of a floating glass box.
                    var km = WaxSkirtMesh(hx, hz, cornerR, patTop + coatT, trayTop + 0.015f, coatT);
                    // The skirt gets a LIGHTER coat material: the full milky
                    // alpha on the side walls veiled the butter into grey fog.
                    var skirtMat = WaxCoatMaterial($"{shellMat.name}Skirt", (waxLayers - 1) / 9f * 0.4f);
                    km.name = $"{name}_WaxSkirt";
                    SaveMeshGroup(new List<Mesh> { km }, $"WaxSkirt_{name}");
                    skirt.AddComponent<MeshFilter>().sharedMesh = km;
                    skirt.AddComponent<MeshRenderer>().sharedMaterial = skirtMat;
                }
            }
            else
            {
                ChildBox(shell.transform, "Shell", new Vector3(0f, (shellT / 2f) / ButterH, 0f),
                         new Vector3(shellW / ButterW, shellT / ButterH, shellD / depth), shellMat, false);
            }
            float shellTopLocal = waxTop / ButterH;

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
            sq.crackedShellMaterial = crackedShellMat;
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

        /// <summary>Rounded-rectangle outline (CCW) in XZ: half-extents hx/hz,
        /// corner radius r, arcSeg points per corner arc.</summary>
        static List<Vector2> RoundedRectPoly(float hx, float hz, float r, int arcSeg)
        {
            var pts = new List<Vector2>();
            var centers = new[]
            {
                new Vector2(hx - r, hz - r), new Vector2(-hx + r, hz - r),
                new Vector2(-hx + r, -hz + r), new Vector2(hx - r, -hz + r),
            };
            for (int c = 0; c < 4; c++)
            {
                float a0 = c * Mathf.PI / 2f;
                for (int i = 0; i < arcSeg; i++)
                {
                    float a = a0 + i * (Mathf.PI / 2f) / arcSeg;
                    pts.Add(centers[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                }
            }
            return pts;
        }

        /// <summary>Clips a polygon to a half-plane n.p &lt;= d.</summary>
        static List<Vector2> ClipHalfPlane(List<Vector2> poly, Vector2 n, float d)
        {
            var outp = new List<Vector2>();
            for (int k = 0; k < poly.Count; k++)
            {
                var A = poly[k]; var B = poly[(k + 1) % poly.Count];
                float da = Vector2.Dot(A, n) - d, db = Vector2.Dot(B, n) - d;
                if (da <= 0f) outp.Add(A);
                if ((da < 0f && db > 0f) || (da > 0f && db < 0f)) outp.Add(A + (B - A) * (da / (da - db)));
            }
            return outp;
        }

        /// <summary>Trims Voronoi cells to a rounded rectangle: each corner
        /// arc is approximated by three tangent clip planes (16-gon, close
        /// enough to round at this scale) so the plates share the coat slab's
        /// silhouette.</summary>
        static void ClipToRoundedRect(List<List<Vector2>> cells, float hx, float hz, float r)
        {
            for (int ci = cells.Count - 1; ci >= 0; ci--)
            {
                var poly = cells[ci];
                foreach (float sx in new[] { -1f, 1f })
                    foreach (float sz in new[] { -1f, 1f })
                    {
                        var cc = new Vector2(sx * (hx - r), sz * (hz - r));
                        foreach (float deg in new[] { 22.5f, 45f, 67.5f })
                        {
                            float a = deg * Mathf.Deg2Rad;
                            var n = new Vector2(sx * Mathf.Cos(a), sz * Mathf.Sin(a));
                            poly = ClipHalfPlane(poly, n, Vector2.Dot(n, cc) + r);
                            if (poly.Count < 3) break;
                        }
                    }
                if (poly.Count < 3) cells.RemoveAt(ci);
                else cells[ci] = poly;
            }
        }

        /// <summary>The seamless top of the wax coat: a t-thick rounded slab.
        /// The top is a radial grid (centre + rings) so the squish dent can
        /// dish it; a bevel band catches the edge glint; a side band and a
        /// bottom fan close the slab. Interior faces never show through the
        /// top (Cull Back), so no camo blotches.</summary>
        static Mesh WaxTopMesh(float hx, float hz, float r, float t)
        {
            const int ArcSeg = 10, Rings = 8;
            float bevel = Mathf.Min(0.008f, t * 0.15f);
            var outline = RoundedRectPoly(hx, hz, r, ArcSeg);
            int n = outline.Count;
            float hy = t / 2f;
            var verts = new List<Vector3>(); var norms = new List<Vector3>();
            var uvs = new List<Vector2>(); var tris = new List<int>();
            void Tri(int a, int b, int d, Vector3 want)
            {
                if (Vector3.Dot(Vector3.Cross(verts[b] - verts[a], verts[d] - verts[a]), want) < 0f) { int tmp = b; b = d; d = tmp; }
                tris.Add(a); tris.Add(b); tris.Add(d);
            }
            int V(Vector3 pos, Vector3 nrm) { verts.Add(pos); norms.Add(nrm); uvs.Add(new Vector2(pos.x * 0.3f + 0.5f, pos.z * 0.3f + 0.5f)); return verts.Count - 1; }
            // inner outline: shrunk by the bevel, carries the flat top
            var inner = new Vector2[n];
            for (int i = 0; i < n; i++) inner[i] = outline[i] - outline[i].normalized * bevel;

            // Top: centre + Rings concentric rings of the inner outline.
            int center = V(new Vector3(0f, hy, 0f), Vector3.up);
            var ring = new int[Rings][];
            for (int k = 1; k <= Rings; k++)
            {
                float s = k / (float)Rings;
                ring[k - 1] = new int[n];
                for (int i = 0; i < n; i++)
                    ring[k - 1][i] = V(new Vector3(inner[i].x * s, hy, inner[i].y * s), Vector3.up);
            }
            for (int i = 0; i < n; i++)
            {
                int i2 = (i + 1) % n;
                Tri(center, ring[0][i], ring[0][i2], Vector3.up);
                for (int k = 1; k < Rings; k++)
                {
                    Tri(ring[k - 1][i], ring[k][i], ring[k - 1][i2], Vector3.up);
                    Tri(ring[k - 1][i2], ring[k][i], ring[k][i2], Vector3.up);
                }
            }
            // Bevel band inner@hy -> outer@(hy-bevel), then side wall to -hy.
            var bev = new int[n]; var side = new int[n]; var bot = new int[n];
            for (int i = 0; i < n; i++)
            {
                var dir = new Vector3(outline[i].x, 0f, outline[i].y).normalized;
                bev[i] = V(new Vector3(outline[i].x, hy - bevel, outline[i].y), (dir + Vector3.up).normalized);
                side[i] = V(new Vector3(outline[i].x, hy - bevel, outline[i].y), dir);
                bot[i] = V(new Vector3(outline[i].x, -hy, outline[i].y), dir);
            }
            for (int i = 0; i < n; i++)
            {
                int i2 = (i + 1) % n;
                var bn = (norms[bev[i]] + norms[bev[i2]]).normalized;
                Tri(ring[Rings - 1][i], bev[i], ring[Rings - 1][i2], bn);
                Tri(ring[Rings - 1][i2], bev[i], bev[i2], bn);
                var sn = (norms[side[i]] + norms[side[i2]]).normalized;
                Tri(side[i], bot[i], side[i2], sn);
                Tri(side[i2], bot[i], bot[i2], sn);
            }
            // Bottom fan (only seen from underneath - Cull Back).
            int cbot = V(new Vector3(0f, -hy, 0f), Vector3.down);
            var botRing = new int[n];
            for (int i = 0; i < n; i++) botRing[i] = V(new Vector3(outline[i].x, -hy, outline[i].y), Vector3.down);
            for (int i = 0; i < n; i++) Tri(cbot, botRing[i], botRing[(i + 1) % n], Vector3.down);

            var m = new Mesh();
            m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
            m.RecalculateBounds(); m.RecalculateTangents();
            return m;
        }

        /// <summary>The clear side skirt of the wax coat: a single-walled
        /// rounded band from the slab's bottom edge down to just above the
        /// tray, open at the bottom, smooth radial normals. bottomInset tucks
        /// the bottom ring inward so the coat hugs the butter going down.</summary>
        static Mesh WaxSkirtMesh(float hx, float hz, float r, float yTop, float yBottom, float bottomInset = 0f)
        {
            const int ArcSeg = 10;
            var outline = RoundedRectPoly(hx, hz, r, ArcSeg);
            var outlineB = RoundedRectPoly(hx - bottomInset, hz - bottomInset, Mathf.Max(0.05f, r - bottomInset), ArcSeg);
            int n = outline.Count;
            var verts = new List<Vector3>(); var norms = new List<Vector3>();
            var uvs = new List<Vector2>(); var tris = new List<int>();
            void Tri(int a, int b, int d, Vector3 want)
            {
                if (Vector3.Dot(Vector3.Cross(verts[b] - verts[a], verts[d] - verts[a]), want) < 0f) { int tmp = b; b = d; d = tmp; }
                tris.Add(a); tris.Add(b); tris.Add(d);
            }
            var top = new int[n]; var bot = new int[n];
            for (int i = 0; i < n; i++)
            {
                var dir = new Vector3(outline[i].x, 0f, outline[i].y).normalized;
                var dirB = new Vector3(outlineB[i].x, 0f, outlineB[i].y).normalized;
                top[i] = verts.Count; verts.Add(new Vector3(outline[i].x, yTop, outline[i].y)); norms.Add(dir);
                uvs.Add(new Vector2(outline[i].x * 0.3f + 0.5f, 0.5f));
                bot[i] = verts.Count; verts.Add(new Vector3(outlineB[i].x, yBottom, outlineB[i].y)); norms.Add(dirB);
                uvs.Add(new Vector2(outlineB[i].x * 0.3f + 0.5f, 0.1f));
            }
            for (int i = 0; i < n; i++)
            {
                int i2 = (i + 1) % n;
                var sn = (norms[top[i]] + norms[top[i2]]).normalized;
                Tri(top[i], bot[i], top[i2], sn);
                Tri(top[i2], bot[i], bot[i2], sn);
            }
            var m = new Mesh();
            m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
            m.RecalculateBounds(); m.RecalculateTangents();
            return m;
        }

        /// <summary>The clear wax coat material: thickness 0..1 drives the
        /// milky build - n=1 is nearly invisible glass, n=10 a thick
        /// translucent shell the yellow butter still reads through.</summary>
        static Material WaxCoatMaterial(string name, float thickness)
        {
            var m = new Material(Shader.Find("Crunchee/Wax")) { name = name };
            m.color = new Color(1f, 0.99f, 0.97f, 1f);
            m.SetFloat("_Thickness", thickness);
            m.SetFloat("_Glossiness", 0.92f);
            m.SetFloat("_SpecStrength", 2f);
            m.SetFloat("_EnvStrength", 0.35f);
            m.renderQueue = 3000;
            return m;
        }

        /// <summary>Butter body/pat materials: saturated pale butter yellow
        /// with a soft marbling, glossy, mirroring almost none of the sky.</summary>
        static Material ButterBodyMat(string name, Color c, float gloss)
        {
            var m = Mat(name, c, 1f, gloss);
            m.mainTexture = ButterSwirlTexture();
            m.SetFloat("_DentAmount", 1f);
            m.SetFloat("_RimStrength", 0.12f);
            m.SetFloat("_EnvStrength", 0.12f);
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

        /// <summary>Glossy butter surface: mostly-uniform creamy yellow with
        /// a soft visible marbling - broad faint golden veins drifting across
        /// the stick (real butter, subtler than the old strong swirl).</summary>
        static Texture2D ButterSwirlTexture()
        {
            return LoadOrCreateTexture("Assets/Textures/ButterSwirl.png", 512, 256, (x, y, w, h) =>
            {
                float u = x / (float)w, v = y / (float)h;
                float s = Mathf.Sin(u * 7.3f + Mathf.Sin(v * 5.1f) * 1.4f + v * 2.2f)
                        + 0.6f * Mathf.Sin(u * 13.7f - v * 4.3f)
                        + 0.35f * Mathf.Sin(v * 17.1f + u * 2.9f);
                float vein = Mathf.SmoothStep(0f, 1f, Mathf.Abs(s) * 0.55f);
                return new Color(1f - vein * 0.05f, 1f - vein * 0.08f, 1f - vein * 0.12f);
            });
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
