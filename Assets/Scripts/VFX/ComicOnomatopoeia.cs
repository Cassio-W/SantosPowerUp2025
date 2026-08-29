using System.Collections;
using UnityEngine;
using TMPro;

namespace ComicVFX
{
    /// <summary>
    /// Efeito visual de Onomatopeias 3D/2D estilo História em Quadrinhos (BAM!, POW!, BOOM!, ZAP!, etc.).
    /// Pode ser usado de 2 formas:
    /// 
    /// 1. COMO UM "PARTICLE SYSTEM" FIXO NO CENÁRIO / OBJETO:
    ///    - Coloque o script em qualquer GameObject no local desejado.
    ///    - Deixe configurado no Inspector.
    ///    - Dispare quando quiser chamando: meuEfeito.Play() ou meuEfeito.Play("NOVO TEXTO!");
    /// 
    /// 2. VIA SPAWN RÁPIDO EM 1 LINHA:
    ///    - ComicOnomatopoeia.Spawn(transform.position, "POW!");
    /// </summary>
    [ExecuteAlways]
    public class ComicOnomatopoeia : MonoBehaviour
    {
        [Header("Configurações do Efeito")]
        [SerializeField] private TMP_Text textMesh;
        [SerializeField] private string sampleText = "POW!";
        [SerializeField] private float duration = 0.8f;
        [SerializeField] private Vector3 maxScale = new Vector3(2.5f, 2.5f, 2.5f);
        [SerializeField] private bool faceCamera = true;
        [Tooltip("Câmera do jogo para onde o texto deve olhar. Se deixado vazio, encontra automaticamente a Main Camera da cena.")]
        [SerializeField] private Camera targetCamera;

        [Header("Comportamento (Estilo Particle System)")]
        [Tooltip("Se marcado, o efeito toca automaticamente ao iniciar a cena.")]
        [SerializeField] private bool playOnAwake = false;
        [Tooltip("Se marcado, destrói o GameObject ao terminar. Desmarque para manter o objeto no local e poder re-disparar com .Play().")]
        [SerializeField] private bool destroyOnFinish = false;
        [Tooltip("Se marcado, repete a animação continuamente com um intervalo.")]
        [SerializeField] private bool loop = false;
        [SerializeField] private float loopInterval = 0.5f;

        [Header("Cores e Efeitos")]
        [SerializeField] private Color primaryColor = new Color(1f, 0.2f, 0.2f, 1f); // Vermelho HQ
        [SerializeField] private Color outlineColor = new Color(0.05f, 0.05f, 0.08f, 1f); // Preto Nanquim

        [Header("Editor Preview (Sem Play Mode)")]
        [Tooltip("Ative para visualizar a onomatopeia estática na Scene View sem dar Play.")]
        [SerializeField] private bool previewInEditor = true;
        [Tooltip("Progresso da animação para testar snap e decaimento (0 = início, 0.1 = snap máximo, 1 = final)")]
        [Range(0f, 1f)]
        [SerializeField] private float previewTimeProgress = 0.3f;

        private Camera mainCam;
        private Coroutine playCoroutine;
        private bool isPlaying = false;

        /// <summary>
        /// Indica se a animação da onomatopeia está atualmente em execução.
        /// </summary>
        public bool IsPlaying => isPlaying;

        #region Spawn Static Helper (Uso Rápido em 1 Linha)

        /// <summary>
        /// Instancia uma nova onomatopeia no mundo que toca e se autodestrói ao terminar.
        /// </summary>
        public static ComicOnomatopoeia Spawn(Vector3 worldPosition, string text, Color? color = null, float duration = 0.8f, float scale = 2.5f)
        {
            GameObject go = new GameObject("Onomatopoeia_" + text);
            go.transform.position = worldPosition;

            ComicOnomatopoeia onomatopoeia = go.AddComponent<ComicOnomatopoeia>();
            onomatopoeia.sampleText = text;
            onomatopoeia.duration = duration;
            onomatopoeia.maxScale = new Vector3(scale, scale, scale);
            onomatopoeia.destroyOnFinish = true;
            onomatopoeia.playOnAwake = false;

            if (color.HasValue)
            {
                onomatopoeia.primaryColor = color.Value;
            }

            onomatopoeia.CreateChildTextMesh();
            onomatopoeia.Setup(text, onomatopoeia.primaryColor);
            onomatopoeia.Play();

            return onomatopoeia;
        }

        #endregion

        #region Métodos de Controle / Disparo (Play / Stop)

        /// <summary>
        /// Dispara a animação do efeito no local onde o objeto está posicionado.
        /// </summary>
        [ContextMenu("▶ Play Effect")]
        public void Play()
        {
            EnsureTextMesh();
            ApplyVisualSettings();

            if (playCoroutine != null)
            {
                StopCoroutine(playCoroutine);
            }

            playCoroutine = StartCoroutine(PlayRoutine());
        }

        /// <summary>
        /// Dispara a animação alterando o texto em tempo de execução.
        /// </summary>
        public void Play(string newText)
        {
            sampleText = newText;
            if (textMesh != null)
            {
                textMesh.text = newText;
            }
            Play();
        }

        /// <summary>
        /// Dispara a animação alterando o texto e a cor.
        /// </summary>
        public void Play(string newText, Color newColor)
        {
            primaryColor = newColor;
            Play(newText);
        }

        /// <summary>
        /// Interrompe a animação imediatamente e oculta o texto.
        /// </summary>
        [ContextMenu("⏹ Stop Effect")]
        public void Stop()
        {
            if (playCoroutine != null)
            {
                StopCoroutine(playCoroutine);
                playCoroutine = null;
            }
            isPlaying = false;
            transform.localScale = Vector3.zero;
        }

        #endregion

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                EnsureTextMesh();
                ApplyVisualSettings();

                if (faceCamera)
                {
                    ApplyBillboardRotation();
                }

#if UNITY_EDITOR
                if (textMesh == null)
                {
                    UnityEditor.EditorApplication.delayCall += () =>
                    {
                        if (this != null && !Application.isPlaying && textMesh == null)
                        {
                            CreateChildTextMesh();
                        }
                    };
                }
#endif
            }
        }

        private void EnsureTextMesh()
        {
            if (textMesh != null) return;

            textMesh = GetComponent<TMP_Text>();
            if (textMesh != null) return;

            textMesh = GetComponentInChildren<TMP_Text>(true);
            if (textMesh != null) return;

            Transform child = transform.Find("ComicText");
            if (child != null)
            {
                textMesh = child.GetComponent<TMP_Text>();
            }
        }

        [ContextMenu("Create / Setup Child TextMeshPro")]
        public void CreateChildTextMesh()
        {
            if (textMesh != null) return;

            Transform existingChild = transform.Find("ComicText");
            GameObject childGO;
            if (existingChild != null)
            {
                childGO = existingChild.gameObject;
            }
            else
            {
                childGO = new GameObject("ComicText");
                childGO.transform.SetParent(transform, false);
                childGO.transform.localPosition = Vector3.zero;
                childGO.transform.localRotation = Quaternion.identity;
            }

            TextMeshPro tmp = childGO.GetComponent<TextMeshPro>();
            if (tmp == null)
            {
                tmp = childGO.AddComponent<TextMeshPro>();
            }

            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 8f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.text = sampleText;

            textMesh = tmp;
            ApplyVisualSettings();
        }

        private void ApplyVisualSettings()
        {
            if (textMesh != null)
            {
                if (!string.IsNullOrEmpty(sampleText) && !Application.isPlaying)
                {
                    textMesh.text = sampleText;
                }
                textMesh.color = primaryColor;

                if (Application.isPlaying)
                {
                    textMesh.outlineColor = outlineColor;
                    textMesh.outlineWidth = 0.25f;
                }
            }

            if (previewInEditor && !Application.isPlaying)
            {
                float scaleMultiplier = CalculateScaleMultiplier(previewTimeProgress);
                transform.localScale = Vector3.Scale(maxScale, new Vector3(scaleMultiplier, scaleMultiplier, scaleMultiplier));
            }
            else if (!Application.isPlaying && !previewInEditor)
            {
                transform.localScale = Vector3.zero;
            }
        }

        private void Start()
        {
            if (!Application.isPlaying) return;

            mainCam = Camera.main;
            EnsureTextMesh();
            ApplyVisualSettings();

            if (playOnAwake)
            {
                Play();
            }
            else
            {
                transform.localScale = Vector3.zero;
            }
        }

        private void Update()
        {
            if (faceCamera)
            {
                ApplyBillboardRotation();
            }
        }

        private void ApplyBillboardRotation()
        {
            Camera cam = GetTargetCamera();
            if (cam != null)
            {
                Vector3 direction = transform.position - cam.transform.position;
                if (direction.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(direction);
                }
            }
        }

        private Camera GetTargetCamera()
        {
            if (targetCamera != null) return targetCamera;

            if (mainCam == null || !mainCam.gameObject.activeInHierarchy)
            {
                mainCam = Camera.main;
                if (mainCam == null)
                {
                    mainCam = FindFirstObjectByType<Camera>();
                }
            }

            return mainCam;
        }

        public void Setup(string text, Color color, float? customDuration = null, Vector3? customScale = null)
        {
            sampleText = text;
            primaryColor = color;
            if (customDuration.HasValue) duration = customDuration.Value;
            if (customScale.HasValue) maxScale = customScale.Value;

            if (textMesh == null) EnsureTextMesh();

            if (textMesh != null)
            {
                textMesh.text = text;
                textMesh.color = color;
            }
        }

        private IEnumerator PlayRoutine()
        {
            isPlaying = true;

            do
            {
                float elapsed = 0f;
                transform.localScale = Vector3.zero;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float normalizedTime = elapsed / duration;

                    float scaleMultiplier = CalculateScaleMultiplier(normalizedTime);
                    transform.localScale = Vector3.Scale(maxScale, new Vector3(scaleMultiplier, scaleMultiplier, scaleMultiplier));
                    yield return null;
                }

                transform.localScale = Vector3.zero;

                if (loop && Application.isPlaying)
                {
                    if (loopInterval > 0f)
                    {
                        yield return new WaitForSeconds(loopInterval);
                    }
                }

            } while (loop && Application.isPlaying);

            isPlaying = false;
            playCoroutine = null;

            if (destroyOnFinish && Application.isPlaying)
            {
                Destroy(gameObject);
            }
        }

        private float CalculateScaleMultiplier(float normalizedTime)
        {
            // 0% a 10%: Crescimento explosivo (Snap)
            if (normalizedTime < 0.10f)
            {
                float t = normalizedTime / 0.10f;
                return Mathf.Lerp(0f, 1.2f, t); // Overshoot de elasticidade
            }
            // 10% a 85%: Sustentação com ligeiro atrito
            else if (normalizedTime < 0.85f)
            {
                float t = (normalizedTime - 0.10f) / 0.75f;
                return Mathf.Lerp(1.2f, 1.0f, t);
            }
            // 85% a 100%: Encolhimento repentino (Pop-Out Decay)
            else
            {
                float t = (normalizedTime - 0.85f) / 0.15f;
                return Mathf.Lerp(1.0f, 0f, t);
            }
        }
    }
}
