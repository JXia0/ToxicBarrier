using UnityEngine;
using UnityEngine.UI;

/// <summary>Draws the selected scene art, using world-to-sprite UVs rather than a schematic replacement.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class MapTerrainGraphic : MaskableGraphic
{
    public ExplorationMapUI Owner;
    public MapZone Zone;
    public bool Full;
    private SpriteRenderer Source => Zone != null ? Zone.GetComponent<SpriteRenderer>() : null;
    public override Texture mainTexture => Source != null && Source.sprite != null ? Source.sprite.texture : Texture2D.whiteTexture;

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var map=MapController.Instance;
        var source=Source;
        if(map==null || Owner==null || source==null || source.sprite==null || (!Full && !Zone.IsUnlocked)) return;
        float radius=Full?Owner.OuterRadius:map.GetMaxAllowedRadius();
        float scale=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.465f/radius;
        Vector2 origin=map.GetMapCenter(), middle=rectTransform.rect.center;
        Zone.GetRingParams(out Vector2 center,out _,out float outer);
        var sprite=source.sprite;
        Color tint=Zone.IsUnlocked?(Full?new Color(.87f,.87f,.82f,1):new Color(.96f,.96f,.91f,1)):new Color(.20f,.24f,.20f,1);
        for(int i=0;i<128;i++)
        {
            float a=i*Mathf.PI*2/128, b=(i+1)*Mathf.PI*2/128;
            Vector2 p=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*outer;
            Vector2 q=center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*outer;
            int start=mesh.currentVertCount;
            Add(center); Add(p); Add(q); mesh.AddTriangle(start,start+1,start+2);
        }
        void Add(Vector2 world)
        {
            Vector2 local=source.transform.InverseTransformPoint(world);
            if(source.flipX) local.x=-local.x;
            if(source.flipY) local.y=-local.y;
            Vector2 pixel=local*sprite.pixelsPerUnit+sprite.pivot+sprite.rect.position;
            Vector2 uv=new Vector2(pixel.x/sprite.texture.width,pixel.y/sprite.texture.height);
            mesh.AddVert(middle+(world-origin)*scale,tint,uv);
        }
    }
}
