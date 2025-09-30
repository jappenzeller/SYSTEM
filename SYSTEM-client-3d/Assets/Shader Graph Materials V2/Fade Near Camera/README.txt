The Fade Near Camera Shader creates a dynamic fading effect near the camera, allowing objects to gradually become transparent. 
It supports customizable textures, material properties, and precise fading controls.

This shader calculates the distance between the position of the vertex and the camera, it is possible to use a parametrized point
 and change it through C#.

Properties:
- **Albedo Texture**: The base color map of the material.
- **Normal Texture**: Adds detailed surface features using a normal map.
- **Normal Strength**: Adjusts the intensity of the normal map effect (0 to 1).
- **Smoothness**: Controls the glossiness of the surface (0 to 1).
- **Metallic**: Sets the metallicity of the material (0 to 1).
- **Start Fading At**: Specifies the distance from the camera where fading begins.
- **Fade Range**: Defines the extent of the fade effect from the start point.
- **Alpha Clip**: Sets the transparency threshold (0 to 1).

Note that I used a custom shader that gets a Noise 3D function from the "3D Noise.hlsl" file so that I can map noise on
 all sides of the mesh without unwrapping the mesh but this is more demanding than a normal texture.
Note also that you can set the shader to be opaque with alpha clip enabled which would hard cut what is transparent, 
 or have the shader as transparent and get smooth transparency.


See my tutorial on it: https://www.youtube.com/watch?v=BH_JWC1fpkI
 at that time I decided to use triplanner mapping instead of a 3D Noise.

Don't forget to leave a review on the asset store, and contact me for any question or query at anasainea@gmail.com