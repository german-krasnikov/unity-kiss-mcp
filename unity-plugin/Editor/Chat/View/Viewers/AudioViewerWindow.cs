// EditorWindow for audio preview: IMGUI waveform + play/pause/stop + scrubber.
// Uses AudioUtilProxy for reflection-based access to UnityEditor.AudioUtil.
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal sealed class AudioViewerWindow : EditorWindow
    {
        [SerializeField] private string _assetPath;

        private AudioClip  _clip;
        private Texture2D  _waveform;
        private bool       _playing;
        private bool       _waveformBaked;

        internal static void Show(string path)
        {
            var w = GetWindow<AudioViewerWindow>("Audio Viewer");
            w.minSize = new Vector2(320, 180);
            w._assetPath = path;
            w.LoadAsset();
        }

        private void OnEnable()
        {
            if (!string.IsNullOrEmpty(_assetPath))
                LoadAsset();
        }

        private void OnDisable()
        {
            if (_playing) { AudioUtilProxy.Stop(); _playing = false; }
            EditorApplication.update -= Repaint;
            if (_waveform != null) { DestroyImmediate(_waveform); _waveform = null; }
        }

        private void LoadAsset()
        {
            _clip = AssetDatabase.LoadAssetAtPath<AudioClip>(_assetPath);
            _waveformBaked = false;
            _waveform = null;
        }

        private void EnsureWaveform()
        {
            if (_waveformBaked || _clip == null) return;
            _waveformBaked = true;
            _waveform = AudioUtilProxy.GetWaveform(_clip, (int)position.width, 80);
        }

        private void OnGUI()
        {
            if (_clip == null)
            {
                EditorGUILayout.HelpBox(_assetPath == null ? "No asset." : "AudioClip not found.", MessageType.Warning);
                return;
            }

            EnsureWaveform();

            // Info label
            var name = Path.GetFileName(_assetPath);
            EditorGUILayout.LabelField($"{name}  —  {_clip.length:F2}s  {_clip.channels}ch  {_clip.frequency}Hz",
                EditorStyles.miniLabel);

            // Waveform
            var waveRect = GUILayoutUtility.GetRect(position.width, 80f);
            if (_waveform != null)
            {
                GUI.DrawTexture(waveRect, _waveform, ScaleMode.StretchToFill);
                HandleScrub(waveRect);
            }
            else
            {
                GUI.Box(waveRect, "Waveform unavailable");
            }

            // Progress bar
            float progress = _clip.length > 0f ? AudioUtilProxy.GetPosition(_clip) / _clip.length : 0f;
            var progRect = GUILayoutUtility.GetRect(position.width, 4f);
            EditorGUI.ProgressBar(progRect, progress, "");

            // Controls row
            EditorGUILayout.BeginHorizontal();
            var playLabel = _playing ? "Pause" : "Play";
            if (GUILayout.Button(playLabel, GUILayout.Width(60)))
                TogglePlay();

            if (GUILayout.Button("Stop", GUILayout.Width(50)))
                StopPlayback();

            var pos = AudioUtilProxy.GetPosition(_clip);
            GUILayout.Label($"{pos:F1}s / {_clip.length:F1}s", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void HandleScrub(Rect rect)
        {
            var e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) &&
                rect.Contains(e.mousePosition))
            {
                var t = (e.mousePosition.x - rect.x) / rect.width;
                var sample = (int)(t * _clip.samples);
                if (_playing) { AudioUtilProxy.Stop(); AudioUtilProxy.Play(_clip, sample); }
                e.Use();
            }
        }

        private void TogglePlay()
        {
            if (_playing)
            {
                AudioUtilProxy.Pause();
                _playing = false;
                EditorApplication.update -= Repaint;
            }
            else
            {
                AudioUtilProxy.Play(_clip);
                _playing = true;
                EditorApplication.update += Repaint;
            }
        }

        private void StopPlayback()
        {
            AudioUtilProxy.Stop();
            _playing = false;
            EditorApplication.update -= Repaint;
            Repaint();
        }

        // IAssetViewer adapter registered by AssetViewerFactory.
        internal sealed class ViewerAdapter : IAssetViewer
        {
            public void Show(string assetPath) => AudioViewerWindow.Show(assetPath);
        }
    }
}
