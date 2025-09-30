The Glass Shader creates a realistic glass effect with customizable refraction, reflection, and tinting properties. 
It supports detailed surface mapping and precise control over the glass appearance.

Properties:
- **Albedo Texture**: The base color map for the glass surface.
- **Color Tint**: Adds a tint to the glass with adjustable color.
- **Refraction Brightness**: Controls the intensity of light refraction.
- **Normal Texture**: Adds surface detail with a normal map for refraction.
- **Refraction Normal Strength**: Adjusts the influence of the normal map on refraction (0 to 1).
- **Tiling**: Controls the repetition of textures across the surface.
- **Index of Refraction (IOR)**: Adjusts the bending of light through the glass (-1 to 1).
- **Reflection Normal Strength**: Defines the normal map's impact on reflections (0 to 1).
- **Smoothness**: Sets the glossiness of the glass surface (0 to 1).
- **Metallic**: Adjusts the metallic appearance of the material (0 to 1).

Tips:
- Experiment with **Refraction Normal Strength** and **Reflection Normal Strength** to balance surface details.
- Use **Tiling** to fit textures seamlessly on the glass surface.
- Combine with vibrant tints for creative, stylized effects.

This shader uses the scene color node so make sure that the opaque texture is enabled in quality settings,
 this makes it great for things like water in general but it will show objects rendered before it,
 to make an object not show in the refraction at all, you can set its material to transparent (which changes its rendering order),
 or have a second camera that only renders the objects to be refracted to a render texture and use that in the shader.

There are two types of this shader, one is tangent which is better for sharp objects since it doesn't give good results on the UV seems of the mesh
 the other is screen space which is better for round and smooth objects but might produce weird result at some values of IOR.
 
Note: URP/HD are set to unlit (you can test with making it lit in the shader graph, graph options) so it will not receive shadows and reflections.
Note: HDRP gives a very bright scene color value so you will have to set the refraction brightness to a very low value.
Note: Built-in shader graph doesn't support the color scene so a normal shader is included instead of a graph.

See my tutorial on it: https://www.youtube.com/watch?v=EELbMlnOzQE

Don't forget to leave a review on the asset store, and contact me for any question or query at anasainea@gmail.com