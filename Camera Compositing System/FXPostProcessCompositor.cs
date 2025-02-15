using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[VolumeComponentMenu("Custom/FXCompositor")]
public class FXPostProcessCompositor : CustomPostProcessVolumeComponent, IPostProcessComponent
{
    public TextureParameter textureA   = new TextureParameter(null);
    public TextureParameter textureB   = new TextureParameter(null);
    public TextureParameter textureKey = new TextureParameter(null);
    public TextureParameter mask       = new TextureParameter(null);

    public ClampedFloatParameter brightness = new ClampedFloatParameter(1f, 0f, 1f);

    private Material material;
    private const string kShaderName = "Hidden/FX/CameraCompositor";

    public bool IsActive() => textureA.value != null && textureB.value != null;

    public override CustomPostProcessInjectionPoint injectionPoint =>
        CustomPostProcessInjectionPoint.AfterPostProcess;

    public override void Setup()
    {
        if (Shader.Find(kShaderName) != null)
            material = new Material(Shader.Find(kShaderName));
        else
            Debug.LogError($"Unable to find shader '{kShaderName}'. The post-process effect will not be applied.");
    }

    public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
    {
        if (!IsActive()) {
            return;

        }

        if (textureA.value != null)
        {
            material.SetTexture("_TextureA", textureA.value);
        }
        if (textureB.value != null)
        {
            material.SetTexture("_TextureB", textureB.value);
        }
        if (textureKey.value != null)
        {
            material.SetTexture("_TextureKey", textureKey.value);
        }
        if (mask.value != null)
        {
            material.SetTexture("_TextureMask", mask.value);
        }
        material.SetFloat("_Brightness", brightness.value);
        HDUtils.DrawFullScreen(cmd, material, destination, null, 0);
    }

    public override void Cleanup()
    {
        CoreUtils.Destroy(material);
    }
}
