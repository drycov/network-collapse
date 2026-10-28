using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NetworkCollapse
{
    /// <summary>Settings view backed by UXML. Depends only on settings values and callbacks.</summary>
    public sealed class HudSettingsController
    {
        public VisualElement Root { get; }
        public Slider VolumeSlider { get; }
        public event Action<float> VolumeChanged;
        public event Action<bool> MuteChanged;
        public event Action<bool> PerformanceChanged;

        private bool muted;
        private bool economy;
        private readonly Button muteButton;
        private readonly Button performanceButton;
        private readonly Label volumeLabel;

        public HudSettingsController(float volume, bool muted, bool economy)
        {
            Root = HudViewAssets.Instantiate("Settings", "settings-root");
            if (Root == null) throw new InvalidOperationException("Settings.uxml not found");
            this.muted = muted;
            this.economy = economy;
            VolumeSlider = Root.Q<Slider>("volume");
            volumeLabel = Root.Q<Label>("volume-label");
            muteButton = Root.Q<Button>("mute");
            performanceButton = Root.Q<Button>("performance");
            if (VolumeSlider == null || volumeLabel == null || muteButton == null || performanceButton == null)
                throw new InvalidOperationException("Settings.uxml missing required named UI controls");

            VolumeSlider.SetValueWithoutNotify(Mathf.Clamp01(volume));
            UpdateLabels();
            VolumeSlider.RegisterValueChangedCallback(e => {
                UpdateLabels();
                VolumeChanged?.Invoke(e.newValue);
            });
            muteButton.clicked += () => {
                this.muted = !this.muted;
                UpdateLabels();
                MuteChanged?.Invoke(this.muted);
            };
            performanceButton.clicked += () => {
                this.economy = !this.economy;
                UpdateLabels();
                PerformanceChanged?.Invoke(this.economy);
            };
        }

        private void UpdateLabels()
        {
            volumeLabel.text = Mathf.RoundToInt(VolumeSlider.value * 100f) + "%";
            muteButton.text = muted ? "ВКЛЮЧИТЬ ЗВУК" : "ОТКЛЮЧИТЬ ЗВУК";
            performanceButton.text = economy ? "ЭКОНОМИЧНЫЙ РЕЖИМ: ВКЛ." : "ЭКОНОМИЧНЫЙ РЕЖИМ: ВЫКЛ.";
        }
    }
}
