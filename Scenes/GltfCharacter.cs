using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;
using Raylib_cs;

namespace CCM.Scenes;

// Evaluates the glTF skeleton ourselves. raylib's UpdateModelAnimation loses the
// root/skin transforms used by these exports and tears the arms from the body.
internal sealed class GltfCharacter
{
    private readonly GltfFile modelFile;
    private readonly Dictionary<string,GltfFile> clips;
    private readonly float[][] vertices;
    private readonly float[][] normals;
    private readonly int[] meshPrimitives;
    private readonly float worldScale;
    private readonly float floorY;
    private Vector3 lastPosition;
    private float lastSeconds=-1;
    private string state="idle";
    private float clipSeconds;
    internal Vector3 BoundsMin { get; private set; }
    internal Vector3 BoundsMax { get; private set; }

    public unsafe GltfCharacter(Model model,string modelPath,float height,string? prefix=null,
        GltfCharacter? reference=null)
    {
        modelFile=new GltfFile(Resolve(modelPath));
        clips=prefix is null
            ? new Dictionary<string,GltfFile>
            {
                ["idle"]=modelFile,["walk"]=modelFile,["run"]=modelFile
            }
            : new Dictionary<string,GltfFile>
            {
                ["idle"]=modelFile,
                ["walk"]=new GltfFile(Resolve(prefix+"_walk.glb")),
                ["run"]=new GltfFile(Resolve(prefix+"_run.glb"))
            };
        meshPrimitives=new int[model.MeshCount];
        vertices=new float[model.MeshCount][];
        normals=new float[model.MeshCount][];
        var used=new HashSet<int>();
        for (int m=0;m<model.MeshCount;m++)
        {
            int count=model.Meshes[m].VertexCount;
            int match=-1;
            for (int p=0;p<modelFile.Primitives.Count;p++)
                if (!used.Contains(p) && modelFile.Primitives[p].Positions.Length==count)
                { match=p; break; }
            if (match<0) throw new InvalidDataException(
                $"No glTF primitive matches raylib mesh {m} ({count} vertices)");
            meshPrimitives[m]=match;
            used.Add(match);
            vertices[m]=new float[count*3];
            normals[m]=new float[count*3];
        }
        // Outfits omit covered skin below the waist. Include boots and other
        // garment meshes when finding the floor and full character height.
        var pose=modelFile.Pose(clips["idle"],0,"idle");
        float bottom=float.PositiveInfinity,top=float.NegativeInfinity;
        foreach (var primitive in modelFile.Primitives)
            for (int v=0;v<primitive.Positions.Length;v++)
            {
                float y=modelFile.Vertex(primitive,v,pose).Y;
                bottom=Math.Min(bottom,y);
                top=Math.Max(top,y);
            }
        if (!float.IsFinite(bottom) || top-bottom<(reference is null ? 0.1f : 0.001f))
            throw new InvalidDataException("Character has no usable bounds");
        floorY=reference?.floorY ?? bottom;
        worldScale=reference?.worldScale ?? height/(top-bottom);
    }

    private static string Resolve(string path)
    {
        if (File.Exists(path)) return path;
        var output=Path.Combine(AppContext.BaseDirectory,path);
        if (File.Exists(output)) return output;
        throw new FileNotFoundException("Character animation asset is missing",path);
    }

    public unsafe void Update(Model model,Vector3 position,float seconds,
        GltfCharacter? reference=null,IReadOnlySet<string>? hiddenNodes=null)
    {
        if (reference is not null)
        {
            state=reference.state;
            clipSeconds=reference.clipSeconds;
        }
        else if (lastSeconds<0 || seconds<lastSeconds)
        {
            lastSeconds=seconds;
            lastPosition=position;
            state="idle";
            clipSeconds=0;
        }
        else if (reference is null && seconds>lastSeconds)
        {
            float elapsed=seconds-lastSeconds;
            var delta=position-lastPosition;
            delta.Y=0;
            float speed=delta.Length()/elapsed;
            string next=speed<0.1f ? "idle" : speed<3.5f ? "walk" : "run";
            clipSeconds=next==state ? clipSeconds+elapsed : 0;
            state=next;
            lastPosition=position;
            lastSeconds=seconds;
        }
        var pose=modelFile.Pose(clips[state],clipSeconds,state);
        BoundsMin=new Vector3(float.PositiveInfinity);
        BoundsMax=new Vector3(float.NegativeInfinity);
        for (int m=0;m<meshPrimitives.Length;m++)
        {
            var primitive=modelFile.Primitives[meshPrimitives[m]];
            bool hidden=hiddenNodes?.Contains(modelFile.NodeName(primitive))==true;
            var output=vertices[m];
            var normalOutput=normals[m];
            for (int v=0;v<primitive.Positions.Length;v++)
            {
                Vector3 point=hidden ? Vector3.Zero : modelFile.Vertex(primitive,v,pose);
                Vector3 normal=hidden ? Vector3.UnitY : modelFile.Normal(primitive,v,pose);
                int offset=v*3;
                output[offset]=point.X*worldScale;
                output[offset+1]=(point.Y-floorY)*worldScale;
                output[offset+2]=point.Z*worldScale;
                normalOutput[offset]=normal.X;
                normalOutput[offset+1]=normal.Y;
                normalOutput[offset+2]=normal.Z;
                var worldPoint=new Vector3(output[offset],output[offset+1],output[offset+2]);
                if (!hidden)
                {
                    BoundsMin=Vector3.Min(BoundsMin,worldPoint);
                    BoundsMax=Vector3.Max(BoundsMax,worldPoint);
                }
            }
            fixed (float* data=output)
                Raylib.UpdateMeshBuffer(model.Meshes[m],0,data,output.Length*sizeof(float),0);
            fixed (float* data=normalOutput)
                Raylib.UpdateMeshBuffer(model.Meshes[m],2,data,normalOutput.Length*sizeof(float),0);
        }
    }

    private sealed class GltfFile
    {
        internal sealed class Node
        {
            public string Name="";
            public int Parent=-1;
            public Vector3 Translation;
            public Quaternion Rotation=Quaternion.Identity;
            public Vector3 Scale=Vector3.One;
            public int Mesh=-1;
            public int Skin=-1;
        }
        internal sealed class Primitive
        {
            public int Node;
            public Vector3[] Positions=[];
            public Vector3[] Normals=[];
            public int[] Joints=[];
            public Vector4[] Weights=[];
            public int[] SkinJoints=[];
            public Matrix4x4[] InverseBinds=[];
        }
        private sealed class Track
        {
            public string Node="";
            public int NodeIndex;
            public string Path="";
            public float[] Times=[];
            public Vector4[] Values=[];
        }
        private readonly byte[] binary;
        private readonly JsonElement[] accessors;
        private readonly JsonElement[] views;
        private readonly List<Node> nodes=[];
        private readonly Dictionary<string,List<Track>> tracks=
            new(StringComparer.OrdinalIgnoreCase);
        public readonly List<Primitive> Primitives=[];
        public string NodeName(Primitive primitive)=>nodes[primitive.Node].Name;

        public GltfFile(string path)
        {
            byte[] bytes=File.ReadAllBytes(path);
            if (bytes.Length<20 || BinaryPrimitives.ReadUInt32LittleEndian(bytes)!=0x46546c67)
                throw new InvalidDataException($"Invalid GLB: {path}");
            int jsonLength=BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12));
            using var document=JsonDocument.Parse(bytes.AsMemory(20,jsonLength));
            var root=document.RootElement;
            int binOffset=20+((jsonLength+3)&~3);
            if (binOffset+8>bytes.Length) throw new InvalidDataException("GLB has no binary chunk");
            int binLength=BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(binOffset));
            binary=bytes.AsSpan(binOffset+8,binLength).ToArray();
            accessors=root.GetProperty("accessors").EnumerateArray().ToArray();
            views=root.GetProperty("bufferViews").EnumerateArray().ToArray();
            foreach (var element in root.GetProperty("nodes").EnumerateArray())
            {
                var node=new Node
                {
                    Name=element.TryGetProperty("name",out var name)?name.GetString()??"":"",
                    Translation=Vec3(element,"translation",Vector3.Zero),
                    Rotation=Quat(element,"rotation"),
                    Scale=Vec3(element,"scale",Vector3.One),
                    Mesh=Int(element,"mesh",-1),
                    Skin=Int(element,"skin",-1)
                };
                nodes.Add(node);
            }
            int index=0;
            foreach (var element in root.GetProperty("nodes").EnumerateArray())
            {
                if (element.TryGetProperty("children",out var children))
                    foreach (var child in children.EnumerateArray())
                        nodes[child.GetInt32()].Parent=index;
                index++;
            }
            if (root.TryGetProperty("meshes",out var meshes))
            {
                var meshArray=meshes.EnumerateArray().ToArray();
                var skins=root.TryGetProperty("skins",out var skinList)
                    ?skinList.EnumerateArray().ToArray():[];
                foreach (int n in Enumerable.Range(0,nodes.Count))
                {
                    var node=nodes[n];
                    if (node.Mesh<0) continue;
                    foreach (var part in meshArray[node.Mesh].GetProperty("primitives").EnumerateArray())
                    {
                        var attributes=part.GetProperty("attributes");
                        var primitive=new Primitive
                        {
                            Node=n,
                            Positions=ReadVec3(attributes.GetProperty("POSITION").GetInt32()),
                            Normals=ReadVec3(attributes.GetProperty("NORMAL").GetInt32())
                        };
                        if (node.Skin>=0 && attributes.TryGetProperty("JOINTS_0",out var joints))
                        {
                            primitive.Joints=ReadJoints(joints.GetInt32());
                            primitive.Weights=ReadVec4(attributes.GetProperty("WEIGHTS_0").GetInt32());
                            var skin=skins[node.Skin];
                            primitive.SkinJoints=skin.GetProperty("joints")
                                .EnumerateArray().Select(x=>x.GetInt32()).ToArray();
                            primitive.InverseBinds=ReadMatrices(
                                skin.GetProperty("inverseBindMatrices").GetInt32());
                        }
                        Primitives.Add(primitive);
                    }
                }
            }
            if (root.TryGetProperty("animations",out var animations))
            {
                foreach (var animation in animations.EnumerateArray())
                {
                    string clipName=animation.GetProperty("name").GetString()??"";
                    var clipTracks=new List<Track>();
                    tracks[clipName]=clipTracks;
                    var samplers=animation.GetProperty("samplers").EnumerateArray().ToArray();
                    foreach (var channel in animation.GetProperty("channels").EnumerateArray())
                    {
                        var target=channel.GetProperty("target");
                        int node=target.GetProperty("node").GetInt32();
                        string type=target.GetProperty("path").GetString()??"";
                        if (type is not ("translation" or "rotation" or "scale")) continue;
                        var sampler=samplers[channel.GetProperty("sampler").GetInt32()];
                        clipTracks.Add(new Track
                        {
                            Node=nodes[node].Name,
                            NodeIndex=node,
                            Path=type,
                            Times=ReadFloats(sampler.GetProperty("input").GetInt32()),
                            Values=ReadValues(sampler.GetProperty("output").GetInt32())
                        });
                    }
                }
            }
        }

        public Matrix4x4[] Pose(GltfFile? clip,float seconds,string clipName="")
        {
            var local=new Matrix4x4[nodes.Count];
            var global=new Matrix4x4[nodes.Count];
            var translations=nodes.Select(n=>n.Translation).ToArray();
            var rotations=nodes.Select(n=>n.Rotation).ToArray();
            var scales=nodes.Select(n=>n.Scale).ToArray();
            if (clip is not null)
            {
                if (!clip.tracks.TryGetValue(clipName,out var selected))
                    throw new InvalidDataException($"Missing {clipName} animation");
                foreach (var track in selected)
                {
                    int target=ReferenceEquals(clip,this) ? track.NodeIndex :
                        nodes.FindIndex(n=>n.Name==track.Node);
                    if (target<0) throw new InvalidDataException(
                        $"Animation bone {track.Node} is missing from model");
                    Vector4 value=Sample(track,seconds);
                    if (track.Path=="translation")
                        translations[target]=new Vector3(value.X,value.Y,value.Z);
                    else if (track.Path=="rotation")
                        rotations[target]=Quaternion.Normalize(
                            new Quaternion(value.X,value.Y,value.Z,value.W));
                    else scales[target]=new Vector3(value.X,value.Y,value.Z);
                }
            }
            for (int i=0;i<nodes.Count;i++)
                local[i]=Matrix4x4.CreateScale(scales[i])*
                    Matrix4x4.CreateFromQuaternion(rotations[i])*
                    Matrix4x4.CreateTranslation(translations[i]);
            var ready=new bool[nodes.Count];
            Matrix4x4 Resolve(int i)
            {
                if (ready[i]) return global[i];
                global[i]=nodes[i].Parent<0 ? local[i] :
                    local[i]*Resolve(nodes[i].Parent);
                ready[i]=true;
                return global[i];
            }
            for (int i=0;i<nodes.Count;i++) Resolve(i);
            return global;
        }

        public Vector3 Vertex(Primitive primitive,int vertex,Matrix4x4[] pose)
        {
            Vector3 position=primitive.Positions[vertex];
            if (primitive.Joints.Length==0)
                return Vector3.Transform(position,pose[primitive.Node]);
            Vector4 weights=primitive.Weights[vertex];
            float sum=weights.X+weights.Y+weights.Z+weights.W;
            if (sum<=0) return Vector3.Transform(position,pose[primitive.Node]);
            Vector3 result=Vector3.Zero;
            for (int i=0;i<4;i++)
            {
                float weight=i switch {0=>weights.X,1=>weights.Y,2=>weights.Z,_=>weights.W};
                if (weight<=0) continue;
                int joint=primitive.Joints[vertex*4+i];
                if (joint>=primitive.SkinJoints.Length) continue;
                var transform=primitive.InverseBinds[joint]*
                    pose[primitive.SkinJoints[joint]];
                result+=Vector3.Transform(position,transform)*(weight/sum);
            }
            return result;
        }

        public Vector3 Normal(Primitive primitive,int vertex,Matrix4x4[] pose)
        {
            Vector3 normal=primitive.Normals[vertex];
            if (primitive.Joints.Length==0)
                return Vector3.Normalize(Vector3.TransformNormal(normal,pose[primitive.Node]));
            Vector4 weights=primitive.Weights[vertex];
            float sum=weights.X+weights.Y+weights.Z+weights.W;
            if (sum<=0)
                return Vector3.Normalize(Vector3.TransformNormal(normal,pose[primitive.Node]));
            Vector3 result=Vector3.Zero;
            for (int i=0;i<4;i++)
            {
                float weight=i switch {0=>weights.X,1=>weights.Y,2=>weights.Z,_=>weights.W};
                if (weight<=0) continue;
                int joint=primitive.Joints[vertex*4+i];
                if (joint>=primitive.SkinJoints.Length) continue;
                var transform=primitive.InverseBinds[joint]*pose[primitive.SkinJoints[joint]];
                result+=Vector3.TransformNormal(normal,transform)*(weight/sum);
            }
            return result.LengthSquared()>0.000001f ? Vector3.Normalize(result) : Vector3.UnitY;
        }

        private static Vector4 Sample(Track track,float seconds)
        {
            int count=track.Times.Length;
            if (count==0) return Vector4.Zero;
            float duration=track.Times[^1];
            float time=duration>0 ? seconds%duration : 0;
            int right=Array.BinarySearch(track.Times,time);
            if (right>=0) return track.Values[right];
            right=~right;
            if (right<=0) return track.Values[0];
            if (right>=count) return track.Values[^1];
            int left=right-1;
            float amount=(time-track.Times[left])/
                (track.Times[right]-track.Times[left]);
            if (track.Path=="rotation")
            {
                var a=track.Values[left];
                var b=track.Values[right];
                var q=Quaternion.Slerp(
                    new Quaternion(a.X,a.Y,a.Z,a.W),
                    new Quaternion(b.X,b.Y,b.Z,b.W),amount);
                return new Vector4(q.X,q.Y,q.Z,q.W);
            }
            return Vector4.Lerp(track.Values[left],track.Values[right],amount);
        }

        private byte[] Element(int accessorIndex,int elementIndex,out int componentType,
            out int components)
        {
            var accessor=accessors[accessorIndex];
            var view=views[accessor.GetProperty("bufferView").GetInt32()];
            componentType=accessor.GetProperty("componentType").GetInt32();
            components=accessor.GetProperty("type").GetString() switch
            {"VEC2"=>2,"VEC3"=>3,"VEC4"=>4,"MAT4"=>16,_=>1};
            int size=componentType switch {5121=>1,5123=>2,5125=>4,5126=>4,
                _=>throw new InvalidDataException("Unsupported glTF component type")};
            int length=size*components;
            int stride=Int(view,"byteStride",length);
            int offset=Int(view,"byteOffset",0)+Int(accessor,"byteOffset",0)+
                elementIndex*stride;
            return binary.AsSpan(offset,length).ToArray();
        }
        private int Count(int accessor)=>accessors[accessor].GetProperty("count").GetInt32();
        private float[] ReadFloats(int accessor)
        {
            var result=new float[Count(accessor)];
            for (int i=0;i<result.Length;i++)
            {
                var data=Element(accessor,i,out _,out _);
                result[i]=BitConverter.ToSingle(data);
            }
            return result;
        }
        private Vector3[] ReadVec3(int accessor)
        {
            var result=new Vector3[Count(accessor)];
            for (int i=0;i<result.Length;i++)
            {
                var data=Element(accessor,i,out _,out _);
                result[i]=new(BitConverter.ToSingle(data,0),
                    BitConverter.ToSingle(data,4),BitConverter.ToSingle(data,8));
            }
            return result;
        }
        private Vector4[] ReadVec4(int accessor)
        {
            var result=new Vector4[Count(accessor)];
            for (int i=0;i<result.Length;i++)
            {
                var data=Element(accessor,i,out _,out _);
                result[i]=new(BitConverter.ToSingle(data,0),
                    BitConverter.ToSingle(data,4),BitConverter.ToSingle(data,8),
                    BitConverter.ToSingle(data,12));
            }
            return result;
        }
        private Vector4[] ReadValues(int accessor)
        {
            var result=new Vector4[Count(accessor)];
            for (int i=0;i<result.Length;i++)
            {
                var data=Element(accessor,i,out _,out int parts);
                result[i]=new(BitConverter.ToSingle(data,0),
                    parts>1?BitConverter.ToSingle(data,4):0,
                    parts>2?BitConverter.ToSingle(data,8):0,
                    parts>3?BitConverter.ToSingle(data,12):0);
            }
            return result;
        }
        private int[] ReadJoints(int accessor)
        {
            var result=new int[Count(accessor)*4];
            for (int i=0;i<result.Length/4;i++)
            {
                var data=Element(accessor,i,out int type,out _);
                for (int j=0;j<4;j++)
                    result[i*4+j]=type==5121?data[j]:
                        BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(j*2));
            }
            return result;
        }
        private Matrix4x4[] ReadMatrices(int accessor)
        {
            var result=new Matrix4x4[Count(accessor)];
            for (int i=0;i<result.Length;i++)
            {
                var data=Element(accessor,i,out _,out _);
                var f=new float[16];
                for (int j=0;j<16;j++) f[j]=BitConverter.ToSingle(data,j*4);
                result[i]=new Matrix4x4(f[0],f[1],f[2],f[3],
                    f[4],f[5],f[6],f[7],f[8],f[9],f[10],f[11],
                    f[12],f[13],f[14],f[15]);
            }
            return result;
        }
        private static int Int(JsonElement e,string key,int fallback)=>
            e.TryGetProperty(key,out var value)?value.GetInt32():fallback;
        private static Vector3 Vec3(JsonElement e,string key,Vector3 fallback)
        {
            if (!e.TryGetProperty(key,out var value)) return fallback;
            var a=value.EnumerateArray().Select(x=>x.GetSingle()).ToArray();
            return new(a[0],a[1],a[2]);
        }
        private static Quaternion Quat(JsonElement e,string key)
        {
            if (!e.TryGetProperty(key,out var value)) return Quaternion.Identity;
            var a=value.EnumerateArray().Select(x=>x.GetSingle()).ToArray();
            return new(a[0],a[1],a[2],a[3]);
        }
    }
}
