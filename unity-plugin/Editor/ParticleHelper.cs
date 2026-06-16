using System;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityMCP.Editor
{
    public static partial class ParticleHelper
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
            EditorUtility.SetDirty(ps);
            if (!EditorApplication.isPlaying)
                EditorSceneManager.MarkSceneDirty(ps.gameObject.scene);
            return $"set: {module}.{prop} = {value}";
        }

        public static string ApplyPreset(string path, string preset)
        {
            var ps = GetPS(path);
            Undo.RecordObject(ps, "Apply Particle Preset");
            ApplyPresetInternal(ps, preset);
            EditorUtility.SetDirty(ps);
            if (!EditorApplication.isPlaying)
                EditorSceneManager.MarkSceneDirty(ps.gameObject.scene);
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
                case "coloroverlifetime":
                case "sizeoverlifetime":
                case "velocityoverlifetime":
                case "rotationoverlifetime":
                case "trails":
                case "collision":
                    if (!string.Equals(prop, "enabled", StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException($"Module '{module}' only supports prop='enabled'. Got '{prop}'");
                    SetModuleEnabled(ps, module, PB(value));
                    break;
                default: throw new ArgumentException($"Unknown module '{module}'. Valid: main, emission, shape, noise, renderer, colorOverLifetime, sizeOverLifetime, velocityOverLifetime, rotationOverLifetime, trails, collision.");
            }
        }

        // Internal test-seam: exposes SetModuleProperty for unit tests
        internal static void SetPropertyDirect(ParticleSystem ps, string module, string prop, string value)
            => SetModuleProperty(ps, module, prop, value);

        static void SetModuleEnabled(ParticleSystem ps, string module, bool enabled)
        {
            switch (module.ToLowerInvariant())
            {
                case "coloroverlifetime":     var c = ps.colorOverLifetime; c.enabled = enabled; break;
                case "sizeoverlifetime":      var s = ps.sizeOverLifetime; s.enabled = enabled; break;
                case "velocityoverlifetime":  var v = ps.velocityOverLifetime; v.enabled = enabled; break;
                case "rotationoverlifetime":  var r = ps.rotationOverLifetime; r.enabled = enabled; break;
                case "trails":                var t = ps.trails; t.enabled = enabled; break;
                case "collision":             var co = ps.collision; co.enabled = enabled; break;
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
