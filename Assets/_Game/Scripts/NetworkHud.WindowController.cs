using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NetworkCollapse
{
    public sealed partial class NetworkHud
    {
        private HudWindowController windowController;
        private void CreateWindowLayer(VisualElement root)
        {
            windowController = new HudWindowController(root, () => ShowTab("operations"));
            windowLayer = windowController.Overlay;
            currentWindow = windowController.Frame;
            windowContent = windowController.Scroll;
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
                windowController.Hide(true);
                return;
            }
            VisualElement content = tab == "missions" ? (missionController != null ? missionController.Root : missionsTab) :
                tab == "inventory" ? inventoryTab :
                tab == "market" ? (marketController != null ? marketController.Root : marketTab) : settingsTab;
            if (content == null) return;
            // Reparent only when switching screens; avoid resetting scroll position on redundant calls.
            if (windowContent.childCount == 1 && windowContent[0] == content)
            {
                if (!menuOpen) windowController.Show(tab == "missions" ? "МИССИИ" : tab == "market" ? "РЫНОК" : tab == "settings" ? "НАСТРОЙКИ" : "ИНВЕНТАРЬ", content);
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
            if (!menuOpen) windowController.Show(caption == null ? tab : caption.text, content);
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
