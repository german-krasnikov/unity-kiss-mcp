using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace UnityMCP.Editor
{
    public static class ParticleSerializer
    {
        private static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        public static string Serialize(string path, string module = null)
        {
            var ps = ParticleHelper.GetPS(path);
            return module == null ? SerializeOverview(ps, path) : SerializeModule(ps, module);
        }

        private static string SerializeOverview(ParticleSystem ps, string path)
        {
            var sb = new StringBuilder();
            sb.Append("ParticleSystem on '").Append(path).AppendLine("'");
            var m = ps.main;
            sb.Append("main: duration=").Append(F(m.duration))
              .Append(" loop=").Append(m.loop ? "true" : "false")
              .Append(" startLifetime=").Append(Curve(m.startLifetime))
              .Append(" startSpeed=").Append(Curve(m.startSpeed))
              .Append(" startSize=").Append(Curve(m.startSize))
              .Append(" maxParticles=").AppendLine(m.maxParticles.ToString());
            ModuleLine(sb, "emission", ps.emission.enabled,
                $"rateOverTime={Curve(ps.emission.rateOverTime)}");
            ModuleLine(sb, "shape", ps.shape.enabled,
                $"type={ps.shape.shapeType} angle={F(ps.shape.angle)} radius={F(ps.shape.radius)}");
            Simple(sb, "colorOverLifetime", ps.colorOverLifetime.enabled);
            Simple(sb, "sizeOverLifetime", ps.sizeOverLifetime.enabled);
            Simple(sb, "velocityOverLifetime", ps.velocityOverLifetime.enabled);
            Simple(sb, "noise", ps.noise.enabled);
            Simple(sb, "trails", ps.trails.enabled);
            Simple(sb, "collision", ps.collision.enabled);
            Simple(sb, "rotationOverLifetime", ps.rotationOverLifetime.enabled);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            sb.Append("renderer: ").Append(r != null ? r.renderMode.ToString() : "none");
            return sb.ToString().TrimEnd();
        }

        private static void ModuleLine(StringBuilder sb, string name, bool enabled, string stats)
        {
            sb.Append(name).Append(": ").Append(enabled ? "enabled" : "disabled");
            if (enabled && !string.IsNullOrEmpty(stats)) sb.Append(' ').Append(stats);
            sb.AppendLine();
        }

        private static void Simple(StringBuilder sb, string name, bool enabled) =>
            sb.Append(name).AppendLine(enabled ? ": enabled" : ": disabled");

        private static string SerializeModule(ParticleSystem ps, string mod)
        {
            var sb = new StringBuilder();
            switch (mod.ToLower())
            {
                case "main":
                    var m = ps.main;
                    sb.AppendLine("main:");
                    KV(sb, "duration", F(m.duration)); KV(sb, "loop", m.loop);
                    KV(sb, "startDelay", Curve(m.startDelay)); KV(sb, "startLifetime", Curve(m.startLifetime));
                    KV(sb, "startSpeed", Curve(m.startSpeed)); KV(sb, "startSize", Curve(m.startSize));
                    KV(sb, "startColor", Gradient(m.startColor)); KV(sb, "gravityModifier", Curve(m.gravityModifier));
                    KV(sb, "simulationSpace", m.simulationSpace.ToString()); KV(sb, "scalingMode", m.scalingMode.ToString());
                    KV(sb, "maxParticles", m.maxParticles.ToString()); KV(sb, "playOnAwake", m.playOnAwake);
                    break;
                case "emission":
                    var e = ps.emission;
                    sb.AppendLine("emission:");
                    KV(sb, "enabled", e.enabled); KV(sb, "rateOverTime", Curve(e.rateOverTime));
                    KV(sb, "rateOverDistance", Curve(e.rateOverDistance)); KV(sb, "burstCount", e.burstCount.ToString());
                    for (int i = 0; i < e.burstCount; i++)
                    {
                        var b = e.GetBurst(i);
                        sb.Append("burst[").Append(i).Append("]: time=").Append(F(b.time))
                          .Append(" count=").Append(Curve(b.count))
                          .Append(" cycles=").Append(b.cycleCount)
                          .Append(" interval=").AppendLine(F(b.repeatInterval));
                    }
                    break;
                case "shape":
                    var sh = ps.shape;
                    sb.AppendLine("shape:");
                    KV(sb, "enabled", sh.enabled); KV(sb, "shapeType", sh.shapeType.ToString());
                    KV(sb, "angle", F(sh.angle)); KV(sb, "radius", F(sh.radius)); KV(sb, "arc", F(sh.arc));
                    break;
                case "coloroverlifetime":
                    sb.AppendLine("colorOverLifetime:");
                    KV(sb, "enabled", ps.colorOverLifetime.enabled);
                    KV(sb, "color", Gradient(ps.colorOverLifetime.color));
                    break;
                case "sizeoverlifetime":
                    sb.AppendLine("sizeOverLifetime:");
                    KV(sb, "enabled", ps.sizeOverLifetime.enabled);
                    KV(sb, "size", Curve(ps.sizeOverLifetime.size));
                    KV(sb, "separateAxes", ps.sizeOverLifetime.separateAxes);
                    break;
                case "velocityoverlifetime":
                    var v = ps.velocityOverLifetime;
                    sb.AppendLine("velocityOverLifetime:");
                    KV(sb, "enabled", v.enabled); KV(sb, "x", Curve(v.x));
                    KV(sb, "y", Curve(v.y)); KV(sb, "z", Curve(v.z)); KV(sb, "space", v.space.ToString());
                    break;
                case "noise":
                    var n = ps.noise;
                    sb.AppendLine("noise:");
                    KV(sb, "enabled", n.enabled); KV(sb, "strength", Curve(n.strength));
                    KV(sb, "frequency", F(n.frequency)); KV(sb, "scrollSpeed", Curve(n.scrollSpeed));
                    KV(sb, "octaveCount", n.octaveCount.ToString());
                    break;
                case "renderer":
                    sb.AppendLine("renderer:");
                    var rr = ps.GetComponent<ParticleSystemRenderer>();
                    if (rr == null) { sb.AppendLine("none"); break; }
                    KV(sb, "renderMode", rr.renderMode.ToString());
                    KV(sb, "material", rr.sharedMaterial != null ? rr.sharedMaterial.name : "none");
                    KV(sb, "sortingOrder", rr.sortingOrder.ToString());
                    KV(sb, "minParticleSize", F(rr.minParticleSize)); KV(sb, "maxParticleSize", F(rr.maxParticleSize));
                    break;
                case "trails":
                    var tr = ps.trails;
                    sb.AppendLine("trails:");
                    KV(sb, "enabled", tr.enabled); KV(sb, "ratio", F(tr.ratio));
                    KV(sb, "lifetime", Curve(tr.lifetime)); KV(sb, "minVertexDistance", F(tr.minVertexDistance));
                    break;
                case "collision":
                    var col = ps.collision;
                    sb.AppendLine("collision:");
                    KV(sb, "enabled", col.enabled); KV(sb, "type", col.type.ToString());
                    KV(sb, "mode", col.mode.ToString()); KV(sb, "bounce", Curve(col.bounce));
                    KV(sb, "lifetimeLoss", Curve(col.lifetimeLoss));
                    break;
                case "rotationoverlifetime":
                    var rot = ps.rotationOverLifetime;
                    sb.AppendLine("rotationOverLifetime:");
                    KV(sb, "enabled", rot.enabled); KV(sb, "z", Curve(rot.z));
                    KV(sb, "separateAxes", rot.separateAxes);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unknown module '{mod}'. Valid: main|emission|shape|colorOverLifetime|sizeOverLifetime|velocityOverLifetime|noise|renderer|trails|collision|rotationOverLifetime");
            }
            return sb.ToString().TrimEnd();
        }

        private static void KV(StringBuilder sb, string k, string v) =>
            sb.Append(k).Append(": ").AppendLine(v);

        private static void KV(StringBuilder sb, string k, bool v) =>
            sb.Append(k).AppendLine(v ? ": true" : ": false");

        private static string Curve(ParticleSystem.MinMaxCurve c) => c.mode switch
        {
            ParticleSystemCurveMode.Constant    => F(c.constant),
            ParticleSystemCurveMode.TwoConstants => F(c.constantMin) + ".." + F(c.constantMax),
            ParticleSystemCurveMode.Curve       => "curve(" + c.curve.keys.Length + " keys)",
            ParticleSystemCurveMode.TwoCurves   => "curves(" + c.curveMin.keys.Length + "," + c.curveMax.keys.Length + " keys)",
            _                                   => F(c.constant)
        };

        private static string Gradient(ParticleSystem.MinMaxGradient g) => g.mode switch
        {
            ParticleSystemGradientMode.Color        => "#" + ColorUtility.ToHtmlStringRGBA(g.color),
            ParticleSystemGradientMode.TwoColors    => "#" + ColorUtility.ToHtmlStringRGBA(g.colorMin) + ".." + "#" + ColorUtility.ToHtmlStringRGBA(g.colorMax),
            ParticleSystemGradientMode.Gradient     => "gradient(" + (g.gradient?.colorKeys.Length ?? 0) + " keys)",
            ParticleSystemGradientMode.TwoGradients => "gradients(" + (g.gradientMin?.colorKeys.Length ?? 0) + "," + (g.gradientMax?.colorKeys.Length ?? 0) + " keys)",
            _                                       => "#" + ColorUtility.ToHtmlStringRGBA(g.color)
        };

        private static string F(float v) => v.ToString("G4", IC);
    }
}
