using System;
using System.Collections;
using UnityEngine;

namespace Mandato.Presentation
{
    public enum StampInkState
    {
        Dry = -1,
        Approve = 0,
        Reject = 1
    }

    /// <summary>
    /// Componente 3D para o Carimbo Presidencial na mesa.
    /// No modo de inspeção de papel (PaperInspect), acompanha a posição do mouse a uma distância clara de hover,
    /// molha nas almofadas de tinta e estampa o documento de proposta tocando a superfície do colisor no clique.
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    public class StampTool3D : MonoBehaviour
    {
        [Header("--- Posição e Rastreamento 3D ---")]
        [Tooltip("Ponto de repouso padrão do carimbo na mesa quando não estiver inspecionando o papel.")]
        [SerializeField] private Transform restAnchor;

        [Tooltip("Câmera utilizada para projetar o raio do mouse. Se vazia, utiliza Camera.main.")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("Referência opcional ao papel de proposta para cálculo da superfície de repouso. Se vazio, busca automaticamente na cena.")]
        [SerializeField] private PaperFocusableObject paperTarget;

        [Tooltip("Distância (em metros) que o carimbo flutua acima da superfície do papel/mesa enquanto o jogador mira.")]
        [SerializeField] [Range(0.01f, 0.60f)] private float hoverHeight = 0.15f;

        [Tooltip("Se ativo, trava a elevação mundial absoluta do carimbo no valor de 'fixedElevationY' em vez de somar à altura da superfície.")]
        [SerializeField] private bool useFixedElevation = false;

        [Tooltip("Elevação mundial fixa (Y) do carimbo no espaço da cena (utilizado quando 'useFixedElevation' estiver ativo).")]
        [SerializeField] private float fixedElevationY = 1.25f;

        [Tooltip("Ajuste fino de contato no colisor ao carimbar (0 = toque na superfície exata do ponto de impacto).")]
        [SerializeField] [Range(-0.05f, 0.05f)] private float stampTouchOffset = 0.0f;

        [Tooltip("Velocidade de interpolação do carimbo seguindo o cursor do mouse.")]
        [SerializeField] private float followSpeed = 22f;

        [Tooltip("LayerMask para detecção de colisão do mouse com o papel, almofadas e mesa.")]
        [SerializeField] private LayerMask interactionLayers = ~0;

        [Header("--- Materiais da Ponta de Tinta ---")]
        [SerializeField] private Renderer inkHeadRenderer;
        [SerializeField] private Material approveInkMaterial;
        [SerializeField] private Material rejectInkMaterial;
        [SerializeField] private Material dryInkMaterial;
        [SerializeField] private Color approveColor = new Color(0.1f, 0.6f, 0.2f);
        [SerializeField] private Color rejectColor = new Color(0.8f, 0.15f, 0.15f);
        [SerializeField] private Color dryColor = new Color(0.2f, 0.2f, 0.2f);

        [Header("--- Áudio e Feedback ---")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip stampSuccessSound;
        [SerializeField] private AudioClip stampDrySound;
        [SerializeField] private AudioClip dipSound;

        private StampInkState currentInk = StampInkState.Dry;
        private Vector3 initialRestPosition;
        private Quaternion initialRestRotation;
        private float workingSurfaceY = 1.0f;
        private Quaternion currentAimRotation = Quaternion.identity;
        private FocusableObject currentHoveredObject;
        private bool isInspectActive = false;
        private bool isPunching = false;
        private bool hasSubmittedDecision = false;
        private Coroutine punchCoroutine;
        private Coroutine returnRoutine;

        public StampInkState CurrentInk => currentInk;
        public bool IsInspectActive => isInspectActive;
        public bool HasSubmittedDecision => hasSubmittedDecision;
        public float HoverHeight { get => hoverHeight; set => hoverHeight = value; }
        public bool UseFixedElevation { get => useFixedElevation; set => useFixedElevation = value; }
        public float FixedElevationY { get => fixedElevationY; set => fixedElevationY = value; }
        public PaperFocusableObject PaperTarget { get => paperTarget; set => paperTarget = value; }
        public AudioClip StampSuccessSound { get => stampSuccessSound; set => stampSuccessSound = value; }
        public AudioClip StampDrySound { get => stampDrySound; set => stampDrySound = value; }
        public AudioClip DipSound { get => dipSound; set => dipSound = value; }

        public event Action<StampInkState> OnInkChanged;
        public event Action<int, Vector2> OnStampApplied;
        public event Action<bool, bool, Vector2> OnStampPreviewUpdated;
        public event Action<int> OnStampDecisionSubmitted;

        /// <summary>
        /// Retorna a elevação mundial consistente (Y) na qual o carimbo deve flutuar durante a mira.
        /// </summary>
        public float GetHoverElevation()
        {
            if (useFixedElevation)
            {
                return fixedElevationY;
            }

            float surfaceY = workingSurfaceY;
            if (paperTarget != null)
            {
                surfaceY = paperTarget.GetTargetFocusedPosition().y;
            }
            return surfaceY + hoverHeight;
        }

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (paperTarget == null)
            {
                paperTarget = FindFirstObjectByType<PaperFocusableObject>(FindObjectsInactive.Exclude);
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>() ?? GetComponentInChildren<AudioSource>();
            }

            if (audioSource == null && gameObject != null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }

#if UNITY_EDITOR
            if (stampSuccessSound == null)
            {
                stampSuccessSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audios/stamp.mp3");
            }
            if (dipSound == null)
            {
                dipSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audios/ink.mp3");
            }
#endif

            EnsureCollidersIgnored();
            CaptureRestTransform();
            UpdateInkVisual();
        }

        private void OnEnable()
        {
            EnsureCollidersIgnored();
        }

        private void EnsureCollidersIgnored()
        {
            int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreRaycastLayer < 0) ignoreRaycastLayer = 2;

            gameObject.layer = ignoreRaycastLayer;
            var colliders = GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders)
            {
                if (col != null)
                {
                    col.gameObject.layer = ignoreRaycastLayer;
                }
            }
        }

        public void CaptureRestTransform()
        {
            if (restAnchor != null)
            {
                initialRestPosition = restAnchor.position;
                initialRestRotation = restAnchor.rotation;
            }
            else
            {
                initialRestPosition = transform.position;
                initialRestRotation = transform.rotation;
            }

            workingSurfaceY = initialRestPosition.y;
            currentAimRotation = initialRestRotation;
        }

        public void SetInspectActive(bool active)
        {
            isInspectActive = active;
            hasSubmittedDecision = false;

            EnsureCollidersIgnored();

            if (currentHoveredObject != null)
            {
                currentHoveredObject.NotifyHoverExit();
                currentHoveredObject = null;
            }

            if (!active)
            {
                OnStampPreviewUpdated?.Invoke(false, false, Vector2.zero);

                if (punchCoroutine != null)
                {
                    StopCoroutine(punchCoroutine);
                    punchCoroutine = null;
                }
                isPunching = false;
                ResetInk();
                ReturnToRestPosition();
            }
            else
            {
                if (returnRoutine != null)
                {
                    StopCoroutine(returnRoutine);
                    returnRoutine = null;
                }

                if (paperTarget == null)
                {
                    paperTarget = FindFirstObjectByType<PaperFocusableObject>(FindObjectsInactive.Exclude);
                }

                // Inicializa a altura de referência da mesa/papel
                if (paperTarget != null)
                {
                    workingSurfaceY = paperTarget.GetTargetFocusedPosition().y;
                }
                else
                {
                    workingSurfaceY = initialRestPosition.y;
                }

                if (targetCamera == null) targetCamera = Camera.main;
                Vector3 camFwd = (targetCamera != null) ? targetCamera.transform.forward : Vector3.forward;
                Vector3 fwdOnDesk = Vector3.ProjectOnPlane(camFwd, Vector3.up).normalized;
                if (fwdOnDesk.sqrMagnitude < 0.001f) fwdOnDesk = Vector3.forward;
                currentAimRotation = Quaternion.LookRotation(fwdOnDesk, Vector3.up);
            }
        }

        public void SetInk(InkType inkType)
        {
            currentInk = (inkType == InkType.Approve) ? StampInkState.Approve : StampInkState.Reject;
            UpdateInkVisual();

            if (audioSource != null && dipSound != null)
            {
                audioSource.PlayOneShot(dipSound);
            }

            OnInkChanged?.Invoke(currentInk);
        }

        public void ResetInk()
        {
            currentInk = StampInkState.Dry;
            UpdateInkVisual();
            OnStampPreviewUpdated?.Invoke(false, false, Vector2.zero);
            OnInkChanged?.Invoke(currentInk);
        }

        private void UpdateInkVisual()
        {
            if (inkHeadRenderer == null) return;

            Material targetMat = currentInk switch
            {
                StampInkState.Approve => approveInkMaterial,
                StampInkState.Reject => rejectInkMaterial,
                _ => dryInkMaterial
            };

            if (targetMat != null)
            {
                inkHeadRenderer.material = targetMat;
            }
            else
            {
                Color tint = currentInk switch
                {
                    StampInkState.Approve => approveColor,
                    StampInkState.Reject => rejectColor,
                    _ => dryColor
                };
                inkHeadRenderer.material.color = tint;
            }
        }

        private void Update()
        {
            if (!isInspectActive || hasSubmittedDecision) return;

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null) return;
            }

            Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
            bool hasHit = RaycastScene(ray, out RaycastHit hit);

            // Atualização de Hover Highlight específica para Almofadas de Tinta (InkPad3D)
            FocusableObject hitFocusable = null;
            if (hasHit && hit.collider != null)
            {
                var inkPad = hit.collider.GetComponentInParent<InkPad3D>() ??
                             hit.collider.GetComponentInChildren<InkPad3D>();
                if (inkPad != null)
                {
                    hitFocusable = inkPad;
                }
            }

            if (hitFocusable != currentHoveredObject)
            {
                if (currentHoveredObject != null)
                {
                    currentHoveredObject.NotifyHoverExit();
                }

                currentHoveredObject = hitFocusable;

                if (currentHoveredObject != null && currentHoveredObject.enabled && currentHoveredObject.gameObject.activeInHierarchy)
                {
                    currentHoveredObject.NotifyHoverEnter();
                }
            }

            // Atualização de Preview Translúcido na Folha de Proposta
            if (!isPunching && currentInk != StampInkState.Dry && hasHit && hit.collider != null)
            {
                var paperFocusable = hit.collider.GetComponentInParent<PaperFocusableObject>() ??
                                     hit.collider.GetComponentInChildren<PaperFocusableObject>();

                bool isPaperHit = (paperFocusable != null) ||
                                  hit.collider.name.IndexOf("papel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  hit.collider.name.IndexOf("paper", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  hit.collider.name.IndexOf("document", StringComparison.OrdinalIgnoreCase) >= 0;

                if (isPaperHit)
                {
                    Vector2 uv = CalculateHitUV(hit);
                    OnStampPreviewUpdated?.Invoke(true, currentInk == StampInkState.Approve, uv);
                }
                else
                {
                    OnStampPreviewUpdated?.Invoke(false, false, Vector2.zero);
                }
            }
            else
            {
                OnStampPreviewUpdated?.Invoke(false, false, Vector2.zero);
            }

            if (!isPunching)
            {
                float targetHoverY = GetHoverElevation();
                Vector3 camFwd = targetCamera.transform.forward;
                Vector3 fwdOnDesk = Vector3.ProjectOnPlane(camFwd, Vector3.up).normalized;
                if (fwdOnDesk.sqrMagnitude < 0.001f) fwdOnDesk = Vector3.forward;

                Vector3 aimPoint;
                Vector3 normal = Vector3.up;

                if (hasHit)
                {
                    aimPoint = hit.point;
                    if (hit.normal.y > 0.4f)
                    {
                        normal = hit.normal;
                    }
                }
                else
                {
                    float refY = workingSurfaceY;
                    if (paperTarget != null)
                    {
                        refY = paperTarget.GetTargetFocusedPosition().y;
                    }

                    Plane surfacePlane = new Plane(Vector3.up, new Vector3(0f, refY, 0f));
                    if (surfacePlane.Raycast(ray, out float enter))
                    {
                        aimPoint = ray.GetPoint(enter);
                    }
                    else
                    {
                        aimPoint = transform.position;
                    }
                }

                // Posiciona o carimbo exatamente acima do ponto de mira (X e Z alinhados ao papel/colisor),
                // mantendo a elevação Y travada na altura configurada
                Vector3 targetPos = new Vector3(aimPoint.x, targetHoverY, aimPoint.z);
                currentAimRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(camFwd, normal).normalized, normal);

                transform.position = Vector3.Lerp(transform.position, targetPos, Time.unscaledDeltaTime * followSpeed);
                transform.rotation = Quaternion.Slerp(transform.rotation, currentAimRotation, Time.unscaledDeltaTime * followSpeed);
            }

            // Clique do Mouse
            if (Input.GetMouseButtonDown(0))
            {
                HandleClick(ray);
            }
        }

        public Vector2 CalculateHitUV(RaycastHit hit)
        {
            Vector2 uv = hit.textureCoord;
            if (uv.sqrMagnitude < 0.0001f && hit.collider != null)
            {
                Vector3 local = hit.collider.transform.InverseTransformPoint(hit.point);
                if (hit.collider is BoxCollider box)
                {
                    float u = Mathf.Clamp01((local.x - box.center.x) / box.size.x + 0.5f);
                    float v = Mathf.Clamp01((local.z - box.center.z) / box.size.z + 0.5f);
                    if (box.size.z < 0.01f)
                    {
                        v = Mathf.Clamp01((local.y - box.center.y) / box.size.y + 0.5f);
                    }
                    uv = new Vector2(u, v);
                }
            }
            return uv;
        }

        private bool IsStampCollider(Collider col)
        {
            if (col == null) return false;
            if (col.gameObject.layer == 2) return true; // Ignore Raycast
            if (col.transform == transform || col.transform.IsChildOf(transform)) return true;
            if (col.GetComponentInParent<StampTool3D>() != null || col.GetComponentInChildren<StampTool3D>() != null) return true;
            return false;
        }

        private bool RaycastScene(Ray ray, out RaycastHit hit)
        {
            hit = default;
            int mask = interactionLayers.value & ~(1 << 2);
            RaycastHit[] hits = Physics.RaycastAll(ray, 50f, mask, QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0) return false;

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var h in hits)
            {
                if (h.collider == null) continue;
                if (IsStampCollider(h.collider)) continue;

                bool isInteractiveTrigger = h.collider.isTrigger && (
                    h.collider.GetComponentInParent<InkPad3D>() != null ||
                    h.collider.GetComponentInParent<PaperFocusableObject>() != null ||
                    h.collider.GetComponentInParent<FocusableObject>() != null
                );

                if (h.collider.isTrigger && !isInteractiveTrigger) continue;

                hit = h;
                return true;
            }

            return false;
        }

        public void HandleClick(Ray ray)
        {
            if (hasSubmittedDecision || isPunching) return;

            int mask = interactionLayers.value & ~(1 << 2);
            RaycastHit[] hits = Physics.RaycastAll(ray, 50f, mask, QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0) return;

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                if (hit.collider == null) continue;
                if (IsStampCollider(hit.collider)) continue;

                Vector3 normal = (hit.normal.sqrMagnitude > 0.01f) ? hit.normal : Vector3.up;
                Vector3 contactPoint = hit.point + (normal * stampTouchOffset);

                // 1. Clicou em uma Almofada de Tinta 3D
                var inkPad = hit.collider.GetComponentInParent<InkPad3D>() ?? hit.collider.GetComponentInChildren<InkPad3D>();
                if (inkPad != null)
                {
                    StartPunch(contactPoint, normal, () =>
                    {
                        inkPad.ApplyInkToStamp(this);
                    });
                    return;
                }

                // 2. Clicou no Papel 3D
                var paperFocusable = hit.collider.GetComponentInParent<PaperFocusableObject>() ??
                                     hit.collider.GetComponentInChildren<PaperFocusableObject>();

                bool isPaperHit = (paperFocusable != null) ||
                                  hit.collider.name.IndexOf("papel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  hit.collider.name.IndexOf("paper", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  hit.collider.name.IndexOf("document", StringComparison.OrdinalIgnoreCase) >= 0;

                if (isPaperHit)
                {
                    if (currentInk == StampInkState.Dry)
                    {
                        // Carimbo seco: avança até tocar a folha e toca som de carimbo seco
                        StartPunch(contactPoint, normal, () =>
                        {
                            if (audioSource != null && stampDrySound != null)
                            {
                                audioSource.PlayOneShot(stampDrySound);
                            }
                        });
                        return;
                    }

                    // Carimbo molhado: calcula UV e confirma a decisão
                    Vector2 uv = CalculateHitUV(hit);
                    int choiceIndex = (int)currentInk;
                    hasSubmittedDecision = true;

                    OnStampPreviewUpdated?.Invoke(false, false, Vector2.zero);

                    if (currentHoveredObject != null)
                    {
                        currentHoveredObject.NotifyHoverExit();
                        currentHoveredObject = null;
                    }

                    StartPunch(contactPoint, normal, () =>
                    {
                        if (audioSource != null && stampSuccessSound != null)
                        {
                            audioSource.PlayOneShot(stampSuccessSound);
                        }

                        OnStampApplied?.Invoke(choiceIndex, uv);
                        OnStampDecisionSubmitted?.Invoke(choiceIndex);
                    });
                    return;
                }
            }
        }

        private void StartPunch(Vector3 targetSurfacePoint, Vector3 surfaceNormal, Action onImpact)
        {
            if (punchCoroutine != null)
            {
                StopCoroutine(punchCoroutine);
            }
            punchCoroutine = StartCoroutine(PunchRoutine(targetSurfacePoint, surfaceNormal, onImpact));
        }

        private IEnumerator PunchRoutine(Vector3 targetSurfacePoint, Vector3 surfaceNormal, Action onImpact)
        {
            isPunching = true;
            Vector3 startPos = transform.position;
            Vector3 impactPos = targetSurfacePoint;
            float punchDuration = 0.07f;
            float liftDuration = 0.14f;

            // Descida firme até tocar o collider
            float elapsed = 0f;
            while (elapsed < punchDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / punchDuration;
                transform.position = Vector3.Lerp(startPos, impactPos, t);
                yield return null;
            }

            transform.position = impactPos;
            onImpact?.Invoke();

            // Retorno para a altura de hover consistente configurada
            elapsed = 0f;
            float targetHoverY = GetHoverElevation();
            Vector3 liftedPos = new Vector3(impactPos.x, targetHoverY, impactPos.z);
            while (elapsed < liftDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / liftDuration);
                transform.position = Vector3.Lerp(impactPos, liftedPos, t);
                yield return null;
            }

            transform.position = liftedPos;
            isPunching = false;
            punchCoroutine = null;
        }

        public void ReturnToRestPosition(float duration = 0.4f)
        {
            if (returnRoutine != null)
            {
                StopCoroutine(returnRoutine);
            }
            returnRoutine = StartCoroutine(ReturnToRestRoutine(duration));
        }

        private IEnumerator ReturnToRestRoutine(float duration)
        {
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            Vector3 targetPos = (restAnchor != null) ? restAnchor.position : initialRestPosition;
            Quaternion targetRot = (restAnchor != null) ? restAnchor.rotation : initialRestRotation;

            float elapsed = 0f;
            duration = Mathf.Max(duration, 0.05f);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                transform.position = Vector3.Lerp(startPos, targetPos, t);
                transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }

            transform.position = targetPos;
            transform.rotation = targetRot;
            returnRoutine = null;
        }
    }
}
