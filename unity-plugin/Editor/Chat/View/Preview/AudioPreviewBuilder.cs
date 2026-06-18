// Preview builder for audio clips: filename + duration/frequency/channels metadata.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed class AudioPreviewBuilder : IPreviewBuilder
    {
        public bool CanBuild(string kindKey, string path)
        {
            if (kindKey == ChipKindKeys.Audio)
                return true;
            return PreviewPathResolver.IsAudioFile(path);
        }

        public VisualElement Build(PreviewRequest request, IPreviewContext context)
        {
            var container = new VisualElement();
            container.AddToClassList("chip-preview-audio");

            var lbl = new Label(Path.GetFileName(request.Path));
            lbl.AddToClassList("chip-preview-audio-label");
            container.Add(lbl);

            var meta = (InlinePreviewBuilder.AudioClipLoader ?? LoadAudioMeta)(request.Path);
            if (meta.HasValue)
            {
                var dur = TimeSpan.FromSeconds(meta.Value.length);
                container.Add(new Label($"{dur:m\\:ss} · {meta.Value.frequency / 1000}kHz · {meta.Value.channels}ch"));
            }

            return container;
        }

        static (float length, int frequency, int channels)? LoadAudioMeta(string path)
        {
            if (!PreviewPathResolver.IsAssetPath(path)) return null;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) return null;
            return (clip.length, clip.frequency, clip.channels);
        }
    }
}
