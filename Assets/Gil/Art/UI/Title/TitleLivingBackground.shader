// 타이틀 키아트를 살아 움직이게 보여 주는 uGUI 셰이더.
// 정지 이미지 한 장 + 모션 마스크(R 천, G 머리카락, B 원경, A 강물)로
// 천·머리카락 살랑임, 원경 불빛 반짝임·네온 흐름·빛 띠, 하늘 링 펄스, 느린 팬/줌을 만든다.
// 모든 값은 머티리얼 인스펙터에서 조정한다. 시간 함수가 연속이라 루프 이음매가 없다.
Shader "Gil/UI/TitleLivingBackground"
{
    Properties
    {
        [PerRendererData] _MainTex ("키아트", 2D) = "white" {}
        _Color ("색조", Color) = (1,1,1,1)
        [NoScaleOffset] _MotionMask ("모션 마스크 (R 천, G 머리카락, B 원경, A 강물)", 2D) = "black" {}

        [Header(Camera  Slow Pan Zoom)]
        _ZoomAmount ("줌 깊이 (0 = 끔)", Range(0, 0.15)) = 0.035
        _ZoomPeriod ("팬/줌 한 주기 (초)", Range(4, 120)) = 40
        _PanDirection ("팬 방향 (x, y: -1~1, 줌 여백 안에서)", Vector) = (0.6, 0.25, 0, 0)

        [Header(Wind Gust)]
        _WindDirection ("바람 방향 (x, y, 키아트 좌상단 기준)", Vector) = (-1, -0.25, 0, 0)
        _GustSpeed ("돌풍 빠르기", Range(0, 3)) = 0.55
        _GustTravel ("돌풍이 화면을 건너는 거리 (픽셀, 클수록 동시에 흔들림)", Range(200, 6000)) = 1800

        [Header(Cloth)]
        _ClothAmplitude ("천 날림 크기 (끝자락 픽셀)", Range(0, 12)) = 5
        _ClothFlutter ("천 끝자락 떨림 (픽셀)", Range(0, 4)) = 0.6

        [Header(Hair)]
        _HairAmplitude ("머리카락 날림 크기 (끝 픽셀)", Range(0, 12)) = 4
        _HairFlutter ("머리카락 끝 떨림 (픽셀)", Range(0, 4)) = 0.8
        _FlutterSpeed ("끝자락 떨림 속도", Range(0, 20)) = 7

        [Header(Far Lights  Twinkle)]
        _LightThreshold ("불빛으로 볼 밝기 기준", Range(0, 1)) = 0.55
        _TwinkleIntensity ("반짝임 세기", Range(0, 2)) = 0.6
        _TwinkleSpeed ("반짝임 속도", Range(0, 10)) = 2.2
        _TwinkleCellSize ("반짝임 단위 크기 (픽셀)", Range(2, 40)) = 7

        [Header(Far Lights  Neon Flow)]
        [HDR] _NeonColor ("네온 흐름 색", Color) = (0.45, 0.95, 1.2, 1)
        _NeonIntensity ("네온 흐름 세기", Range(0, 3)) = 0.9
        _NeonSpeed ("네온 흐름 속도 (초당 화면 비율)", Range(0, 2)) = 0.12
        _NeonColumnWidth ("네온 줄 너비 (픽셀)", Range(1, 30)) = 6

        [Header(Far Lights  Light Sweep)]
        [HDR] _SweepColor ("빛 띠 색", Color) = (1.0, 0.85, 0.65, 1)
        _SweepIntensity ("빛 띠 세기 (불빛만 밝힘)", Range(0, 3)) = 0.9
        _SweepPeriod ("빛 띠 지나가는 주기 (초)", Range(2, 60)) = 14
        _SweepWidth ("빛 띠 너비 (화면 비율)", Range(0.01, 0.5)) = 0.08
        _SweepAngle ("빛 띠 각도 (도)", Range(-90, 90)) = 25

        [Header(Sky Ring Hologram)]
        _RingCenter ("링 중심 (키아트 픽셀, 좌상단 원점)", Vector) = (1438, 92, 0, 0)
        _RingRadius ("링 반지름 (긴축, 짧은축 픽셀)", Vector) = (134, 14, 0, 0)
        _RingTilt ("링 기울기 (도)", Range(-90, 90)) = 17.4
        _RingThickness ("링 찾는 범위 (픽셀, 그려진 링을 덮을 만큼)", Range(0.5, 20)) = 6
        [HDR] _RingColor ("링 색", Color) = (1.0, 0.9, 0.55, 1)
        _RingIntensity ("링 세기", Range(0, 3)) = 1.1
        _RingRunSpeed ("링 위를 도는 빛 속도 (바퀴/초)", Range(-2, 2)) = 0.18
        _RingPulseSpeed ("링 맥동 속도", Range(0, 10)) = 1.3

        [Header(River)]
        _WaterAmplitude ("강물 일렁임 크기 (픽셀)", Range(0, 6)) = 1.5
        _WaterSpeed ("강물 일렁임 속도", Range(0, 5)) = 1.2
        _WaterShimmer ("강물 반짝임 세기", Range(0, 2)) = 0.5

        // uGUI 마스크/스텐실 호환용 (UI/Default와 같음)
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            sampler2D _MotionMask;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            float _ZoomAmount, _ZoomPeriod;
            float4 _PanDirection;
            float4 _WindDirection;
            float _GustSpeed, _GustTravel, _FlutterSpeed;
            float _ClothAmplitude, _ClothFlutter;
            float _HairAmplitude, _HairFlutter;
            float _LightThreshold, _TwinkleIntensity, _TwinkleSpeed, _TwinkleCellSize;
            float4 _NeonColor; float _NeonIntensity, _NeonSpeed, _NeonColumnWidth;
            float4 _SweepColor; float _SweepIntensity, _SweepPeriod, _SweepWidth, _SweepAngle;
            float4 _RingCenter, _RingRadius; float _RingTilt, _RingThickness;
            float4 _RingColor; float _RingIntensity, _RingRunSpeed, _RingPulseSpeed;
            float _WaterAmplitude, _WaterSpeed, _WaterShimmer;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // 평균이 0 근처인 돌풍 곡선. 짧게 세지고 길게 쉰다.
            float Gust(float x)
            {
                const float TAU = 6.2831853;
                float a = 0.5 + 0.5 * sin(x * TAU * 0.37);
                float b = 0.5 + 0.5 * sin(x * TAU * 0.61 + 1.7);
                return a * a * a + 0.6 * b * b * b + 0.15 * sin(x * TAU * 1.3 + 0.4) - 0.5;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                const float TAU = 6.2831853;
                float t = _Time.y;
                float2 texSize = _MainTex_TexelSize.zw;

                // 느린 팬/줌: 줌 여백 안에서만 움직여 가장자리가 비지 않게 한다.
                float phase = TAU * t / _ZoomPeriod;
                float zoom = _ZoomAmount * (0.5 - 0.5 * cos(phase));
                float scale = 1.0 - zoom;
                float2 pan = clamp(_PanDirection.xy, -1, 1) * (zoom * 0.5) * sin(phase * 0.5);
                float2 uv = 0.5 + (IN.texcoord - 0.5) * scale + pan;

                // 원본 픽셀 좌표(좌상단 원점). 크기 값을 픽셀로 받기 위해 쓴다.
                float2 px = float2(uv.x, 1.0 - uv.y) * texSize;

                fixed4 m = tex2D(_MotionMask, uv);
                float2 wind = normalize(_WindDirection.xy + 1e-4);
                float2 windPerp = float2(-wind.y, wind.x);

                // 바람: 물결이 아니라 한쪽으로 확 밀었다가 천천히 잦아드는 돌풍.
                // 마스크 값이 뿌리(0)에서 끝자락(1)으로 커지므로 뿌리는 고정되고 끝만 날린다.
                // 돌풍은 바람이 불어오는 쪽부터 조금씩 늦게 닿는다.
                float gustArg = t * _GustSpeed - dot(px, wind) / _GustTravel;
                float flutterArg = t * _FlutterSpeed + dot(px, windPerp) * 0.035;
                float clothGust = Gust(gustArg);
                float hairGust = Gust(gustArg + 0.12);

                float2 offset = wind * clothGust * _ClothAmplitude * m.r
                              + windPerp * sin(flutterArg) * _ClothFlutter * m.r * m.r;
                offset += wind * hairGust * _HairAmplitude * m.g
                        + windPerp * sin(flutterArg * 1.3 + 1.1) * _HairFlutter * m.g * m.g;

                // 강물: 가로로 일렁인다.
                float water = sin(px.y * 0.35 + t * _WaterSpeed * 2.0 + sin(px.x * 0.02 + t * _WaterSpeed));
                offset.x += water * _WaterAmplitude * m.a;

                // 오프셋은 픽셀 단위 → UV로. 좌상단 기준 y는 뒤집는다.
                float2 sampleUv = uv - float2(offset.x, -offset.y) / texSize;
                fixed4 color = (tex2D(_MainTex, sampleUv) + _TextureSampleAdd) * IN.color;

                // 원경 불빛: 밝은 픽셀만 고른다.
                float far = m.b;
                float lum = dot(color.rgb, float3(0.299, 0.587, 0.114));
                float light = smoothstep(_LightThreshold, min(_LightThreshold + 0.25, 1.0), lum) * far;

                // 반짝임: 칸마다 위상이 다른 깜빡임.
                float2 cell = floor(px / _TwinkleCellSize);
                float h = Hash21(cell);
                float tw = sin(t * _TwinkleSpeed * (0.6 + h) + h * TAU) * 0.5 + 0.5;
                tw = tw * tw * tw;
                float3 add = color.rgb * light * (tw - 0.35) * _TwinkleIntensity;

                // 네온 흐름: 청록 불빛(빌딩 네온)을 따라 위로 흐르는 빛 줄기.
                float cyan = saturate((color.b - color.r) * 2.5);
                float column = floor(px.x / _NeonColumnWidth);
                float hc = Hash21(float2(column, 7.0));
                float flow = frac(uv.y * (0.8 + hc) - t * _NeonSpeed * (0.6 + hc) + hc);
                flow = pow(flow, 18.0) * step(0.45, hc);
                add += _NeonColor.rgb * flow * cyan * light * _NeonIntensity;

                // 빛 띠: 원경을 천천히 지나가며 이미 그려진 불빛만 밝힌다.
                // 새 빛을 덧그리지 않으므로 앞 건물에 가려진 곳은 그대로 가려진다.
                float ang = radians(_SweepAngle);
                float2 sweepDir = float2(cos(ang), sin(ang));
                float sd = dot(uv - 0.5, sweepDir);
                float sweepPos = frac(t / _SweepPeriod) * 2.0 - 1.0;
                float sweep = exp(-pow((sd - sweepPos) / _SweepWidth, 2.0));
                add += lerp(color.rgb, _SweepColor.rgb * lum, 0.5) * sweep * light * _SweepIntensity;

                // 강물 반짝임.
                add += color.rgb * light * m.a * (sin(px.x * 0.15 + t * _WaterSpeed * 3.0) * 0.5 + 0.5) * _WaterShimmer;

                // 하늘 링 홀로그램: 타원 근처에서 그림에 그려진 금빛 링 픽셀만 골라
                // 링을 따라 도는 빛과 맥동을 준다. 탑 뒤로 돌아가는 부분은 그림에 없으니 빛도 없다.
                float tilt = radians(_RingTilt);
                float2 d = px - _RingCenter.xy;
                float2 r = float2(d.x * cos(tilt) + d.y * sin(tilt), -d.x * sin(tilt) + d.y * cos(tilt));
                float2 radius = max(_RingRadius.xy, 1.0);
                float2 e = r / radius;
                float el = max(length(e), 1e-3);
                // 타원까지의 거리를 픽셀로 근사해 링 전체 두께가 고르게 한다.
                float ringDist = (el - 1.0) / max(length(e / el / radius), 1e-4);
                float ringBand = exp(-pow(ringDist / _RingThickness, 2.0));
                float ringAngle = atan2(e.y, e.x) / TAU;
                float runner = pow(frac(ringAngle - t * _RingRunSpeed), 6.0);
                float pulse = 0.5 + 0.5 * sin(t * _RingPulseSpeed);
                float warm = saturate((color.r - color.b) * 4.0);
                float painted = ringBand * warm * smoothstep(_LightThreshold * 0.6, _LightThreshold + 0.2, lum) * far;
                add += lerp(color.rgb, _RingColor.rgb, 0.5) * painted * (runner * 1.6 + pulse * 0.4) * _RingIntensity;

                color.rgb += add;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }
}
