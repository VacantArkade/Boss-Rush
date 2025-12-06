using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;

public class TimeStopManager : MonoBehaviour
{
    public static TimeStopManager Instance;

    public Volume timeStopVolume;

    void Awake()
    {
        Instance = this;
        timeStopVolume.weight = 0f;
    }

    public void EnableTimeStopEffect(bool enable, float duration = 1f)
    {
        StopAllCoroutines();
        StartCoroutine(FadeWeight(enable ? 1f : 0f, duration));
    }

    private IEnumerator FadeWeight(float targetWeight, float duration)
    {
        float startWeight = timeStopVolume.weight;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            timeStopVolume.weight = Mathf.Lerp(startWeight, targetWeight, t);
            yield return null;
        }

        timeStopVolume.weight = targetWeight;
    }
}