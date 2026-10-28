using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NetworkCollapse
{
    public sealed partial class NetworkHud
    {
        private static VisualElement SettingsSection(string heading, string description)
        {
            var section = new VisualElement();
            section.style.paddingTop = 17;
            section.style.paddingBottom = 14;
            section.style.paddingLeft = 16;
            section.style.paddingRight = 16;
            section.style.marginBottom = 14;
            section.style.backgroundColor = new Color(.055f, .13f, .17f, 1f);
            section.style.borderLeftWidth = 3;
            section.style.borderLeftColor = new Color(.25f, .78f, .7f, 1f);
            section.Add(Heading(heading, 15));
            section.Add(Body(description));
            return section;
        }
    }
}
