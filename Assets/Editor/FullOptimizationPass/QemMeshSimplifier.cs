// Full cross-platform optimization pass — quadric-error-metric mesh simplifier.
// Subset-placement half-edge collapse: vertices only move to existing positions,
// wedge (UV-seam) copies move together so no cracks open at seams.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BCAT.OptimizationPass
{
    public static class QemMeshSimplifier
    {
        // 4x4 symmetric matrix stored as 10 doubles for the plane quadric.
        private struct Quadric
        {
            public double m00, m01, m02, m03, m11, m12, m13, m22, m23, m33;

            public static Quadric FromPlane(double a, double b, double c, double d, double w)
            {
                Quadric q;
                q.m00 = a * a * w; q.m01 = a * b * w; q.m02 = a * c * w; q.m03 = a * d * w;
                q.m11 = b * b * w; q.m12 = b * c * w; q.m13 = b * d * w;
                q.m22 = c * c * w; q.m23 = c * d * w;
                q.m33 = d * d * w;
                return q;
            }

            public void Add(in Quadric o)
            {
                m00 += o.m00; m01 += o.m01; m02 += o.m02; m03 += o.m03;
                m11 += o.m11; m12 += o.m12; m13 += o.m13;
                m22 += o.m22; m23 += o.m23; m33 += o.m33;
            }

            public double Evaluate(Vector3 v)
            {
                double x = v.x, y = v.y, z = v.z;
                return m00 * x * x + 2 * m01 * x * y + 2 * m02 * x * z + 2 * m03 * x
                     + m11 * y * y + 2 * m12 * y * z + 2 * m13 * y
                     + m22 * z * z + 2 * m23 * z
                     + m33;
            }
        }

        private struct Face
        {
            public int c0, c1, c2;      // position-cluster ids
            public int w0, w1, w2;      // original wedge (vertex) indices
            public int submesh;
            public bool dead;
        }

        private struct HeapEntry : IComparable<HeapEntry>
        {
            public double cost;
            public int a, b;            // collapse a -> b (a merged into b)
            public int va, vb;          // version stamps
            public int CompareTo(HeapEntry other) => cost.CompareTo(other.cost);
        }

        private class MinHeap
        {
            private readonly List<HeapEntry> _items = new List<HeapEntry>(1 << 16);
            public int Count => _items.Count;
            public void Push(HeapEntry e)
            {
                _items.Add(e);
                int i = _items.Count - 1;
                while (i > 0)
                {
                    int p = (i - 1) >> 1;
                    if (_items[p].CompareTo(_items[i]) <= 0) break;
                    (_items[p], _items[i]) = (_items[i], _items[p]);
                    i = p;
                }
            }
            public HeapEntry Pop()
            {
                HeapEntry top = _items[0];
                int last = _items.Count - 1;
                _items[0] = _items[last];
                _items.RemoveAt(last);
                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1, r = l + 1, s = i;
                    if (l < _items.Count && _items[l].CompareTo(_items[s]) < 0) s = l;
                    if (r < _items.Count && _items[r].CompareTo(_items[s]) < 0) s = r;
                    if (s == i) break;
                    (_items[s], _items[i]) = (_items[i], _items[s]);
                    i = s;
                }
                return top;
            }
        }

        /// <summary>
        /// Simplify a mesh to approximately targetRatio of its original triangle count.
        /// Returns a new Mesh (not saved to the AssetDatabase) or null on failure.
        /// </summary>
        public static Mesh Simplify(Mesh source, float targetRatio, out int resultTris)
        {
            resultTris = 0;
            if (source == null) return null;
            try { return SimplifyCore(source, targetRatio, out resultTris); }
            catch (Exception e)
            {
                Debug.LogWarning($"[QEM] simplify failed for {source.name}: {e.Message}");
                return null;
            }
        }

        private static Mesh SimplifyCore(Mesh source, float targetRatio, out int resultTris)
        {
            resultTris = 0;
            Vector3[] positions = source.vertices;
            if (positions == null || positions.Length == 0) return null;

            int subMeshCount = source.subMeshCount;
            var faces = new List<Face>();
            for (int s = 0; s < subMeshCount; s++)
            {
                if (source.GetTopology(s) != MeshTopology.Triangles) return null;
                int[] idx = source.GetTriangles(s);
                for (int i = 0; i + 2 < idx.Length; i += 3)
                    faces.Add(new Face { w0 = idx[i], w1 = idx[i + 1], w2 = idx[i + 2], submesh = s });
            }
            int totalFaces = faces.Count;
            if (totalFaces < 400) return null; // not worth simplifying
            int targetFaces = Mathf.Max(64, Mathf.RoundToInt(totalFaces * targetRatio));

            // ---- Weld wedge vertices into position clusters ----
            int n = positions.Length;
            var clusterOf = new int[n];
            var clusterRep = new List<int>();     // representative wedge index per cluster
            var keyMap = new Dictionary<Vector3Int, int>(n);
            const float weldScale = 100000f;      // 1e-5 position weld
            for (int i = 0; i < n; i++)
            {
                Vector3 p = positions[i];
                var key = new Vector3Int(Mathf.RoundToInt(p.x * weldScale),
                                         Mathf.RoundToInt(p.y * weldScale),
                                         Mathf.RoundToInt(p.z * weldScale));
                if (!keyMap.TryGetValue(key, out int c))
                {
                    c = clusterRep.Count;
                    clusterRep.Add(i);
                    keyMap.Add(key, c);
                }
                clusterOf[i] = c;
            }
            int clusterCount = clusterRep.Count;
            var clusterPos = new Vector3[clusterCount];
            for (int c = 0; c < clusterCount; c++) clusterPos[c] = positions[clusterRep[c]];

            for (int f = 0; f < faces.Count; f++)
            {
                Face face = faces[f];
                face.c0 = clusterOf[face.w0];
                face.c1 = clusterOf[face.w1];
                face.c2 = clusterOf[face.w2];
                if (face.c0 == face.c1 || face.c1 == face.c2 || face.c0 == face.c2) face.dead = true;
                faces[f] = face;
            }

            // ---- Quadrics, adjacency, edges ----
            var quadrics = new Quadric[clusterCount];
            var facesOfCluster = new List<int>[clusterCount];
            for (int c = 0; c < clusterCount; c++) facesOfCluster[c] = new List<int>(8);
            var edgeUse = new Dictionary<long, int>(totalFaces * 2);
            int liveFaces = 0;

            for (int f = 0; f < faces.Count; f++)
            {
                Face face = faces[f];
                if (face.dead) continue;
                liveFaces++;
                Vector3 p0 = clusterPos[face.c0], p1 = clusterPos[face.c1], p2 = clusterPos[face.c2];
                Vector3 nrm = Vector3.Cross(p1 - p0, p2 - p0);
                double area2 = nrm.magnitude;
                if (area2 < 1e-12)
                {
                    face.dead = true; faces[f] = face; liveFaces--;
                    continue;
                }
                Vector3 unit = nrm / (float)area2;
                double d = -Vector3.Dot(unit, p0);
                var q = Quadric.FromPlane(unit.x, unit.y, unit.z, d, area2 * 0.5);
                quadrics[face.c0].Add(q);
                quadrics[face.c1].Add(q);
                quadrics[face.c2].Add(q);
                facesOfCluster[face.c0].Add(f);
                facesOfCluster[face.c1].Add(f);
                facesOfCluster[face.c2].Add(f);
                CountEdge(edgeUse, face.c0, face.c1);
                CountEdge(edgeUse, face.c1, face.c2);
                CountEdge(edgeUse, face.c2, face.c0);
            }

            // Border penalty: edges used by exactly one face get a perpendicular
            // constraint plane so open boundaries keep their silhouette.
            foreach (var kv in edgeUse)
            {
                if (kv.Value != 1) continue;
                int a = (int)(kv.Key >> 32), b = (int)(kv.Key & 0xffffffff);
                // find the single face using this edge to compute a constraint normal
                Vector3 pa = clusterPos[a], pb = clusterPos[b];
                Vector3 edge = pb - pa;
                foreach (int f in facesOfCluster[a])
                {
                    Face face = faces[f];
                    if (face.dead) continue;
                    bool hasB = face.c0 == b || face.c1 == b || face.c2 == b;
                    if (!hasB) continue;
                    Vector3 fp0 = clusterPos[face.c0], fp1 = clusterPos[face.c1], fp2 = clusterPos[face.c2];
                    Vector3 fn = Vector3.Cross(fp1 - fp0, fp2 - fp0).normalized;
                    Vector3 cn = Vector3.Cross(edge, fn).normalized;
                    if (cn.sqrMagnitude < 0.5f) continue;
                    double d = -Vector3.Dot(cn, pa);
                    double w = edge.sqrMagnitude * 100.0; // strong border weight
                    var q = Quadric.FromPlane(cn.x, cn.y, cn.z, d, w);
                    quadrics[a].Add(q);
                    quadrics[b].Add(q);
                    break;
                }
            }

            var version = new int[clusterCount];
            var heap = new MinHeap();
            foreach (var kv in edgeUse)
            {
                int a = (int)(kv.Key >> 32), b = (int)(kv.Key & 0xffffffff);
                PushEdge(heap, quadrics, clusterPos, version, a, b);
            }

            // ---- Greedy collapse loop ----
            var parent = new int[clusterCount];
            for (int c = 0; c < clusterCount; c++) parent[c] = c;
            var scratch = new List<int>(32);

            while (liveFaces > targetFaces && heap.Count > 0)
            {
                HeapEntry e = heap.Pop();
                int a = Find(parent, e.a), b = Find(parent, e.b);
                if (a == b) continue;
                if (e.va != version[e.a] || e.vb != version[e.b]) continue; // stale

                // Normal-flip guard: faces touching a but not b must not invert.
                bool reject = false;
                Vector3 newPos = clusterPos[b];
                foreach (int f in facesOfCluster[a])
                {
                    Face face = faces[f];
                    if (face.dead) continue;
                    if (face.c0 == b || face.c1 == b || face.c2 == b) continue; // will degenerate or share b
                    Vector3 q0 = face.c0 == a ? newPos : clusterPos[face.c0];
                    Vector3 q1 = face.c1 == a ? newPos : clusterPos[face.c1];
                    Vector3 q2 = face.c2 == a ? newPos : clusterPos[face.c2];
                    Vector3 oldN = Vector3.Cross(clusterPos[face.c1] - clusterPos[face.c0],
                                                 clusterPos[face.c2] - clusterPos[face.c0]);
                    Vector3 newN = Vector3.Cross(q1 - q0, q2 - q0);
                    if (Vector3.Dot(oldN, newN) < 0f || newN.sqrMagnitude < 1e-14f) { reject = true; break; }
                }
                if (reject) continue;

                // Merge a into b.
                parent[a] = b;
                quadrics[b].Add(quadrics[a]);
                version[a]++;
                version[b]++;

                scratch.Clear();
                foreach (int f in facesOfCluster[a])
                {
                    Face face = faces[f];
                    if (face.dead) continue;
                    if (face.c0 == a) face.c0 = b;
                    if (face.c1 == a) face.c1 = b;
                    if (face.c2 == a) face.c2 = b;
                    if (face.c0 == face.c1 || face.c1 == face.c2 || face.c0 == face.c2)
                    {
                        face.dead = true;
                        liveFaces--;
                    }
                    else
                    {
                        scratch.Add(f);
                    }
                    faces[f] = face;
                }
                foreach (int f in scratch) facesOfCluster[b].Add(f);
                facesOfCluster[a].Clear();

                // Refresh candidate edges around b.
                var neighbors = new HashSet<int>();
                foreach (int f in facesOfCluster[b])
                {
                    Face face = faces[f];
                    if (face.dead) continue;
                    if (face.c0 != b) neighbors.Add(face.c0);
                    if (face.c1 != b) neighbors.Add(face.c1);
                    if (face.c2 != b) neighbors.Add(face.c2);
                }
                foreach (int nb in neighbors)
                    PushEdge(heap, quadrics, clusterPos, version, b, nb);
            }

            // ---- Rebuild ----
            // Wedge vertices whose cluster merged move to the surviving cluster position.
            var finalPos = new Vector3[n];
            for (int i = 0; i < n; i++)
                finalPos[i] = clusterPos[Find(parent, clusterOf[i])];

            var usedWedges = new Dictionary<int, int>(n / 2);
            var subTris = new List<int>[subMeshCount];
            for (int s = 0; s < subMeshCount; s++) subTris[s] = new List<int>();
            int outFaces = 0;
            foreach (var face in faces)
            {
                if (face.dead) continue;
                outFaces++;
                subTris[face.submesh].Add(MapWedge(usedWedges, face.w0));
                subTris[face.submesh].Add(MapWedge(usedWedges, face.w1));
                subTris[face.submesh].Add(MapWedge(usedWedges, face.w2));
            }
            if (outFaces == 0) return null;

            int outVerts = usedWedges.Count;
            var outPos = new Vector3[outVerts];
            foreach (var kv in usedWedges) outPos[kv.Value] = finalPos[kv.Key];

            var result = new Mesh
            {
                name = source.name + "_simplified",
                indexFormat = outVerts > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            result.vertices = outPos;
            CopyAttr(source.uv, usedWedges, outVerts, v => result.uv = v);
            CopyAttr(source.uv2, usedWedges, outVerts, v => result.uv2 = v);
            CopyAttr(source.colors32, usedWedges, outVerts, v => result.colors32 = v);

            result.subMeshCount = subMeshCount;
            for (int s = 0; s < subMeshCount; s++)
                result.SetTriangles(subTris[s], s, false);

            result.RecalculateNormals();
            if (source.tangents != null && source.tangents.Length == n)
                result.RecalculateTangents();
            result.RecalculateBounds();

            resultTris = outFaces;
            return result;
        }

        private static void CopyAttr<T>(T[] src, Dictionary<int, int> usedWedges, int outVerts, Action<T[]> assign)
        {
            if (src == null || src.Length == 0) return;
            var dst = new T[outVerts];
            foreach (var kv in usedWedges)
                if (kv.Key < src.Length) dst[kv.Value] = src[kv.Key];
            assign(dst);
        }

        private static int MapWedge(Dictionary<int, int> map, int wedge)
        {
            if (!map.TryGetValue(wedge, out int mapped))
            {
                mapped = map.Count;
                map.Add(wedge, mapped);
            }
            return mapped;
        }

        private static int Find(int[] parent, int c)
        {
            while (parent[c] != c)
            {
                parent[c] = parent[parent[c]];
                c = parent[c];
            }
            return c;
        }

        private static void CountEdge(Dictionary<long, int> edges, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            edges.TryGetValue(key, out int count);
            edges[key] = count + 1;
        }

        private static void PushEdge(MinHeap heap, Quadric[] quadrics, Vector3[] clusterPos, int[] version, int a, int b)
        {
            if (a == b) return;
            Quadric sum = quadrics[a];
            sum.Add(quadrics[b]);
            double costToB = sum.Evaluate(clusterPos[b]); // collapse a -> b
            double costToA = sum.Evaluate(clusterPos[a]); // collapse b -> a
            if (costToB <= costToA)
                heap.Push(new HeapEntry { cost = costToB, a = a, b = b, va = version[a], vb = version[b] });
            else
                heap.Push(new HeapEntry { cost = costToA, a = b, b = a, va = version[b], vb = version[a] });
        }
    }
}
