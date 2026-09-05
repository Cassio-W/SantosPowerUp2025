using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace ComicVFX
{
    public class ToonOutlineFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            public Material outlineMaterial = null;
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            public Color outlineColor = new Color(0.04f, 0.04f, 0.07f, 1.0f);
            [Range(0.5f, 5.0f)] public float thickness = 1.25f;
            [Range(0.001f, 0.2f)] public float depthThreshold = 0.015f;
            [Range(0.1f, 10.0f)] public float depthSensitivity = 1.5f;
        }

        public Settings settings = new Settings();

        private static Material _sharedMaskMaterial;
        private static readonly int HoverMaskProp = Shader.PropertyToID("_HoverMask");
        private static readonly int OutlineColorProp = Shader.PropertyToID("_OutlineColor");
        private static readonly int ThicknessProp = Shader.PropertyToID("_Thickness");
        private static readonly int DepthThresholdProp = Shader.PropertyToID("_DepthThreshold");
        private static readonly int DepthSensitivityProp = Shader.PropertyToID("_DepthSensitivity");
        private static readonly int HighlightOutlineColorProp = Shader.PropertyToID("_HighlightOutlineColor");
        private static readonly int HasHighlightProp = Shader.PropertyToID("_HasHighlight");

        public static Material GetMaskMaterial()
        {
            if (_sharedMaskMaterial == null)
            {
                Shader maskShader = Shader.Find("Hidden/ComicVFX/UnlitMask");
                if (maskShader != null)
                {
                    _sharedMaskMaterial = new Material(maskShader) { hideFlags = HideFlags.HideAndDontSave };
                }
            }
            return _sharedMaskMaterial;
        }

        class ToonOutlinePass : ScriptableRenderPass
        {
            private Settings settings;

            private class MaskPassData
            {
                public List<Renderer> renderers;
                public Material maskMaterial;
            }

            private class PassData
            {
                public Material material;
                public Color color;
                public float thickness;
                public float depthThreshold;
                public float depthSensitivity;
                public TextureHandle source;
                public TextureHandle hoverMask;
                public bool hasHighlight;
                public Color highlightColor;
                public float highlightWeight;
            }

            public ToonOutlinePass(Settings settings)
            {
                this.settings = settings;
                this.renderPassEvent = settings.renderPassEvent;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            private static bool CollectActiveHighlights(List<Renderer> outRenderers, out Color outHighlightColor, out float outHighlightWeight)
            {
                outHighlightColor = Color.white;
                outHighlightWeight = 0f;
                outRenderers.Clear();

                // 1. Prioridade absoluta para o objeto ativamente focado pelo mouse (ActiveHighlightedObject)
                FocusableObject active = FocusableObject.ActiveHighlightedObject;
                if (active != null && active.enabled && active.gameObject.activeInHierarchy && active.IsHovered && !active.IsFocused && active.EnableOutlineHighlight)
                {
                    if (active.TargetRenderers != null && active.TargetRenderers.Count > 0)
                    {
                        for (int i = 0; i < active.TargetRenderers.Count; i++)
                        {
                            Renderer r = active.TargetRenderers[i];
                            if (r != null && r.enabled && r.gameObject.activeInHierarchy)
                            {
                                outRenderers.Add(r);
                            }
                        }

                        if (outRenderers.Count > 0)
                        {
                            outHighlightColor = active.HighlightOutlineColor;
                            outHighlightWeight = active.CurrentHighlightWeight > 0.001f ? active.CurrentHighlightWeight : 1f;
                            return true;
                        }
                    }
                }

                // 2. Fallback caso a lista seja usada para um único objeto em transição
                List<FocusableObject> activeList = FocusableObject.ActiveHighlightedObjects;
                if (activeList != null && activeList.Count > 0)
                {
                    for (int i = activeList.Count - 1; i >= 0; i--)
                    {
                        FocusableObject fo = activeList[i];
                        if (fo == null || !fo.enabled || !fo.gameObject.activeInHierarchy || !fo.IsHovered || fo.IsFocused)
                        {
                            activeList.RemoveAt(i);
                            continue;
                        }

                        if (fo.CurrentHighlightWeight > 0.001f && fo.TargetRenderers != null && fo.TargetRenderers.Count > 0)
                        {
                            for (int r = 0; r < fo.TargetRenderers.Count; r++)
                            {
                                Renderer ren = fo.TargetRenderers[r];
                                if (ren != null && ren.enabled && ren.gameObject.activeInHierarchy)
                                {
                                    outRenderers.Add(ren);
                                }
                            }

                            if (fo.CurrentHighlightWeight > outHighlightWeight)
                            {
                                outHighlightWeight = fo.CurrentHighlightWeight;
                                outHighlightColor = fo.HighlightOutlineColor;
                            }
                        }
                    }
                }

                return outRenderers.Count > 0 && outHighlightWeight > 0.001f;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (settings == null || settings.outlineMaterial == null) return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (resourceData == null || cameraData == null) return;

                TextureHandle activeColor = resourceData.activeColorTexture;
                if (!activeColor.IsValid()) return;

                RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;

                List<Renderer> highlightRenderers = new List<Renderer>();
                Color highlightColor;
                float highlightWeight;
                bool hasHighlight = CollectActiveHighlights(highlightRenderers, out highlightColor, out highlightWeight);
                TextureHandle hoverMaskTex = TextureHandle.nullHandle;

                if (hasHighlight)
                {
                    Material maskMat = GetMaskMaterial();
                    if (maskMat != null)
                    {
                        RenderTextureDescriptor maskDesc = desc;
                        maskDesc.colorFormat = RenderTextureFormat.R8;

                        hoverMaskTex = UniversalRenderer.CreateRenderGraphTexture(renderGraph, maskDesc, "_HoverMaskTemp", true);

                        using (var maskBuilder = renderGraph.AddRasterRenderPass<MaskPassData>("HoverMaskPass", out var maskData))
                        {
                            maskData.renderers = highlightRenderers;
                            maskData.maskMaterial = maskMat;

                            maskBuilder.SetRenderAttachment(hoverMaskTex, 0, AccessFlags.Write);
                            maskBuilder.AllowPassCulling(false);
                            maskBuilder.AllowGlobalStateModification(true);

                            maskBuilder.SetRenderFunc((MaskPassData data, RasterGraphContext context) =>
                            {
                                context.cmd.ClearRenderTarget(false, true, Color.black);
                                if (data.renderers != null && data.maskMaterial != null)
                                {
                                    for (int i = 0; i < data.renderers.Count; i++)
                                    {
                                        Renderer r = data.renderers[i];
                                        if (r != null && r.enabled && r.gameObject.activeInHierarchy)
                                        {
                                            context.cmd.DrawRenderer(r, data.maskMaterial, 0, 0);
                                        }
                                    }
                                }
                            });
                        }
                    }
                    else
                    {
                        hasHighlight = false;
                    }
                }

                TextureHandle tempTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_ToonOutlineTemp", true);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("ToonOutlinePass", out var passData))
                {
                    passData.material = settings.outlineMaterial;
                    passData.color = settings.outlineColor;
                    passData.thickness = settings.thickness;
                    passData.depthThreshold = settings.depthThreshold;
                    passData.depthSensitivity = settings.depthSensitivity;
                    passData.source = activeColor;
                    passData.hasHighlight = hasHighlight;
                    passData.highlightColor = highlightColor;
                    passData.highlightWeight = highlightWeight;
                    passData.hoverMask = hoverMaskTex;

                    builder.UseTexture(activeColor, AccessFlags.Read);
                    if (hasHighlight && hoverMaskTex.IsValid())
                    {
                        builder.UseTexture(hoverMaskTex, AccessFlags.Read);
                    }

                    builder.SetRenderAttachment(tempTexture, 0, AccessFlags.Write);
                    builder.AllowPassCulling(false);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        if (data.material == null) return;

                        data.material.SetColor(OutlineColorProp, data.color);
                        data.material.SetFloat(ThicknessProp, data.thickness);
                        data.material.SetFloat(DepthThresholdProp, data.depthThreshold);
                        data.material.SetFloat(DepthSensitivityProp, data.depthSensitivity);

                        if (data.hasHighlight && data.hoverMask.IsValid())
                        {
                            context.cmd.SetGlobalTexture(HoverMaskProp, data.hoverMask);
                            data.material.SetColor(HighlightOutlineColorProp, data.highlightColor);
                            data.material.SetFloat(HasHighlightProp, data.highlightWeight);
                        }
                        else
                        {
                            data.material.SetFloat(HasHighlightProp, 0.0f);
                        }

                        Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                    });
                }

                resourceData.cameraColor = tempTexture;
            }

#pragma warning disable CS0618, CS0672
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (settings == null || settings.outlineMaterial == null) return;

                List<Renderer> highlightRenderers = new List<Renderer>();
                Color highlightColor;
                float highlightWeight;
                bool hasHighlight = CollectActiveHighlights(highlightRenderers, out highlightColor, out highlightWeight);

                CommandBuffer cmd = CommandBufferPool.Get("ToonOutlinePass_Legacy");

                int maskId = Shader.PropertyToID("_LegacyHoverMask");

                if (hasHighlight)
                {
                    Material maskMat = GetMaskMaterial();
                    if (maskMat != null)
                    {
                        RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
                        desc.depthBufferBits = 0;
                        desc.colorFormat = RenderTextureFormat.R8;
                        cmd.GetTemporaryRT(maskId, desc, FilterMode.Bilinear);
                        cmd.SetRenderTarget(maskId);
                        cmd.ClearRenderTarget(false, true, Color.black);

                        for (int i = 0; i < highlightRenderers.Count; i++)
                        {
                            Renderer r = highlightRenderers[i];
                            if (r != null && r.enabled && r.gameObject.activeInHierarchy)
                            {
                                cmd.DrawRenderer(r, maskMat, 0, 0);
                            }
                        }

                        cmd.SetGlobalTexture(HoverMaskProp, maskId);
                        settings.outlineMaterial.SetColor(HighlightOutlineColorProp, highlightColor);
                        settings.outlineMaterial.SetFloat(HasHighlightProp, highlightWeight);
                    }
                    else
                    {
                        settings.outlineMaterial.SetFloat(HasHighlightProp, 0.0f);
                    }
                }
                else
                {
                    settings.outlineMaterial.SetFloat(HasHighlightProp, 0.0f);
                }

                settings.outlineMaterial.SetColor(OutlineColorProp, settings.outlineColor);
                settings.outlineMaterial.SetFloat(ThicknessProp, settings.thickness);
                settings.outlineMaterial.SetFloat(DepthThresholdProp, settings.depthThreshold);
                settings.outlineMaterial.SetFloat(DepthSensitivityProp, settings.depthSensitivity);

                RTHandle cameraTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
                Blit(cmd, cameraTarget, cameraTarget, settings.outlineMaterial, 0);

                if (hasHighlight)
                {
                    cmd.ReleaseTemporaryRT(maskId);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
#pragma warning restore CS0618, CS0672
        }

        private ToonOutlinePass pass;

        public override void Create()
        {
            pass = new ToonOutlinePass(settings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.outlineMaterial != null)
            {
                renderer.EnqueuePass(pass);
            }
        }
    }
}
