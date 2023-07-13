using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static Unity.Burst.Intrinsics.X86.Avx;

public class FocusRenderPass : ScriptableRenderPass
{

    private Material focus_material;
    private RTHandle CameraColorTargetHandle;//Referencia al Frame buffer
    private RTHandle CameraDepthTargetHandle;//Referencia al Depth buffer

    public FocusRenderPass(Material focus_mat)
    {
        focus_material = focus_mat;

        renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    RedBlueFocusComponent volume_config;
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        VolumeStack stack = VolumeManager.instance.stack;
        volume_config = stack.GetComponent<RedBlueFocusComponent>();

        //Sirve para definir las acciones que hará unity con la librería de gráficas. 
        //Puede que decida hacer las cosas en diferente orden o aprovechar a paralelizar lo que pueda.
        CommandBuffer cmd_buff = CommandBufferPool.Get();

        //Esto controla como lo que corre acá se muestra en el frame debugger.
        //Obviamente se logguea lo que le mandamos a hacer al command buffer
        using (new ProfilingScope(cmd_buff,
        new ProfilingSampler("Custom Post Process Effects/Spider-Man Focus")))
        {
            if (volume_config)
            {
                focus_material.SetFloat("_Intensity", volume_config.intensity.value);
                focus_material.SetFloat("_Thickness", volume_config.Thickness.value);
                focus_material.SetFloat("_DepthThickness", volume_config.DepthThickness.value);
                focus_material.SetFloat("_DepthThreshold", volume_config.DepthThreshold.value);
                focus_material.SetVector("_FocusPosition", volume_config.Focus.value);
                focus_material.SetColor("_RightTint", volume_config.RightTint.value);
                focus_material.SetColor("_LeftTint", volume_config.LeftTint.value);
            }
            Blitter.BlitCameraTexture(cmd_buff, CameraColorTargetHandle, CameraColorTargetHandle, focus_material, 0);
        }

        context.ExecuteCommandBuffer(cmd_buff);
        cmd_buff.Clear();

        CommandBufferPool.Release(cmd_buff);
    }


    //Recibe la referencia a los buffers que sobrescribirá
    internal void SetTarget(RTHandle cameraColorTargetHandle, RTHandle cameraDepthTargetHandle)
    {
        CameraColorTargetHandle = cameraColorTargetHandle;
        CameraDepthTargetHandle = cameraDepthTargetHandle;
    }
}
