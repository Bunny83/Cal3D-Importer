/* * * * *
 * Cal3D importer
 * -------------
 * 
 * This is a collection of some tools to read (mostly) the binary representation of
 * a Cal3D model (*.csf[skeleton], *.cmf[mesh], *.caf[animation], *.crf[material]).
 * It uses data types from the Unity engine (Vector2, Vector3, Quaterion, Matrix4x4)
 * but could be edited to work without that dependency. It reads the data into
 * appropriate class types for further processing.
 * 
 * Currently the XML format is not supported besides for materials (*.xrf).
 * I've also implemented a reader for a character configuration file (*.cfg) which
 * doesn't seem to be part of the specification but seems to be a de facto standard.
 * Such a config file just groups all the seperate files together in a simple key - 
 * value pair format. It also contains a scale factor.
 * 
 * NOTE: All data is read into a left-handed-system with y up and z forward. It does
 *       perform an axis flip between y and z. See the "RHS2LHS" helpers at the end
 *       for more details.
 *       
 *       The specification of the format already warns that it does not yet specify
 *       a specific endian format for the binary data. This loader uses the
 *       BinaryReader class which usually reads in little endian. All sample files
 *       i got were in little endian.
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
using System.IO;
using System.Text;
using System.Xml;
using UnityEngine;

namespace B83.MeshTools.Cal3D
{
    public class Cal3DBone
    {
        public string name;
        public int id;
        public Vector3 localPos;
        public Quaternion localRot;
        public Matrix4x4 localToBone;
        public List<int> children;
        public int parent;
    }

    public class Cal3DSkeleton
    {
        public List<Cal3DBone> bones;
    }

    public struct Cal3DKeyframe
    {
        public float time;
        public Vector3 localPos;
        public Quaternion localRot;
    }
    public class Cal3DAnimTrack
    {
        public int boneID;
        public List<Cal3DKeyframe> keyframes;
    }
    public class Cal3DAnimation
    {
        public string name;
        public float duration;
        public List<Cal3DAnimTrack> tracks;
    }

    public struct Cal3DBoneWeight
    {
        public int boneID;
        public float weight;
        public Cal3DBoneWeight(int aBoneID, float aWeight)
        {
            boneID = aBoneID;
            weight = aWeight;
        }
    }
    public class Cal3DVertex
    {
        public Vector3 localPos;
        public Vector3 localNormal;
        public int collapseID;
        public int faceCollapseCount;
        public Vector2[] uvMaps;
        public List<Cal3DBoneWeight> boneWeights;
        public float weight;
    }
    public struct Cal3DSpring
    {
        public int vertexID0;
        public int vertexID1;
        public float springCoeff;
        public float idleLength;
    }

    public class Cal3DSubmesh
    {
        public int materialID;
        public int LODsteps;
        public int uvCount;
        public List<Cal3DVertex> vertices;
        public List<Cal3DSpring> springs;
        public List<int> triangles;
    }
    public class Cal3DMesh
    {
        public string name;
        public List<Cal3DSubmesh> submeshes;
    }

    public class Cal3DMaterial
    {
        public string name;
        public Color32 ambientColor;
        public Color32 diffuseColor;
        public Color32 specularColor;
        public float shininess;
        public List<string> textureNames;
    }

    public class Cal3DObject
    {
        public string name;
        public DirectoryInfo path;
        public float scale;
        public Cal3DSkeleton skeleton;
        public List<Cal3DMesh> meshes = new List<Cal3DMesh>();
        public List<Cal3DAnimation> animations = new List<Cal3DAnimation>();
        public List<Cal3DMaterial> materials = new List<Cal3DMaterial>();
    }

    public class Cal3DTools
    {
        private static uint m_CSFMagic = 0x00465343;
        private static uint m_CAFMagic = 0x00464143;
        private static uint m_CMFMagic = 0x00464D43;
        private static uint m_CRFMagic = 0x00465243;

        #region Cal3DSkeleton
        public static Cal3DSkeleton ReadSkeleton(string aFileName, float aScale)
        {
            using (var stream = File.Open(aFileName, FileMode.Open))
            using (var reader = new BinaryReader(stream))
                return ReadSkeleton(reader, aScale);
        }
        public static Cal3DSkeleton ReadSkeleton(BinaryReader aReader, float aScale)
        {
            if (aReader.ReadUInt32() != m_CSFMagic)
                return null;
            int format = aReader.ReadInt32();
            if (format != 700)
                Debug.LogWarning("CSF file format is " + format + " while 700 was expected");
            int boneCount = aReader.ReadInt32();
            var skeleton = new Cal3DSkeleton();
            skeleton.bones = new List<Cal3DBone>(boneCount);
            for (int i = 0; i < boneCount; i++)
                skeleton.bones.Add(ReadBone(aReader, i, aScale));
            return skeleton;
        }
        private static Cal3DBone ReadBone(BinaryReader aReader, int aID, float aScale)
        {
            var bone = new Cal3DBone();
            bone.id = aID;
            bone.name = ReadLengthString(aReader);
            bone.localPos = RHS2LHS(ReadVector3(aReader))* aScale;
            bone.localRot = RHS2LHS(ReadQuaternion(aReader));
            bone.localToBone = Matrix4x4.TRS(RHS2LHS(ReadVector3(aReader))* aScale, RHS2LHS(ReadQuaternion(aReader)), Vector3.one);
            bone.parent = aReader.ReadInt32();
            int count = aReader.ReadInt32();
            bone.children = new List<int>(count);
            for (int i = 0; i < count; i++)
                bone.children.Add(aReader.ReadInt32());
            return bone;
        }

        #endregion Cal3DSkeleton

        #region Cal3DMesh
        public static Cal3DMesh ReadMesh(string aFileName, float aScale)
        {
            using (var stream = File.Open(aFileName, FileMode.Open))
            using (var reader = new BinaryReader(stream))
                return ReadMesh(reader, aScale, Path.GetFileNameWithoutExtension(aFileName));
        }
        public static Cal3DMesh ReadMesh(BinaryReader aReader, float aScale, string aName)
        {
            if (aReader.ReadUInt32() != m_CMFMagic)
                return null;
            int format = aReader.ReadInt32();
            if (format != 700)
                Debug.LogWarning("CMF file format is " + format + " while 700 was expected");
            var mesh = new Cal3DMesh();
            mesh.name = aName;
            int subMeshCount = aReader.ReadInt32();
            mesh.submeshes = new List<Cal3DSubmesh>(subMeshCount);
            for (int i = 0; i < subMeshCount; i++)
                mesh.submeshes.Add(ReadSubmesh(aReader, aScale));
            return mesh;
        }
        private static Cal3DSubmesh ReadSubmesh(BinaryReader aReader, float aScale)
        {
            var submesh = new Cal3DSubmesh();
            submesh.materialID = aReader.ReadInt32();
            int vertexCount = aReader.ReadInt32();
            int triangleCount = aReader.ReadInt32();
            submesh.LODsteps = aReader.ReadInt32();
            int springCount = aReader.ReadInt32();
            submesh.uvCount = aReader.ReadInt32();
            int indicesCount = triangleCount * 3;
            submesh.vertices = new List<Cal3DVertex>(vertexCount);
            submesh.triangles = new List<int>(indicesCount);
            submesh.springs = new List<Cal3DSpring>(springCount);
            for (int i = 0; i < vertexCount; i++)
                submesh.vertices.Add(ReadVertex(aReader, submesh.uvCount, springCount, aScale));
            for (int i = 0; i < springCount; i++)
            {
                var spring = new Cal3DSpring();
                spring.vertexID0 = aReader.ReadInt32();
                spring.vertexID1 = aReader.ReadInt32();
                spring.springCoeff = aReader.ReadSingle();
                spring.idleLength = aReader.ReadSingle() * aScale;
                submesh.springs.Add(spring);
            }
            for (int i = 0; i < indicesCount; i += 3)
            {
                int i0 = aReader.ReadInt32();
                int i1 = aReader.ReadInt32();
                int i2 = aReader.ReadInt32();
                submesh.triangles.Add(i0);
                submesh.triangles.Add(i2);
                submesh.triangles.Add(i1);
            }
            return submesh;
        }

        private static Cal3DVertex ReadVertex(BinaryReader aReader, int uvMapCount, int springCount, float aScale)
        {
            var vert = new Cal3DVertex();
            vert.localPos = RHS2LHS(ReadVector3(aReader))*aScale;
            vert.localNormal = RHS2LHS(ReadVector3(aReader));
            vert.collapseID = aReader.ReadInt32();
            vert.faceCollapseCount = aReader.ReadInt32();
            vert.uvMaps = new Vector2[uvMapCount];
            for (int i = 0; i < uvMapCount; i++)
            {
                vert.uvMaps[i] = ReadVector2(aReader);
                vert.uvMaps[i].y = -vert.uvMaps[i].y;
            }
            int boneWeightCount = aReader.ReadInt32();
            vert.boneWeights = new List<Cal3DBoneWeight>(boneWeightCount);
            for (int i = 0; i < boneWeightCount; i++)
                vert.boneWeights.Add(new Cal3DBoneWeight(aReader.ReadInt32(), aReader.ReadSingle()));
            if (springCount > 0)
                vert.weight = aReader.ReadSingle();
            return vert;
        }

        #endregion Cal3DMesh

        #region Cal3DAnimation
        public static Cal3DAnimation ReadAnimation(string aFileName, float aScale)
        {
            using (var stream = File.Open(aFileName, FileMode.Open))
            using (var reader = new BinaryReader(stream))
                return ReadAnimation(reader, aScale, Path.GetFileNameWithoutExtension(aFileName));
        }
        public static Cal3DAnimation ReadAnimation(BinaryReader aReader, float aScale, string aName)
        {
            if (aReader.ReadUInt32() != m_CAFMagic)
                return null;
            int format = aReader.ReadInt32();
            if (format != 700)
                Debug.LogWarning("CAF file format is " + format + " while 700 was expected");
            var anim = new Cal3DAnimation();
            anim.name = aName;
            anim.duration = aReader.ReadSingle();
            int trackCount = aReader.ReadInt32();
            anim.tracks = new List<Cal3DAnimTrack>(trackCount);
            for (int i = 0; i < trackCount; i++)
                anim.tracks.Add(ReadTrack(aReader, aScale));
            return anim;
        }
        private static Cal3DAnimTrack ReadTrack(BinaryReader aReader, float aScale)
        {
            var track = new Cal3DAnimTrack();
            track.boneID = aReader.ReadInt32();
            int keyframeCount = aReader.ReadInt32();
            track.keyframes = new List<Cal3DKeyframe>(keyframeCount);
            for (int i = 0; i < keyframeCount; i++)
            {
                var frame = new Cal3DKeyframe();
                frame.time = aReader.ReadSingle();
                frame.localPos = RHS2LHS(ReadVector3(aReader)) * aScale;
                frame.localRot = RHS2LHS(ReadQuaternion(aReader));
                track.keyframes.Add(frame);
            }
            return track;
        }

        #endregion Cal3DAnimation

        #region Cal3DMaterial
        public static Cal3DMaterial ReadMaterial(string aFileName)
        {
            using (var stream = File.Open(aFileName, FileMode.Open))
            using (var reader = new BinaryReader(stream))
                return ReadMaterial(reader, Path.GetFileNameWithoutExtension(aFileName));
        }
        public static Cal3DMaterial ReadMaterialXML(string aFileName)
        {
            using (var stream = File.Open(aFileName, FileMode.Open))
                return ReadMaterialXML(stream, Path.GetFileNameWithoutExtension(aFileName));
        }

        public static Cal3DMaterial ReadMaterial(BinaryReader aReader, string aName)
        {
            if (aReader.ReadUInt32() != m_CRFMagic)
                return null;
            int format = aReader.ReadInt32();
            if (format != 700)
                Debug.LogWarning("CRF file format is " + format + " while 700 was expected");
            var mat = new Cal3DMaterial();
            mat.textureNames = new List<string>();
            mat.name = aName;

            mat.ambientColor = ReadColor32(aReader);
            mat.diffuseColor = ReadColor32(aReader);
            mat.specularColor = ReadColor32(aReader);
            mat.shininess = aReader.ReadSingle();
            int textureCount = aReader.ReadInt32();
            for(int i = 0; i < textureCount; i++)
                mat.textureNames.Add(ReadLengthString(aReader).Replace("\0",""));
            return mat;
        }
        public static Cal3DMaterial ReadMaterialXML(Stream aStream, string aName)
        {
            Cal3DMaterial mat = new Cal3DMaterial();
            mat.name = aName;
            mat.textureNames = new List<string>();
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.ConformanceLevel = ConformanceLevel.Fragment;
            using (XmlReader reader = XmlReader.Create(aStream, settings))
            {
                while (reader.Read())
                {
                    if (reader.IsStartElement())
                    {
                        switch(reader.Name.ToUpper())
                        {
                            case "HEADER":
                                var magic = reader.GetAttribute("MAGIC");
                                if (magic != "XRF")
                                {
                                    Debug.LogError("File is not a XML Cal3D material file. MAGIC should be XRF but actually is " + magic);
                                    return null;
                                }
                                var version = reader.GetAttribute("VERSION");
                                if (version != "900")
                                    Debug.LogWarning("XRF file version not 900. Got " + version);
                                break;
                            case "AMBIENT":
                                {
                                    reader.Read();
                                    mat.ambientColor = ParseColor32(reader.ReadContentAsString());
                                }
                                break;
                            case "DIFFUSE":
                                {
                                    reader.Read();
                                    mat.diffuseColor = ParseColor32(reader.ReadContentAsString());
                                }
                                break;
                            case "SPECULAR":
                                {
                                    reader.Read();
                                    mat.specularColor = ParseColor32(reader.ReadContentAsString());
                                }
                                break;
                            case "SHININESS":
                                {
                                    reader.Read();
                                    mat.shininess = reader.ReadContentAsFloat();
                                }
                                break;
                            case "MAP":
                                {
                                    reader.Read();
                                    mat.textureNames.Add(reader.ReadContentAsString());
                                }
                                break;
                        }
                    }
                }
            }
            return mat;
        }

        private static Color32 ParseColor32(string aText)
        {
            Color32 col = Color.white;
            aText = aText.Trim();
            var data = aText.Split(' ');
            if (data.Length > 0)
                byte.TryParse(data[0], out col.r);
            if (data.Length > 1)
                byte.TryParse(data[1], out col.g);
            if (data.Length > 2)
                byte.TryParse(data[2], out col.b);
            if (data.Length > 3)
                byte.TryParse(data[3], out col.a);
            return col;
        }

        #endregion Cal3DMaterial


        public static Cal3DObject ReadObject(string aFileName)
        {
            FileInfo file = new FileInfo(aFileName);
            using (var stream = File.Open(aFileName, FileMode.Open))
            using (var reader = new StreamReader(stream))
                return ReadObject(reader, file.Directory, Path.GetFileNameWithoutExtension(file.Name));
        }

        public static Cal3DObject ReadObject(TextReader aReader, DirectoryInfo aPath, string aName)
        {
            var obj = new Cal3DObject();
            obj.name = aName;
            obj.path = aPath;
            string line;
            while ((line = aReader.ReadLine())!= null)
            {
                int i = line.IndexOf('#');
                if (i == 0)
                    continue;
                else if (i > 0)
                    line = line.Substring(0,i);
                i = line.IndexOf('=');
                if (i < 0)
                    continue;
                string name = line.Substring(0, i).Trim().ToLower();
                string value = line.Substring(i + 1).Trim();
                switch(name)
                {
                    case "scale":
                        float.TryParse(value, out obj.scale);
                        break;
                    case "skeleton":
                        obj.skeleton = ReadSkeleton(Path.Combine(aPath.FullName, value), obj.scale);
                        break;
                    case "animation":
                        obj.animations.Add(ReadAnimation(Path.Combine(aPath.FullName, value),obj.scale));
                        break;
                    case "mesh":
                        obj.meshes.Add(ReadMesh(Path.Combine(aPath.FullName, value), obj.scale));
                        break;
                    case "material":
                        FileInfo fileName = new FileInfo(Path.Combine(aPath.FullName, value));
                        var ext = fileName.Extension.ToLower();
                        if (ext == ".xrf")
                            obj.materials.Add(ReadMaterialXML(fileName.FullName));
                        else
                            obj.materials.Add(ReadMaterial(fileName.FullName));
                        break;
                }
            }
            return obj;
        }

        private static string ReadLengthString(BinaryReader aReader)
        {
            int l = aReader.ReadInt32();
            StringBuilder sb = new StringBuilder(l);
            for (int i = 0; i < l; i++)
                sb.Append((char)aReader.ReadByte());
            return sb.ToString();
        }
        private static Color32 ReadColor32(BinaryReader aReader)
        {
            return new Color32(aReader.ReadByte(), aReader.ReadByte(), aReader.ReadByte(), aReader.ReadByte());
        }
        private static Vector3 ReadVector2(BinaryReader aReader)
        {
            return new Vector3(aReader.ReadSingle(), aReader.ReadSingle());
        }
        private static Vector3 ReadVector3(BinaryReader aReader)
        {
            return new Vector3(aReader.ReadSingle(), aReader.ReadSingle(), aReader.ReadSingle());
        }
        private static Quaternion ReadQuaternion(BinaryReader aReader)
        {
            return new Quaternion(aReader.ReadSingle(), aReader.ReadSingle(), aReader.ReadSingle(), aReader.ReadSingle());
        }
        private static Quaternion RHS2LHS(Quaternion aQ)
        {
            var v = RHS2LHS(new Vector3(aQ.x, aQ.y, aQ.z));
            return new Quaternion(v.x, v.y, v.z, -aQ.w);
        }
        private static Vector3 RHS2LHS(Vector3 aV)
        {
            return new Vector3(aV.x, aV.z, -aV.y);
        }
    }
}
