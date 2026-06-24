using UnityEngine;

public class MonsterAudioController : MonoBehaviour
{
    public AK.Wwise.Event playIdle;
    public AK.Wwise.Event stopIdle;
    public AK.Wwise.Event playChase;
    public AK.Wwise.Event stopChase;
    public AK.Wwise.Event playAttack;
    public AK.Wwise.Event playStep;
    public AK.Wwise.Event playDetection;

    public void StartIdleAudio() => playIdle?.Post(gameObject);
    public void StopIdleAudio() => stopIdle?.Post(gameObject);

    public void StartChaseAudio() => playChase?.Post(gameObject);
    public void StopChaseAudio() => stopChase?.Post(gameObject);

    public void PlayAttackAudio() => playAttack?.Post(gameObject);
    public void PlayMonsterStep() => playStep?.Post(gameObject);
    public void PlayDetectionAudio() => playDetection?.Post(gameObject);
}