
# Ray Motion Blur Effect

This repository contains an example of the motion blur effect that can be applied to pointing rays in virtual reality. 

## Scenes

The effect can be seen without an HMD in the `RayEffectDemo_NoHMD` scene. The ray is automatically moved across the screen with the effect applied.

To try the effect while wearing an HMD, start the `RayEffectDemo_HMD` scene. The ray will appear in your right hand. Activate either the 'InvisibleRayRoot' or the 'InvisibleRayRootNoBlur' GameObject to show/hide the effect.


## Implementation

The ray motion blur effect is implemented in the following files:

- The `Scripts/RayMotionBlur/RayMotionBlur.cs` script is attached to a GameObject that is attached to one of the OVR hand anchors. It creates proxy geometry for the blur based on the motion of the hand. The script can be provided with a GameObject (with mesh component) to render when the blur mode is deactivated. A game object with a quad mesh (`Scripts/Shapes/QuadMesh.cs`) should also be provided to determine the proportions of the blurred ray. 

- `Resources/Shaders/RayMotionBlurShader_Simple.shader` is the shader that renders the proxy geometry, adjusting the opacity according to the position of each fragment on the ray.


### Opacity Functions

The opacity at each point on the ray trail is controlled by a number of opacity functions.

1. Position-dependent opacity function. This varies the opacity of the ray along the length of the ray trail, smoothly rising from 0 at the leading edge, to a max value, and back down at the trailing edge.

2. Speed-dependent opacity function. This function modulates the opacity resulting from function 1, reducing the opacity of all parts of the ray trail as the ray moves faster.

3. Blur activation function. The blurred ray should degrade to a non-blurred ray when the ray is moved slowly (or not at all). When the ray moves with a certain speed, this function blends between no blur (only the ray visible, not the trail) and blur (when the ray and trail are visible with speed- and position-depdendent opacity).





