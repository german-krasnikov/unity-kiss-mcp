using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor.Tests
{
    public abstract class MultiSceneTestBase
    {
        protected Scene _additiveScene;
        protected readonly List<GameObject> _toDestroy = new();
        protected readonly List<Scene> _extraScenes = new();
        private readonly List<string> _extraPaths = new();
        private string _tempPath;
        private string _additiveTempPath;
        protected string _savedMainSceneName;

        [SetUp]
        public virtual void SetUp()
        {
            TestPaths.EnsureFolder();
            var current = SceneManager.GetActiveScene();
            _tempPath = TestPaths.TempFolder + $"/{GetType().Name}_temp.unity";
            if (string.IsNullOrEmpty(current.path))
                EditorSceneManager.SaveScene(current, _tempPath);
            _savedMainSceneName = current.name;
            _additiveScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            _additiveTempPath = TestPaths.TempFolder + $"/{GetType().Name}_additive_temp.unity";
            EditorSceneManager.SaveScene(_additiveScene, _additiveTempPath);
            // Restore main scene as active — NewScene(Additive) hijacks it
            SceneManager.SetActiveScene(current);
        }

        [TearDown]
        public virtual void TearDown()
        {
            foreach (var go in _toDestroy)
                if (go != null) Object.DestroyImmediate(go);
            _toDestroy.Clear();
            foreach (var s in _extraScenes)
                if (s.IsValid()) EditorSceneManager.CloseScene(s, true);
            _extraScenes.Clear();
            foreach (var p in _extraPaths)
                if (p != null && File.Exists(p)) AssetDatabase.DeleteAsset(p);
            _extraPaths.Clear();
            if (_additiveScene.IsValid())
                EditorSceneManager.CloseScene(_additiveScene, true);
            _additiveScene = default;
            if (_additiveTempPath != null && File.Exists(_additiveTempPath))
                AssetDatabase.DeleteAsset(_additiveTempPath);
            if (_tempPath != null && File.Exists(_tempPath))
                AssetDatabase.DeleteAsset(_tempPath);
        }

        protected GameObject CreateIn(Scene scene, string name)
        {
            var go = new GameObject(name);
            _toDestroy.Add(go);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        protected GameObject CreateChild(GameObject parent, string name)
        {
            var go = new GameObject(name);
            _toDestroy.Add(go);
            go.transform.SetParent(parent.transform);
            return go;
        }

        protected Scene AddScene()
        {
            var active = SceneManager.GetActiveScene();
            var s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var path = TestPaths.TempFolder + $"/{GetType().Name}_extra_{_extraScenes.Count}.unity";
            EditorSceneManager.SaveScene(s, path);
            _extraPaths.Add(path);
            _extraScenes.Add(s);
            // Restore active scene — NewScene(Additive) hijacks it
            SceneManager.SetActiveScene(active);
            return s;
        }

        protected string MainSceneName => SceneManager.GetActiveScene().name;
    }
}
