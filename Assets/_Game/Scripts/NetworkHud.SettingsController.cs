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
        private void BuildSettingsView()
        {
            settingsTab = new VisualElement();
            settingsTab.style.flexShrink = 0;
            settingsTab.style.width = Length.Percent(100);
            settingsTab.Add(Heading("ЗВУК И ПРОИЗВОДИТЕЛЬНОСТЬ", 17));
            settingsTab.Add(Body("Изменения применяются сразу и сохраняются для следующих запусков."));
            var soundCard = SettingsSection("АУДИО", "Общая громкость и звуковые эффекты");
            settingsTab.Add(soundCard);
            volumeSlider = new Slider(0f, 1f) { value = PlayerPrefs.GetFloat("NC.Volume", .48f) };
            volumeSlider.style.width = Length.Percent(100);
            volumeSlider.style.height = 25;
            volumeSlider.style.marginBottom = 12;
            volumeSlider.RegisterValueChangedCallback(e => { PlayerPrefs.SetFloat("NC.Volume", e.newValue); VolumeChanged?.Invoke(e.newValue); });
            soundCard.Add(Body("ОБЩАЯ ГРОМКОСТЬ"));
            soundCard.Add(volumeSlider);
            var volumeText = Body("");
            volumeText.text = Mathf.RoundToInt(volumeSlider.value * 100) + "%";
            soundCard.Add(volumeText);
            volumeSlider.RegisterValueChangedCallback(e => volumeText.text = Mathf.RoundToInt(e.newValue * 100) + "%");
            muteToggle = new Toggle { value = PlayerPrefs.GetInt("NC.Muted", 0) != 0 };
            muteToggle.style.display = DisplayStyle.None;
            soundCard.Add(muteToggle);
            var muteButton = ActionButton("", () => { muteToggle.value = !muteToggle.value; });
            muteButton.text = muteToggle.value ? "ВКЛЮЧИТЬ ЗВУК" : "ОТКЛЮЧИТЬ ЗВУК";
            muteToggle.RegisterValueChangedCallback(e => {
                PlayerPrefs.SetInt("NC.Muted", e.newValue ? 1 : 0);
                MuteChanged?.Invoke(e.newValue);
                muteButton.text = e.newValue ? "ВКЛЮЧИТЬ ЗВУК" : "ОТКЛЮЧИТЬ ЗВУК";
            });
            soundCard.Add(muteButton);
            var graphicsCard = SettingsSection("ГРАФИКА", "Режим производительности и частота кадров");
            settingsTab.Add(graphicsCard);
            var performance = new Toggle { value = PlayerPrefs.GetInt("NC.LowGraphics", 0) != 0 };
            performance.style.display = DisplayStyle.None;
            graphicsCard.Add(performance);
            var performanceButton = ActionButton("", () => performance.value = !performance.value);
            performanceButton.text = performance.value ? "ЭКОНОМИЧНЫЙ РЕЖИМ: ВКЛ." : "ЭКОНОМИЧНЫЙ РЕЖИМ: ВЫКЛ.";
            performance.RegisterValueChangedCallback(e => {
                PlayerPrefs.SetInt("NC.LowGraphics", e.newValue ? 1 : 0);
                PerformanceModeChanged?.Invoke(e.newValue);
                performanceButton.text = e.newValue ? "ЭКОНОМИЧНЫЙ РЕЖИМ: ВКЛ." : "ЭКОНОМИЧНЫЙ РЕЖИМ: ВЫКЛ.";
            });
            graphicsCard.Add(performanceButton);
            graphicsCard.Add(Body("Экономичный режим ограничивает частоту кадров до 30 FPS."));
        }

    }
}
