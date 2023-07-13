using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;



[VolumeComponentMenuForRenderPipeline("Custom/Spider-verse/3D Glasses focus", typeof(UniversalRenderPipeline))]
public class RedBlueFocusComponent : VolumeComponent, IPostProcessComponent
{
    [Header("Into the Spider-Verse focus")]
    public ClampedFloatParameter intensity = new ClampedFloatParameter(0.5f, 0, 1, true);
    public FloatParameter Thickness = new FloatParameter(1, true);
    public ClampedFloatParameter DepthThreshold = new ClampedFloatParameter(0.5f, 0, 1, true);
    public FloatParameter DepthThickness = new FloatParameter(1, true);
    public Vector2Parameter Focus = new Vector2Parameter(new Vector2());
    public ColorParameter RightTint = new ColorParameter(new Color());
    public ColorParameter LeftTint = new ColorParameter(new Color());

    public bool IsActive()
    {
        return true;
    }

    public bool IsTileCompatible()
    {
        return false;
    }
}
