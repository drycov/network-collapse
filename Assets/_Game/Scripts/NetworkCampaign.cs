using System;
using System.Collections.Generic;

namespace NetworkCollapse
{
    public enum MissionKind { RestoreAvailability, BuildLink, RepairDevice, UpgradeLink, SurviveIncident, ConfigureDevice }
    [Serializable] public sealed class StoryMission
    {
        public string Id, Title, Briefing;
        public MissionKind Kind;
        public int Target, Progress, Reward;
        public bool Completed;
    }
    [Serializable] public sealed class Achievement
    {
        public string Id, Title, Description;
        public bool Unlocked;
    }
    [Serializable] public sealed class InventoryItem
    {
        public string Id, Name, Description;
        public int Quantity;
    }
    // Campaign is level-independent: progression is driven by real player actions and routing ticks.
    public sealed class NetworkCampaign
    {
        public readonly List<StoryMission> Missions = new List<StoryMission>();
        public readonly List<Achievement> Achievements = new List<Achievement>();
        public readonly List<InventoryItem> Inventory = new List<InventoryItem>();
        public int Chapter { get; private set; }
        public string ChapterTitle => Chapter == 0 ? "ACT I / BLACKOUT" : Chapter == 1 ? "ACT II / THE BACKBONE" : "ACT III / SHADOW TRAFFIC";
        public StoryMission Current => Chapter < Missions.Count ? Missions[Chapter] : null;
        public event Action<string> Notification;
        public NetworkCampaign()
        {
            Missions.Add(new StoryMission { Id="m1", Title="Первый сигнал", Briefing="Удерживайте доступность сети не ниже 70% на 3 тактах.", Kind=MissionKind.RestoreAvailability, Target=3, Reward=30 });
            Missions.Add(new StoryMission { Id="m2", Title="Обходной канал", Briefing="Постройте резервную линию между двумя узлами.", Kind=MissionKind.BuildLink, Target=1, Reward=45 });
            Missions.Add(new StoryMission { Id="m3", Title="Возвращение узла", Briefing="Запустите ремонт аварийного маршрутизатора или коммутатора.", Kind=MissionKind.RepairDevice, Target=1, Reward=25 });
            Missions.Add(new StoryMission { Id="m4", Title="Расширение магистрали", Briefing="Увеличьте пропускную способность любого канала.", Kind=MissionKind.UpgradeLink, Target=1, Reward=40 });
            Missions.Add(new StoryMission { Id="m5", Title="Под давлением", Briefing="Переживите аварийный сценарий и сохраните доступность не ниже 60%.", Kind=MissionKind.SurviveIncident, Target=1, Reward=55 });
            Missions.Add(new StoryMission { Id="m6", Title="Новый протокол", Briefing="Примените конфигурацию оборудования.", Kind=MissionKind.ConfigureDevice, Target=1, Reward=60 });
            Achievements.Add(new Achievement { Id="first_link", Title="Архитектор", Description="Первый построенный канал" });
            Achievements.Add(new Achievement { Id="first_repair", Title="Дежурная смена", Description="Первый ремонт оборудования" });
            Achievements.Add(new Achievement { Id="survivor", Title="Антикризисный штаб", Description="Сеть выдержала инцидент" });
            Achievements.Add(new Achievement { Id="campaign", Title="Оператор города", Description="Завершены все сюжетные задания" });
            Inventory.Add(new InventoryItem { Id="repair", Name="Ремонтный комплект", Description="Начисляет 15 кредитов на ремонт", Quantity=1 });
            Inventory.Add(new InventoryItem { Id="reserve", Name="Резервный трансивер", Description="Начисляет 25 кредитов на модернизацию", Quantity=0 });
        }
        private void Award(string id)
        {
            var a = Achievements.Find(x => x.Id == id);
            if (a != null && !a.Unlocked) { a.Unlocked = true; Notification?.Invoke("Достижение: " + a.Title); }
        }
        public void OnAction(MissionKind kind, bool success = true)
        {
            if (!success) return;
            if (kind == MissionKind.BuildLink) Award("first_link");
            if (kind == MissionKind.RepairDevice) Award("first_repair");
            if (kind == MissionKind.SurviveIncident) Award("survivor");
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
                mission.Progress = simulation.Availability >= .70f ? mission.Progress + 1 : 0;
                CheckCompletion();
            }
        }
        private void CheckCompletion()
        {
            var mission = Current;
            if (mission == null || mission.Progress < mission.Target) return;
            mission.Completed = true;
            Notification?.Invoke("Задание выполнено: " + mission.Title + " / +" + mission.Reward + " кр.");
            Chapter++;
            if (Chapter == 2) Inventory.Find(x => x.Id == "reserve").Quantity++;
            if (Current == null) Award("campaign");
        }
        public bool UseItem(string id, NetworkSimulation simulation)
        {
            var item = Inventory.Find(x => x.Id == id);
            if (item == null || item.Quantity < 1 || simulation.GameOver) return false;
            int amount = id == "repair" ? 15 : id == "reserve" ? 25 : 0;
            if (amount == 0) return false;
            item.Quantity--;
            simulation.GrantCredits(amount);
            Notification?.Invoke("Использовано: " + item.Name + " / +" + amount + " кр.");
            return true;
        }
        public string MissionText()
        {
            var m = Current;
            return m == null ? "КАМПАНИЯ ЗАВЕРШЕНА" : ChapterTitle + "\n" + m.Title + "\n" + m.Briefing + "\nПрогресс: " + m.Progress + "/" + m.Target;
        }
        public string AchievementText()
        {
            var lines = new List<string>();
            foreach (var a in Achievements) lines.Add((a.Unlocked ? "[OK] " : "[--] ") + a.Title);
            return string.Join("\n", lines);
        }
        public string InventoryText()
        {
            var lines = new List<string>();
            foreach (var item in Inventory) lines.Add(item.Name + " x" + item.Quantity);
            return string.Join("\n", lines);
        }
    }
}
