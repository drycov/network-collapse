using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NetworkCollapse
{
    public sealed partial class NetworkHud
    {
        private void CreateWindowLayer(VisualElement root)
        {
            windowLayer = new VisualElement();
            windowLayer.style.position = Position.Absolute;
            windowLayer.style.left = 0; windowLayer.style.right = 0;
            windowLayer.style.top = 0; windowLayer.style.bottom = 0;
            windowLayer.style.justifyContent = Justify.Center;
            windowLayer.style.alignItems = Align.Center;
            windowLayer.style.backgroundColor = new Color(0f, .01f, .025f, .62f);
            windowContent = new ScrollView(ScrollViewMode.Vertical);
            windowContent.style.width = Length.Percent(100);
            windowContent.style.flexGrow = 1;
            windowContent.style.flexShrink = 1;
            windowContent.style.minHeight = 0;
            windowContent.style.flexBasis = 0;
            windowContent.verticalScrollerVisibility = ScrollerVisibility.Auto;
            windowContent.style.overflow = Overflow.Hidden;
            windowContent.contentContainer.style.flexShrink = 0;
            windowContent.contentContainer.style.width = Length.Percent(100);
            windowContent.style.paddingLeft = 16;
            windowContent.style.paddingRight = 16;
            windowContent.style.paddingBottom = 16;
            windowContent.style.paddingTop = 12;
            var frame = new VisualElement();
            currentWindow = frame;
            frame.style.width = Length.Percent(72);
            frame.style.maxWidth = 860;
            frame.style.height = Length.Percent(82);
            frame.style.minHeight = 260;
            frame.style.maxHeight = Length.Percent(88);
            frame.style.overflow = Overflow.Hidden;
            frame.style.flexShrink = 1;
            frame.style.backgroundColor = new Color(.015f, .06f, .085f, 1f);
            frame.style.borderTopWidth = 2;
            frame.style.borderTopColor = new Color(.24f, .78f, .71f);
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.paddingLeft = 16;
            header.style.paddingTop = 12;
            header.style.paddingRight = 16;
            header.style.minHeight = 52;
            header.style.flexShrink = 0;
            var caption = Heading("ОКНО ОПЕРАЦИЙ", 17);
            caption.name = "window-title";
            header.Add(caption);
            header.Add(ActionButton("ЗАКРЫТЬ  ×", () => ShowTab("operations")));
            frame.Add(header);
            frame.Add(windowContent);
            windowLayer.Add(frame);
            root.Add(windowLayer);
            windowLayer.style.display = DisplayStyle.None;
            AdjustLayout(root);
        }

        private void ShowTab(string tab)
        {
            activeTab = tab;
            if (operationsTab != null) operationsTab.style.display = DisplayStyle.Flex;
            foreach (var panel in new[] { missionsTab, inventoryTab, marketTab, settingsTab })
                if (panel != null) panel.style.display = DisplayStyle.None;
            if (windowLayer == null) return;
            if (tab == "market" && world != null) RenderMarket();
            if (tab == "operations")
            {
                windowLayer.style.display = DisplayStyle.None;
                windowContent.Clear();
                return;
            }
            VisualElement content = tab == "missions" ? missionsTab :
                tab == "inventory" ? inventoryTab :
                tab == "market" ? marketTab : settingsTab;
            if (content == null) return;
            // Reparent only when switching screens; avoid resetting scroll position on redundant calls.
            if (windowContent.childCount == 1 && windowContent[0] == content)
            {
                windowLayer.style.display = menuOpen ? DisplayStyle.None : DisplayStyle.Flex;
                return;
            }
            windowContent.Clear();
            windowContent.Add(content);
            content.style.display = DisplayStyle.Flex;
            content.style.flexShrink = 0;
            content.style.width = Length.Percent(100);
            var caption = currentWindow.Q<Label>("window-title");
            if (caption != null) caption.text = tab == "missions" ? "MISSION CONTROL" :
                tab == "inventory" ? "ИНВЕНТАРЬ И РЕСУРСЫ" :
                tab == "market" ? "СТРАТЕГИЧЕСКАЯ КАРТА / РЫНОК" : "НАСТРОЙКИ";
            windowLayer.style.display = menuOpen ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void OpenMenu(bool open)
        {
            menuOpen = open;
            if (mainMenu != null) mainMenu.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (dialogueOverlay != null && open) dialogueOverlay.style.display = DisplayStyle.None;
            if (open && windowLayer != null) windowLayer.style.display = DisplayStyle.None;
            if (windowLayer != null) windowLayer.style.display = open || activeTab == "operations" ? DisplayStyle.None : DisplayStyle.Flex;
            if (leftDock != null) leftDock.style.display = open ? DisplayStyle.None : DisplayStyle.Flex;
            if (rightDock != null) rightDock.style.display = open || (SelectedId < 0 && SelectedLinkId < 0 && !BuildMode) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void ToggleMenu() => OpenMenu(!menuOpen);

        public void CloseGameWindow() => ShowTab("operations");

        public void ShowInspector(bool show)
        {
            if (rightDock != null && !menuOpen) rightDock.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
