using System;
using System.Collections;
using Mandato.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mandato.Presentation
{
    /// <summary>
    /// Controla efeitos visuais de câmera e ambiente com base nos atributos do jogo.
    /// - Poluição (climaticChanges): Controla o peso do Volume de Poluição e o tom amarelado/smog do céu (CEU.mat: H=58, S=60%).
    /// - Corrupção (corruption): Controla o peso do Volume Retrô/Mórbido e o tom vermelho-sangue do céu (CEU.mat: H=0, S=100%).
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp", null)]
    public class AttributeCameraEffects : MonoBehaviour
    {
        public static AttributeCameraEffects Instance { get; private set; }

        [Header("--- Poluição / Clima (Volume) ---")]
        [Tooltip("Volume de pós-processamento aplicado quando a poluição estiver alta (climaticChanges < 50%). Se vazio, busca automaticamente na cena.")]
        [SerializeField] private Volume pollutionVolume;

        [Tooltip("Limite do atributo (0 a 100) abaixo do qual o efeito de poluição começa a aumentar.")]
        [Range(0, 100)] [SerializeField] private float pollutionStartThreshold = 50f;

        [Header("--- Corrupção (Volume Retrô / Mórbido) ---")]
        [Tooltip("Volume de pós-processamento da visão corrompida/mórbida (grão, vinheta vinho/escura, aberração cromática, contraste alto). Se vazio, busca automaticamente.")]
        [SerializeField] private Volume corruptionVolume;

        [Tooltip("Valor de corrupção (0 a 100) a partir do qual o filtro retrô/mórbido começa a agir.")]
        [Range(0, 100)] [SerializeField] private float corruptionStartThreshold = 0f;

        [Tooltip("Valor de corrupção (0 a 100) no qual o filtro atinge o peso máximo (weight = 1.0).")]
        [Range(0, 100)] [SerializeField] private float corruptionMaxThreshold = 100f;

        [Header("--- Céu / Skybox (CEU.mat) ---")]
        [Tooltip("Material do céu (CEU.mat). Se deixado vazio, utiliza o RenderSettings.skybox.")]
        [SerializeField] private Material skyMaterial;

        [Header("--- Céu: Tonalidade de Poluição ---")]
        [Tooltip("Matiz (Hue) da poluição na escala de 0 a 360 da Unity. Padrão: 58 (tom amarelado/poluído).")]
        [Range(0f, 360f)] [SerializeField] private float pollutionHue = 58f;

        [Tooltip("Saturação máxima (0 a 100%) da cor de poluição no céu quando o clima atingir 0%.")]
        [Range(0f, 100f)] [SerializeField] private float maxPollutionSaturation = 60f;

        [Header("--- Céu: Tonalidade de Corrupção ---")]
        [Tooltip("Matiz (Hue) da corrupção no céu na escala de 0 a 360 da Unity. Padrão: 0 (Vermelho sangue / cólera).")]
        [Range(0f, 360f)] [SerializeField] private float corruptionHue = 0f;

        [Tooltip("Saturação máxima (0 a 100%) da cor de corrupção no céu quando a corrupção atingir 100%.")]
        [Range(0f, 100f)] [SerializeField] private float maxCorruptionSaturation = 100f;

        [Header("--- Animação e Transição ---")]
        [Tooltip("Duração da transição suave dos efeitos (em segundos).")]
        [SerializeField] private float transitionDuration = 1.2f;

        [Tooltip("Curva de interpolação suave da transição.")]
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Se ativo, as transições ocorrem mesmo com o jogo pausado (Time.timeScale = 0).")]
        [SerializeField] private bool useUnscaledTime = true;

        // Controle interno de Céu e Atributos
        private string _tintPropName = "_Tint";
        private Color _initialSkyColor = Color.white;
        private float _initialHue;
        private float _initialSaturation;
        private float _initialValue = 1f;
        private bool _hasSkyColorSaved = false;

        private float _currentClimate = 50f;
        private float _currentCorruption = 0f;

        private Coroutine _pollutionRoutine;
        private Coroutine _corruptionRoutine;
        private Coroutine _skyRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            SetupSkyMaterial();
            FindPollutionVolumeIfNeeded();
            FindCorruptionVolumeIfNeeded();
        }

        private void OnDisable()
        {
            StopAllEffectCoroutines();
            RestoreOriginalSkyColor();
        }

        private void OnApplicationQuit()
        {
            RestoreOriginalSkyColor();
        }

        /// <summary>
        /// Aplica os efeitos visuais correspondentes aos atributos atuais da run.
        /// </summary>
        public void ApplyAttributeEffects(StatBlock stats, bool instant = false)
        {
            if (stats == null) return;
            ApplyAttributeEffects(stats.climaticChanges, stats.corruption, instant);
        }

        /// <summary>
        /// Aplica os efeitos visuais correspondentes aos valores numéricos de clima e corrupção.
        /// </summary>
        public void ApplyAttributeEffects(float climate, float corruption, bool instant = false)
        {
            _currentClimate = climate;
            _currentCorruption = corruption;

            UpdatePollutionVolume(_currentClimate, instant);
            UpdateCorruptionVolume(_currentCorruption, instant);
            UpdateSkyEffect(instant);
        }

        /// <summary>
        /// Atualiza individualmente o efeito de poluição (Volume e Céu).
        /// </summary>
        public void UpdatePollutionEffect(float climateValue, bool instant = false)
        {
            _currentClimate = climateValue;
            UpdatePollutionVolume(climateValue, instant);
            UpdateSkyEffect(instant);
        }

        /// <summary>
        /// Atualiza individualmente o efeito de corrupção (Volume e Céu).
        /// </summary>
        public void UpdateCorruptionEffect(float corruptionValue, bool instant = false)
        {
            _currentCorruption = corruptionValue;
            UpdateCorruptionVolume(corruptionValue, instant);
            UpdateSkyEffect(instant);
        }

        private void UpdatePollutionVolume(float climateValue, bool instant)
        {
            FindPollutionVolumeIfNeeded();
            if (pollutionVolume == null) return;

            float pollutionFactor = 0f;
            if (climateValue < pollutionStartThreshold)
            {
                pollutionFactor = Mathf.Clamp01((pollutionStartThreshold - climateValue) / pollutionStartThreshold);
            }

            float targetWeight = pollutionFactor;

            if (_pollutionRoutine != null)
            {
                StopCoroutine(_pollutionRoutine);
                _pollutionRoutine = null;
            }

            if (instant || !gameObject.activeInHierarchy)
            {
                pollutionVolume.weight = targetWeight;
            }
            else
            {
                _pollutionRoutine = StartCoroutine(AnimateVolumeWeight(pollutionVolume, targetWeight));
            }
        }

        private void UpdateCorruptionVolume(float corruptionValue, bool instant)
        {
            FindCorruptionVolumeIfNeeded();
            if (corruptionVolume == null) return;

            float corruptionFactor = 0f;
            if (corruptionValue > corruptionStartThreshold)
            {
                float range = Mathf.Max(0.001f, corruptionMaxThreshold - corruptionStartThreshold);
                corruptionFactor = Mathf.Clamp01((corruptionValue - corruptionStartThreshold) / range);
            }

            float targetWeight = corruptionFactor;

            if (_corruptionRoutine != null)
            {
                StopCoroutine(_corruptionRoutine);
                _corruptionRoutine = null;
            }

            if (instant || !gameObject.activeInHierarchy)
            {
                corruptionVolume.weight = targetWeight;
            }
            else
            {
                _corruptionRoutine = StartCoroutine(AnimateVolumeWeight(corruptionVolume, targetWeight));
            }
        }

        private IEnumerator AnimateVolumeWeight(Volume volume, float targetWeight)
        {
            if (volume == null) yield break;

            float startWeight = volume.weight;
            float elapsed = 0f;
            float duration = Mathf.Max(0.05f, transitionDuration);

            while (elapsed < duration)
            {
                float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                elapsed += delta;
                float t = Mathf.Clamp01(elapsed / duration);
                float curveT = transitionCurve != null ? transitionCurve.Evaluate(t) : Mathf.SmoothStep(0f, 1f, t);

                if (volume != null)
                {
                    volume.weight = Mathf.Lerp(startWeight, targetWeight, curveT);
                }

                yield return null;
            }

            if (volume != null)
            {
                volume.weight = targetWeight;
            }
        }

        /// <summary>
        /// Combina os fatores de poluição e corrupção para calcular e aplicar suavemente a cor do céu (CEU.mat).
        /// </summary>
        private void UpdateSkyEffect(bool instant)
        {
            if (skyMaterial == null || !skyMaterial.HasProperty(_tintPropName)) return;

            // Fator de poluição [0, 1]
            float pollutionFactor = 0f;
            if (_currentClimate < pollutionStartThreshold)
            {
                pollutionFactor = Mathf.Clamp01((pollutionStartThreshold - _currentClimate) / pollutionStartThreshold);
            }

            // Fator de corrupção [0, 1]
            float corruptionFactor = 0f;
            if (_currentCorruption > corruptionStartThreshold)
            {
                corruptionFactor = Mathf.Clamp01(corruptionValueNormalized(_currentCorruption));
            }

            Color targetColor;

            // Se nenhum efeito estiver ativo, usa a cor base original
            if (pollutionFactor <= 0.001f && corruptionFactor <= 0.001f)
            {
                targetColor = _initialSkyColor;
            }
            else
            {
                // Cor alvo se fosse apenas Poluição (H=58, S até 60%)
                float targetPolS = Mathf.Lerp(_initialSaturation, maxPollutionSaturation / 100f, pollutionFactor);
                Color polColor = Color.HSVToRGB(pollutionHue / 360f, targetPolS, _initialValue);

                // Cor alvo se fosse apenas Corrupção (H=0, S até 100%)
                float targetCorS = Mathf.Lerp(_initialSaturation, maxCorruptionSaturation / 100f, corruptionFactor);
                Color corColor = Color.HSVToRGB(corruptionHue / 360f, targetCorS, _initialValue);

                // Se ambos estiverem presentes, mescla proporcionalmente
                float totalInfluence = pollutionFactor + corruptionFactor;
                float corruptionShare = corruptionFactor / Mathf.Max(0.0001f, totalInfluence);
                Color blendedEffectColor = Color.Lerp(polColor, corColor, corruptionShare);

                float maxInfluence = Mathf.Clamp01(Mathf.Max(pollutionFactor, corruptionFactor));
                targetColor = Color.Lerp(_initialSkyColor, blendedEffectColor, maxInfluence);
            }

            if (_skyRoutine != null)
            {
                StopCoroutine(_skyRoutine);
                _skyRoutine = null;
            }

            if (instant || !gameObject.activeInHierarchy)
            {
                skyMaterial.SetColor(_tintPropName, targetColor);
            }
            else
            {
                _skyRoutine = StartCoroutine(AnimateSkyColor(targetColor));
            }
        }

        private IEnumerator AnimateSkyColor(Color targetColor)
        {
            if (skyMaterial == null) yield break;

            Color startColor = skyMaterial.GetColor(_tintPropName);
            float elapsed = 0f;
            float duration = Mathf.Max(0.05f, transitionDuration);

            while (elapsed < duration)
            {
                float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                elapsed += delta;
                float t = Mathf.Clamp01(elapsed / duration);
                float curveT = transitionCurve != null ? transitionCurve.Evaluate(t) : Mathf.SmoothStep(0f, 1f, t);

                if (skyMaterial != null)
                {
                    skyMaterial.SetColor(_tintPropName, Color.Lerp(startColor, targetColor, curveT));
                }

                yield return null;
            }

            if (skyMaterial != null)
            {
                skyMaterial.SetColor(_tintPropName, targetColor);
            }
        }

        private float corruptionValueNormalized(float value)
        {
            float range = Mathf.Max(0.001f, corruptionMaxThreshold - corruptionStartThreshold);
            return Mathf.Clamp01((value - corruptionStartThreshold) / range);
        }

        private void SetupSkyMaterial()
        {
            if (skyMaterial == null)
            {
                skyMaterial = RenderSettings.skybox;
            }

            if (skyMaterial != null)
            {
                if (skyMaterial.HasProperty("_Tint"))
                {
                    _tintPropName = "_Tint";
                }
                else if (skyMaterial.HasProperty("_SkyTint"))
                {
                    _tintPropName = "_SkyTint";
                }
                else if (skyMaterial.HasProperty("_BaseColor"))
                {
                    _tintPropName = "_BaseColor";
                }
                else if (skyMaterial.HasProperty("_Color"))
                {
                    _tintPropName = "_Color";
                }

                _initialSkyColor = skyMaterial.GetColor(_tintPropName);
                Color.RGBToHSV(_initialSkyColor, out _initialHue, out _initialSaturation, out _initialValue);
                _hasSkyColorSaved = true;
            }
        }

        private void FindPollutionVolumeIfNeeded()
        {
            if (pollutionVolume != null) return;

            var allVolumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
            foreach (var v in allVolumes)
            {
                if (v == null) continue;
                string objName = v.name.ToLower();
                string profName = v.sharedProfile != null ? v.sharedProfile.name.ToLower() : "";

                if (objName.Contains("polu") || objName.Contains("pollut") || objName.Contains("smog") ||
                    profName.Contains("polu") || profName.Contains("pollut") || profName.Contains("smog"))
                {
                    pollutionVolume = v;
                    break;
                }
            }
        }

        private void FindCorruptionVolumeIfNeeded()
        {
            if (corruptionVolume != null) return;

            var allVolumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
            foreach (var v in allVolumes)
            {
                if (v == null) continue;
                string objName = v.name.ToLower();
                string profName = v.sharedProfile != null ? v.sharedProfile.name.ToLower() : "";

                if (objName.Contains("corrup") || objName.Contains("corrupt") || objName.Contains("morb") || objName.Contains("evil") ||
                    profName.Contains("corrup") || profName.Contains("corrupt") || profName.Contains("morb") || profName.Contains("evil"))
                {
                    corruptionVolume = v;
                    break;
                }
            }
        }

        private void StopAllEffectCoroutines()
        {
            if (_pollutionRoutine != null)
            {
                StopCoroutine(_pollutionRoutine);
                _pollutionRoutine = null;
            }
            if (_corruptionRoutine != null)
            {
                StopCoroutine(_corruptionRoutine);
                _corruptionRoutine = null;
            }
            if (_skyRoutine != null)
            {
                StopCoroutine(_skyRoutine);
                _skyRoutine = null;
            }
        }

        private void RestoreOriginalSkyColor()
        {
            if (_hasSkyColorSaved && skyMaterial != null && skyMaterial.HasProperty(_tintPropName))
            {
                skyMaterial.SetColor(_tintPropName, _initialSkyColor);
            }
        }

        #region Context Menu Testing (Editor)

        [ContextMenu("Test: Poluição 100% (Clima Limpo / Efeito 0)")]
        private void TestPollutionClean() => UpdatePollutionEffect(100f);

        [ContextMenu("Test: Poluição 50% (Limite / Efeito 0)")]
        private void TestPollutionThreshold() => UpdatePollutionEffect(50f);

        [ContextMenu("Test: Poluição 25% (Poluição Média / Efeito 0.5)")]
        private void TestPollutionMedium() => UpdatePollutionEffect(25f);

        [ContextMenu("Test: Poluição 0% (Poluição Máxima / Efeito 1.0)")]
        private void TestPollutionMax() => UpdatePollutionEffect(0f);

        [ContextMenu("Test: Corrupção 0% (Sem Corrupção / Efeito 0)")]
        private void TestCorruptionZero() => UpdateCorruptionEffect(0f);

        [ContextMenu("Test: Corrupção 25% (Corrupção Baixa / Efeito 0.25)")]
        private void TestCorruptionLow() => UpdateCorruptionEffect(25f);

        [ContextMenu("Test: Corrupção 50% (Corrupção Moderada / Efeito 0.50)")]
        private void TestCorruptionMedium() => UpdateCorruptionEffect(50f);

        [ContextMenu("Test: Corrupção 100% (Corrupção Máxima / Efeito 1.0)")]
        private void TestCorruptionMax() => UpdateCorruptionEffect(100f);

        [ContextMenu("Test: Caos Total (Poluição Máxima + Corrupção Máxima)")]
        private void TestTotalChaos()
        {
            _currentClimate = 0f;
            _currentCorruption = 100f;
            UpdatePollutionVolume(0f, false);
            UpdateCorruptionVolume(100f, false);
            UpdateSkyEffect(false);
        }

        #endregion
    }
}
