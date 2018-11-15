using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CheatTools.ObjectTree
{
    public class ObjectTreeViewer
    {
        private readonly Action<InspectorStackEntry[]> _inspectorOpenCallback;
        private readonly HashSet<GameObject> _openedObjects = new HashSet<GameObject>();
        private Vector2 _propertiesScrollPosition;
        private Transform _selectedTransform;
        private Vector2 _treeScrollPosition;
        private Rect _windowRect;
        private bool _scrollTreeToSelected;
        private bool _enabled;
        private List<GameObject> _cachedRootGameObjects;
        private readonly Dictionary<Image, Texture2D> _imagePreviewCache = new Dictionary<Image, Texture2D>();

        public void SelectAndShowObject(Transform target)
        {
            _selectedTransform = target;

            target = target.parent;
            while (target != null)
            {
                _openedObjects.Add(target.gameObject);
                target = target.parent;
            }

            _scrollTreeToSelected = true;
            Enabled = true;
        }

        public ObjectTreeViewer(Action<InspectorStackEntry[]> inspectorOpenCallback)
        {
            _inspectorOpenCallback = inspectorOpenCallback ?? throw new ArgumentNullException(nameof(inspectorOpenCallback));
        }

        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (value && !_enabled)
                    UpdateCaches();

                _enabled = value;
            }
        }

        public void UpdateCaches()
        {
            _cachedRootGameObjects = Resources.FindObjectsOfTypeAll<Transform>()
                                    .Where(t => t.parent == null)
                                    .Select(x => x.gameObject)
                                    .ToList();

            _imagePreviewCache.Clear();
        }

        private void OnInspectorOpen(params InspectorStackEntry[] items)
        {
            _inspectorOpenCallback.Invoke(items);
        }

        public void UpdateWindowSize(Rect screenRect)
        {
            const int width = 300;
            //const int padding = 3;

            var height = screenRect.height;

            _windowRect = new Rect(screenRect.xMax - width, screenRect.yMin, width, height);
        }

        private void DisplayObjectTreeHelper(GameObject go, int indent)
        {
            var c = GUI.color;
            if (_selectedTransform == go.transform)
            {
                GUI.color = Color.cyan;
                if (_scrollTreeToSelected && Event.current.type == EventType.Repaint)
                {
                    _scrollTreeToSelected = false;
                    _treeScrollPosition.y = GUILayoutUtility.GetLastRect().y - 50;
                }
            }
            else if (!go.activeSelf)
            {
                GUI.color = new Color(1, 1, 1, 0.6f);
            }

            GUILayout.BeginHorizontal();
            {
                GUILayout.Space(indent * 20f);

                GUILayout.BeginHorizontal();
                {
                    if (go.transform.childCount != 0)
                        if (GUILayout.Toggle(_openedObjects.Contains(go), "", GUILayout.ExpandWidth(false)))
                        {
                            if (_openedObjects.Contains(go) == false)
                                _openedObjects.Add(go);
                        }
                        else
                        {
                            if (_openedObjects.Contains(go))
                                _openedObjects.Remove(go);
                        }
                    else
                        GUILayout.Space(20f);

                    if (GUILayout.Button(go.name, GUI.skin.label, GUILayout.ExpandWidth(true), GUILayout.MinWidth(200)))
                    {
                        if (_selectedTransform == go.transform)
                        {
                            if (_openedObjects.Contains(go) == false)
                                _openedObjects.Add(go);
                            else
                                _openedObjects.Remove(go);
                        }
                        _selectedTransform = go.transform;
                    }

                    GUI.color = c;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndHorizontal();

            if (_openedObjects.Contains(go))
                for (var i = 0; i < go.transform.childCount; ++i)
                    DisplayObjectTreeHelper(go.transform.GetChild(i).gameObject, indent + 1);
        }

        public void DisplayViewer()
        {
            if (Enabled)
            {
                Utilities.DrawSolidWindowBackground(_windowRect);
                _windowRect = GUILayout.Window(593, _windowRect, WindowFunc, "Scene Object Browser");
            }
        }

        private void WindowFunc(int id)
        {
            GUILayout.BeginVertical();
            {
                DisplayObjectTree();

                DisplayControls();

                DisplayObjectProperties();
            }
            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }

        private void DisplayControls()
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            {
                if (GUILayout.Button("Clear AssetBundle Cache"))
                    foreach (var pair in AssetBundleManager.ManifestBundlePack)
                        foreach (var bundle in new Dictionary<string, LoadedAssetBundle>(pair.Value.LoadedAssetBundles))
                            AssetBundleManager.UnloadAssetBundle(bundle.Key, true, pair.Key);

                if (GUILayout.Button("Open log file", GUILayout.ExpandWidth(false)))
                    Process.Start(Path.Combine(Application.dataPath, "output_log.txt"));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Close", GUILayout.ExpandWidth(false)))
                    Enabled = false;
            }
            GUILayout.EndHorizontal();
        }

        private void DisplayObjectProperties()
        {
            _propertiesScrollPosition = GUILayout.BeginScrollView(_propertiesScrollPosition, GUI.skin.box);
            {
                if (_selectedTransform == null)
                {
                    GUILayout.Label("No object selected");
                }
                else
                {
                    GUILayout.BeginVertical(GUI.skin.box);
                    {
                        var fullTransfromPath = GetFullTransfromPath(_selectedTransform);

                        GUILayout.TextArea(fullTransfromPath, GUI.skin.label);

                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label($"Layer {_selectedTransform.gameObject.layer} ({LayerMask.LayerToName(_selectedTransform.gameObject.layer)})");

                            GUILayout.Space(8);

                            GUILayout.Toggle(_selectedTransform.gameObject.isStatic, "isStatic");

                            _selectedTransform.gameObject.SetActive(GUILayout.Toggle(_selectedTransform.gameObject.activeSelf, "Active", GUILayout.ExpandWidth(false)));

                            GUILayout.FlexibleSpace();

                            if (GUILayout.Button("Inspect"))
                                OnInspectorOpen(new InspectorStackEntry(_selectedTransform.gameObject, fullTransfromPath));

                            if (GUILayout.Button("X"))
                                Object.Destroy(_selectedTransform.gameObject);
                        }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndVertical();

                    foreach (var component in _selectedTransform.GetComponents<Component>())
                    {
                        if (component == null)
                            continue;

                        DrawSingleComponent(component);
                    }
                }
            }
            GUILayout.EndScrollView();
        }

        private void DrawSingleComponent(Component component)
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            {
                if (component is Behaviour bh)
                    bh.enabled = GUILayout.Toggle(bh.enabled, "", GUILayout.ExpandWidth(false));

                if(GUILayout.Button(component.GetType().Name, GUI.skin.label))
                {
                    OnInspectorOpen(new InspectorStackEntry(component.transform, GetFullTransfromPath(component.transform)),
                        new InspectorStackEntry(component, component.GetType().FullName));
                }

                switch (component)
                {
                    case Image img:
                        if (img.sprite != null && img.sprite.texture != null)
                        {
                            GUILayout.Label(img.sprite.name);

                            if (!_imagePreviewCache.TryGetValue(img, out var tex))
                            {
                                try
                                {
                                    var newImg = img.sprite.texture.GetPixels(
                                        (int)img.sprite.textureRect.x, (int)img.sprite.textureRect.y,
                                        (int)img.sprite.textureRect.width,
                                        (int)img.sprite.textureRect.height);
                                    tex = new Texture2D((int)img.sprite.textureRect.width,
                                        (int)img.sprite.textureRect.height);
                                    tex.SetPixels(newImg);
                                    //todo tex.Resize(0, 0); get proper width
                                    tex.Apply();
                                }
                                catch (Exception)
                                {
                                    tex = null;
                                }

                                _imagePreviewCache.Add(img, tex);
                            }

                            if (tex != null)
                                GUILayout.Label(tex);
                            else
                                GUILayout.Label("Can't display texture");
                        }
                        //todo img.sprite.texture.EncodeToPNG() button
                        break;
                    case Slider b:
                        {
                            for (var i = 0; i < b.onValueChanged.GetPersistentEventCount(); ++i)
                                GUILayout.Label(
                                    $"{b.onValueChanged.GetPersistentTarget(i).GetType().FullName}.{b.onValueChanged.GetPersistentMethodName(i)}");
                            break;
                        }
                    case Text text:
                        GUILayout.Label(
                            $"{text.text} {text.font} {text.fontStyle} {text.fontSize} {text.alignment} {text.alignByGeometry} {text.resizeTextForBestFit} {text.color}");
                        break;
                    case RawImage r:
                        GUILayout.Label(r.mainTexture);
                        break;
                    case Renderer re:
                        GUILayout.Label(re.material != null
                            ? re.material.shader.name
                            : "[No material]");
                        break;
                    case Button b:
                        {
                            for (var i = 0; i < b.onClick.GetPersistentEventCount(); ++i)
                                GUILayout.Label(
                                    $"{b.onClick.GetPersistentTarget(i).GetType().FullName}.{b.onClick.GetPersistentMethodName(i)}");
                            var calls = (IList)b.onClick.GetPrivateExplicit<UnityEventBase>("m_Calls")
                                .GetPrivate("m_RuntimeCalls");
                            foreach (var call in calls)
                            {
                                var unityAction = (UnityAction)call.GetPrivate("Delegate");
                                GUILayout.Label(
                                    $"{unityAction.Target.GetType().FullName}.{unityAction.Method.Name}");
                            }
                            break;
                        }
                    case Toggle b:
                        {
                            for (var i = 0; i < b.onValueChanged.GetPersistentEventCount(); ++i)
                                GUILayout.Label(
                                    $"{b.onValueChanged.GetPersistentTarget(i).GetType().FullName}.{b.onValueChanged.GetPersistentMethodName(i)}");
                            var calls = (IList)b.onValueChanged.GetPrivateExplicit<UnityEventBase>("m_Calls")
                                .GetPrivate("m_RuntimeCalls");
                            foreach (var call in calls)
                            {
                                var unityAction = (UnityAction<bool>)call.GetPrivate("Delegate");
                                GUILayout.Label(
                                    $"{unityAction.Target.GetType().FullName}.{unityAction.Method.Name}");
                            }
                            break;
                        }
                    case RectTransform rt:
                        GUILayout.Label("anchorMin " + rt.anchorMin);
                        GUILayout.Label("anchorMax " + rt.anchorMax);
                        GUILayout.Label("offsetMin " + rt.offsetMin);
                        GUILayout.Label("offsetMax " + rt.offsetMax);
                        GUILayout.Label("rect " + rt.rect);
                        break;
                }

                GUILayout.FlexibleSpace();

                if (!(component is Transform))
                {
                    if (GUILayout.Button("R"))
                    {
                        var t = component.GetType();
                        var g = component.gameObject;

                        IEnumerator RecreateCo()
                        {
                            Object.Destroy(component);
                            yield return null;
                            g.AddComponent(t);
                        }

                        Object.FindObjectOfType<CheatTools>().StartCoroutine(RecreateCo());
                    }

                    if (GUILayout.Button("X"))
                    {
                        Object.Destroy(component);
                    }
                }
            }
            GUILayout.EndHorizontal();
        }

        private static string GetFullTransfromPath(Transform target)
        {
            var name = target.name;
            var parent = target.parent;
            while (parent != null)
            {
                name = $"{parent.name}/{name}";
                parent = parent.parent;
            }
            return name;
        }

        private void DisplayObjectTree()
        {
            _treeScrollPosition = GUILayout.BeginScrollView(_treeScrollPosition, GUI.skin.box,
                GUILayout.Height(_windowRect.height / 2), GUILayout.ExpandWidth(true));
            {
                foreach (var rootGameObject in
                    SceneManager.GetActiveScene().GetRootGameObjects()
                    .Concat(_cachedRootGameObjects.Where(x => x != null))
                    .Distinct()
                    .OrderBy(x => x.name))
                {
                    DisplayObjectTreeHelper(rootGameObject, 0);
                }
            }
            GUILayout.EndScrollView();
        }
    }
}