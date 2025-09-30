The Intersection Shader creates a dynamic visual effect that highlights intersecting surfaces using customizable 
patterns, fresnel effects, and colors. 
Ideal for futuristic and stylized visual designs.

Properties:
- **Multy**: Controls the scaling of the intersection effect.
- **Offset**: Adjusts the depth of the effect.
- **Fresnel Power**: Sets the strength of the fresnel effect for edge highlights.
- **Pattern Texture**: A texture applied to the intersecting areas for added detail.
- **Color**: Defines the color applied to the intersection effect.

Tips:
- Experiment with different pattern textures for unique effects.
- Combine fresnel and color for glowing, futuristic visuals.
- Adjust the **Offset** dynamically for animated effects.


This shader calculates the distance of other meshes from it using the depth texture, so make sure that it is enabled in the quality settings,
 this only works for intersection with non transparent objects (because of the rendering order).


See my tutorial on it: https://www.youtube.com/watch?v=ayd8L6ZyCvw

Don't forget to leave a review on the asset store, and contact me for any question or query at anasainea@gmail.com