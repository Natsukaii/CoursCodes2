#ifndef MAINLIGHT_INCLUDED
#define MAINLIGHT_INCLUDED

void GetMainLightData_float(out half3 direction, out half3 color)
{
#ifdef SHADERGRAPH_PREVIEW
    direction = half3(-0.3,-0.8, 0.6);
    color = half3(1.0,1.0,1.0);
#else
#if defined(UNIVERSAL_LIGHTING_INCLUDED)
        Light mainLight = GetMainLight();
        direction = mainLight.direction;
        color = mainLight.color;
#endif
#endif
}

void AdditionalLights_float(half3 WorldPosition, half3 WorldNormal, half ShadowEdge, half SmoothEdgeSize, out half3 Diffuse)
{
    half3 diffuseColor = 0;
    
#ifndef SHADERGRAPH_PREVIEW
    int lightCount = GetAdditionalLightsCount();
    for (int i = 0; i < lightCount; i++)
    {
        Light light = GetAdditionalLight(i, WorldPosition);
        half3 lightColor = light.color;
        half inRange = step(0.0001, light.distanceAttenuation);
        half lightDot = (dot(WorldNormal, light.direction) + 1) * 0.5 * light.distanceAttenuation;
        half lightForce = smoothstep(ShadowEdge, ShadowEdge + SmoothEdgeSize, lightDot);
        diffuseColor += lightColor * lightForce;
    }
#endif
    
    Diffuse = diffuseColor;
}

#endif