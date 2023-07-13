using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class BenDayRenderPass : ScriptableRenderPass
{
    private Material m_BloomMaterial;
    private Material compositeMaterial;

    RenderTextureDescriptor m_Descriptor;
    RTHandle m_cameraColorTarget;
    RTHandle m_cameraDepthTarget;

    const int k_MaxPyramidSize = 16;
    private int[] _BloomMinUp;
    private int[] _BloomMinDown;
    //Un RTHandle es una RenderTexture que también maneja minMapping
    private RTHandle[] m_BloomMipUp;
    private RTHandle[] m_BloomMipDown;
    private GraphicsFormat hdrFormat;
    
    //Guarda los materiales por los que el pase va a pasar al frame y registra cuándo se debe correr el evento en la pipeline
    //Además, maneja todo el formateo que se necesita para solo jalar el bloom de Unity
    public BenDayRenderPass(Material m_BloomMaterial, Material compositeMaterial)
    {
        this.m_BloomMaterial = m_BloomMaterial;
        this.compositeMaterial = compositeMaterial;

        renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

         _BloomMinUp = new int[k_MaxPyramidSize];
         _BloomMinDown = new int[k_MaxPyramidSize];
         m_BloomMipUp = new RTHandle[k_MaxPyramidSize];
         m_BloomMipDown = new RTHandle[k_MaxPyramidSize];

        for (int i = 0; i < k_MaxPyramidSize; i++)
        {
            //Conseguir las IDs de cada parámetro en el shader para luego poder pasarselo
            _BloomMinUp[i] = Shader.PropertyToID("_BloomMinUp" + i);
            _BloomMinDown[i] = Shader.PropertyToID("_BloomMinDown" + i);
            //Alocar el espacio para los descriptores que el bloom usará luego, copiado y pegado de la implementación de Unity
            m_BloomMipUp[i] = RTHandles.Alloc(_BloomMinUp[i], name:"_BloomMinUp" + i);
            m_BloomMipDown[i] = RTHandles.Alloc(_BloomMinDown[i], name:"_BloomMinDown" + i);
        }

        const FormatUsage usage = FormatUsage.Linear | FormatUsage.Render;
        if(SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, usage))//HDR fallback
            hdrFormat = GraphicsFormat.B10G11R11_UFloatPack32;
        else
            hdrFormat = QualitySettings.activeColorSpace == ColorSpace.Linear ?
               GraphicsFormat.R8G8B8A8_SRGB : GraphicsFormat.R8G8B8A8_UNorm;
        
    }

    private BenDayBloomSRPComponent m_BloomEffect;
    //Define lo que el pase hace y cuándo
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        VolumeStack stack = VolumeManager.instance.stack;
        m_BloomEffect = stack.GetComponent<BenDayBloomSRPComponent>();

        CommandBuffer cmd_buff = CommandBufferPool.Get();

        //Esto controla como lo que corre acá se muestra en el frame debugger.
        //Obviamente se logguea lo que le mandamos a hacer al command buffer
        using (new ProfilingScope(cmd_buff, 
            new ProfilingSampler("Custom Post Process Effects")))
        {
            SetupBloom(cmd_buff, m_cameraColorTarget);

            compositeMaterial.SetFloat("_Cutoff", m_BloomEffect.dotCutoff.value);
            compositeMaterial.SetFloat("_Density", m_BloomEffect.dotDensity.value);
            compositeMaterial.SetVector("_Direction", m_BloomEffect.scrollDirection.value);
            compositeMaterial.SetTexture("_Bloom_Texture", m_BloomMipUp[0]);


            //Pasar el frame buffer por el material
            Blitter.BlitCameraTexture(cmd_buff, m_cameraColorTarget, m_cameraColorTarget, compositeMaterial, 0);
        }

        context.ExecuteCommandBuffer(cmd_buff);
        cmd_buff.Clear();

        CommandBufferPool.Release(cmd_buff);
    }

    private void SetupBloom(CommandBuffer cmd_buff, RTHandle source)
    {
        int downres = 1;
        int tw = m_Descriptor.width >> downres; //¿Por qué hacer esto en vez de /2? ¿Para que escale con downres?
        int th = m_Descriptor.height >> downres; //me guta

        //Determine max iteration count
        int maxSize = Mathf.Max(tw, th);
        int iterations = Mathf.FloorToInt(Mathf.Log(maxSize, 2f) - 1);
        int mipCount = Mathf.Clamp(iterations, 1, m_BloomEffect.maxIter.value);

        //Pre-filtering parameters
        float clamp = m_BloomEffect.clamp.value;
        float threshold = Mathf.GammaToLinearSpace(m_BloomEffect.threshold.value);
        float thresholdKnee = threshold * 0.5f;

        //Material setup
        float scatter = Mathf.Lerp(0.05f, 0.095f, m_BloomEffect.scatter.value); //Acá sí no entiendo,
                                                                                //fijo es interno a cómo ellos hicieron el bloom.
        var bloomMaterial = m_BloomMaterial;

        bloomMaterial.SetVector("_Params", new Vector4(scatter, clamp, threshold, thresholdKnee));

        //Prefilter
        var desc = GetCompatibleDescriptor(tw, th, hdrFormat);
        for(int i = 0; i < mipCount; i++)
        {
            RenderingUtils.ReAllocateIfNeeded(ref m_BloomMipUp[i], desc, FilterMode.Bilinear,
                TextureWrapMode.Clamp, name: m_BloomMipUp[i].name);
            RenderingUtils.ReAllocateIfNeeded(ref m_BloomMipDown[i], desc, FilterMode.Bilinear,
                TextureWrapMode.Clamp, name: m_BloomMipDown[i].name);
            desc.width = Mathf.Max(1, desc.width >> 1);
            desc.height = Mathf.Max(1, desc.height >> 1);
        }

        Blitter.BlitCameraTexture(cmd_buff, source, m_BloomMipDown[0], RenderBufferLoadAction.DontCare, 
                RenderBufferStoreAction.Store, bloomMaterial, 0);

        // Downsample
        var lastDown = m_BloomMipDown[0];
        for (int i = 1; i < mipCount; i++)
        {
            //Pasar todas las texturas por el bloom de unity una vez por cada uno de los pases.
            //El primer pase hace 2x downsampling y 9-tap gaussian blur
            //El segundo hace 9-tap gaussian blur con un 5-tap gaussian blur y bilinear filtering
            Blitter.BlitCameraTexture(cmd_buff, lastDown, m_BloomMipUp[i], RenderBufferLoadAction.DontCare, 
                RenderBufferStoreAction.Store, bloomMaterial, 1);
            Blitter.BlitCameraTexture(cmd_buff, m_BloomMipUp[i], m_BloomMipDown[i], RenderBufferLoadAction.DontCare,
                RenderBufferStoreAction.Store, bloomMaterial, 2);
            lastDown = m_BloomMipDown[i];
        }

        //Usar el ultimo pase para manejar Upsampling de los mip maps 
        for(int i = mipCount - 2; i >= 0; i--)
        {
            var lowMip = (i == mipCount - 2) ? m_BloomMipDown[i + 1] : m_BloomMipUp[i + 1];
            var highMip = m_BloomMipDown[i];
            var dst = m_BloomMipUp[i];

            cmd_buff.SetGlobalTexture("_SourceTexLowMip", lowMip);
            Blitter.BlitCameraTexture(cmd_buff, highMip, dst, 
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, bloomMaterial, 3);
        }

        //Guardar el resultado de pasar el frame por el bloom shader en la textura que toma el shader de BenDay
       // cmd_buff.SetGlobalTexture("_Bloom_Texture", m_BloomMipUp[0]);
        cmd_buff.SetGlobalFloat("_BloomIntensity", m_BloomEffect.intensity.value);
    }

    internal static RenderTextureDescriptor GetCompatibleDescriptor(RenderTextureDescriptor desc, int w, int h, 
        GraphicsFormat format, DepthBits depthBufferBits = DepthBits.None) 
    {
        desc.depthBufferBits = (int)depthBufferBits;
        desc.msaaSamples = 1;
        desc.width = w;
        desc.height = h;
        desc.graphicsFormat = format;
        return desc;
    }

    RenderTextureDescriptor GetCompatibleDescriptor(int width, int height, GraphicsFormat format, DepthBits depthBufferBits = DepthBits.None)
    => GetCompatibleDescriptor(m_Descriptor, width, height, format, depthBufferBits);


    public override void OnCameraSetup(CommandBuffer cmd_buff, ref RenderingData renderingData) 
        => m_Descriptor = renderingData.cameraData.cameraTargetDescriptor; //Arcane
    

    
    public void SetTarget(RTHandle cameraColorTargetHandle, RTHandle cameraDepthTargetHandle)
    {
        m_cameraColorTarget = cameraColorTargetHandle;
        m_cameraDepthTarget = cameraDepthTargetHandle;
    }
}
