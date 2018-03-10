/* * * * *
 * Cal3D importer
 * -------------
 * 
 * This file contains the bridge between the imported Cal3D data and Unity. This
 * file is an editor script so it need to be placed in an editor folder in the
 * project. This file required the Cal3DTools.cs to work properly
 * 
 * Note: To import a Cal3D model into Unity you should first copy all the file
 *       into a folder in your project. The import method will create a prefab
 *       in the same folder as the *.cfg file so the source file has to be inside
 *       a valid assetpath.
 *       Currently all generated assets are stored as subassets of the generated
 *       prefab which includes Meshes, AnimationClips as well as Materials.
 * 
 * The MIT License (MIT)
 * 
 * Copyright (c) 2018+ Markus Göbel (Bunny83)
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 * 
 * * * * */

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

namespace B83.MeshTools.Editor
{
    using B83.MeshTools.Cal3D;
    public class Cal3DImportWindow : EditorWindow
    {
        [MenuItem("Tools/Cal3D/Open Importer")]
        static void Init()
        {
            GetWindow<Cal3DImportWindow>("Cal3D Importer");
        }
        static string[] fileFilters = new string[] { "Cal3D Object", "cfg"};

        [MenuItem("Tools/Cal3D/Import *cfg")]
        static void ImportCal3D()
        {
            string file = EditorUtility.OpenFilePanelWithFilters("Select file", "", fileFilters);
            if (!string.IsNullOrEmpty(file))
            {
                var cfg = Cal3DTools.ReadObject(file);
                Cal3DToUnity.CreatePrefab(cfg, new FileInfo(file).Directory.FullName);
            }
        }

        void OnGUI()
        {
            if (GUILayout.Button("Import Cal3D Configuration file"))
            {
                ImportCal3D();
            }
        }
    }

    public class Cal3DToUnity
    {
        public static void CreatePrefab(Cal3DObject aObj, string aTargetPath)
        {
            var obj = new GameObject(aObj.name);
            Transform root;
            var bones = CreateSkeleton(aObj.skeleton, out root);
            root.SetParent(obj.transform, false);
            //obj.AddComponent<DrawBones>();
            string prefabPath = PathToAssetPath(Path.Combine(aTargetPath, aObj.name + ".prefab"));
            var prefab = PrefabUtility.CreatePrefab(prefabPath, obj);
            List<Material> materials = new List<Material>(aObj.materials.Count);

            foreach(var mat in aObj.materials)
            {
                Material matInst = CreateMaterial(mat, aObj.path.FullName);
                if (matInst == null)
                {
                    matInst = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
                    Debug.LogWarning("material " + mat.name + " could not be created. Using default material");
                }
                else
                    AssetDatabase.AddObjectToAsset(matInst, prefab);
                materials.Add(matInst);
            }

            foreach (var mesh in aObj.meshes)
            {
                var meshObj = new GameObject(mesh.name);
                var renderer = meshObj.AddComponent<SkinnedMeshRenderer>();
                var meshInst = CreateMesh(mesh);
                AssetDatabase.AddObjectToAsset(meshInst, prefab);
                renderer.sharedMesh = meshInst;
                renderer.rootBone = obj.transform;
                renderer.bones = bones;
                ApplyBindposes(renderer.sharedMesh, aObj.skeleton);
                var mats = new Material[mesh.submeshes.Count];
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = materials[mesh.submeshes[i].materialID];
                renderer.sharedMaterials = mats;
                renderer.localBounds = renderer.sharedMesh.bounds;
                meshObj.transform.SetParent(obj.transform, false);
            }

            if (aObj.animations.Count > 0)
            {
                var animation = obj.AddComponent<Animation>();
                foreach (var anim in aObj.animations)
                {
                    var clip = CreateAnimation(anim, aObj.skeleton, bones, obj.transform);
                    clip.legacy = true;
                    animation.AddClip(clip, clip.name);
                    AssetDatabase.AddObjectToAsset(clip, prefab);
                }
            }
            PrefabUtility.ReplacePrefab(obj, prefab);
            Object.DestroyImmediate(obj);
        }

        public static AnimationClip CreateAnimation(Cal3DAnimation aAnim, Cal3DSkeleton aSkel, Transform[] aBones, Transform aRoot)
        {
            var clip = new AnimationClip();
            clip.name = aAnim.name;
            foreach(var track in aAnim.tracks)
            {
                var xPosCurve = new AnimationCurve();
                var yPosCurve = new AnimationCurve();
                var zPosCurve = new AnimationCurve();

                var xRotCurve = new AnimationCurve();
                var yRotCurve = new AnimationCurve();
                var zRotCurve = new AnimationCurve();
                var wRotCurve = new AnimationCurve();
                for(int i = 0; i < track.keyframes.Count; i++)
                {
                    var kf = track.keyframes[i];
                    xPosCurve.AddKey(kf.time, kf.localPos.x);
                    yPosCurve.AddKey(kf.time, kf.localPos.y);
                    zPosCurve.AddKey(kf.time, kf.localPos.z);

                    xRotCurve.AddKey(kf.time, kf.localRot.x);
                    yRotCurve.AddKey(kf.time, kf.localRot.y);
                    zRotCurve.AddKey(kf.time, kf.localRot.z);
                    wRotCurve.AddKey(kf.time, kf.localRot.w);
                }

                var bone = aBones[track.boneID];
                var pathname = AnimationUtility.CalculateTransformPath(bone, aRoot);
                
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(pathname, typeof(Transform), "localPosition.x"), xPosCurve);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(pathname, typeof(Transform), "localPosition.y"), yPosCurve);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(pathname, typeof(Transform), "localPosition.z"), zPosCurve);

                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(pathname, typeof(Transform), "localRotation.x"), xRotCurve);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(pathname, typeof(Transform), "localRotation.y"), yRotCurve);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(pathname, typeof(Transform), "localRotation.z"), zRotCurve);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(pathname, typeof(Transform), "localRotation.w"), wRotCurve);
            }
            return clip;
        }
        public static Material CreateMaterial(Cal3DMaterial aMat, string aMaterialPath)
        {
            var mat = new Material(Shader.Find("Standard"));
            mat.name = aMat.name;
            mat.SetColor("_Color", aMat.diffuseColor);

            if (aMat.textureNames.Count > 0)
            {
                
                string path = Path.Combine(aMaterialPath, aMat.textureNames[0]);
                path = PathToAssetPath(path);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if(tex != null)
                    mat.SetTexture("_MainTex", tex);
            }
            return mat;
        }
        public static Mesh CreateMesh(Cal3DMesh aMesh)
        {
            var submeshes = aMesh.submeshes;
            var mesh = new Mesh();
            mesh.name = aMesh.name;
            mesh.subMeshCount = submeshes.Count;
            int uvCount = 0;
            int vertexCount = 0;
            foreach (var submesh in submeshes)
            {
                vertexCount += submesh.vertices.Count;
                if (submesh.uvCount > uvCount)
                    uvCount = submesh.uvCount;
                if (uvCount >= 4)
                {
                    uvCount = 4;
                    break;
                }
            }

            List<Vector3> vertices = new List<Vector3>(vertexCount);
            List<Vector3> normals = new List<Vector3>(vertexCount);
            List<BoneWeight> boneWeights = new List<BoneWeight>(vertexCount);
            List<List<Vector2>> uvs = new List<List<Vector2>>(uvCount);
            int[][] triangles = new int[submeshes.Count][];
            bool hasBoneWeights = false;
            for(int i = 0; i < uvCount; i++)
                uvs.Add(new List<Vector2>(vertexCount));

            for (int i = 0; i < submeshes.Count; i++)
            {
                var verts = submeshes[i].vertices;
                int vertexOffset = vertices.Count;
                for(int n = 0; n < verts.Count; n++)
                {
                    var v = verts[n];
                    vertices.Add(v.localPos);
                    normals.Add(v.localNormal);
                    int count = v.uvMaps.Length;
                    for (int j = 0; j < uvCount; j++)
                    {
                        if (j < count)
                            uvs[j].Add(v.uvMaps[j]);
                        else
                            uvs[j].Add(Vector2.zero);
                    }
                    BoneWeight w = new BoneWeight();
                    var weights = v.boneWeights;
                    if (weights.Count >= 1)
                    {
                        hasBoneWeights = true;
                        w.boneIndex0 = weights[0].boneID;
                        w.weight0 = weights[0].weight;
                    }
                    if (weights.Count >= 2)
                    {
                        w.boneIndex1 = weights[1].boneID;
                        w.weight1 = weights[1].weight;
                    }
                    if (weights.Count >= 3)
                    {
                        w.boneIndex2 = weights[2].boneID;
                        w.weight2 = weights[2].weight;
                    }
                    if (weights.Count >= 4)
                    {
                        w.boneIndex3 = weights[3].boneID;
                        w.weight3 = weights[3].weight;
                    }
                    boneWeights.Add(w);
                }
                var tmp = triangles[i] = new int[submeshes[i].triangles.Count];
                var tris = submeshes[i].triangles;
                for (int n = 0; n < tmp.Length; n++)
                    tmp[n] = tris[n] + vertexOffset;
            }
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            for (int i = 0; i < uvs.Count; i++)
                mesh.SetUVs(i, uvs[i]);
            if (hasBoneWeights)
                mesh.boneWeights = boneWeights.ToArray();
            for (int i = 0; i < submeshes.Count; i++)
                mesh.SetTriangles(triangles[i], i, false);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        public static void ApplyBindposes(Mesh aMesh, Cal3DSkeleton aSkel)
        {
            Matrix4x4[] bindposes = new Matrix4x4[aSkel.bones.Count];
            for(int i = 0; i < bindposes.Length; i++)
                bindposes[i] = aSkel.bones[i].localToBone;
            aMesh.bindposes = bindposes;
        }

        public static Transform[] CreateSkeleton(Cal3DSkeleton aSkel, out Transform aRoot)
        {
            var bones = new Transform[aSkel.bones.Count];
            for(int i = 0; i < bones.Length; i++)
                bones[i] = new GameObject(aSkel.bones[i].name).transform;

            for(int i = 0; i < bones.Length; i++)
            {
                var b = aSkel.bones[i];
                var p = bones[i];
                foreach(int child in b.children)
                {
                    var c = bones[child];
                    c.SetParent(p, false);
                }
                p.localPosition = b.localPos;
                p.localRotation = b.localRot;
            }
            aRoot = (bones.Length >0)?bones[0].root:null;
            return bones;
        }

        public static string PathToAssetPath(string aPath)
        {
            aPath = aPath.Substring(Application.dataPath.Length-6).Replace('\\', '/');
            return aPath;
        }
    }
}