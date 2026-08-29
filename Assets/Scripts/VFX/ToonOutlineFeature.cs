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

        class ToonOutlinePass : ScriptableRenderPass
        {
            private Settings settings;

            private class PassData
            {
                public Material material;
                public Color color;
                public float thickness;
                public float depthThreshold;
                public float depthSensitivity;
                public TextureHandle source;
            }

            public ToonOutlinePass(Settings settings)
            {
                this.settings = settings;
                this.renderPassEvent = settings.renderPassEvent;
                ConfigureInput(ScriptableRenderPassInput.Depth);
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
                TextureHandle tempTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_ToonOutlineTemp", true);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("ToonOutlinePass", out var passData))
                {
                    passData.material = settings.outlineMaterial;
                    passData.color = settings.outlineColor;
                    passData.thickness = settings.thickness;
                    passData.depthThreshold = settings.depthThreshold;
                    passData.depthSensitivity = settings.depthSensitivity;
                    passData.source = activeColor;

                    builder.UseTexture(activeColor, AccessFlags.Read);
                    builder.SetRenderAttachment(tempTexture, 0, AccessFlags.Write);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        if (data.material == null) return;

                        data.material.SetColor("_OutlineColor", data.color);
                        data.material.SetFloat("_Thickness", data.thickness);
                        data.material.SetFloat("_DepthThreshold", data.depthThreshold);
                        data.material.SetFloat("_DepthSensitivity", data.depthSensitivity);

                        Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                    });
                }

                // Redirect target camera color to tempTexture for subsequent URP output
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

                CommandBuffer cmd = CommandBufferPool.Get("ToonOutlinePass_Legacy");
                settings.outlineMaterial.SetColor("_OutlineColor", settings.outlineColor);
                settings.outlineMaterial.SetFloat("_Thickness", settings.thickness);
                settings.outlineMaterial.SetFloat("_DepthThreshold", settings.depthThreshold);
                settings.outlineMaterial.SetFloat("_DepthSensitivity", settings.depthSensitivity);

                RTHandle cameraTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
                Blit(cmd, cameraTarget, cameraTarget, settings.outlineMaterial, 0);

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
