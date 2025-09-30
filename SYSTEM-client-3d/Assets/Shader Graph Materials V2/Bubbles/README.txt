The Bubbles Shader creates a bubble effect with adjustable transparency, reflection blending, and dynamic splash properties. 
It includes options for reflection blur and cubemap integration.

This shader uses a gradient to specify the range and order of colors of the bubble specularity which requires a reflection probe near the object.

HDRP must use a cubemap for reflections because its shader graph doesn't support the reflection probe node.
You can create a cubemap by backing a reflection probe.

Properties:
- **Add Alpha**: Controls the overall transparency of the bubbles (0 to 1).
- **Alpha Fresnel Power**: Adjusts the fresnel effect on transparency.
- **Color/Reflection Blend**: Balances between the bubble's color and reflections.
- **Splashes Reducer**: Reduces the intensity of splash effects.
- **Splashes Speed**: Adjusts the speed of splash movement (0 to 0.5).
- **Reflection Blur**: Blurs the reflections for a softer effect (0 to 5).
- **Use Cubemap**: Toggles the use of a cubemap for environment reflections.
- **Cubemap**: Defines the environment map for reflections.


See my tutorial on it: https://www.youtube.com/watch?v=F6t8LR2mX1I
 and if you like to see it as a cloth in VR checkout this one: https://www.youtube.com/watch?v=c_noNpc3w1Y

Don't forget to leave a review on the asset store, and contact me for any question or query at anasainea@gmail.com