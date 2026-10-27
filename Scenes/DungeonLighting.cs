using System.Numerics;
using CCM.Game;
using Raylib_cs;

namespace CCM.Scenes;

internal sealed unsafe class DungeonLighting : IDisposable
{
    private const int MaxFireplaces=3;
    private const int LightsPerFireplace=3;
    private const int LightCount=MaxFireplaces*LightsPerFireplace;
    private readonly Model playerModel;
    private readonly Model enemyModel;
    private readonly Model fireplaceModel;
    private readonly Model cubeModel;
    private readonly DungeonWorld world;
    private readonly FireplaceLightRig[] rigs;
    private readonly Shader[] playerShaders;
    private readonly Shader[] enemyShaders;
    private readonly Shader[] fireplaceShaders;
    private readonly List<(Model Model,Shader[] Previous)> sceneryShaders=[];
    private readonly Shader shader;
    private readonly int doorMinLocation;
    private readonly int doorMaxLocation;
    private readonly int[] positionLocations=new int[LightCount];
    private readonly int[] colorLocations=new int[LightCount];
    private readonly int[] visibilityLocations=new int[LightCount];
    private readonly Vector3[] positions=new Vector3[LightCount];
    private readonly Vector3[] colors=new Vector3[LightCount];
    private bool disposed;

    public DungeonLighting(Model player,Model enemy,Model fireplace,DungeonWorld world,
        IEnumerable<Model>? sceneryModels=null)
    {
        this.world=world;
        if (world.Fireplaces.Count!=MaxFireplaces)
            throw new InvalidOperationException("Dungeon lighting expects three fireplaces.");
        rigs=
        [
            new(world.Fireplaces[0],
            [
                new(new(-6.5f,2.3f,4.6f),new(0.70f,0.67f,0.63f)),
                new(new(-4.4f,2.4f,3.2f),new(0.58f,0.56f,0.53f))
            ]),
            new(world.Fireplaces[1],
            [
                new(new(0.8f,2.4f,-10.7f),new(0.68f,0.65f,0.61f)),
                new(new(-0.8f,2.2f,-14.3f),new(0.58f,0.56f,0.53f))
            ]),
            new(world.Fireplaces[2],
            [
                new(new(6.5f,2.4f,-37.4f),new(0.70f,0.67f,0.63f)),
                new(new(3.0f,2.4f,-35.5f),new(0.59f,0.57f,0.54f))
            ])
        ];
        if (rigs.Any(r=>r.Secondary.Count!=LightsPerFireplace-1))
            throw new InvalidOperationException("Each fireplace needs two secondary lights.");
        playerModel=player;
        enemyModel=enemy;
        fireplaceModel=fireplace;
        string basePath=Path.Combine(AppContext.BaseDirectory,"Assets","Shaders");
        shader=Raylib.LoadShader(Path.Combine(basePath,"dungeon_lighting.vs"),
            Path.Combine(basePath,"dungeon_lighting.fs"));
        if (shader.Id==0) throw new InvalidOperationException("Dungeon lighting shader failed to load.");
        for (int i=0;i<LightCount;i++)
        {
            positionLocations[i]=Raylib.GetShaderLocation(shader,$"lightPosition{i}");
            colorLocations[i]=Raylib.GetShaderLocation(shader,$"lightColor{i}");
            visibilityLocations[i]=Raylib.GetShaderLocation(shader,$"lightVisibility{i}");
            if (positionLocations[i]<0 || colorLocations[i]<0 || visibilityLocations[i]<0)
                throw new InvalidOperationException($"Fireplace light {i} uniform is missing.");
        }
        doorMinLocation=Raylib.GetShaderLocation(shader,"doorMin");
        doorMaxLocation=Raylib.GetShaderLocation(shader,"doorMax");
        if (doorMinLocation<0 || doorMaxLocation<0)
            throw new InvalidOperationException("Door shadow uniforms are missing.");
        playerShaders=Bind(player);
        enemyShaders=Bind(enemy);
        fireplaceShaders=Bind(fireplace);
        if (sceneryModels is not null)
            foreach (var model in sceneryModels)
                sceneryShaders.Add((model,Bind(model)));
        cubeModel=Raylib.LoadModelFromMesh(Raylib.GenMeshCube(1,1,1));
        cubeModel.Materials[0].Shader=shader;
        for (int i=0;i<LightCount;i++) SetLight(i,Vector3.Zero,Vector3.Zero);
    }

    private Shader[] Bind(Model model)
    {
        var previous=new Shader[model.MaterialCount];
        for (int i=0;i<model.MaterialCount;i++)
        {
            previous[i]=model.Materials[i].Shader;
            model.Materials[i].Shader=shader;
        }
        return previous;
    }

    private void SetLight(int index,Vector3 position,Vector3 color)
    {
        positions[index]=position;
        colors[index]=color;
        Raylib.SetShaderValue(shader,positionLocations[index],
            new float[] { position.X,position.Y,position.Z },ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(shader,colorLocations[index],
            new float[] { color.X,color.Y,color.Z },ShaderUniformDataType.Vec3);
    }

    public void Update(float seconds,float fireplaceLightIntensity)
    {
        var door=world.Interactables.First(item=>item.Kind==InteractionKind.Door).Bounds;
        Raylib.SetShaderValue(shader,doorMinLocation,
            new float[] { door.Min.X,door.Min.Y,door.Min.Z },ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(shader,doorMaxLocation,
            new float[] { door.Max.X,door.Max.Y,door.Max.Z },ShaderUniformDataType.Vec3);
        for (int i=0;i<MaxFireplaces;i++)
        {
            var rig=rigs[i];
            float intensity=FireplaceFlame.Intensity(rig.Fireplace,seconds)*
                fireplaceLightIntensity;
            int first=i*LightsPerFireplace;
            Vector3 main=rig.MainPosition;
            SetLight(first,main,new Vector3(2.35f,2.10f,1.88f)*intensity);
            for (int j=0;j<rig.Secondary.Count;j++)
            {
                var bounce=rig.Secondary[j];
                Vector3 color=intensity>0 && !Occluded(main,bounce.Position)
                    ? bounce.Color*intensity : Vector3.Zero;
                SetLight(first+j+1,bounce.Position,color);
            }
        }
    }

    public void DrawCube(Vector3 centre,Vector3 size,Color colour)
    {
        SetModelReceiver(centre,size);
        Raylib.DrawModelEx(cubeModel,centre,Vector3.UnitY,0,size,colour);
    }

    public void SetModelReceiver(Vector3 position,Vector3 size=default)
    {
        for (int i=0;i<LightCount;i++)
        {
            Vector3 half=size*0.5f;
            Vector3 sample=Vector3.Clamp(positions[i],position-half,position+half);
            float visible=colors[i].LengthSquared()>0 &&
                !Occluded(positions[i],sample,includeDoor:false) ? 1f : 0f;
            Raylib.SetShaderValue(shader,visibilityLocations[i],
                new float[] { visible },ShaderUniformDataType.Float);
        }
    }

    public Color LitColor(Color colour,Vector3 centre,Vector3 size)
    {
        Vector3 light=Vector3.Zero;
        Vector3 half=size*0.5f;
        for (int i=0;i<LightCount;i++)
        {
            if (colors[i].LengthSquared()==0) continue;
            Vector3 sample=Vector3.Clamp(positions[i],centre-half,centre+half);
            if (Occluded(positions[i],sample)) continue;
            float distanceSquared=Vector3.DistanceSquared(positions[i],sample);
            light+=colors[i]*Falloff(distanceSquared);
        }
        return new Color(
            (byte)Math.Clamp(colour.R*light.X,0,255),
            (byte)Math.Clamp(colour.G*light.Y,0,255),
            (byte)Math.Clamp(colour.B*light.Z,0,255),
            colour.A);
    }

    private static float Falloff(float distanceSquared) =>
        MathF.Max(0,1-distanceSquared/400f)/(1+distanceSquared*0.018f);

    private bool Occluded(Vector3 from,Vector3 to,bool includeDoor=true)
    {
        Vector3 direction=to-from;
        foreach (var block in world.StaticSolids)
            if (Blocks(block)) return true;
        if (includeDoor)
            foreach (var item in world.Interactables)
                if (item.Kind==InteractionKind.Door && Blocks(item.Bounds)) return true;
        return false;

        bool Blocks(Block block) =>
            DungeonWorld.SegmentHit(from,direction,block.Min,block.Max,out float hit) &&
            hit>0.001f && hit<0.995f;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed=true;
        for (int i=0;i<playerShaders.Length;i++)
            playerModel.Materials[i].Shader=playerShaders[i];
        for (int i=0;i<enemyShaders.Length;i++)
            enemyModel.Materials[i].Shader=enemyShaders[i];
        for (int i=0;i<fireplaceShaders.Length;i++)
            fireplaceModel.Materials[i].Shader=fireplaceShaders[i];
        foreach (var (model,previous) in sceneryShaders)
            for (int i=0;i<previous.Length;i++) model.Materials[i].Shader=previous[i];
        Raylib.UnloadModel(cubeModel);
        Raylib.UnloadShader(shader);
    }
}
