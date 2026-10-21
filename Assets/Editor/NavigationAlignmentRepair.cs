using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Unity.AI.Navigation;

/// <summary>Explicit footprint-only correction. Preserves scene placements, art, ring sizes and gameplay.</summary>
[InitializeOnLoad]
public static class NavigationAlignmentRepair
{
    static NavigationAlignmentRepair() { EditorApplication.update += Tick; }
    static void Tick()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            !File.Exists("Temp/ToxicBarrierNavigation.repair")) return;
        File.Delete("Temp/ToxicBarrierNavigation.repair");
        try { Repair(); }
        catch(Exception e) { File.WriteAllText("Temp/ToxicBarrierNavigation.result","FAIL\n"+e); Debug.LogException(e); }
    }
    [MenuItem("Tools/Toxic Barrier/Repair Building Ground Alignment")]
    public static void Repair()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        if(SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save your current scene before repairing navigation.");
        var scene=SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity");
        if(!scene.IsValid() || !scene.isLoaded) scene=EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        if(scene.isDirty) throw new InvalidOperationException("Save GameScene before repairing navigation.");
        Directory.CreateDirectory(".utmp/navigation-backup");
        File.Copy(scene.path,".utmp/navigation-backup/GameScene-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity");
        var objects=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SpriteRenderer>(true));
        var buildings=objects.Where(r=>r.sprite!=null && AssetDatabase.GetAssetPath(r.sprite).StartsWith("Assets/PeterRadiant/Tiny Swords/Buildings/Black Buildings/")).ToArray();
        var lines=new List<string>();
        foreach(var renderer in buildings)
        {
            Vector3 position=renderer.transform.position;
            WorldLayoutPolishTool.Fit(renderer);
            var feet=renderer.GetComponent<PolygonCollider2D>();
            BuildingNavigationGeometry.Sync(renderer.gameObject,feet);
            // Sort at the corrected feet while leaving the sprite and collision in place.
            var group=renderer.GetComponentInParent<SortingGroup>();
            if(group!=null && group.transform!=renderer.transform && group.name.EndsWith("_GroundSort"))
            {
                Vector2[] points=Enumerable.Range(0,feet.pathCount).SelectMany(i=>feet.GetPath(i)).ToArray();
                group.transform.position=renderer.transform.TransformPoint(new Vector3(0,(points.Min(p=>p.y)+points.Max(p=>p.y))*.5f));
                renderer.transform.position=position;
            }
            if(Vector3.Distance(position,renderer.transform.position)>.0001f) throw new Exception("Art moved: "+renderer.name);
            lines.Add(renderer.name+": world art placement unchanged; "+feet.pathCount+" ground-contact paths.");
        }
        var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Component>(true)).ToArray();
        var outer=all.OfType<MapZone>().OrderBy(z=>z.MapLevel).Last();
        outer.GetRingParams(out Vector2 center,out _,out float radius);
        var ground=all.OfType<BoxCollider>().Single(c=>c.name=="NavigationGround");
        ground.transform.position=center; ground.size=new Vector3((radius+2)*2,(radius+2)*2,.1f);
        Physics2D.SyncTransforms(); Physics.SyncTransforms();
        var surface=all.OfType<NavMeshSurface>().Single();
        surface.overrideVoxelSize=true; surface.voxelSize=.025f;
        NavMesh2DSetupTool.BakeNavigation(surface);
        foreach(var renderer in buildings)
        {
            Bounds b=renderer.GetComponent<PolygonCollider2D>().bounds;
            Vector3[] corners={new Vector3(b.min.x-.65f,b.min.y-.65f),new Vector3(b.max.x+.65f,b.min.y-.65f),
                new Vector3(b.max.x+.65f,b.max.y+.65f),new Vector3(b.min.x-.65f,b.max.y+.65f)};
            for(int i=0;i<4;i++)
            {
                var path=new NavMeshPath();
                if(!NavMesh.SamplePosition(corners[i],out var from,.4f,NavMesh.AllAreas) ||
                   !NavMesh.SamplePosition(corners[(i+1)%4],out var to,.4f,NavMesh.AllAreas) ||
                   !NavMesh.CalculatePath(from.position,to.position,NavMesh.AllAreas,path) || path.status!=NavMeshPathStatus.PathComplete)
                    throw new Exception("Blocked route: "+renderer.name+" side "+i);
            }
        }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        lines.Add("PASS: "+buildings.Length+" unchanged building placements; "+buildings.Length*4+" complete routes around corrected foundations.");
        File.WriteAllText("Temp/ToxicBarrierNavigation.result",string.Join("\n",lines));
    }
}
