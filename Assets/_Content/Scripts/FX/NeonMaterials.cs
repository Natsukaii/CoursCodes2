using UnityEngine;
using UnityEngine.Rendering;

namespace Arena.FX
{
    /// <summary>
    /// Runtime neon materials: unlit and additive (URP Particles/Unlit), so they glow and never cast shadows.
    /// </summary>
    public static class NeonMaterials
    {
        public static Material CreateAdditive(Shader shader, Texture texture, Color color)
        {
            if (!shader)
                shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

            Material material = new Material(shader) { name = "Neon (runtime)" };
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;

            if (texture)
                material.SetTexture("_BaseMap", texture);

            material.SetColor("_BaseColor", color);
            return material;
        }

        /// <summary>
        /// Cube without collider, used as a neon tube.
        /// </summary>
        public static GameObject CreateTube(string name, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        /// <summary>
        /// Flat quad lying on the ground, without collider.
        /// </summary>
        public static GameObject CreateGroundQuad(string name, Material material, float size)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(size, size, 1f);
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }
    }
}
