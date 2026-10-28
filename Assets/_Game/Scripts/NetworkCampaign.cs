using System;
using System.Collections.Generic;

namespace NetworkCollapse
{
    public enum MissionKind
    {
        RestoreAvailability,
        BuildLink,
        RepairDevice,
        UpgradeLink,
        SurviveIncident,
        ConfigureDevice
    }

    [Serializable]
    public sealed class StoryMission
    {
        public string Id, Title, Briefing;
        public MissionKind Kind;
        public int Target, Progress, Reward;
        public bool Completed;
    }

    [Serializable]
    public sealed class Achievement
    {
        public string Id, Title, Description;
        public bool Unlocked;
    }

    [Serializable]
    public sealed class InventoryItem
    {
        public string Id, Name, Description;
        public int Quantity;
    }

    /// <summary>
    /// Access-network campaign in the spirit of Uplink: missions, ranking credit, gear, story acts.
    /// Focus is pure access/aggregation operations — no generic hacking fantasy.
    /// </summary>
    public sealed class NetworkCampaign
    {
        public readonly List<StoryMission> Missions = new List<StoryMission>();
        public readonly List<Achievement> Achievements = new List<Achievement>();
        public readonly List<InventoryItem> Inventory = new List<InventoryItem>();

        public int Chapter { get; private set; }
        public int Rank { get; private set; } = 1;
        public int Reputation { get; private set; }

        public string ChapterTitle =>
            Chapter <= 1 ? "АКТ I · BLACKOUT" :
            Chapter <= 3 ? "АКТ II · THE BACKBONE" :
            Chapter <= 5 ? "АКТ III · SHADOW TRAFFIC" :
            "АКТ IV · CITY LINK";

        public string StoryBriefing =>
            Chapter == 0
                ? "Городской access-сегмент лёг после ночного шторма. Вы — дежурный оператор NETOPS. Восстановите базовую доступность и докажите, что сеть ещё жива."
                : Chapter == 1
                    ? "Абоненты требуют резервных трактов. Постройте обходной канал — redundancy важнее идеальной схемы."
                    : Chapter == 2
                        ? "На агрегации горят трансиверы. Запустите ремонт и верните узел в production."
                        : Chapter == 3
                            ? "Магистраль задыхается. Поднимите capacity критичного канала до приемлемого уровня."
                            : Chapter == 4
                                ? "Инцидент уже в эфире. Удержите доступность выше 60% — иначе город уйдёт в offline."
                                : Chapter == 5
                                    ? "Нужен новый VLAN/MTU профиль на access-оборудовании. Примените конфигурацию без потери сервиса."
                                    : "Кампания закрыта. Сеть access-уровня стабилизирована. Дальше — только ваша смена и репутация.";

        public StoryMission Current => Chapter < Missions.Count ? Missions[Chapter] : null;

        public event Action<string> Notification;
        public event Action<int> CreditsAwarded;
        public event Action StateChanged;
        [Serializable] public sealed class SaveData
        {
            public int Chapter, Rank, Reputation;
            public List<int> MissionProgress = new List<int>();
            public List<bool> MissionCompleted = new List<bool>();
            public List<bool> AchievementUnlocked = new List<bool>();
            public List<int> InventoryQuantities = new List<int>();
        }
        public SaveData Capture()
        {
            var save = new SaveData { Chapter = Chapter, Rank = Rank, Reputation = Reputation };
            foreach (var m in Missions) { save.MissionProgress.Add(m.Progress); save.MissionCompleted.Add(m.Completed); }
            foreach (var a in Achievements) save.AchievementUnlocked.Add(a.Unlocked);
            foreach (var item in Inventory) save.InventoryQuantities.Add(item.Quantity);
            return save;
        }
        public void Restore(SaveData save)
        {
            if (save == null) return;
            Chapter = Math.Max(0, Math.Min(Missions.Count, save.Chapter));
            Rank = Math.Max(1, save.Rank);
            Reputation = Math.Max(0, save.Reputation);
            for (int i = 0; i < Missions.Count && i < save.MissionProgress.Count; i++) Missions[i].Progress = Math.Max(0, save.MissionProgress[i]);
            for (int i = 0; i < Missions.Count && i < save.MissionCompleted.Count; i++) Missions[i].Completed = save.MissionCompleted[i];
            for (int i = 0; i < Achievements.Count && i < save.AchievementUnlocked.Count; i++) Achievements[i].Unlocked = save.AchievementUnlocked[i];
            for (int i = 0; i < Inventory.Count && i < save.InventoryQuantities.Count; i++) Inventory[i].Quantity = Math.Max(0, save.InventoryQuantities[i]);
            StateChanged?.Invoke();
        }
        public string MissionBoard()
        {
            var lines = new List<string>();
            for (int i = 0; i < Missions.Count; i++)
            {
                var m = Missions[i];
                lines.Add((m.Completed ? "[DONE] " : i == Chapter ? "[ACTIVE] " : "[LOCKED] ") + m.Title + " | +" + m.Reward + " кр.");
            }
            return string.Join("\n", lines);
        }

        public NetworkCampaign()
        {
            Missions.Add(new StoryMission
            {
                Id = "m1", Title = "Первый сигнал",
                Briefing = "Удержите доступность ≥ 70% на протяжении 3 тактов симуляции.",
                Kind = MissionKind.RestoreAvailability, Target = 3, Reward = 35
            });
            Missions.Add(new StoryMission
            {
                Id = "m2", Title = "Обходной канал",
                Briefing = "Постройте резервную линию между двумя узлами (режим строительства).",
                Kind = MissionKind.BuildLink, Target = 1, Reward = 50
            });
            Missions.Add(new StoryMission
            {
                Id = "m3", Title = "Возвращение узла",
                Briefing = "Запустите timed-ремонт аварийного маршрутизатора или коммутатора.",
                Kind = MissionKind.RepairDevice, Target = 1, Reward = 30
            });
            Missions.Add(new StoryMission
            {
                Id = "m4", Title = "Расширение магистрали",
                Briefing = "Увеличьте пропускную способность любого канала (upgrade).",
                Kind = MissionKind.UpgradeLink, Target = 1, Reward = 45
            });
            Missions.Add(new StoryMission
            {
                Id = "m5", Title = "Под давлением",
                Briefing = "Переживите аварийный сценарий и сохраните доступность ≥ 60% на следующем тике.",
                Kind = MissionKind.SurviveIncident, Target = 1, Reward = 60
            });
            Missions.Add(new StoryMission
            {
                Id = "m6", Title = "Новый протокол",
                Briefing = "Примените конфигурацию оборудования (VLAN / MTU / routing).",
                Kind = MissionKind.ConfigureDevice, Target = 1, Reward = 70
            });

            Achievements.Add(new Achievement { Id = "first_link", Title = "Архитектор", Description = "Первый построенный канал" });
            Achievements.Add(new Achievement { Id = "first_repair", Title = "Дежурная смена", Description = "Первый ремонт оборудования" });
            Achievements.Add(new Achievement { Id = "survivor", Title = "Антикризисный штаб", Description = "Сеть выдержала инцидент" });
            Achievements.Add(new Achievement { Id = "config", Title = "Инженер доступа", Description = "Первая применённая конфигурация" });
            Achievements.Add(new Achievement { Id = "campaign", Title = "Оператор города", Description = "Все сюжетные задания закрыты" });
            Achievements.Add(new Achievement { Id = "rank3", Title = "Senior NOC", Description = "Репутация 3+" });

            Inventory.Add(new InventoryItem
            {
                Id = "repair", Name = "Ремонтный комплект",
                Description = "+15 кредитов на полевой ремонт", Quantity = 2
            });
            Inventory.Add(new InventoryItem
            {
                Id = "reserve", Name = "Резервный трансивер",
                Description = "+25 кредитов на модернизацию линка", Quantity = 0
            });
            Inventory.Add(new InventoryItem
            {
                Id = "patch", Name = "Патч-корд 10G",
                Description = "+20 кредитов (универсальный расходник)", Quantity = 1
            });
        }

        private void Award(string id)
        {
            var a = Achievements.Find(x => x.Id == id);
            if (a != null && !a.Unlocked)
            {
                a.Unlocked = true;
                Reputation++;
                if (Reputation >= 3) Rank = Math.Max(Rank, 3);
                else if (Reputation >= 1) Rank = Math.Max(Rank, 2);
                Notification?.Invoke("Достижение: " + a.Title);
                StateChanged?.Invoke();
                if (Reputation >= 3) Award("rank3");
            }
        }

        public void OnAction(MissionKind kind, bool success = true)
        {
            if (!success) return;
            if (kind == MissionKind.BuildLink) Award("first_link");
            if (kind == MissionKind.RepairDevice) Award("first_repair");
            if (kind == MissionKind.SurviveIncident) Award("survivor");
            if (kind == MissionKind.ConfigureDevice) Award("config");

            var mission = Current;
            if (mission == null || mission.Kind != kind) return;
            mission.Progress++;
            CheckCompletion();
        }

        public void OnTick(NetworkSimulation simulation)
        {
            var mission = Current;
            if (mission == null) return;

            if (mission.Kind == MissionKind.RestoreAvailability)
            {
                mission.Progress = simulation.Availability >= 0.70f ? mission.Progress + 1 : 0;
                CheckCompletion();
            }
            else if (mission.Kind == MissionKind.SurviveIncident && simulation.LastScenario != IncidentScenario.None)
            {
                if (simulation.Availability >= 0.60f)
                {
                    mission.Progress = Math.Max(mission.Progress, 1);
                    CheckCompletion();
                }
            }
        }

        private void CheckCompletion()
        {
            var mission = Current;
            if (mission == null || mission.Progress < mission.Target) return;

            mission.Completed = true;
            CreditsAwarded?.Invoke(mission.Reward);
            Reputation++;
            Notification?.Invoke("Задание выполнено: " + mission.Title + " / +" + mission.Reward + " кр.");
            Chapter++;

            if (Chapter == 2)
            {
                var reserve = Inventory.Find(x => x.Id == "reserve");
                if (reserve != null) reserve.Quantity++;
            }
            if (Chapter == 4)
            {
                var patch = Inventory.Find(x => x.Id == "patch");
                if (patch != null) patch.Quantity++;
            }

            if (Current == null) Award("campaign");
            StateChanged?.Invoke();
        }

        public bool UseItem(string id, NetworkSimulation simulation)
        {
            var item = Inventory.Find(x => x.Id == id);
            if (item == null || item.Quantity < 1 || simulation.GameOver) return false;

            int amount = id == "repair" ? 15 : id == "reserve" ? 25 : id == "patch" ? 20 : 0;
            if (amount == 0) return false;

            item.Quantity--;
            StateChanged?.Invoke();
            simulation.GrantCredits(amount);
            Notification?.Invoke("Инвентарь: " + item.Name + " → +" + amount + " кр.");
            return true;
        }

        public string MissionText()
        {
            var m = Current;
            if (m == null) return ChapterTitle + "\nКАМПАНИЯ ЗАВЕРШЕНА\nРепутация: " + Reputation + " · Ранг: " + Rank;
            return ChapterTitle + "\n" + m.Title + "\n" + m.Briefing + "\nПрогресс: " + m.Progress + "/" + m.Target +
                   "\nНаграда: " + m.Reward + " кр.";
        }

        public string AchievementText()
        {
            var lines = new List<string>();
            foreach (var a in Achievements)
                lines.Add((a.Unlocked ? "[OK] " : "[  ] ") + a.Title);
            return string.Join("\n", lines);
        }

        public string InventoryText()
        {
            var lines = new List<string>();
            foreach (var item in Inventory)
                lines.Add(item.Name + " ×" + item.Quantity);
            return string.Join("\n", lines);
        }
    }
}
