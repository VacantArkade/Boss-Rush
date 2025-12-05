using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class TimeStopManager : MonoBehaviour
{
    public static TimeStopManager Instance;

    public Volume timeStopVolume;
    private ColorAdjustments colorAdjustments;
    private Vignette vignette;

    void Awake()
    {
        Instance = this;
        timeStopVolume.profile.TryGet(out colorAdjustments);
        timeStopVolume.profile.TryGet(out vignette);
    }

    public void EnableTimeStopEffect(bool enable, float duration = 1f)
    {
        StopAllCoroutines();
        StartCoroutine(FadeEffect(enable, duration));
    }

    private IEnumerator FadeEffect(bool enable, float duration)
    {
        float startSat = colorAdjustments.saturation.value;
        float targetSat = enable ? -40f : 0f;

        float startVignette = vignette.intensity.value;
        float targetVignette = enable ? 0.4f : 0f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            colorAdjustments.saturation.value = Mathf.Lerp(startSat, targetSat, t);
            vignette.intensity.value = Mathf.Lerp(startVignette, targetVignette, t);

            yield return null;
        }

        colorAdjustments.saturation.value = targetSat;
        vignette.intensity.value = targetVignette;
    }
}