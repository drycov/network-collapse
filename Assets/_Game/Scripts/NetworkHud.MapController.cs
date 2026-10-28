using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NetworkCollapse
{
    public sealed partial class NetworkHud
    {
        private void AdjustLayout(VisualElement root)
        {
            float width = root.resolvedStyle.width;
            if (width < 200f || Mathf.Abs(width - lastLayoutWidth) < 2f) return;
            lastLayoutWidth = width;
            bool compact = width < 1100f;
            float panelWidth = compact ? Mathf.Clamp(width * .25f, 190f, 260f) : 310f;
            if (leftDock != null) leftDock.style.width = panelWidth;
            if (rightDock != null) rightDock.style.width = panelWidth;
            if (statusBar != null) statusBar.style.fontSize = width < 900f ? 10 : 13;
            if (hudAlert != null)
            {
                hudAlert.style.left = panelWidth + 22f;
                hudAlert.style.right = panelWidth + 22f;
            }
            if (currentWindow != null)
            {
                currentWindow.style.width = width < 800 ? Length.Percent(94) :
                    width < 1300 ? Length.Percent(84) : Length.Percent(72);
                currentWindow.style.maxWidth = 960;
                currentWindow.style.height = Length.Percent(width < 800 ? 89 : 82);
            }
            if (mainMenuCard != null) mainMenuCard.style.maxWidth = Length.Percent(92);
        }

        public bool IsPointerOverHud(Vector2 screen)
        {
            if (menuOpen || (windowLayer != null && windowLayer.resolvedStyle.display != DisplayStyle.None))
                return true;
            float leftWidth = leftDock != null && leftDock.resolvedStyle.display != DisplayStyle.None
                ? leftDock.resolvedStyle.width + 16f : 0f;
            float rightWidth = rightDock != null && rightDock.resolvedStyle.display != DisplayStyle.None
                ? rightDock.resolvedStyle.width + 16f : 0f;
            return screen.y > Screen.height - 65f ||
                screen.x < leftWidth || screen.x > Screen.width - rightWidth;
        }
    }
}
