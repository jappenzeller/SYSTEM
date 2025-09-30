The Bend The World Shader allows for dynamic bending effects across a surface while maintaining material 
details like textures, normal mapping, and ambient occlusion. 
It provides customizable properties for smoothness, metallicity, and height adjustments.

This shader uses the position of the camera to bend the mesh the further it is from the camera.
Note that it is possible to have a parametrized point parameter which you can change using "Shader.SetGlobalVector("_Param_Name", Vector3);"
 from a C# script.

Properties:
- **Albedo Texture**: The base color map for the material.
- **Normal Texture**: Adds surface detail using a normal map.
- **Normal Strength**: Controls the intensity of the normal map effect (0 to 1).
- **Smoothness**: Adjusts the glossiness of the surface (0 to 1).
- **Metallic**: Sets the metallicity of the material (0 to 1).
- **Start Bending At**: Specifies where the bending effect begins.
- **Bending Range**: Defines the extent of the bending effect.
- **Height**: Adjusts the height or elevation effect on the surface.
- **Ambient Occlusion (AO) Texture**: Enhances shadowing in crevices.
- **Emission Color**: Adds a glowing effect with customizable color.

Tips:
- Use high-quality textures for detailed surface effects.
- Experiment with bending range and height to create dynamic deformations.
- Add emission for glowing or highlighting specific areas of the surface.


See my tutorial on it: https://www.youtube.com/watch?v=qY85Hf90K68

Don't forget to leave a review on the asset store, and contact me for any question or query at anasainea@gmail.com