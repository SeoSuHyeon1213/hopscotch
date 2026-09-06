using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using URPGlitch;
using UnityEditor.Rendering;

public static class EffectSetup
{
    const string RENDERER_PC     = "Assets/Settings/PC_Renderer.asset";
    const string RENDERER_MOBILE = "Assets/Settings/Mobile_Renderer.asset";
    const string GLITCH_PROFILE  = "Assets/Settings/GlitchVolumeProfile.asset";

    [MenuItem("Tools/MediaKingdom/Setup Effects")]
    public static void Run()
    {
        AddGlitchFeatures(RENDERER_PC);
        AddGlitchFeatures(RENDERER_MOBILE);
        Debug.Log("[EffectSetup] RendererFeature 등록 완료. 씬에 GlitchController 오브젝트를 추가하고 GlitchVolumeProfile을 할당해 주세요.");
    }

    static void AddGlitchFeatures(string rendererPath)
    {
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (renderer == null)
        {
            Debug.LogError($"[EffectSetup] 렌더러를 찾을 수 없음: {rendererPath}");
            return;
        }

        var analogShader  = AssetDatabase.LoadAssetAtPath<Shader>(
            AssetDatabase.GUIDToAssetPath("97d9be06c8e6c1e4781abe10d57474a8"));
        var digitalShader = AssetDatabase.LoadAssetAtPath<Shader>(
            AssetDatabase.GUIDToAssetPath("f445ed039be4e2c4c81930580ce0fa68"));
        var compatShader  = AssetDatabase.LoadAssetAtPath<Shader>(
            AssetDatabase.GUIDToAssetPath("2f7db6c094d160148aa3595fd806f507"));

        // 기존 Feature의 null 셰이더 보정
        foreach (var f in renderer.rendererFeatures)
        {
            if (f is AnalogGlitchRenderFeature)
            {
                var so = new SerializedObject(f);
                var prop = so.FindProperty("anaglogGlitchShader");
                if (prop != null && prop.objectReferenceValue == null)
                {
                    prop.objectReferenceValue = analogShader;
                    so.ApplyModifiedProperties();
                    Debug.Log($"[EffectSetup] {rendererPath} AnalogGlitch 셰이더 보정됨.");
                }
            }
            else if (f is DigitalGlitchFeature)
            {
                var so = new SerializedObject(f);
                var shaderProp  = so.FindProperty("shader");
                var compatProp  = so.FindProperty("compatShader");
                bool changed = false;
                if (shaderProp != null && shaderProp.objectReferenceValue == null)
                { shaderProp.objectReferenceValue = digitalShader; changed = true; }
                if (compatProp != null && compatProp.objectReferenceValue == null)
                { compatProp.objectReferenceValue = compatShader; changed = true; }
                if (changed)
                {
                    so.ApplyModifiedProperties();
                    Debug.Log($"[EffectSetup] {rendererPath} DigitalGlitch 셰이더 보정됨.");
                }
            }
        }

        bool hasDigital = renderer.rendererFeatures.Any(f => f is DigitalGlitchFeature);
        bool hasAnalog  = renderer.rendererFeatures.Any(f => f is AnalogGlitchRenderFeature);

        if (!hasDigital)
        {
            var feature = ScriptableObject.CreateInstance<DigitalGlitchFeature>();
            feature.name = "DigitalGlitchFeature";
            var so = new SerializedObject(feature);
            so.FindProperty("shader").objectReferenceValue       = digitalShader;
            so.FindProperty("compatShader").objectReferenceValue = compatShader;
            so.FindProperty("renderPassEvent").intValue          = 550;
            so.ApplyModifiedProperties();
            AssetDatabase.AddObjectToAsset(feature, renderer);
            renderer.rendererFeatures.Add(feature);
            Debug.Log($"[EffectSetup] {rendererPath} 에 DigitalGlitchFeature 추가됨.");
        }

        if (!hasAnalog)
        {
            var feature = ScriptableObject.CreateInstance<AnalogGlitchRenderFeature>();
            feature.name = "AnalogGlitchRenderFeature";
            var so = new SerializedObject(feature);
            so.FindProperty("anaglogGlitchShader").objectReferenceValue = analogShader;
            so.FindProperty("renderPassEvent").intValue                  = 550;
            so.ApplyModifiedProperties();
            AssetDatabase.AddObjectToAsset(feature, renderer);
            renderer.rendererFeatures.Add(feature);
            Debug.Log($"[EffectSetup] {rendererPath} 에 AnalogGlitchRenderFeature 추가됨.");
        }

        EditorUtility.SetDirty(renderer);
        AssetDatabase.SaveAssets();
    }

    // --- 씬 내 오브젝트 자동 생성 ---
    [MenuItem("Tools/MediaKingdom/Create GlitchController in Scene")]
    public static void CreateGlitchControllerObject()
    {
        var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(GLITCH_PROFILE);
        if (profile == null)
        {
            Debug.LogError("[EffectSetup] GlitchVolumeProfile.asset을 찾을 수 없습니다.");
            return;
        }

        // Volume GameObject
        var go = new GameObject("GlitchVolume");
        var vol = go.AddComponent<UnityEngine.Rendering.Volume>();
        vol.isGlobal = true;
        vol.priority = 10;
        vol.profile = profile;

        // GlitchController
        go.AddComponent<GlitchController>();

        Undo.RegisterCreatedObjectUndo(go, "Create GlitchVolume");
        Selection.activeGameObject = go;

        Debug.Log("[EffectSetup] GlitchVolume 오브젝트가 씬에 생성되었습니다. TerritoryManager의 glitchController 필드에 연결해 주세요.");
    }

    [MenuItem("Tools/MediaKingdom/Create VineMoss ParticleSystem in Scene")]
    public static void CreateVineMossParticleSystem()
    {
        var go = new GameObject("VineMossEffect");
        var ps = go.AddComponent<ParticleSystem>();

        // 기본 파티클 세팅 (이끼·덩굴 분위기)
        var main = ps.main;
        main.startLifetime = 5f;
        main.startSpeed = 0.08f;
        main.startSize = 0.12f;
        main.startColor = new Color(0.18f, 0.55f, 0.2f, 0.85f);
        main.maxParticles = 200;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 2f;

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.2f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0f)));

        // IdleVineEffect 컴포넌트 추가
        var controller = go.AddComponent<IdleVineEffect>();

        Undo.RegisterCreatedObjectUndo(go, "Create VineMoss PS");
        Selection.activeGameObject = go;

        Debug.Log("[EffectSetup] VineMossEffect 오브젝트가 씬에 생성되었습니다. " +
                  "NovaShader 머티리얼을 Renderer에 할당하고 TerritoryManager의 vineEffect 필드에 연결해 주세요.");
    }
}
