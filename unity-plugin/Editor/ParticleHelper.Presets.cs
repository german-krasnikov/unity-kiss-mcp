using UnityEngine;

namespace UnityMCP.Editor
{
    public static partial class ParticleHelper
    {
        static void ApplyPresetInternal(ParticleSystem ps, string preset)
        {
            switch (preset.ToLowerInvariant())
            {
                case "fire": PresetFire(ps); break;     case "smoke": PresetSmoke(ps); break;
                case "sparks": PresetSparks(ps); break;  case "rain": PresetRain(ps); break;
                case "snow": PresetSnow(ps); break;      case "explosion": PresetExplosion(ps); break;
                case "magic": PresetMagic(ps); break;    case "dust": PresetDust(ps); break;
                case "blood": PresetBlood(ps); break;    case "trail": PresetTrail(ps); break;
                default: throw new System.ArgumentException($"Unknown preset '{preset}'. Valid: fire, smoke, sparks, rain, snow, explosion, magic, dust, blood, trail.");
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
    }
}
