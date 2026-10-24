using System;
using System.Collections;
using System.Collections.Generic;
using Mandato.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mandato.Presentation
{
    /// <summary>
    /// Controla efeitos visuais de câmera e ambiente com base nos atributos do jogo.
    /// - Poluição (climaticChanges): Controla o peso do Volume de Poluição e o tom amarelado/smog do céu (CEU.mat: H=58, S=60%).
    /// - Corrupção (corruption): Controla o peso do Volume Retrô/Mórbido e o tom vermelho-sangue do céu (CEU.mat: H=0, S=100%).
    /// - Guerra (Fogo): Quando em guerra e a barra cai abaixo de 50%, aumenta o peso do Volume "Fogo" até 1.0 em 0% (derrota)
    ///   e ativa progressivamente as partículas "Fire" em ordem sempre aleatória.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp", null)]
    public class AttributeCameraEffects : MonoBehaviour
    {
        public static AttributeCameraEffects Instance { get; private set; }

        /// <summary>
        /// Provedor opcional do StatBlock ativo para sincronização em tempo real de poluição e corrupção.
        /// </summary>
        public static Func<StatBlock> ActiveStatsProvider { get; set; }

        /// <summary>
        /// Provedor opcional do WarSnapshot ativo para sincronização em tempo real de efeitos bélicos (fogo).
        /// </summary>
        public static Func<WarSnapshot> ActiveWarProvider { get; set; }

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

        [Header("--- Guerra: Fogo (Volume & Partículas) ---")]
        [Tooltip("Volume de pós-processamento 'Fogo' que aumenta quando a barra de guerra cai abaixo de 50% até o máximo em 0% (derrota).")]
        [SerializeField] private Volume fireVolume;

        [Tooltip("Limite da barra de guerra (0 a 100) abaixo do qual o volume 'Fogo' e as partículas 'Fire' começam a ser ativados.")]
        [Range(1f, 100f)] [SerializeField] private float warFireStartThreshold = 50f;

        [Tooltip("Objeto pai ('Fogo') que contém as partículas 'Fire' na cena.")]
        [SerializeField] private GameObject fireParticlesContainer;

        [Tooltip("Lista de objetos de partículas 'Fire' ativados em ordem aleatória conforme a barra de guerra cai abaixo de 50%.")]
        [SerializeField] private List<GameObject> fireParticleObjects = new List<GameObject>();

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
        private bool _isWarFireActive = false;
        private float _currentWarProgress = 50f;

        private readonly List<GameObject> _activeFireOrder = new List<GameObject>();

        private Coroutine _pollutionRoutine;
        private Coroutine _corruptionRoutine;
        private Coroutine _fireRoutine;
        private Coroutine _skyRoutine;

        public IReadOnlyList<GameObject> ActiveFireOrder => _activeFireOrder;
        public float FireVolumeWeight => fireVolume != null ? fireVolume.weight : 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying)
                    Destroy(this);
                else
                    DestroyImmediate(this);
                return;
            }
            Instance = this;

            EnsureWarFireReferences();
            SetupSkyMaterial();
            UpdateWarFireEffect(false, warFireStartThreshold, instant: true);
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

        private void Update()
        {
            if (!Application.isPlaying) return;

            if (ActiveStatsProvider != null)
            {
                var stats = ActiveStatsProvider.Invoke();
                if (stats != null)
                {
                    if (Mathf.Abs(stats.climaticChanges - _currentClimate) > 0.01f ||
                        Mathf.Abs(stats.corruption - _currentCorruption) > 0.01f)
                    {
                        ApplyAttributeEffects(stats, instant: false);
                    }
                }
            }

            if (ActiveWarProvider != null)
            {
                var war = ActiveWarProvider.Invoke();
                if (war != null)
                {
                    bool activeOrDefeated = war.IsActive || war.Outcome == WarOutcome.Defeat;
                    float progress = war.Outcome == WarOutcome.Defeat ? 0f : war.Progress;
                    if (_isWarFireActive != activeOrDefeated || Mathf.Abs(_currentWarProgress - progress) > 0.01f)
                    {
                        ApplyWarEffects(war, instant: false);
                    }
                }
                else if (_isWarFireActive)
                {
                    ApplyWarEffects(null, instant: false);
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                ActiveStatsProvider = null;
                ActiveWarProvider = null;
            }
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
        /// Aplica os efeitos visuais de fogo (Volume "Fogo" e partículas "Fire") com base no estado da guerra.
        /// Acima ou igual a 50% (ou fora de guerra), o peso do volume é 0 e nenhuma partícula fica ativa.
        /// Abaixo de 50%, o volume aumenta até 1.0 em 0% (derrota) e as partículas "Fire" vão ligando em ordem aleatória.
        /// </summary>
        public void ApplyWarEffects(WarSnapshot war, bool instant = false)
        {
            if (war == null)
            {
                UpdateWarFireEffect(false, warFireStartThreshold, instant);
                return;
            }

            bool activeOrDefeated = war.IsActive || war.Outcome == WarOutcome.Defeat;
            float progress = war.Outcome == WarOutcome.Defeat ? 0f : war.Progress;
            UpdateWarFireEffect(activeOrDefeated, progress, instant);
        }

        /// <summary>
        /// Atualiza diretamente o Volume "Fogo" e a ativação aleatória das partículas "Fire".
        /// </summary>
        public void UpdateWarFireEffect(bool isWarActive, float warProgress, bool instant = false)
        {
            EnsureWarFireReferences();

            _isWarFireActive = isWarActive;
            _currentWarProgress = Mathf.Clamp(warProgress, 0f, 100f);

            float threshold = Mathf.Max(1f, warFireStartThreshold);
            float fireFactor = 0f;

            if (isWarActive && _currentWarProgress < threshold)
            {
                fireFactor = Mathf.Clamp01((threshold - _currentWarProgress) / threshold);
            }

            UpdateFireVolume(fireFactor, instant);
            UpdateFireParticles(isWarActive && _currentWarProgress < threshold, fireFactor, _currentWarProgress <= 0.001f);
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

        private void UpdateFireVolume(float fireFactor, bool instant)
        {
            if (fireVolume == null) return;

            float targetWeight = Mathf.Clamp01(fireFactor);

            if (_fireRoutine != null)
            {
                StopCoroutine(_fireRoutine);
                _fireRoutine = null;
            }

            if (instant || !gameObject.activeInHierarchy)
            {
                fireVolume.weight = targetWeight;
            }
            else
            {
                _fireRoutine = StartCoroutine(AnimateVolumeWeight(fireVolume, targetWeight));
            }
        }

        private void UpdateFireParticles(bool shouldActivateAny, float fireFactor, bool forceAll)
        {
            if (fireParticleObjects == null || fireParticleObjects.Count == 0)
            {
                if (fireParticlesContainer != null && !shouldActivateAny)
                {
                    fireParticlesContainer.SetActive(false);
                }
                return;
            }

            if (!shouldActivateAny || fireFactor <= 0f)
            {
                _activeFireOrder.Clear();

                for (int i = 0; i < fireParticleObjects.Count; i++)
                {
                    if (fireParticleObjects[i] != null && fireParticleObjects[i].activeSelf)
                    {
                        fireParticleObjects[i].SetActive(false);
                    }
                }

                if (fireParticlesContainer != null && fireParticlesContainer.activeSelf)
                {
                    fireParticlesContainer.SetActive(false);
                }
                return;
            }

            if (fireParticlesContainer != null && !fireParticlesContainer.activeSelf)
            {
                fireParticlesContainer.SetActive(true);
            }

            // Remove referências nulas caso algum objeto tenha sido destruído
            for (int i = _activeFireOrder.Count - 1; i >= 0; i--)
            {
                if (_activeFireOrder[i] == null || !fireParticleObjects.Contains(_activeFireOrder[i]))
                {
                    _activeFireOrder.RemoveAt(i);
                }
            }

            int total = fireParticleObjects.Count;
            int targetCount = forceAll
                ? total
                : Mathf.Clamp(Mathf.CeilToInt(Mathf.Clamp01(fireFactor) * total), 1, total);

            // Sorteia novas partículas "Fire" em ordem sempre aleatória até atingir targetCount
            if (_activeFireOrder.Count < targetCount)
            {
                List<GameObject> availableCandidates = new List<GameObject>(total);
                for (int i = 0; i < total; i++)
                {
                    GameObject candidate = fireParticleObjects[i];
                    if (candidate != null && !_activeFireOrder.Contains(candidate))
                    {
                        availableCandidates.Add(candidate);
                    }
                }

                while (_activeFireOrder.Count < targetCount && availableCandidates.Count > 0)
                {
                    int randomIndex = UnityEngine.Random.Range(0, availableCandidates.Count);
                    GameObject chosen = availableCandidates[randomIndex];
                    availableCandidates.RemoveAt(randomIndex);
                    _activeFireOrder.Add(chosen);
                }
            }
            else if (_activeFireOrder.Count > targetCount)
            {
                while (_activeFireOrder.Count > targetCount)
                {
                    _activeFireOrder.RemoveAt(_activeFireOrder.Count - 1);
                }
            }

            // Sincroniza o estado ativo de cada partícula
            for (int i = 0; i < total; i++)
            {
                GameObject particleObj = fireParticleObjects[i];
                if (particleObj == null) continue;

                bool shouldBeActive = _activeFireOrder.Contains(particleObj);
                if (particleObj.activeSelf != shouldBeActive)
                {
                    particleObj.SetActive(shouldBeActive);
                }

                if (shouldBeActive)
                {
                    ParticleSystem ps = particleObj.GetComponent<ParticleSystem>();
                    if (ps != null && !ps.isPlaying)
                    {
                        ps.Play(true);
                    }
                }
            }
        }

        private void EnsureWarFireReferences()
        {
            if (fireVolume == null)
            {
                Transform childFogo = transform.Find("Fogo");
                if (childFogo != null)
                {
                    fireVolume = childFogo.GetComponent<Volume>();
                }

                if (fireVolume == null)
                {
                    Volume[] volumes = FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    for (int i = 0; i < volumes.Length; i++)
                    {
                        if (volumes[i] != null && string.Equals(volumes[i].gameObject.name, "Fogo", StringComparison.OrdinalIgnoreCase))
                        {
                            fireVolume = volumes[i];
                            break;
                        }
                    }
                }
            }

            if (fireParticleObjects != null)
            {
                for (int i = fireParticleObjects.Count - 1; i >= 0; i--)
                {
                    if (fireParticleObjects[i] == null)
                    {
                        fireParticleObjects.RemoveAt(i);
                    }
                }
            }
            else
            {
                fireParticleObjects = new List<GameObject>();
            }

            if (fireParticlesContainer == null && gameObject.scene.IsValid())
            {
                GameObject[] roots = gameObject.scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    GameObject root = roots[i];
                    if (root != null
                        && (fireVolume == null || root != fireVolume.gameObject)
                        && string.Equals(root.name, "Fogo", StringComparison.OrdinalIgnoreCase))
                    {
                        fireParticlesContainer = root;
                        break;
                    }
                }
            }

            if (fireParticleObjects.Count == 0 && fireParticlesContainer != null)
            {
                Transform containerTransform = fireParticlesContainer.transform;
                for (int i = 0; i < containerTransform.childCount; i++)
                {
                    Transform child = containerTransform.GetChild(i);
                    if (child == null) continue;

                    if (child.name.StartsWith("Fire", StringComparison.OrdinalIgnoreCase)
                        || child.GetComponent<ParticleSystem>() != null)
                    {
                        fireParticleObjects.Add(child.gameObject);
                    }
                }
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
            if (_fireRoutine != null)
            {
                StopCoroutine(_fireRoutine);
                _fireRoutine = null;
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

        [ContextMenu("Test: Guerra Fogo >= 50% (Sem Fogo / Efeito 0)")]
        private void TestWarFireSafe() => UpdateWarFireEffect(true, 50f);

        [ContextMenu("Test: Guerra Fogo 25% (Metade das Partículas / Efeito 0.5)")]
        private void TestWarFireHalf() => UpdateWarFireEffect(true, 25f);

        [ContextMenu("Test: Guerra Fogo 0% Derrota (Todas as Partículas / Efeito 1.0)")]
        private void TestWarFireDefeat() => UpdateWarFireEffect(true, 0f);

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
