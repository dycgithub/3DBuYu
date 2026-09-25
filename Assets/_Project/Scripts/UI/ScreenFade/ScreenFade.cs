using System;
using System.Collections;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine;

public class ScreenFade : MonoBehaviour
{
    public enum FadeType
    {
        Shutters,
        RadialWipe,
        PlainBlack,
        Goop
    }
    
    public float fadeDuration = 0.5f;
    public FadeType fadeType = FadeType.Shutters;
    
    public int _fadeAmount=Shader.PropertyToID("_FadeAmount");
    public int _useShutters=Shader.PropertyToID("_useShutters");
    public int _useRadialWipe=Shader.PropertyToID("_useRadialWipe");
    public int _usePlainBlack=Shader.PropertyToID("_usePlainBlack");
    public int _useGoop=Shader.PropertyToID("_useGoop");

    private int? _lastEffect;//用来判断是否启动效果

    private Image image;
    private Material material;

    private void Awake()
    {
        image = GetComponent<Image>();
        material = image.material;
    }

    public void FadeOut(FadeType fadeType)
    {
        ChangeFadeEffect(fadeType);
        StartFadeOut();
    }

    public void FadeIn(FadeType fadeType)
    {
        ChangeFadeEffect(this.fadeType);
        StartFadeIn();
    }

    private void StartFadeIn()
    {
        material.SetFloat(_fadeAmount, 1f);
        material.DOFade(0f, _fadeAmount, fadeDuration).SetEase(Ease.InOutSine);
    }

    private void StartFadeOut()
    {
        material.SetFloat(_fadeAmount, 0f);
        material.DOFade(1f, _fadeAmount, fadeDuration).SetEase(Ease.InOutSine);
    }
    

    private void SwitchEffect(int effectToTurnOn)
    {
        material.SetFloat(effectToTurnOn,1f);
        _lastEffect = effectToTurnOn;
    }

    private void ChangeFadeEffect(FadeType fadeType1)
    {
        if (_lastEffect.HasValue)
        {
            material.SetFloat(_lastEffect.Value, 0f);
        }

        switch (fadeType)
        {
            case FadeType.Shutters:
                SwitchEffect(_useShutters);
                break;
            case FadeType.RadialWipe:
                SwitchEffect(_useRadialWipe);
                break;
            case FadeType.PlainBlack:
                SwitchEffect(_usePlainBlack);
                break;
            case FadeType.Goop:
                SwitchEffect(_useGoop);
                break;
        }
    }
    
}
