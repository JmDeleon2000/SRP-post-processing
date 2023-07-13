using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

//Maneja varios pases de rendering
[System.Serializable]
public class BenDayRendererFeature : ScriptableRendererFeature
{
    [SerializeField]
    private Shader m_bloomShader;
    [SerializeField]
    private Shader m_compositeShader;


    private Material m_bloomMaterial;
    private Material m_compositeMaterial;

    private BenDayRenderPass m_CustomPass;
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_CustomPass);
    }

    public override void Create()
    {
        //Crear los materiales. Importantisimo borrarlos en Dispose para evitar leakear materiales a memoria o peor, a disco
        m_bloomMaterial = CoreUtils.CreateEngineMaterial(m_bloomShader);
        m_compositeMaterial = CoreUtils.CreateEngineMaterial(m_compositeShader);

        //Mandarle los materiales al render pass para que los pueda usar
        m_CustomPass = new BenDayRenderPass(m_bloomMaterial, m_compositeMaterial);
    }
    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(m_bloomMaterial);
        CoreUtils.Destroy(m_compositeMaterial); 
    }


    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        if(renderingData.cameraData.cameraType == CameraType.Game) //evitar hacer el efecto de post processing en el editor de escena
        {
            m_CustomPass.ConfigureInput(ScriptableRenderPassInput.Depth);
            m_CustomPass.ConfigureInput(ScriptableRenderPassInput.Color);
            m_CustomPass.SetTarget(renderer.cameraColorTargetHandle, renderer.cameraDepthTargetHandle);
        }
    }
}
