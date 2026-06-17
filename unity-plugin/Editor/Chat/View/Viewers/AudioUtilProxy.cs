// Reflection wrapper for UnityEditor.AudioUtil (internal Unity class).
// All calls are null-guarded — degrades gracefully if signature changes in future Unity versions.
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal static class AudioUtilProxy
    {
        private static readonly Type _type =
            typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.AudioUtil");

        private static readonly MethodInfo _play =
            _type?.GetMethod("PlayPreviewClip",
                BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);

        private static readonly MethodInfo _stop =
            _type?.GetMethod("StopAllPreviewClips",
                BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);

        private static readonly MethodInfo _pause =
            _type?.GetMethod("PausePreviewClip",
                BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);

        private static readonly MethodInfo _resume =
            _type?.GetMethod("ResumePreviewClip",
                BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);

        private static readonly MethodInfo _pos =
            _type?.GetMethod("GetClipPosition",
                BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(AudioClip) }, null);

        private static readonly MethodInfo _wave =
            _type?.GetMethod("GetWaveFormTexture",
                BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(AudioClip), typeof(int), typeof(int) }, null);

        internal static bool IsAvailable => _type != null;

        internal static void Play(AudioClip clip, int startSample = 0, bool loop = false)
        {
            if (clip == null) return;
            _play?.Invoke(null, new object[] { clip, startSample, loop });
        }

        internal static void Stop()   => _stop?.Invoke(null, null);
        internal static void Pause()  => _pause?.Invoke(null, null);
        internal static void Resume() => _resume?.Invoke(null, null);

        internal static float GetPosition(AudioClip clip)
        {
            if (clip == null || _pos == null) return 0f;
            return (float)_pos.Invoke(null, new object[] { clip });
        }

        internal static Texture2D GetWaveform(AudioClip clip, int width, int height)
        {
            if (clip == null || _wave == null) return null;
            return _wave.Invoke(null, new object[] { clip, width, height }) as Texture2D;
        }
    }
}
