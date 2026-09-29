using PuffyBird.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace PuffyBird.Rendering
{
    /// <summary>
    /// Éclairage de la scène : soleil ou lune avec ombres douces temps réel, lucioles (lumières
    /// ponctuelles) la nuit, lumière ambiante en harmoniques sphériques, brouillard
    /// atmosphérique et sonde de réflexion rendue une fois par thème (reflets « façon
    /// raytracing » sans raytracing matériel).
    /// </summary>
    public sealed class LightingRig
    {
        const int FireflyCount = 3;

        readonly Light _sun;
        readonly Light[] _fireflies = new Light[FireflyCount];
        readonly Transform[] _fireflyBodies = new Transform[FireflyCount];
        readonly Vector3[] _fireflyCenters = new Vector3[FireflyCount];
        readonly ReflectionProbe _probe;
        bool _probeDirty;
        int _probeDelayFrames;

        public LightingRig(Transform parent, MaterialLibrary materials)
        {
            var sunGo = new GameObject("Soleil");
            sunGo.transform.SetParent(parent, false);
            _sun = sunGo.AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.shadows = LightShadows.Soft;
            _sun.shadowStrength = 0.8f;
            _sun.shadowBias = 0.03f;
            _sun.shadowNormalBias = 0.3f;

            var fireflyMesh = new MeshBuilder().AddSphere(Vector3.zero, 0.03f, Color.white, 10, 8).Build("Luciole");
            var fireflyMat = materials.Lit("Luciole", Color.white, 0.2f, 0f, 0f);
            fireflyMat.SetColor(MaterialLibrary.EmissionColor, Palette.Hex("#FFE58A") * 3f);
            for (int i = 0; i < FireflyCount; i++)
            {
                var go = new GameObject("Luciole " + (i + 1));
                go.transform.SetParent(parent, false);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 1.6f;
                light.intensity = 1.6f;
                light.color = Palette.Hex("#FFD66B");
                light.shadows = LightShadows.None;
                _fireflies[i] = light;

                var body = new GameObject("Corps");
                body.transform.SetParent(go.transform, false);
                body.AddComponent<MeshFilter>().sharedMesh = fireflyMesh;
                var r = body.AddComponent<MeshRenderer>();
                r.sharedMaterial = fireflyMat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                _fireflyBodies[i] = body.transform;
                _fireflyCenters[i] = new Vector3(-1.1f + i * 1.2f, 1.2f + 0.6f * i, 1.2f + 0.7f * i);
            }

            var probeGo = new GameObject("Sonde de réflexion");
            probeGo.transform.SetParent(parent, false);
            probeGo.transform.position = new Vector3(0f, 2f, 0f);
            _probe = probeGo.AddComponent<ReflectionProbe>();
            _probe.mode = ReflectionProbeMode.Realtime;
            _probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            _probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            _probe.resolution = 64;
            _probe.size = new Vector3(120f, 60f, 120f);
            _probe.hdr = true;
            _probe.importance = 1;
        }

        public void ApplyTheme(Theme theme, Palette.ThemeColors colors)
        {
            _sun.color = colors.SunColor;
            _sun.intensity = colors.SunIntensity;
            _sun.transform.rotation = Quaternion.Euler(colors.SunEuler);
            RenderSettings.sun = _sun;

            bool night = theme == Theme.Night;
            for (int i = 0; i < FireflyCount; i++) _fireflies[i].gameObject.SetActive(night);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = colors.AmbientSky;
            RenderSettings.ambientEquatorColor = colors.AmbientEquator;
            RenderSettings.ambientGroundColor = colors.AmbientGround;
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(colors.AmbientEquator * 0.55f);
            sh.AddDirectionalLight(Vector3.up, colors.AmbientSky, 0.6f);
            sh.AddDirectionalLight(Vector3.down, colors.AmbientGround, 0.35f);
            RenderSettings.ambientProbe = sh;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = colors.Fog;
            RenderSettings.fogStartDistance = night ? 12f : 18f;
            RenderSettings.fogEndDistance = night ? 70f : 95f;

            // La sonde est rendue quelques images plus tard, une fois le décor recoloré.
            _probeDirty = true;
            _probeDelayFrames = 2;
        }

        public void Update(float time)
        {
            for (int i = 0; i < FireflyCount; i++)
            {
                if (!_fireflies[i].gameObject.activeSelf) continue;
                var c = _fireflyCenters[i];
                float t = time * (0.35f + 0.1f * i) + i * 2.1f;
                var p = c + new Vector3(Mathf.Sin(t) * 0.9f, Mathf.Sin(t * 1.7f) * 0.35f, Mathf.Cos(t * 0.8f) * 0.4f);
                _fireflies[i].transform.position = p;
                _fireflies[i].intensity = 1.3f + Mathf.Sin(time * 6f + i * 1.3f) * 0.35f;
            }

            if (_probeDirty && --_probeDelayFrames <= 0)
            {
                _probeDirty = false;
                _probe.RenderProbe();
            }
        }
    }
}
