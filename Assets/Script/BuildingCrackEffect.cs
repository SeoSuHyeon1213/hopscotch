using UnityEngine;

/// <summary>
/// IDLE 지속 시간에 따라 건물에 균열/붕괴 파티클을 스폰합니다.
/// TerritoryManager → OnIdle() / OnTapOrHold() 에서 매 프레임 호출됩니다.
///
/// Inspector 슬롯:
///   Human Zone       → HumanZone ZoneController
///   Crack Prefab     → Particle Pack: Prefabs/Dust계열 또는 Free Quick Effects의 Smoke/Dust 프리팹
///   Collapse Prefab  → Particle Pack: Prefabs/Explosion계열 또는 Free Quick Effects의 Explosion 프리팹
/// </summary>
public class BuildingCrackEffect : MonoBehaviour
{
    [Header("건물 참조")]
    [SerializeField] ZoneController humanZone;

    [Header("파티클 프리팹")]
    [Tooltip("균열 지속 파티클 – Particle Pack Dust 계열 또는 Free Quick Effects Smoke 프리팹")]
    [SerializeField] GameObject crackParticlePrefab;
    [Tooltip("붕괴 버스트 파티클 – Particle Pack Explosion 계열 또는 Free Quick Effects Explosion 프리팹")]
    [SerializeField] GameObject collapseParticlePrefab;

    [Header("데미지 설정")]
    [Tooltip("균열 파티클 발생 데미지 임계값 (0~1)")]
    [SerializeField] float crackThreshold    = 0.35f;
    [Tooltip("붕괴 파티클 발생 데미지 임계값 (0~1)")]
    [SerializeField] float collapseThreshold = 0.75f;
    [Tooltip("IDLE 시 초당 데미지 증가량")]
    [SerializeField] float damageSpeed       = 0.06f;
    [Tooltip("TAP/HOLD 시 초당 데미지 감소량")]
    [SerializeField] float repairSpeed       = 0.20f;
    [Tooltip("붕괴 파티클 재발생 최소 간격 (초)")]
    [SerializeField] float collapseInterval  = 2.5f;

    GameObject[] buildings;
    float[]      damage;
    GameObject[] crackInstances;
    float[]      lastCollapseTime;

    void Start()
    {
        if (humanZone == null)
        {
            Debug.LogWarning("[BuildingCrackEffect] humanZone이 연결되지 않았습니다. Inspector에서 HumanZone ZoneController를 연결해 주세요.");
            return;
        }
        if (humanZone.fixedObjects == null || humanZone.fixedObjects.Length == 0)
        {
            Debug.LogWarning("[BuildingCrackEffect] humanZone.fixedObjects가 비어 있습니다.");
            return;
        }

        buildings        = humanZone.fixedObjects;
        int n            = buildings.Length;
        damage           = new float[n];
        crackInstances   = new GameObject[n];
        lastCollapseTime = new float[n];

        //Debug.Log($"[BuildingCrackEffect] 건물 {n}개 데미지 추적 시작.");
    }

    // TerritoryManager → OnIdle()에서 매 프레임 호출
    public void OnIdle()
    {
        if (buildings == null) return;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (buildings[i] == null) continue;
            damage[i] = Mathf.MoveTowards(damage[i], 1f, Time.deltaTime * damageSpeed);
            UpdateEffect(i);
        }
    }

    // TAP / HOLD 시 호출 → 수리 (균열·붕괴 파티클 즉시 제거)
    public void OnTapOrHold()
    {
        if (buildings == null) return;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (buildings[i] == null) continue;
            damage[i] = Mathf.MoveTowards(damage[i], 0f, Time.deltaTime * repairSpeed);
            RemoveCrackParticle(i);                  // 데미지 임계값 무관하게 즉시 제거
            lastCollapseTime[i] = Time.time;         // 붕괴 파티클 재발생 타이머 리셋
        }
    }

    void UpdateEffect(int i)
    {
        float d = damage[i];
        GameObject building = buildings[i];

        // ── 균열 파티클: 임계값 도달 시 한 번 스폰, 건물에 부착해 지속 재생 ──
        if (d >= crackThreshold && crackInstances[i] == null && crackParticlePrefab != null)
        {
            Vector3 pos = building.transform.position + Vector3.up * 0.5f;
            crackInstances[i] = Instantiate(crackParticlePrefab, pos, Quaternion.identity, building.transform);
        }

        // ── 붕괴 파티클: 임계값 초과 시 collapseInterval마다 버스트 스폰 ──
        if (d >= collapseThreshold && collapseParticlePrefab != null)
        {
            if (Time.time - lastCollapseTime[i] >= collapseInterval)
            {
                lastCollapseTime[i] = Time.time;
                Vector3 pos = building.transform.position + Vector3.up * 1.5f;
                GameObject burst = Instantiate(collapseParticlePrefab, pos, Quaternion.identity);
                Destroy(burst, 4f);
            }
        }
    }

    void RemoveCrackParticle(int i)
    {
        if (crackInstances[i] != null)
        {
            Destroy(crackInstances[i]);
            crackInstances[i] = null;
        }
    }

    void OnDestroy()
    {
        if (crackInstances == null) return;
        foreach (var c in crackInstances)
            if (c != null) Destroy(c);
    }
}
