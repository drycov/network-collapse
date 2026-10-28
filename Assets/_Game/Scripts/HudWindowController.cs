using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NetworkCollapse
{
    /// <summary>Owns the view tree for a modal window. No dependency on MonoBehaviour or game simulation.</summary>
    public sealed class HudWindowController
    {
        private readonly VisualElement overlay;
        private readonly VisualElement frame;
        private readonly Label title;
        private readonly ScrollView scroll;
        public bool IsOpen => overlay.resolvedStyle.display != DisplayStyle.None;
        public VisualElement Overlay => overlay;
        public VisualElement Frame => frame;
        public ScrollView Scroll => scroll;

        public HudWindowController(VisualElement root, Action onClose)
        {
            overlay = new VisualElement();
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0; overlay.style.top = 0;
            overlay.style.right = 0; overlay.style.bottom = 0;
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;
            overlay.style.backgroundColor = new Color(0f, .01f, .025f, .75f);

            var tree = Resources.Load<VisualTreeAsset>("NetworkUI/Window");
            var generated = tree == null ? null : tree.CloneTree();
            frame = generated == null ? new VisualElement() : generated.Q<VisualElement>("window-frame");
            if (frame == null) throw new InvalidOperationException("Missing window-frame in NetworkUI/Window.uxml");
            title = frame.Q<Label>("window-title");
            scroll = frame.Q<ScrollView>("window-scroll");
            if (title == null || scroll == null)
                throw new InvalidOperationException("Window.uxml is missing named title or scroll view");
            var close = frame.Q<Button>("window-close");
            if (close != null) close.clicked += () => onClose?.Invoke();

            if (generated != null)
            {
                // CloneTree creates a template container; move its frame into the overlay.
                frame.RemoveFromHierarchy();
            }
            overlay.Add(frame);
            root.Add(overlay);
            overlay.style.display = DisplayStyle.None;
        }

        public void Show(string caption, VisualElement content)
        {
            if (content == null) return;
            if (title != null) title.text = caption;
            if (scroll.childCount != 1 || scroll[0] != content)
            {
                scroll.Clear();
                content.style.flexShrink = 0;
                content.style.width = Length.Percent(100);
                scroll.Add(content);
            }
            overlay.style.display = DisplayStyle.Flex;
        }

        public void Hide(bool clear = false)
        {
            overlay.style.display = DisplayStyle.None;
            if (clear) scroll.Clear();
        }

        public void SetResponsiveWidth(float viewport)
        {
            frame.style.width = viewport < 800 ? Length.Percent(94) :
                viewport < 1300 ? Length.Percent(84) : Length.Percent(72);
            frame.style.height = Length.Percent(viewport < 800 ? 89 : 82);
        }
    }

    /// <summary>Explicitly scoped prefab/style lookup for runtime UI Toolkit views.</summary>
    public static class HudViewAssets
    {
        public static void AttachStyle(VisualElement root)
        {
            var style = Resources.Load<StyleSheet>("NetworkUI/NetworkHud");
            if (style != null && !root.styleSheets.Contains(style)) root.styleSheets.Add(style);
            else if (style == null) Debug.LogWarning("NetworkUI/NetworkHud.uss not found");
        }
        public static VisualElement Instantiate(string asset, string namedRoot)
        {
            var tree = Resources.Load<VisualTreeAsset>("NetworkUI/" + asset);
            if (tree == null)
            {
                Debug.LogWarning("Missing UI layout: NetworkUI/" + asset);
                return null;
            }
            var clone = tree.CloneTree();
            var target = clone.Q<VisualElement>(namedRoot);
            if (target != null) target.RemoveFromHierarchy();
            return target;
        }
    }
}
