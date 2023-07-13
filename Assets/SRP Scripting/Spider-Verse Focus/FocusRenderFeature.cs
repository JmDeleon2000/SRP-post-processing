using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

//La idea de este script es parametrizar, inicializar y orquestrar uno o varios pases
//Luego casa pase orquestrará el uso de materiales y texturas
public class FocusRenderFeature : ScriptableRendererFeature
{
    [SerializeField]
    private Shader focus_shader;
    private Material focus_material;

    private FocusRenderPass custom_pass;
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        => renderer.EnqueuePass(custom_pass);


    //Inicializar el pase y los materiales que necesita
    public override void Create()
    {
        //Crea un material que se borrará cuando se active y se llame al renderer feature
        focus_material = CoreUtils.CreateEngineMaterial(focus_shader);

        //Crear el pase y darle el material
        custom_pass = new FocusRenderPass(focus_material);
    }


    //BORAR el material, si no se leakea a DISCO, ni siquiera a memoria
    protected override void Dispose(bool disposing) => CoreUtils.Destroy(focus_material);

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType == CameraType.Game) //evitar hacer el efecto de post processing en el editor de escena
        {
            custom_pass.ConfigureInput(ScriptableRenderPassInput.Depth);
            custom_pass.ConfigureInput(ScriptableRenderPassInput.Color);
            custom_pass.SetTarget(renderer.cameraColorTargetHandle, renderer.cameraDepthTargetHandle);
        }
    }

}
