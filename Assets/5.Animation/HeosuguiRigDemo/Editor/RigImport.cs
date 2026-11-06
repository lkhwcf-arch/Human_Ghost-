using System;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.Rendering;

namespace HeosuguiTrial.Editor
{
    internal sealed class RigImport : AssetPostprocessor
    {
        private const string Sheet = "Assets/5.Animation/HeosuguiRigDemo/Heosugui.png";
        private static bool queued;
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
            string[] moved, string[] oldPaths, bool domainReload)
        {
            if (queued || (!domainReload && !imported.Contains(Sheet))) return;
            queued = true;
            EditorApplication.delayCall += () => { queued = false; EnsureWeights(); };
        }

        [MenuItem("Tools/Heosugui/Verify Rig")]
        private static void EnsureWeights()
        {
            var importer = AssetImporter.GetAtPath(Sheet);
            if (importer == null) return;
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var rect = provider.GetSpriteRects().First(x => x.name.EndsWith("_0", StringComparison.Ordinal));
            var meshes = provider.GetDataProvider<ISpriteMeshDataProvider>();
            var vertices = meshes.GetVertices(rect.spriteID);
            if (vertices.Length == 0) { Debug.LogError("Heosugui rig mesh is missing."); return; }
            if (vertices.All(v => Total(v.boneWeight) > .999f)) return;
            for (int i = 0; i < vertices.Length; ++i)
                vertices[i].boneWeight = Weights(vertices[i].position);
            meshes.SetVertices(rect.spriteID, vertices);
            provider.Apply();
            importer.SaveAndReimport();
        }

        private static float Total(BoneWeight b) => b.weight0 + b.weight1 + b.weight2 + b.weight3;
        // Source-frame pixel coordinates, measured from the top left.
        private static readonly Vector4[] segments = {
            new Vector4(136, 151, 135, 125),
            new Vector4(135, 128, 132, 85),
            new Vector4(120, 74, 82, 35),
            new Vector4(115, 85, 91, 136),
            new Vector4(91, 136, 62, 191),
            new Vector4(62, 191, 47, 219),
            new Vector4(152, 84, 185, 117),
            new Vector4(185, 117, 210, 159),
            new Vector4(210, 159, 219, 201),
            new Vector4(119, 159, 108, 195),
            new Vector4(108, 195, 91, 228),
            new Vector4(91, 228, 71, 244),
            new Vector4(151, 160, 173, 196),
            new Vector4(173, 196, 198, 243)
        };

        private static BoneWeight Weights(Vector2 vertex)
        {
            var point = new Vector2(vertex.x, 256f - vertex.y);
            var ranks = segments.Select((s, i) => {
                Vector2 start = new Vector2(s.x, s.y);
                Vector2 axis = new Vector2(s.z, s.w) - start;
                float along = Mathf.Clamp01(Vector2.Dot(point - start, axis) / axis.sqrMagnitude);
                float distance = (point - start - along * axis).sqrMagnitude;
                double strength = Math.Pow(distance + 75, -2.6);
                if (point.y > 130 && point.y < 229 && (i < 3 || i > 5))
                {
                    float boundary = 112f - (point.y - 130f) * .35f;
                    strength *= 1 - Mathf.Clamp01((boundary - point.x) / 9f) * .9999;
                }
                return new { i, value = strength };
            }).OrderByDescending(x => x.value).Take(4).ToArray();
            double sum = ranks.Sum(x => x.value);
            return new BoneWeight {
                boneIndex0 = ranks[0].i, weight0 = (float)(ranks[0].value / sum),
                boneIndex1 = ranks[1].i, weight1 = (float)(ranks[1].value / sum),
                boneIndex2 = ranks[2].i, weight2 = (float)(ranks[2].value / sum),
                boneIndex3 = ranks[3].i, weight3 = (float)(ranks[3].value / sum)
            };
        }
    }
}
