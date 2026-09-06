using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ZoneController : MonoBehaviour
{
    [Header("플레인 설정")]
    public Transform plane;
    public float minX        = 0.1f;
    public float maxX        = 2.0f;
    [Tooltip("침범 포함 절대 최대 크기 (0 = 무제한)")]
    public float hardMaxX    = 5.0f;
    public float scaleSpeed  = 1.0f;
    [Tooltip("경계선 고정 방향: 1 = 오른쪽 고정(humanZone), -1 = 왼쪽 고정(natureZone), 0 = 중심 스케일")]
    public float fixedEdgeDirection = 0f;

    [Header("동적 스폰 오브젝트")]
    public GameObject[] prefabs;
    public int maxObjects    = 20;
    public float spawnMargin = 0.5f;
    [Tooltip("플레인 X 스케일이 이 값만큼 커질 때마다 새 영역에 스폰")]
    public float spawnStepX   = 0.2f;
    [Tooltip("확장 스텝당 스폰할 오브젝트 수")]
    public int   spawnPerStep = 2;
    [Tooltip("오브젝트 제거 시 크기 축소 속도 (높을수록 빠르게 사라짐)")]
    public float scaleOutSpeed = 6f;

    [Header("위치 고정 오브젝트")]
    public GameObject[] fixedObjects;
    [Tooltip("플레인 경계에서 이 거리 안에 들어오기 시작하면 축소 시작")]
    public float fixedFadeRange = 1.5f;

    [Header("경계 근접 파티클")]
    [Tooltip("경계선이 가까이 올 때 건물에 재생할 파티클 프리팹")]
    public ParticleSystem borderParticlePrefab;
    [Tooltip("경계선이 이 거리 안으로 들어오면 파티클 재생 (월드 단위)")]
    public float borderEffectRange = 2f;

    [Header("동물 설정")]
    public AnimalManager[] animals;

    // 내부 상태
    private float currentX;
    private float targetX;
    private float fixedEdgeWorldX;
    private float lastSpawnedAtX;     // Update 스텝 스폰 기준점
    private List<GameObject> spawnedObjects = new List<GameObject>();
    private Vector3[] fixedOriginalScales;

    public IReadOnlyList<GameObject> SpawnedObjects => spawnedObjects;

    private ParticleSystem[] fixedObjectParticles;

    void Start()
    {
        currentX = plane.localScale.x;
        targetX  = currentX;
        fixedEdgeWorldX = plane.position.x + fixedEdgeDirection * currentX * 5f;

        // 고정 오브젝트 원본 스케일 저장
        if (fixedObjects != null)
        {
            fixedOriginalScales = new Vector3[fixedObjects.Length];
            for (int i = 0; i < fixedObjects.Length; i++)
                if (fixedObjects[i] != null)
                    fixedOriginalScales[i] = fixedObjects[i].transform.localScale;
        }

        SpawnObjects(maxObjects);
        lastSpawnedAtX = currentX;

        // 경계 파티클 인스턴스 생성 (fixedObjects마다 1개)
        if (borderParticlePrefab != null && fixedObjects != null)
        {
            fixedObjectParticles = new ParticleSystem[fixedObjects.Length];
            for (int i = 0; i < fixedObjects.Length; i++)
            {
                if (fixedObjects[i] == null) continue;
                var ps = Instantiate(borderParticlePrefab,
                                     fixedObjects[i].transform.position,
                                     Quaternion.identity);
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                fixedObjectParticles[i] = ps;
            }
        }
    }

    void Update()
    {
        currentX = Mathf.Lerp(currentX, targetX, Time.deltaTime * scaleSpeed);
        plane.localScale = new Vector3(currentX, plane.localScale.y, plane.localScale.z);

        // 축소 시 스폰 기준점 리셋 (재확장 때 새 영역부터 다시 스폰)
        if (currentX < lastSpawnedAtX)
            lastSpawnedAtX = currentX;

        // currentX가 spawnStepX만큼 커질 때마다 새 영역에 오브젝트 스폰
        // 용량 체크는 SpawnInExpandedStrip 내부에서 처리하므로 여기서는 제거
        while (currentX >= lastSpawnedAtX + spawnStepX)
        {
            float stepFrom  = lastSpawnedAtX;
            lastSpawnedAtX += spawnStepX;
            StartCoroutine(SpawnInExpandedStrip(stepFrom, lastSpawnedAtX));
        }

        if (fixedEdgeDirection != 0f)
        {
            float newCenterX = fixedEdgeWorldX - fixedEdgeDirection * currentX * 5f;
            plane.position = new Vector3(newCenterX, plane.position.y, plane.position.z);
        }

        UpdateFixedObjects();
        CullOutOfBoundsSpawned();
        UpdateAnimalRange();
    }

    // ─── 외부에서 호출 ────────────────────────────────

    public void Expand(float amount)
    {
        float prevTarget = targetX;
        float ceiling = hardMaxX > 0f ? hardMaxX : float.MaxValue;
        targetX = Mathf.Clamp(targetX + amount, minX, ceiling);

        // 오브젝트가 전혀 없을 때만 즉시 복사 스폰
        // 일반 확장 시 스폰은 Update()의 스텝 스폰이 처리
        if (targetX > prevTarget && !HasActiveObjects())
            StartCoroutine(SpawnRandomCopies());
    }

    public void Shrink(float amount)
    {
        targetX = Mathf.Max(targetX - amount, minX);  // 하한(minX)만 유지, 상한 없음

        StartCoroutine(RemoveExcess(EffectiveMaxObjects(targetX)));
    }

    // ─── 위치 고정 오브젝트 경계 처리 ────────────────

    void UpdateFixedObjects()
    {
        if (fixedObjects == null || fixedOriginalScales == null) return;

        // targetX 기준으로 경계 계산 → Shrink 호출 즉시 페이드 반응
        float halfX = targetX * 5f - spawnMargin;
        float halfZ = plane.localScale.z * 5f - spawnMargin;
        float centerX = fixedEdgeDirection != 0f
            ? fixedEdgeWorldX - fixedEdgeDirection * targetX * 5f
            : plane.position.x;
        Vector3 center = new Vector3(centerX, plane.position.y, plane.position.z);

        for (int i = 0; i < fixedObjects.Length; i++)
        {
            GameObject obj = fixedObjects[i];
            if (obj == null) continue;

            Vector3 pos = obj.transform.position;

            // 플레인 경계까지 남은 거리 (음수 = 경계 밖)
            float marginX = halfX - Mathf.Abs(pos.x - center.x);
            float marginZ = halfZ - Mathf.Abs(pos.z - center.z);
            float margin  = Mathf.Min(marginX, marginZ);  // 가장 가까운 경계 기준

            // 경계 안쪽 fadeRange 범위 내에서 0→1로 변환
            float t = Mathf.Clamp01(margin / fixedFadeRange);

            if (t <= 0f)
            {
                if (obj.activeSelf) obj.SetActive(false);
            }
            else
            {
                if (!obj.activeSelf) obj.SetActive(true);
                obj.transform.localScale = Vector3.Lerp(Vector3.zero, fixedOriginalScales[i], t);
            }

            // 경계 근접 파티클 제어
            if (fixedObjectParticles != null && i < fixedObjectParticles.Length)
            {
                var ps = fixedObjectParticles[i];
                if (ps != null)
                {
                    bool nearBorder = margin > 0f && margin < borderEffectRange;
                    if (nearBorder)
                    {
                        // 경계가 가까울수록 방출량 증가 (0→1)
                        float proximity = 1f - Mathf.Clamp01(margin / borderEffectRange);
                        var em = ps.emission;
                        em.rateOverTimeMultiplier = proximity;
                        if (!ps.isPlaying) ps.Play();
                    }
                    else
                    {
                        if (ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                }
            }
        }
    }

    // ─── 경계 밖 spawnedObjects 즉시 제거 ────────────

    void CullOutOfBoundsSpawned()
    {
        if (spawnedObjects.Count == 0) return;

        float halfX   = targetX * 5f - spawnMargin;
        float halfZ   = plane.localScale.z * 5f - spawnMargin;
        float centerX = fixedEdgeDirection != 0f
            ? fixedEdgeWorldX - fixedEdgeDirection * targetX * 5f
            : plane.position.x;

        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            GameObject obj = spawnedObjects[i];
            if (obj == null) { spawnedObjects.RemoveAt(i); continue; }

            float mx = halfX - Mathf.Abs(obj.transform.position.x - centerX);
            float mz = halfZ - Mathf.Abs(obj.transform.position.z - plane.position.z);
            if (Mathf.Min(mx, mz) < 0f)
            {
                spawnedObjects.RemoveAt(i);
                StartCoroutine(ScaleOutAndDestroy(obj));
            }
        }
    }

    // ─── 활성 오브젝트 유무 확인 ──────────────────────

    bool HasActiveObjects()
    {
        if (fixedObjects != null)
            foreach (var obj in fixedObjects)
                if (obj != null && obj.activeSelf) return true;

        return spawnedObjects.Count > 0;
    }

    // ─── 오브젝트 없을 때 고정 오브젝트 랜덤 복사 스폰 ──

    IEnumerator SpawnRandomCopies()
    {
        if (fixedObjects == null || fixedObjects.Length == 0) yield break;

        var sources = new List<(GameObject obj, Vector3 scale)>();
        for (int i = 0; i < fixedObjects.Length; i++)
            if (fixedObjects[i] != null)
                sources.Add((fixedObjects[i], fixedOriginalScales[i]));

        if (sources.Count == 0) yield break;

        float halfX = Mathf.Max(targetX           * 5f - spawnMargin, 0f);
        float halfZ = Mathf.Max(plane.localScale.z * 5f - spawnMargin, 0f);

        int count = Random.Range(1, 4);
        for (int i = 0; i < count; i++)
        {
            float x = Random.Range(-halfX, halfX);
            float z = Random.Range(-halfZ, halfZ);
            Vector3 spawnPos = new Vector3(plane.position.x + x, 0f, plane.position.z + z);

            int idx = Random.Range(0, sources.Count);
            GameObject copy = Instantiate(sources[idx].obj, spawnPos,
                Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            copy.transform.localScale = Vector3.zero;
            copy.SetActive(true);
            StartCoroutine(ScaleIn(copy, sources[idx].scale));
            spawnedObjects.Add(copy);

            yield return new WaitForSeconds(0.15f);
        }
    }

    // ─── 확장 영역 스텝 스폰 ─────────────────────────────

    IEnumerator SpawnInExpandedStrip(float fromX, float toX)
    {
        bool hasPrefabs = prefabs != null && prefabs.Length > 0;
        bool hasFixed   = fixedObjects != null && fixedOriginalScales != null
                       && fixedObjects.Length > 0;
        if (!hasPrefabs && !hasFixed) yield break;

        float fromHalfX = Mathf.Max(fromX * 5f - spawnMargin, 0f);
        float toHalfX   = Mathf.Max(toX   * 5f - spawnMargin, 0f);
        float halfZ     = Mathf.Max(plane.localScale.z * 5f - spawnMargin, 0f);

        for (int i = 0; i < spawnPerStep; i++)
        {
            if (spawnedObjects.Count >= EffectiveMaxObjects(currentX)) yield break;

            // fixedEdgeDirection에 따라 새 띠 위치 결정
            float x;
            if (fixedEdgeDirection == 0f)
            {
                // 중심 스케일: 양쪽 띠 중 랜덤
                float side = Random.value > 0.5f ? 1f : -1f;
                x = side * Random.Range(fromHalfX, toHalfX);
            }
            else
            {
                // 고정 엣지: 확장 방향(반대쪽)의 띠에만 스폰
                x = -fixedEdgeDirection * Random.Range(fromHalfX, toHalfX);
            }
            float z = Random.Range(-halfZ, halfZ);
            Vector3 spawnPos = new Vector3(plane.position.x + x, 0f, plane.position.z + z);

            GameObject obj;
            Vector3 targetScale;

            if (hasPrefabs)
            {
                GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
                obj = Instantiate(prefab, spawnPos, Quaternion.Euler(0, Random.Range(0f, 360f), 0));
                targetScale = Vector3.one;
            }
            else
            {
                // fixedObjects에서 null이 아닌 항목 랜덤 선택
                int idx = -1;
                for (int attempt = 0; attempt < fixedObjects.Length * 2; attempt++)
                {
                    int candidate = Random.Range(0, fixedObjects.Length);
                    if (fixedObjects[candidate] != null) { idx = candidate; break; }
                }
                if (idx < 0) yield break;

                obj = Instantiate(fixedObjects[idx], spawnPos,
                          Quaternion.Euler(0, Random.Range(0f, 360f), 0));
                targetScale = fixedOriginalScales[idx];
                obj.SetActive(true);
            }

            obj.transform.localScale = Vector3.zero;
            StartCoroutine(ScaleIn(obj, targetScale));
            spawnedObjects.Add(obj);

            yield return new WaitForSeconds(0.1f);
        }
    }

    // ─── 동적 스폰 / 제거 ─────────────────────────────

    void SpawnObjects(int count)
    {
        for (int i = 0; i < count; i++)
            SpawnOne();
    }

    IEnumerator SpawnSequence(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (spawnedObjects.Count < EffectiveMaxObjects(currentX))
                SpawnOne();
            yield return new WaitForSeconds(0.1f);
        }
    }

    void SpawnOne()
    {
        if (prefabs == null || prefabs.Length == 0) return;

        float planeHalfX = currentX            * 5f - spawnMargin;
        float planeHalfZ = plane.localScale.z   * 5f - spawnMargin;

        Vector3 spawnPos = new Vector3(
            plane.position.x + Random.Range(-planeHalfX, planeHalfX),
            0f,
            plane.position.z + Random.Range(-planeHalfZ, planeHalfZ)
        );

        GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
        GameObject obj    = Instantiate(prefab, spawnPos,
                                Quaternion.Euler(0, Random.Range(0f, 360f), 0));

        obj.transform.localScale = Vector3.zero;
        StartCoroutine(ScaleIn(obj, Vector3.one));
        spawnedObjects.Add(obj);
    }

    IEnumerator ScaleIn(GameObject obj, Vector3 targetScale)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 2f;
            if (obj != null)
                obj.transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, t);
            yield return null;
        }
        if (obj != null)
            obj.transform.localScale = targetScale;
    }

    IEnumerator RemoveExcess(int keepCount)
    {
        while (spawnedObjects.Count > keepCount)
        {
            int last = spawnedObjects.Count - 1;
            GameObject obj = spawnedObjects[last];
            spawnedObjects.RemoveAt(last);
            StartCoroutine(ScaleOutAndDestroy(obj));
        }
        yield break;
    }

    IEnumerator ScaleOutAndDestroy(GameObject obj)
    {
        yield return StartCoroutine(ScaleOut(obj));
        if (obj != null) Destroy(obj);
    }

    IEnumerator ScaleOut(GameObject obj)
    {
        float t = 1f;
        Vector3 startScale = obj != null ? obj.transform.localScale : Vector3.one;
        while (t > 0f)
        {
            t -= Time.deltaTime * scaleOutSpeed;
            if (obj != null)
                obj.transform.localScale = Vector3.Lerp(Vector3.zero, startScale, t);
            yield return null;
        }
    }

    // ─── 씬 종료 시 파티클 정리 ──────────────────────────

    void OnDestroy()
    {
        if (fixedObjectParticles != null)
            foreach (var ps in fixedObjectParticles)
                if (ps != null) Destroy(ps.gameObject);
    }

    // ─── 플레인 넓이 비례 최대 오브젝트 수 ───────────────

    int EffectiveMaxObjects(float x)
    {
        if (maxX <= minX) return maxObjects;
        float ratio = (x - minX) / (maxX - minX);
        return Mathf.Max(1, Mathf.RoundToInt(maxObjects * ratio));
    }

    // ─── 동물 이동범위 동기화 ─────────────────────────

    void UpdateAnimalRange()
    {
        if (animals == null) return;

        float ratio = Mathf.Clamp01((currentX - minX) / (maxX - minX));

        float halfX = currentX            * 5f - spawnMargin;
        float halfZ = plane.localScale.z   * 5f - spawnMargin;

        foreach (var a in animals)
        {
            if (a == null) continue;
            a.moveRange = Mathf.Lerp(1f, 8f, ratio);
            a.speed     = Mathf.Lerp(0.5f, 4f, ratio);
            a.SetBounds(plane.position, halfX, halfZ);
        }
    }
}
