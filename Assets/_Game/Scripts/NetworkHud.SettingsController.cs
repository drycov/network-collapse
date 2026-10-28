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
        private HudSettingsController settingsController;
        private void BuildSettingsView()
        {
            settingsController = new HudSettingsController(
                PlayerPrefs.GetFloat("NC.Volume", .48f),
                PlayerPrefs.GetInt("NC.Muted", 0) != 0,
                PlayerPrefs.GetInt("NC.LowGraphics", 0) != 0);
            settingsTab = settingsController.Root;
            volumeSlider = settingsController.VolumeSlider;
            settingsController.VolumeChanged += value => {
                PlayerPrefs.SetFloat("NC.Volume", value);
                VolumeChanged?.Invoke(value);
            };
            settingsController.MuteChanged += value => {
                PlayerPrefs.SetInt("NC.Muted", value ? 1 : 0);
                MuteChanged?.Invoke(value);
            };
            settingsController.PerformanceChanged += value => {
                PlayerPrefs.SetInt("NC.LowGraphics", value ? 1 : 0);
                PerformanceModeChanged?.Invoke(value);
            };
        }
    }
}
