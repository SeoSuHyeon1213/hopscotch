using UnityEngine;

// ZoneController의 UpdateAnimalRange()가 SetBounds()를 먼저 호출한 뒤
// AnimalManager가 SetNewTarget()을 실행하도록 실행 순서를 뒤로 밀어둔다.
[DefaultExecutionOrder(10)]
public class AnimalManager : MonoBehaviour
{
    [SerializeField] public float speed = 3f;
    [SerializeField] public float moveRange = 10f;
    [SerializeField] float waitTime = 1.5f;

    Vector3 targetPos;
    float timer;
    bool isWaiting;

    // ZoneController가 주입하는 플레인 경계
    Vector3 boundsCenter;
    float boundsHalfX;
    float boundsHalfZ;
    bool hasBounds;

    void Start()
    {
        // SetBounds()가 아직 호출되지 않은 경우를 대비해
        // 첫 목표를 현재 위치로 설정하여 경계 밖 이동 방지
        targetPos = transform.position;
        isWaiting = true;
        timer = waitTime;
    }

    void Update()
    {
        if (isWaiting)
        {
            timer -= Time.deltaTime;
            if (timer <= 0) { isWaiting = false; SetNewTarget(); }
            return;
        }

        Vector3 direction = targetPos - transform.position;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(direction), Time.deltaTime * 5f);
        }

        transform.position = Vector3.MoveTowards(
            transform.position, targetPos, speed * Time.deltaTime);

        // 플레인이 축소될 때 현재 위치도 즉시 경계 안으로 클램프
        if (hasBounds)
            ClampPosition();

        if (Vector3.Distance(transform.position, targetPos) < 0.1f)
        {
            isWaiting = true;
            timer = waitTime;
        }
    }

    /// <summary>
    /// ZoneController에서 플레인 크기 변경 시 매 프레임 호출.
    /// 경계가 줄어들면 현재 목표 지점도 즉시 재조정된다.
    /// </summary>
    public void SetBounds(Vector3 center, float halfX, float halfZ)
    {
        boundsCenter = center;
        boundsHalfX  = halfX;
        boundsHalfZ  = halfZ;
        hasBounds    = true;

        // 기존 목표가 새 경계 밖이면 경계 안으로 재조정
        targetPos.x = Mathf.Clamp(targetPos.x, center.x - halfX, center.x + halfX);
        targetPos.z = Mathf.Clamp(targetPos.z, center.z - halfZ, center.z + halfZ);
    }

    void SetNewTarget()
    {
        float x = transform.position.x + Random.Range(-moveRange, moveRange);
        float z = transform.position.z + Random.Range(-moveRange, moveRange);

        if (hasBounds)
        {
            x = Mathf.Clamp(x, boundsCenter.x - boundsHalfX, boundsCenter.x + boundsHalfX);
            z = Mathf.Clamp(z, boundsCenter.z - boundsHalfZ, boundsCenter.z + boundsHalfZ);
        }

        targetPos = new Vector3(x, transform.position.y, z);
    }

    void ClampPosition()
    {
        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, boundsCenter.x - boundsHalfX, boundsCenter.x + boundsHalfX);
        pos.z = Mathf.Clamp(pos.z, boundsCenter.z - boundsHalfZ, boundsCenter.z + boundsHalfZ);
        transform.position = pos;
    }
}
