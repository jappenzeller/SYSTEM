Overview:
The Moving Tiles Shader creates dynamic tiled surfaces with adjustable properties for height, texture mapping, 
and material effects. 
It supports textures like Albedo, Normal maps, and Ambient Occlusion, with settings for smoothness and metallicity.

This shader has a point parameter which you can change using "Shader.SetGlobalVector("_Player_Position", Vector3);" from a C# script,
 the MovePlayerAndSetShaderPosition script in the same folder is attached to the capsule
 and it will move the player back and forth and set the shader parameter to make the effect work.

Properties:
- **Height**: Controls the height of the tiles.
- **Distance**: Adjusts the distance to the point where the effect will start.
- **Range**: Defines the range of effect over distance.
- **Albedo Texture**: Base color map for the surface.
- **Normal Texture**: Adds surface detail with a normal map.
- **Ambient Occlusion (AO) Texture**: Enhances shadows in crevices.
- **Tiling**: Adjusts the repetition of textures across the surface.
- **Offset**: Fine-tunes the placement of textures.
- **Normal Strength**: Sets the intensity of the normal map.
- **Noise Scale**: Modifies the scale of noise to randomize the effect.
- **Smoothness**: Defines the glossiness of the surface (0 to 1).
- **Metallic**: Controls the metallicity of the material (0 to 1).

Tips:
- Assign high-quality textures for better results.
- Adjust smoothness and metallicity for various material finishes.
- Use the range property to adjust the effect spread.


See my tutorial on it: https://www.youtube.com/watch?v=SSt2ypkAXeM

Don't forget to leave a review on the asset store, and contact me for any question or query at anasainea@gmail.com