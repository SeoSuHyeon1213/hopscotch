using UnityEngine;

public class TerritoryManager : MonoBehaviour
{
    public ZoneController natureZone;
    public ZoneController humanZone;

    [Header("점령 강도")]
    public float tapAmount  = 0.1f;
    public float holdAmount = 0.05f;
    public float idleAmount = 0.104f;

    [Header("이펙트")]
    public GlitchController    glitchController;
    public IdleVineEffect      vineEffect;
    public BuildingCrackEffect crackEffect;

    public void OnTap()
    {
        humanZone.Expand(tapAmount);
        natureZone.Shrink(tapAmount);

        glitchController?.OnTap();
        vineEffect?.OnTapOrHold();
        crackEffect?.OnTapOrHold();
    }

    public void OnHold()
    {
        humanZone.Expand(holdAmount);
        natureZone.Shrink(holdAmount);

        glitchController?.OnHold();
        vineEffect?.OnTapOrHold();
        crackEffect?.OnTapOrHold();
    }

    public void OnIdle()
    {
        natureZone.Expand(idleAmount * Time.deltaTime);
        humanZone.Shrink(idleAmount * Time.deltaTime);

        vineEffect?.OnIdle();
        crackEffect?.OnIdle();
        glitchController?.OnIdle();
    }
}