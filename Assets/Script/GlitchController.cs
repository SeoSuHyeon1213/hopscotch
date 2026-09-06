using UnityEngine;
using UnityEngine.Rendering;
using URPGlitch;

[RequireComponent(typeof(Volume))]
public class GlitchController : MonoBehaviour
{
    [SerializeField] Volume volume;

    [Header("TAP 설정")]
    [SerializeField] float tapIntensity = 0.9f;
    [SerializeField] float tapDecaySpeed = 1.8f;

    [Header("HOLD 설정")]
    [SerializeField] float holdBuildupSpeed = 1.5f;
    [SerializeField] float holdAnalogMax = 0.7f;

    DigitalGlitchVolume digital;
    AnalogGlitchVolume analog;

    float digitalTarget;
    float holdAccum;

    void Awake()
    {
        if (volume == null) volume = GetComponent<Volume>();
        if (!volume.profile.TryGet(out digital))
            Debug.LogWarning("[GlitchController] GlitchVolumeProfile에 DigitalGlitchVolume이 없습니다.");
        if (!volume.profile.TryGet(out analog))
            Debug.LogWarning("[GlitchController] GlitchVolumeProfile에 AnalogGlitchVolume이 없습니다.");
    }

    void Update()
    {
        // digital 강도 목표로 수렴
        if (digital != null)
            digital.intensity.value = Mathf.Lerp(digital.intensity.value, digitalTarget, Time.deltaTime * 12f);

        // 자연 감쇠
        digitalTarget = Mathf.MoveTowards(digitalTarget, 0f, Time.deltaTime * tapDecaySpeed);
        holdAccum = Mathf.MoveTowards(holdAccum, 0f, Time.deltaTime * 3f);

        // Analog: holdAccum에 비례
        if (analog != null)
        {
            float a = holdAccum * holdAnalogMax;
            analog.scanLineJitter.value  = Mathf.Lerp(analog.scanLineJitter.value,  a * 0.9f, Time.deltaTime * 6f);
            analog.colorDrift.value      = Mathf.Lerp(analog.colorDrift.value,      a * 0.6f, Time.deltaTime * 6f);
            analog.horizontalShake.value = Mathf.Lerp(analog.horizontalShake.value, a * 0.3f, Time.deltaTime * 6f);
        }
    }

    // TerritoryManager → OnTap()에서 호출
    public void OnTap()
    {
        digitalTarget = tapIntensity;
    }

    // TerritoryManager → OnHold()에서 매 프레임 호출
    public void OnHold()
    {
        holdAccum = Mathf.Min(holdAccum + Time.deltaTime * holdBuildupSpeed, 1f);
        digitalTarget = Mathf.Max(digitalTarget, holdAccum * 0.55f);
    }

    // IDLE 시에는 별도 호출 불필요 — Update 감쇠가 처리
    public void OnIdle() { }
}
