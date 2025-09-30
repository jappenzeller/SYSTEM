This shader uses an exposed parametrized point parameter which you can change using "material.SetVector("_Param_Name", Vector3);"
 or you can uncheck the exposed checkbox to make it the same value across all materials and use "Shader.SetGlobalVector("_Param_Name", Vector3);"
 from a C# script.

There are two shaders in the folder one would suck all vertices into the point, and the other would shrink the object
 while it is being sucked to the point.


Properties:
- **Albedo Texture**: The base color texture for the surface.
- **Base Color**: Sets the overall color of the material.
- **Normal Texture**: Adds detailed surface features using a normal map.
- **Normal Strength**: Adjusts the intensity of the normal map effect (0 to 1).
- **Ambient Occlusion (AO) Texture**: Enhances shadow details in crevices.
- **Tiling**: Controls the repetition of textures across the surface.
- **Smoothness**: Defines the glossiness of the surface (0 to 1).
- **Metallic**: Determines the metallic appearance of the material (0 to 1).
- **Emission Color**: Adds glowing effects with customizable colors.
- **Hole Position**: Sets the position of the black hole effect.
- **Distance**: Controls the extent of the black hole's influence.

Tips:
- Experiment with Tiling and Smoothness to achieve diverse surface styles.
- Use vibrant emission colors for glowing, sci-fi effects.
- Adjust the Hole Position and Distance dynamically for interactive effects.


See my tutorial on it: https://www.youtube.com/watch?v=eujfez6W53E

Don't forget to leave a review on the asset store, and contact me for any question or query at anasainea@gmail.com