Shader"Unlit/RayMotionBlurShader_Simple"
{
    Properties
    {
        _UseRealTime("_UseRealTime", Int) = 0
        _RayMovesRight("_RayMovesRight", Int) = 1
        _NumTimeIntervals("_NumTimeIntervals", Int) = 1
        _OpacityPerSecond("_OpacityPerSecond", Float) = 1.0
        _TargetFrameRate("_TargetFrameRate", Int) = 72

        _RaySpeed("_RaySpeed", Float) = 1.0
        _BlurTransitionStartSpeed("_BlurTransitionStartSpeed", Float) = 0.5
        _BlurTransitionEndSpeed("_BlurTransitionEndSpeed", Float) = 9
        _OpacityFunctionFadeOutStart("_OpacityFunctionFadeOutStart", Float) = 0.6
        _OpacityFunctionFadeInEnd("_OpacityFunctionFadeInEnd", Float) = 0.9
        _BlurDistanceFromAnchorFadeStart("_BlurDistanceFromAnchorFadeStart", Float) = 0.1
        _BlurDistanceFromAnchorFadeEnd("_BlurDistanceFromAnchorFadeEnd", Float) = 0.6
        _SpeedBasedOpacityFadeMinSpeed("_SpeedBasedOpacityFadeMinSpeed", Float) = 100
        _SpeedBasedOpacityFadeMaxSpeed("_SpeedBasedOpacityFadeMaxSpeed", Float) = 400

        //patchIntersectionEpsilon("patchIntersectionEpsilon", Float) = 0.01
        


    }
    SubShader
    {
        Tags{ "RenderType" = "Transparent" "Queue" = "Transparent"}
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        //ZWrite Off
        Cull Off

        Pass
        {
            
            CGPROGRAM
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            StructuredBuffer<float4> RayVertices;
            
            
            int _UseRealTime;
            int _RayMovesRight;
            int _NumTimeIntervals;
            float _OpacityPerSecond;
            int _TargetFrameRate = 72;

            float _RaySpeed;
            float _BlurTransitionStartSpeed;
            float _BlurTransitionEndSpeed;
            float _OpacityFunctionFadeOutStart;
            float _OpacityFunctionFadeInEnd;
            float _BlurDistanceFromAnchorFadeStart;
            float _BlurDistanceFromAnchorFadeEnd;

            float _SpeedBasedOpacityFadeMaxSpeed;
            float _SpeedBasedOpacityFadeMinSpeed;

            
            static const int numEdges = 2;
            static const int numVerticesPerEdge = 2;
            static const int numVerticesPerTimeInterval = numEdges * numVerticesPerEdge;
            static const float FLT_MAX = 3.402823466e+38F;
            static const float frameDuration = 1.f / _TargetFrameRate;
            static const float patchIntersectionEpsilon = 0.01f;
            
            static const float blurSpeedBasedActivationFactor = clamp((_RaySpeed - _BlurTransitionStartSpeed) / (_BlurTransitionEndSpeed - _BlurTransitionStartSpeed), 0.f, 1.f);

            static const float3 currentRayStart = 0.5 * (RayVertices[_NumTimeIntervals * 4] + RayVertices[_NumTimeIntervals * 4 + 2]);

            static const float PI = 3.14159265f;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID 
            };

            struct v2g
            {
                float4 clipPos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO_EYE_INDEX
            };
            
            struct g2f
            {
                float4 clipPos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float triId : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Composes a floating point value with the magnitude of x and the sign of y. 
            float copysignf(float x, float y)
            {
                return abs(x) * sign(y);
            }
            
            // ray - bilinear patch intersection from Ushio
            // https://github.com/Ushio/BilinearPatch/blob/master/main.cpp
            // based on paper "Cool Patches: A Geometric Approach to Ray/Bilinear Patch Intersections"
            // adapted to return only the u-coordinate of the intersection between patch and ray, or a negative number
            // if u-coordinate is outside of [0,1]:
            //  -> intersectionFound is true if u is in range [-eps,1+eps]
            //  -> intersectionIsFallback is true if u has range [-eps,0] or [1,1+eps], so that we know this intersection can be overwritten by a better intersection
            float getRayPatchIntersectionU(in float3 rayOrigin, in float3 rayDirection, in int vertexIds[4], out bool intersectionFound, out bool intersectionIsFallback)
            {
                intersectionFound = false;
                intersectionIsFallback = false;

                //float tnear = FLT_MAX;  
                float u = -1.f;
                //float v = -1.f;
                
                float3 rd = rayDirection;
                float3 ro = rayOrigin;

                float3 p00 = RayVertices[vertexIds[0]].xyz;
                float3 p01 = RayVertices[vertexIds[1]].xyz;
                float3 p10 = RayVertices[vertexIds[2]].xyz;
                float3 p11 = RayVertices[vertexIds[3]].xyz;

                if (distance(p00, p10) < 0.00001f || distance(p01, p11) < 0.00001f) {
                    return -1.f;
                }

                //{
                //    // calculate input edges
                //    float3 e00_10 = p10 - p00;
                //    float3 e01_11 = p11 - p01;
                //    p01 -= (e00_10 * 0.5f);
                //    p11 -= (e01_11 * 0.5f);
                //}

                float a = dot( rd, cross( p10 - p00, p11 - p01 ) );
                float c = dot( p01 - p00, cross( rd, p00 - ro ) );
                float b = dot( rd, cross( p10 - p11, p11 - ro ) ) - a - c;

                float det = b * b - 4.0f * a * c;

                if( 0.0f <= det )
                {
                    // it might be better to check if a == 0
                    float k = ( -b - copysignf( sqrt( det ), b ) ) / 2.0f;
                    float u1 = k / a;
                    float u2 = c / k;

                    if( isfinite( u1 ) && 0.0f <= u1 && u1 <= 1.0f )
                    {
                        float thisU = u1;
                        float3 pa = ( 1.0f - thisU ) * p00 + thisU * p10;
                        float3 pb = ( 1.0f - thisU ) * p01 + thisU * p11;
                        float3 ab = pb - pa;

                        float3 n = cross( ab, rd );
                        float length2n = dot( n, n );
                        float3 n2 = cross( pa - ro, n );
                        //float v0 = dot( n2, rd ) / length2n;
                        float t = dot( n2, ab ) / length2n;

                        //if (0.0f <= t && t < tnear && 0.0f <= v0 && v0 <= 1.0f)
                        if( 0.0f <= t )
                        {
                            //tnear = t;
                            u = thisU;
                            intersectionFound = true;
                            //v = v0;
                        }
                    }

                    // why not 'else if'?
                    if( 0.0f <= u2 && u2 <= 1.0f )
                    {
                        float thisU = u2;
                        float3 pa = ( 1.0f - thisU ) * p00 + thisU * p10;
                        float3 pb = ( 1.0f - thisU ) * p01 + thisU * p11;
                        float3 ab = pb - pa;

                        float3 n = cross( ab, rd );
                        float length2n = dot( n, n );
                        float3 n2 = cross( pa - ro, n );
                        //float v0 = dot( n2, rd ) / length2n;
                        float t = dot( n2, ab ) / length2n;

                        //if( 0.0f <= t && t < tnear && 0.0f <= v0 && v0 <= 1.0f )
                        if (0.0f <= t)
                        {
                            //tnear = t;
                            u = thisU;
                            intersectionFound = true;
                            //v = v0;
                        }
                    }

                    // if no suitable u is found yet, check if there is a 'u' value just bigger than one
                    // which we will pretend is 1, to avoid inconsistencies

                    if (u < 0.0f) {
                        if (isfinite(u1) && -patchIntersectionEpsilon <= u1 && u1 <= (1.f + patchIntersectionEpsilon)) {
                            u = clamp(u1, 0.f, 1.f);
                            intersectionFound = true;
                            intersectionIsFallback = true;
                        }
                        // as above: why not 'else if'?
                        if (-patchIntersectionEpsilon <= u2 && u2 <= (1.f + patchIntersectionEpsilon)) {
                            u = clamp(u2, 0.f, 1.f);
                            intersectionFound = true;
                            intersectionIsFallback = true;
                        }
                    }
                }

                // if( tnear != FLT_MAX )
                // {
                //     float3 pa = ( 1.0f - u ) * p00 + u * p10;
                //     float3 pb = ( 1.0f - u ) * p01 + u * p11;
                //     float3 pc = ( 1.0f - v ) * p00 + v * p01;
                //     float3 pd = ( 1.0f - v ) * p10 + v * p11;
                //     float3 nlm = cross( pb - pa, pc - pd );
                //     nlm = normalize( nlm );
                // }

                //return float3(tnear, u, v);
                return u;
            }


            v2g vert (appdata v)
            {
                v2g o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.clipPos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz;
                return o;
            }

            [maxvertexcount(3)]
            void geom(triangle v2g input[3], uint pid : SV_PrimitiveID, inout TriangleStream<g2f> triStream)
            {
                for (int i = 0; i < 3; i++)
                {
                    g2f o;
                    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input[i]);
                    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                    o.clipPos = input[i].clipPos;
                    o.worldPos = input[i].worldPos;
                    o.triId = (float) pid;
                    triStream.Append(o);
                }
            }
            
            fixed4 frag (g2f i) : SV_Target
            {
                // this is required, for world space cam pos variable to refer to correct eye
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                
                bool rayMovesRight = (_RayMovesRight > 0);

                // find fragment's position on ray trail 
                // by intersecting viewing ray with quads that form the ray trail

                float posOnRayTrail = 0.f;
                float posOnRayTrailFallback = 0.f;

                const float rayTrailDistPerQuad = 1.f / float(_NumTimeIntervals);

                float3 cameraPosition = _WorldSpaceCameraPos;
                float3 rayDirection = normalize(i.worldPos - cameraPosition);
                
                int leadingEdgeIdx = int(rayMovesRight);
                int trailingEdgeIdx = int(!rayMovesRight);

                int patchVertexIds[4] = { 0,0,0,0 };

                bool intersectionFound = false;
                bool intersectionIsFallback = false;
                float u = 0.f;
                float u_t = 0.f;

                int quadIdxWithIntersection = -1;
                int fallbackQuad = -1;

                for (int quadIdx = 0; quadIdx < _NumTimeIntervals; quadIdx++)
                {
                    // get the vertices that form the two edges of this quad
                    int firstEdgeStartIdx = (quadIdx * 4) + (leadingEdgeIdx * 2);
                    int secondEdgeStartIdx = ((quadIdx + 1) * 4) + (leadingEdgeIdx * 2);

                    // The quads link leading edges of the ray. 
                    // If only leading edges are considered, the ray blur does not which doesn't gracefully handle the case when the ray is stationary (the leading edges are very close together, so the ray disappears).
                    // To include the whole of the ray trail, extend the 'oldest' quad to include the trailing edge of the oldest ray position considered
                    // The first quad processed is the oldest and therefore the trailing edge of that ray position should be considered as follows:
                    if (quadIdx == 0) {
                        firstEdgeStartIdx = (quadIdx * 4) + (trailingEdgeIdx * 2);
                    }

                    patchVertexIds[0] = firstEdgeStartIdx;
                    patchVertexIds[1] = firstEdgeStartIdx + 1;
                    patchVertexIds[2] = secondEdgeStartIdx;
                    patchVertexIds[3] = secondEdgeStartIdx + 1;

                    float u_t = getRayPatchIntersectionU(cameraPosition, rayDirection, patchVertexIds, intersectionFound, intersectionIsFallback);

                    if (intersectionFound) {
                        // convert u-coordinate for single quad to coordinate across all quads
                        u = (u_t * rayTrailDistPerQuad) + (quadIdx * rayTrailDistPerQuad);

                        if (intersectionIsFallback) {
                            posOnRayTrailFallback = u;
                        }
                        else {
                            posOnRayTrail = u;
                        }

                        quadIdxWithIntersection = quadIdx;
                    }
                }

                // use fallback if necessary
                if (posOnRayTrail == 0.f && posOnRayTrailFallback > 0.f) {
                    posOnRayTrail = posOnRayTrailFallback;
                }

                float blurOpacity = 0.f;
                // here opacity is set based on the 'posOnRayTrail'
                {
                    //// piecewise position-dependent opacity function:
                    //float blurOpacityPositionDependent = 0.f;
                    //if (posOnRayTrail > _OpacityFunctionFadeInEnd) {
                    //    blurOpacityPositionDependent = 1.f - ((posOnRayTrail - _OpacityFunctionFadeInEnd) / (1 - _OpacityFunctionFadeInEnd));
                    //}
                    //else if (posOnRayTrail > _OpacityFunctionFadeOutStart) {
                    //    blurOpacityPositionDependent = 1.f;
                    //}
                    //else if (posOnRayTrail >= 0.f) {
                    //    blurOpacityPositionDependent = posOnRayTrail / _OpacityFunctionFadeOutStart;
                    //}

                    // simple sine position-dependent opacity function:
                    float blurOpacityPositionDependent = sin(posOnRayTrail * PI);

                    // TODO try asymmetric forward weighted sine curve (as described in DOI:10.1109/WHC.2009.4810863)

                    blurOpacity = blurOpacityPositionDependent;
                }

                // speed dependent opacity function that modifies the overall opacity of the moving ray
                {
                    float blurOpacitySpeedDependentFactor = 1.f - clamp((_RaySpeed - _SpeedBasedOpacityFadeMinSpeed) / (_SpeedBasedOpacityFadeMaxSpeed - _SpeedBasedOpacityFadeMinSpeed), 0.f, 1.f);
                    blurOpacitySpeedDependentFactor = blurOpacitySpeedDependentFactor * 0.9f + 0.1f;
                    blurOpacity *= blurOpacitySpeedDependentFactor;
                }

                // here the effect of above opacity functions is modulated based on the overall speed of the ray
                // this helps to gracefully degrade to a normal, non-blurred ray when movement speed is low.
                // blend between having an position dependent opacity function (when in motion) and having no position-dependent opacity (when static)
                // blurSpeedBasedActivationFactor controls this, and is a constant for all fragments based on current speed of the ray
                {
                    // get nonblur opacity (what ray looks like if no blur is used) using triangle ID
                    float nonBlurOpacity = (i.triId <= 1.0) ? 1.0 : 0.0;
                    blurOpacity = lerp(nonBlurOpacity, blurOpacity, blurSpeedBasedActivationFactor);
                }

                // use distance from ray start to avoid blurring start of ray
                //float distanceFromRayStart = distance(currentRayStart, i.worldPos);
                //float distanceFromRayStartBlendFactor = clamp((distanceFromRayStart - _BlurDistanceFromAnchorFadeStart) / (_BlurDistanceFromAnchorFadeEnd - _BlurDistanceFromAnchorFadeStart), 0.f, 1.f);
                //float finalOpacity = lerp(nonBlurOpacity, blendedBlurOpacity, distanceFromRayStartBlendFactor);

                return fixed4(1.f, 1.f, 1.f, blurOpacity);

            }
            ENDCG
        }
    }
}
