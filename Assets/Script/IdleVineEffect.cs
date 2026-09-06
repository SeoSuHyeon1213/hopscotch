using UnityEngine;
using System.Collections.Generic;

public class IdleVineEffect : MonoBehaviour
{
    [Header("건물 덩굴 잠식")]
    [SerializeField] ZoneController humanZone;
    [SerializeField] Color vineColor = new Color(0.13f, 0.38f, 0.08f);

    [Header("파티클 설정")]
    [SerializeField] ParticleSystem vineParticles;
    [SerializeField] float maxEmissionRate = 18f;
    [SerializeField] float idleBuildupSpeed = 0.4f;
    [SerializeField] float suppressSpeed = 3f;

    [Header("크기 펄스 (IDLE 시 살짝 흔들림)")]
    [SerializeField] bool enableSizePulse = true;
    [SerializeField] float pulseAmount = 0.15f;
    [SerializeField] float pulseFrequency = 0.7f;

    float idleIntensity;
    bool isIdle;
    ParticleSystem.EmissionModule emission;
    ParticleSystem.MainModule main;
    float baseStartSize;

    struct BuildingRenderer
    {
        public Renderer renderer;
        public Color originalColor;
        public MaterialPropertyBlock mpb;
        public string colorProp;
    }
    readonly List<BuildingRenderer> buildingRenderers = new List<BuildingRenderer>();
    readonly HashSet<Renderer> trackedRenderers = new HashSet<Renderer>();
    int lastKnownSpawnCount = -1;

    void Awake()
    {
        if (vineParticles == null)
            vineParticles = GetComponentInChildren<ParticleSystem>();

        if (vineParticles != null)
        {
            emission = vineParticles.emission;
            main = vineParticles.main;
            baseStartSize = main.startSize.constant;
        }
        else
            Debug.LogWarning("[IdleVineEffect] ParticleSystem을 찾을 수 없습니다. 인스펙터에서 연결하거나 자식으로 추가해 주세요.");
    }

    void Start()
    {
        CollectBuildingRenderers();
    }

    void CollectBuildingRenderers()
    {
        buildingRenderers.Clear();
        trackedRenderers.Clear();
        lastKnownSpawnCount = -1;

        if (humanZone == null)
        {
            Debug.LogWarning("[IdleVineEffect] humanZone이 연결되지 않았습니다. Inspector에서 HumanZone ZoneController를 연결해 주세요.");
            return;
        }

        CollectFromObjects(humanZone.fixedObjects);

        if (buildingRenderers.Count == 0)
            Debug.LogWarning("[IdleVineEffect] humanZone fixedObjects에서 Renderer를 찾지 못했습니다. fixedObjects 배열이 비어 있지 않은지 확인해 주세요.");
        //else
           // Debug.Log($"[IdleVineEffect] 건물 Renderer {buildingRenderers.Count}개 수집 완료.");
    }

    // spawnedObjects 수 변동 시 새 오브젝트 렌더러 추가, 소멸된 항목 정리
    void SyncSpawnedRenderers()
    {
        if (humanZone == null) return;
        var spawned = humanZone.SpawnedObjects;
        int currentCount = spawned?.Count ?? 0;
        if (currentCount == lastKnownSpawnCount) return;
        lastKnownSpawnCount = currentCount;

        // 소멸된 렌더러 정리
        buildingRenderers.RemoveAll(br => br.renderer == null);
        trackedRenderers.RemoveWhere(r => r == null);

        // 새로 생긴 spawnedObjects 등록
        if (spawned != null)
            foreach (var obj in spawned)
                CollectFromSingleObject(obj);
    }

    void CollectFromObjects(GameObject[] objects)
    {
        if (objects == null) return;
        foreach (var obj in objects)
            CollectFromSingleObject(obj);
    }

    void CollectFromSingleObject(GameObject obj)
    {
        if (obj == null) return;
        foreach (var r in obj.GetComponentsInChildren<Renderer>())
        {
            if (r == null || trackedRenderers.Contains(r)) continue;
            if (r.sharedMaterial == null) continue;

            string prop = r.sharedMaterial.HasProperty("_BaseColor") ? "_BaseColor"
                        : r.sharedMaterial.HasProperty("_Color")     ? "_Color"
                        : null;
            if (prop == null) continue;

            var mpb = new MaterialPropertyBlock();
            buildingRenderers.Add(new BuildingRenderer
            {
                renderer      = r,
                originalColor = r.sharedMaterial.GetColor(prop),
                mpb           = mpb,
                colorProp     = prop
            });
            trackedRenderers.Add(r);
        }
    }

    void Update()
    {
        if (!isIdle)
            idleIntensity = Mathf.MoveTowards(idleIntensity, 0f, Time.deltaTime * suppressSpeed);
        isIdle = false;
        SyncSpawnedRenderers();
        ApplyEffect();
    }

    // TerritoryManager → OnIdle()에서 매 프레임 호출
    public void OnIdle()
    {
        isIdle = true;
        idleIntensity = Mathf.MoveTowards(idleIntensity, 1f, Time.deltaTime * idleBuildupSpeed);
        ApplyEffect();
    }

    // TAP / HOLD 발생 시 호출 → 덩굴 억제
    public void OnTapOrHold()
    {
        idleIntensity = Mathf.MoveTowards(idleIntensity, 0f, Time.deltaTime * suppressSpeed * 2f);
        ApplyEffect();
    }

    void ApplyEffect()
    {
        // 파티클 제어
        if (vineParticles != null)
        {
            emission.rateOverTime = idleIntensity * maxEmissionRate;
            if (enableSizePulse && idleIntensity > 0.01f)
            {
                float pulse = 1f + Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f)
                                   * pulseAmount * idleIntensity;
                var m = vineParticles.main;
                m.startSize = baseStartSize * pulse;
            }
        }

        // 건물 머티리얼 이끼·덩굴 오버레이
        foreach (var br in buildingRenderers)
        {
            if (br.renderer == null) continue;
            br.mpb.SetColor(br.colorProp, Color.Lerp(br.originalColor, vineColor, idleIntensity));
            br.renderer.SetPropertyBlock(br.mpb);
        }
    }
}
