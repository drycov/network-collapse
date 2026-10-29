using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NetworkCollapse
{
    public sealed partial class NetworkHud
    {
        public void SetCampaign(NetworkCampaign value)
        {
            campaign = value;
            previewMission = -1;
            if (missionPicker != null && campaign != null)
            {
                missionPicker.Clear();
                for (int i = 0; i < campaign.Missions.Count; i++)
                {
                    int index = i;
                    missionPicker.Add(ActionButton((i + 1) + ". " + campaign.Missions[i].Title, () => { previewMission = index; Refresh(); }));
                }
            }
            Refresh();
        }

        public void SetTutorial(string instruction)
        {
            if (tutorialLabel != null) tutorialLabel.text = instruction ?? string.Empty;
        }

        public void ShowBriefing()
        {
            if (dialogueOverlay == null || campaign == null) return;
            briefingText.text = campaign.ChapterTitle + "\n\n" + campaign.StoryBriefing + "\n\n" + campaign.MissionText();
            dialogueOverlay.style.display = DisplayStyle.Flex;
        }

        public void SelectScreen(int screen)
        {
            ShowTab(screen == 1 ? "missions" : screen == 2 ? "inventory" : "operations");
        }
    }
}
