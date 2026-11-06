#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Crunchee.EditorTools
{
    /// <summary>
    /// Editor-time SDF meshing for the Pudding Bunny (v12): primitives,
    /// smooth-min/max blending, and a Surface Nets mesher that turns an
    /// implicit field into a smooth mesh with gradient normals - no more
    /// seams where separate primitives used to clip into each other.
    /// </summary>
    public static class SdfMesher
    {
        public delegate float Field(Vector3 p);

        // ------------------------------------------------ primitives
        public static float Sphere(Vector3 p, Vector3 c, float r) => (p - c).magnitude - r;

        public static float Ellipsoid(Vector3 p, Vector3 c, Vector3 r)
        {
            var q = p - c;
            float k0 = Mathf.Sqrt(q.x * q.x / (r.x * r.x) + q.y * q.y / (r.y * r.y) + q.z * q.z / (r.z * r.z));
            if (k0 < 1e-6f) return -Mathf.Min(r.x, Mathf.Min(r.y, r.z));
            float k1 = Mathf.Sqrt(q.x * q.x / (r.x * r.x * r.x * r.x) + q.y * q.y / (r.y * r.y * r.y * r.y) + q.z * q.z / (r.z * r.z * r.z * r.z));
            return k0 * (k0 - 1f) / Mathf.Max(k1, 1e-6f);
        }

        /// <summary>Ellipsoid in a rotated frame.</summary>
        public static float Ellipsoid(Vector3 p, Vector3 c, Vector3 r, Quaternion rot)
            => Ellipsoid(Quaternion.Inverse(rot) * (p - c), Vector3.zero, r);

        public static float Capsule(Vector3 p, Vector3 a, Vector3 b, float r)
        {
            var pa = p - a; var ba = b - a;
            float h = Mathf.Clamp01(Vector3.Dot(pa, ba) / Mathf.Max(ba.sqrMagnitude, 1e-8f));
            return (pa - ba * h).magnitude - r;
        }

        public static float RoundBox(Vector3 p, Vector3 c, Vector3 half, float r)
        {
            var q = new Vector3(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y), Mathf.Abs(p.z - c.z)) - half;
            var m = Vector3.Max(q, Vector3.zero);
            return m.magnitude + Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0f) - r;
        }

        // ------------------------------------------------ blending
        public static float Smin(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }

        public static float Smax(float a, float b, float k) => -Smin(-a, -b, k);

        // ------------------------------------------------ sampling
        public static Vector3 Gradient(Field f, Vector3 p)
        {
            const float e = 0.003f;
            var g = new Vector3(
                f(p + new Vector3(e, 0f, 0f)) - f(p - new Vector3(e, 0f, 0f)),
                f(p + new Vector3(0f, e, 0f)) - f(p - new Vector3(0f, e, 0f)),
                f(p + new Vector3(0f, 0f, e)) - f(p - new Vector3(0f, 0f, e)));
            return g.sqrMagnitude < 1e-10f ? Vector3.up : g.normalized;
        }

        /// <summary>Marches from an OUTSIDE origin along dir to the field
        /// surface (f = 0), bisect-refined, and returns the surface point and
        /// outward normal - used to sit face features ON the head so nothing
        /// floats or clips.</summary>
        public static Vector3 SurfacePoint(Field f, Vector3 origin, Vector3 dir, out Vector3 normal)
        {
            dir = dir.normalized;
            float t = 0f;
            for (int i = 0; i < 300; i++)
            {
                float d = f(origin + dir * t);
                if (d < 0.0005f) break;
                t += Mathf.Min(d, 0.02f);
                if (t > 6f) break;
            }
            float lo = Mathf.Max(0f, t - 0.02f), hi = t;
            for (int i = 0; i < 18; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (f(origin + dir * mid) < 0f) hi = mid; else lo = mid;
            }
            var p = origin + dir * ((lo + hi) * 0.5f);
            normal = Gradient(f, p);
            return p;
        }

        // ------------------------------------------------ Surface Nets
        /// <summary>Naive Surface Nets over [min,max] with cubic cells sized
        /// so `res` spans size.x. One vertex per sign-crossing cell (average
        /// of edge crossings), quads stitched across sign-changing grid
        /// edges, normals from the field gradient.</summary>
        public static Mesh Build(Field f, Vector3 min, Vector3 max, int res)
        {
            var size = max - min;
            float cell = size.x / res;
            int nx = res;
            int ny = Mathf.Max(4, Mathf.CeilToInt(size.y / cell));
            int nz = Mathf.Max(4, Mathf.CeilToInt(size.z / cell));

            var vals = new float[nx + 1, ny + 1, nz + 1];
            for (int i = 0; i <= nx; i++)
                for (int j = 0; j <= ny; j++)
                    for (int k = 0; k <= nz; k++)
                        vals[i, j, k] = f(min + new Vector3(i * cell, j * cell, k * cell));

            // one vertex per sign-crossing cell
            var cidx = new int[nx, ny, nz];
            for (int i = 0; i < nx; i++) for (int j = 0; j < ny; j++) for (int k = 0; k < nz; k++) cidx[i, j, k] = -1;
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var crossing = new List<Vector3>(12);
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                    for (int k = 0; k < nz; k++)
                    {
                        float v0 = vals[i, j, k];
                        bool inside = v0 < 0f, change = false;
                        for (int c = 0; c < 8 && !change; c++)
                            if ((vals[i + (c & 1), j + ((c >> 1) & 1), k + ((c >> 2) & 1)] < 0f) != inside) change = true;
                        if (!change) continue;
                        // average the zero-crossing positions on the 12 edges
                        crossing.Clear();
                        for (int e = 0; e < 12; e++)
                        {
                            int c0 = EdgeCorners[e, 0], c1 = EdgeCorners[e, 1];
                            float a = vals[i + (c0 & 1), j + ((c0 >> 1) & 1), k + ((c0 >> 2) & 1)];
                            float b = vals[i + (c1 & 1), j + ((c1 >> 1) & 1), k + ((c1 >> 2) & 1)];
                            if ((a < 0f) == (b < 0f)) continue;
                            float t = a / (a - b);
                            var pa = new Vector3(i + (c0 & 1), j + ((c0 >> 1) & 1), k + ((c0 >> 2) & 1));
                            var pb = new Vector3(i + (c1 & 1), j + ((c1 >> 1) & 1), k + ((c1 >> 2) & 1));
                            crossing.Add(Vector3.Lerp(pa, pb, t));
                        }
                        var pos = Vector3.zero;
                        foreach (var c in crossing) pos += c;
                        pos /= crossing.Count;
                        pos = min + pos * cell;
                        cidx[i, j, k] = verts.Count;
                        verts.Add(pos);
                        norms.Add(Gradient(f, pos));
                        uvs.Add(new Vector2(pos.x * 0.3f + 0.5f, pos.z * 0.3f + 0.5f));
                    }

            // stitch a quad across every sign-changing grid edge
            var tris = new List<int>();
            void Tri(int a, int b, int c, Vector3 want)
            {
                if (Vector3.Dot(Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]), want) < 0f) { int tmp = b; b = c; c = tmp; }
                tris.Add(a); tris.Add(b); tris.Add(c);
            }
            var quad = new int[4];
            for (int axis = 0; axis < 3; axis++)
            {
                int xm = axis == 0 ? nx - 1 : nx, ym = axis == 1 ? ny - 1 : ny, zm = axis == 2 ? nz - 1 : nz;
                for (int i = 0; i <= nx; i++)
                    for (int j = 0; j <= ny; j++)
                        for (int k = 0; k <= nz; k++)
                        {
                            int i2 = axis == 0 ? i + 1 : i, j2 = axis == 1 ? j + 1 : j, k2 = axis == 2 ? k + 1 : k;
                            if (i2 > xm || j2 > ym || k2 > zm) continue;
                            float a = vals[i, j, k], b = vals[i2, j2, k2];
                            if ((a < 0f) == (b < 0f)) continue;
                            // the edge is shared by up to 4 cells around it
                            int n = 0;
                            for (int s = 0; s < 4; s++)
                            {
                                // cyclic order of the four cells around the edge
                                int dj = QuadOffset[axis, s, 0], dk = QuadOffset[axis, s, 1];
                                int ci = axis == 0 ? i : i + dj, cj = axis == 1 ? j : j + dj, ck = axis == 2 ? k : k + dk;
                                if (axis == 0) { cj = j + dj; ck = k + dk; }
                                else if (axis == 1) { ci = i + dj; ck = k + dk; }
                                else { ci = i + dj; cj = j + dk; }
                                if (ci < 0 || cj < 0 || ck < 0 || ci >= nx || cj >= ny || ck >= nz) { n = -1; break; }
                                if (cidx[ci, cj, ck] < 0) { n = -1; break; }
                                quad[n++] = cidx[ci, cj, ck];
                            }
                            if (n != 4) continue; // surface clipped by bounds - skip rather than leave a hole
                            var want = Gradient(f, new Vector3(min.x + (i + 0.5f * (axis == 0 ? 1 : 0)) * cell,
                                                               min.y + (j + 0.5f * (axis == 1 ? 1 : 0)) * cell,
                                                               min.z + (k + 0.5f * (axis == 2 ? 1 : 0)) * cell));
                            Tri(quad[0], quad[1], quad[2], want);
                            Tri(quad[0], quad[2], quad[3], want);
                        }
                }

            var m = new Mesh();
            m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
            m.RecalculateBounds(); m.RecalculateTangents();
            return m;
        }

        // corners of each of a cell's 12 edges, packed as 3-bit (x,y,z) indices
        static readonly int[,] EdgeCorners =
        {
            {0,1},{2,3},{4,5},{6,7},     // x edges
            {0,2},{1,3},{4,6},{5,7},     // y edges
            {0,4},{1,5},{2,6},{3,7},     // z edges
        };

        // cyclic (dj,dk) offsets of the four cells sharing an edge per axis
        static readonly int[,,] QuadOffset =
        {
            { {-1,-1},{0,-1},{0,0},{-1,0} },   // x edge: cells (j,k) around row
            { {-1,-1},{0,-1},{0,0},{-1,0} },   // y edge: cells (i,k)
            { {-1,-1},{0,-1},{0,0},{-1,0} },   // z edge: cells (i,j)
        };
    }
}
#endif
