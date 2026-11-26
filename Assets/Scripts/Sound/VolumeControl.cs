using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class VolumeControl : MonoBehaviour
{
    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("Exposed Parameters (Nombres exactos en el Mixer)")]
    [SerializeField] private string masterParam = "VolumeMaster";
    [SerializeField] private string musicParam = "VolumeMusic";
    [SerializeField] private string sfxParam = "VolumeSFX";
    [SerializeField] private string ambientParam = "VolumeAmbience";

    [Header("Sliders")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider ambientSlider;

    [Header("Volumen Inicial por Defecto (si no hay guardado previo)")]
    [Range(0.0001f, 1f)]
    [SerializeField] private float defaultVolume = 0.75f;

    private void Start()
    {
        ConfigurarSlider(masterSlider, masterParam, "Pref_MasterVol", defaultVolume);
        ConfigurarSlider(musicSlider, musicParam, "Pref_MusicVol", defaultVolume);
        ConfigurarSlider(sfxSlider, sfxParam, "Pref_SFXVol", defaultVolume);
        ConfigurarSlider(ambientSlider, ambientParam, "Pref_AmbientVol", defaultVolume);

        if (masterSlider != null) masterSlider.onValueChanged.AddListener(SetMasterVolume);
        if (musicSlider != null) musicSlider.onValueChanged.AddListener(SetMusicVolume);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(SetSFXVolume);
        if (ambientSlider != null) ambientSlider.onValueChanged.AddListener(SetAmbientVolume);
    }

    private void ConfigurarSlider(Slider slider, string paramName, string prefKey, float fallbackValue)
    {
        float savedLinear = Mathf.Clamp(PlayerPrefs.GetFloat(prefKey, fallbackValue), 0.0001f, 1f);

        if (slider != null)
        {
            slider.minValue = 0.0001f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(savedLinear);
        }

        AplicarVolumen(paramName, savedLinear);
    }

    public void SetMasterVolume(float value)
    {
        AplicarVolumen(masterParam, value);
        PlayerPrefs.SetFloat("Pref_MasterVol", value);
        PlayerPrefs.Save();
    }

    public void SetMusicVolume(float value)
    {
        AplicarVolumen(musicParam, value);
        PlayerPrefs.SetFloat("Pref_MusicVol", value);
        PlayerPrefs.Save();
    }

    public void SetSFXVolume(float value)
    {
        AplicarVolumen(sfxParam, value);
        PlayerPrefs.SetFloat("Pref_SFXVol", value);
        PlayerPrefs.Save();
    }

    public void SetAmbientVolume(float value)
    {
        AplicarVolumen(ambientParam, value);
        PlayerPrefs.SetFloat("Pref_AmbientVol", value);
        PlayerPrefs.Save();
    }

    private void AplicarVolumen(string parameter, float linearValue)
    {
        if (audioMixer == null) return;
        float db = Mathf.Log10(Mathf.Clamp(linearValue, 0.0001f, 1f)) * 20f;
        audioMixer.SetFloat(parameter, db);
    }
}