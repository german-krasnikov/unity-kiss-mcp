using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    public static class ParticleHelper
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Create(string parentPath, string name, string preset)
        {
            if (string.IsNullOrEmpty(name)) name = "ParticleSystem";
            var go = new GameObject(name);
            go.AddComponent<ParticleSystem>();
            Undo.RegisterCreatedObjectUndo(go, "Create Particle System");
            if (!string.IsNullOrEmpty(parentPath))
            {
                var parent = ComponentSerializer.FindObject(parentPath);
                if (parent == null) throw new ArgumentException(ErrorHelper.ObjectNotFound(parentPath));
                go.transform.SetParent(parent.transform);
            }
            var mat = CreateParticleMaterial();
            if (mat != null) go.GetComponent<ParticleSystemRenderer>().material = mat;
            if (!string.IsNullOrEmpty(preset)) ApplyPresetInternal(go.GetComponent<ParticleSystem>(), preset);
            return $"created: {ComponentSerializer.GetPath(go)}";
        }

        public static string SetProperty(string path, string module, string prop, string value)
        {
            var ps = GetPS(path);
            Undo.RecordObject(ps, "Set Particle Property");
            SetModuleProperty(ps, module, prop, value);
            return $"set: {module}.{prop} = {value}";
        }

        public static string ApplyPreset(string path, string preset)
        {
            var ps = GetPS(path);
            Undo.RecordObject(ps, "Apply Particle Preset");
            ApplyPresetInternal(ps, preset);
            return $"preset: {preset} applied to {path}";
        }

        internal static ParticleSystem GetPS(string path)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null) throw new ArgumentException(ErrorHelper.ObjectNotFound(path));
            var ps = go.GetComponent<ParticleSystem>();
            if (ps == null) throw new ArgumentException(ErrorHelper.ComponentNotFound("ParticleSystem", go));
            return ps;
        }

        static Material CreateParticleMaterial(bool additive = false)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                      ?? Shader.Find("Particles/Standard Unlit");
            if (shader == null) return null;
            var mat = new Material(shader);
            if (shader.name.Contains("Universal"))
            {
                mat.SetFloat("_Surface", 1);
                if (additive) mat.SetFloat("_Blend", 1);
            }
            else mat.SetFloat("_Mode", additive ? 4f : 2f);
            var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            if (tex != null) mat.mainTexture = tex;
            return mat;
        }

        // --- SetProperty ---

        static void SetModuleProperty(ParticleSystem ps, string module, string prop, string value)
        {
            switch (module.ToLowerInvariant())
            {
                case "main": SetMain(ps, prop, value); break;
                case "emission": SetEmission(ps, prop, value); break;
                case "shape": SetShape(ps, prop, value); break;
                case "noise": SetNoise(ps, prop, value); break;
                case "renderer": SetRenderer(ps, prop, value); break;
                case "coloroverlifetime": var c = ps.colorOverLifetime; c.enabled = PB(value); break;
                case "sizeoverlifetime": var s = ps.sizeOverLifetime; s.enabled = PB(value); break;
                case "velocityoverlifetime": var v2 = ps.velocityOverLifetime; v2.enabled = PB(value); break;
                case "rotationoverlifetime": var r = ps.rotationOverLifetime; r.enabled = PB(value); break;
                case "trails": var t = ps.trails; t.enabled = PB(value); break;
                case "collision": var co = ps.collision; co.enabled = PB(value); break;
                default: throw new ArgumentException($"Unknown module '{module}'. Valid: main, emission, shape, noise, renderer, colorOverLifetime, sizeOverLifetime, velocityOverLifetime, rotationOverLifetime, trails, collision.");
            }
        }

        static void SetMain(ParticleSystem ps, string prop, string value)
        {
            var m = ps.main;
            switch (prop.ToLowerInvariant())
            {
                case "duration": m.duration = PF(value); break;
                case "startdelay": m.startDelay = PMM(value); break;
                case "startspeed": m.startSpeed = PMM(value); break;
                case "startsize": m.startSize = PMM(value); break;
                case "startlifetime": m.startLifetime = PMM(value); break;
                case "gravitymodifier": m.gravityModifier = PMM(value); break;
                case "loop": m.loop = PB(value); break;
                case "playonawake": m.playOnAwake = PB(value); break;
                case "maxparticles": m.maxParticles = int.Parse(value, Inv); break;
                case "startcolor": m.startColor = ValueParser.ParseColor(value); break;
                case "simulationspace": m.simulationSpace = PE<ParticleSystemSimulationSpace>(value); break;
                case "scalingmode": m.scalingMode = PE<ParticleSystemScalingMode>(value); break;
                case "startsize3d": m.startSize3D = PB(value); break;
                case "startsizex": m.startSizeX = PMM(value); break;
                case "startsizey": m.startSizeY = PMM(value); break;
                case "startsizez": m.startSizeZ = PMM(value); break;
                default: throw new ArgumentException($"Unknown main property '{prop}'.");
            }
        }

        static void SetEmission(ParticleSystem ps, string prop, string value)
        {
            var m = ps.emission;
            switch (prop.ToLowerInvariant())
            {
                case "enabled": m.enabled = PB(value); break;
                case "rateovertime": m.rateOverTime = PMM(value); break;
                case "rateoverdistance": m.rateOverDistance = PMM(value); break;
                default: throw new ArgumentException($"Unknown emission property '{prop}'.");
            }
        }

        static void SetShape(ParticleSystem ps, string prop, string value)
        {
            var m = ps.shape;
            switch (prop.ToLowerInvariant())
            {
                case "enabled": m.enabled = PB(value); break;
                case "shapetype": m.shapeType = PE<ParticleSystemShapeType>(value); break;
                case "angle": m.angle = PF(value); break;
                case "radius": m.radius = PF(value); break;
                case "radiusthickness": m.radiusThickness = PF(value); break;
                case "scale": m.scale = ValueParser.ParseVector3(value); break;
                case "position": m.position = ValueParser.ParseVector3(value); break;
                default: throw new ArgumentException($"Unknown shape property '{prop}'.");
            }
        }

        static void SetNoise(ParticleSystem ps, string prop, string value)
        {
            var m = ps.noise;
            switch (prop.ToLowerInvariant())
            {
                case "enabled": m.enabled = PB(value); break;
                case "strength": m.strength = PMM(value); break;
                case "frequency": m.frequency = PF(value); break;
                case "scrollspeed": m.scrollSpeed = PMM(value); break;
                case "damping": m.damping = PB(value); break;
                case "octavecount": m.octaveCount = int.Parse(value, Inv); break;
                default: throw new ArgumentException($"Unknown noise property '{prop}'.");
            }
        }

        static void SetRenderer(ParticleSystem ps, string prop, string value)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            if (r == null) throw new ArgumentException("No ParticleSystemRenderer found.");
            switch (prop.ToLowerInvariant())
            {
                case "rendermode": r.renderMode = PE<ParticleSystemRenderMode>(value); break;
                case "velocityscale": r.velocityScale = PF(value); break;
                case "lengthscale": r.lengthScale = PF(value); break;
                default: throw new ArgumentException($"Unknown renderer property '{prop}'.");
            }
        }

        // --- Presets ---

        static void ApplyPresetInternal(ParticleSystem ps, string preset)
        {
            switch (preset.ToLowerInvariant())
            {
                case "fire": PresetFire(ps); break;     case "smoke": PresetSmoke(ps); break;
                case "sparks": PresetSparks(ps); break;  case "rain": PresetRain(ps); break;
                case "snow": PresetSnow(ps); break;      case "explosion": PresetExplosion(ps); break;
                case "magic": PresetMagic(ps); break;    case "dust": PresetDust(ps); break;
                case "blood": PresetBlood(ps); break;    case "trail": PresetTrail(ps); break;
                default: throw new ArgumentException($"Unknown preset '{preset}'. Valid: fire, smoke, sparks, rain, snow, explosion, magic, dust, blood, trail.");
            }
        }

        static void PresetFire(ParticleSystem ps)
        {
            var m = ps.main;
            m.startLifetime = MM(0.5f, 1.5f); m.startSpeed = MM(1, 3); m.startSize = MM(0.3f, 0.8f);
            m.startColor = C(1f, 0.6f, 0f); m.gravityModifier = -0.2f; m.maxParticles = 200;
            var em = ps.emission; em.rateOverTime = 40;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 15; sh.radius = 0.3f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = Grad(C(1, 1, 0), C(1, 0.3f, 0, 0.8f), C(0, 0, 0, 0));
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = Curve(0, 0.5f, 1, 1.5f);
            var n = ps.noise; n.enabled = true; n.strength = 0.5f; n.frequency = 1;
        }

        static void PresetSmoke(ParticleSystem ps)
        {
            var m = ps.main;
            m.startLifetime = MM(3, 6); m.startSpeed = MM(0.5f, 1.5f); m.startSize = MM(0.5f, 1);
            m.startColor = C(0.5f, 0.5f, 0.5f, 0.6f); m.gravityModifier = -0.05f; m.maxParticles = 100;
            var em = ps.emission; em.rateOverTime = 15;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 20; sh.radius = 0.5f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = Grad(C(0.6f, 0.6f, 0.6f, 0.4f), C(0.4f, 0.4f, 0.4f, 0.2f), C(0.3f, 0.3f, 0.3f, 0));
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = Curve(0, 0.5f, 1, 3);
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = MM(-Mathf.PI, Mathf.PI);
            var n = ps.noise; n.enabled = true; n.strength = 1; n.frequency = 0.3f;
        }

        static void PresetSparks(ParticleSystem ps)
        {
            var m = ps.main; m.duration = 0.5f; m.loop = false;
            m.startLifetime = MM(0.3f, 0.8f); m.startSpeed = MM(3, 8); m.startSize = MM(0.02f, 0.08f);
            m.startColor = C(1, 0.7f, 0.2f); m.gravityModifier = 1; m.maxParticles = 500;
            var em = ps.emission; em.rateOverTime = 0;
            em.SetBursts(new[] { new ParticleSystem.Burst(0, 30, 60) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 45; sh.radius = 0.1f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = Grad(C(1, 0.9f, 0.5f), C(1, 0.5f, 0.1f, 0.5f), C(0.5f, 0.2f, 0, 0));
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.05f; r.lengthScale = 2;
        }

        static void PresetRain(ParticleSystem ps)
        {
            var m = ps.main; m.startLifetime = 1.5f; m.startSpeed = MM(10, 15);
            m.startSize3D = true; m.startSizeX = 0.02f; m.startSizeY = 0.5f; m.startSizeZ = 0.02f;
            m.maxParticles = 3000;
            var em = ps.emission; em.rateOverTime = 500;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(20, 0, 20); sh.position = new Vector3(0, 15, 0);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.1f; r.lengthScale = 3;
        }

        static void PresetSnow(ParticleSystem ps)
        {
            var m = ps.main;
            m.startLifetime = MM(5, 10); m.startSpeed = MM(0.5f, 1.5f); m.startSize = MM(0.05f, 0.15f);
            m.startColor = Color.white; m.gravityModifier = 0.1f; m.maxParticles = 2000;
            var em = ps.emission; em.rateOverTime = 100;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(20, 0, 20); sh.position = new Vector3(0, 10, 0);
            var vol = ps.velocityOverLifetime; vol.enabled = true;
            vol.x = MM(-0.5f, 0.5f); vol.z = MM(-0.5f, 0.5f);
            var n = ps.noise; n.enabled = true; n.strength = 0.3f; n.frequency = 0.5f;
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = MM(-Mathf.PI, Mathf.PI);
        }

        static void PresetExplosion(ParticleSystem ps)
        {
            var m = ps.main; m.duration = 0.5f; m.loop = false;
            m.startLifetime = MM(0.3f, 1); m.startSpeed = MM(5, 15); m.startSize = MM(0.2f, 0.8f);
            m.gravityModifier = 0.5f; m.maxParticles = 200;
            var em = ps.emission; em.rateOverTime = 0;
            em.SetBursts(new[] { new ParticleSystem.Burst(0, 50, 100) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.5f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = Grad(C(1, 1, 1), C(1, 0.5f, 0), C(0.3f, 0.1f, 0, 0.5f), C(0, 0, 0, 0));
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = Curve(0, 1, 1, 0);
        }

        static void PresetMagic(ParticleSystem ps)
        {
            var m = ps.main;
            m.startLifetime = MM(1, 2); m.startSpeed = MM(0.5f, 2); m.startSize = MM(0.1f, 0.3f);
            m.startColor = C(0.3f, 0.3f, 1); m.maxParticles = 300;
            var em = ps.emission; em.rateOverTime = 30;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 1; sh.radiusThickness = 0;
            var vol = ps.velocityOverLifetime; vol.enabled = true; vol.orbitalY = 2; vol.radial = -0.5f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = Grad(C(0.2f, 0.2f, 1, 0), C(0.6f, 0.2f, 1), C(0.1f, 0.1f, 0.5f, 0));
            var sol = ps.sizeOverLifetime; sol.enabled = true;
            sol.size = Curve3(0, 0.5f, 0.4f, 1.5f, 1, 0.3f);
            var n = ps.noise; n.enabled = true; n.strength = 0.3f; n.frequency = 1.5f;
        }

        static void PresetDust(ParticleSystem ps)
        {
            var m = ps.main;
            m.startLifetime = MM(3, 8); m.startSpeed = MM(0.05f, 0.2f); m.startSize = MM(0.02f, 0.06f);
            m.startColor = C(0.76f, 0.7f, 0.5f, 0.3f); m.gravityModifier = -0.01f; m.maxParticles = 100;
            var em = ps.emission; em.rateOverTime = 5;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(5, 3, 5);
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = FadeInOut(C(0.76f, 0.7f, 0.5f), 0.3f);
            var n = ps.noise; n.enabled = true; n.strength = 0.2f; n.frequency = 0.5f;
        }

        static void PresetBlood(ParticleSystem ps)
        {
            var m = ps.main; m.duration = 0.3f; m.loop = false;
            m.startLifetime = MM(0.3f, 0.8f); m.startSpeed = MM(3, 8); m.startSize = MM(0.05f, 0.2f);
            m.startColor = C(0.5f, 0, 0); m.gravityModifier = 1.5f; m.maxParticles = 100;
            var em = ps.emission; em.rateOverTime = 0;
            em.SetBursts(new[] { new ParticleSystem.Burst(0, 20, 40) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 30; sh.radius = 0.1f;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = Grad(C(0.7f, 0, 0), C(0.3f, 0, 0, 0.5f), C(0.2f, 0, 0, 0));
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = Curve(0, 0.5f, 1, 1);
        }

        static void PresetTrail(ParticleSystem ps)
        {
            var m = ps.main;
            m.startLifetime = 2; m.startSpeed = 0; m.startSize = 0.1f;
            m.startColor = C(0.5f, 0.8f, 1); m.maxParticles = 200;
            var em = ps.emission; em.rateOverTime = 0; em.rateOverDistance = 10;
            var sh = ps.shape; sh.enabled = false;
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = FadeOut(C(0.5f, 0.8f, 1));
            var tr = ps.trails; tr.enabled = true; tr.lifetime = 0.5f;
            tr.widthOverTrail = Curve(0, 1, 1, 0);
        }

        // --- Helpers ---

        static ParticleSystem.MinMaxCurve MM(float a, float b) => new ParticleSystem.MinMaxCurve(a, b);
        static float PF(string v) => float.Parse(v, Inv);
        static bool PB(string v) => v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
        static T PE<T>(string v) where T : struct => (T)Enum.Parse(typeof(T), v, true);
        static Color C(float r, float g, float b, float a = 1) => new Color(r, g, b, a);

        static ParticleSystem.MinMaxCurve PMM(string v)
        {
            if (v.Contains(","))
            {
                var p = v.Split(',');
                return MM(float.Parse(p[0], Inv), float.Parse(p[1], Inv));
            }
            return new ParticleSystem.MinMaxCurve(PF(v));
        }

        static ParticleSystem.MinMaxGradient Grad(params Color[] colors)
        {
            var g = new Gradient();
            var keys = new GradientColorKey[colors.Length];
            var alphas = new GradientAlphaKey[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                float t = colors.Length > 1 ? (float)i / (colors.Length - 1) : 0;
                keys[i] = new GradientColorKey(colors[i], t);
                alphas[i] = new GradientAlphaKey(colors[i].a, t);
            }
            g.SetKeys(keys, alphas);
            return new ParticleSystem.MinMaxGradient(g);
        }

        static ParticleSystem.MinMaxGradient FadeOut(Color c)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c, 0), new GradientColorKey(c, 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            return new ParticleSystem.MinMaxGradient(g);
        }

        static ParticleSystem.MinMaxGradient FadeInOut(Color c, float peak)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c, 0), new GradientColorKey(c, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(peak, 0.3f), new GradientAlphaKey(peak, 0.7f), new GradientAlphaKey(0, 1) });
            return new ParticleSystem.MinMaxGradient(g);
        }

        static ParticleSystem.MinMaxCurve Curve(float t0, float v0, float t1, float v1) =>
            new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(t0, v0, t1, v1));

        static ParticleSystem.MinMaxCurve Curve3(float t0, float v0, float t1, float v1, float t2, float v2) =>
            new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(t0, v0), new Keyframe(t1, v1), new Keyframe(t2, v2)));
    }
}
