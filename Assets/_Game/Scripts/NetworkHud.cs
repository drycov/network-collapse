using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NetworkCollapse
{
    public sealed class NetworkHud : MonoBehaviour
    {
        private NetworkSimulation model;
        private UIDocument document;
        private PanelSettings panelSettings;
        private Label kpis, details, title, objectiveLabel;
        private Button repair, failure, build, upgrade, repairLink, nextLevel;
        private Label connectionInfo, eventLog;
        private Button modeButton;
        public bool BuildMode { get; private set; }
        public int SelectedLinkId { get; private set; } = -1;
        public event Action<bool> BuildModeChanged;
        public event Action<EquipmentTier?> FilterChanged;
        public event Action<bool> RouteVisibilityChanged;
        private Button routeButton;
        private bool routesVisible = true;
        private readonly System.Collections.Generic.List<string> events = new System.Collections.Generic.List<string>();

        public event Action<int, int> BuildLinkRequested;
        public event Action<int> UpgradeLinkRequested;
        public event Action<int> RepairLinkRequested;
        public event Action<IncidentScenario> IncidentRequested;
        public event Action NextLevelRequested;
        public int TargetId { get; private set; } = -1;
        public event Action<int> DeviceAction;
        public event Action RandomFailureRequested;
        public event Action ResetRequested;
        public int SelectedId { get; private set; } = -1;

        public bool IsPointerOverHud(Vector2 screen)
        {
            return screen.x < 300 || screen.x > Screen.width - 310;
        }

        public void Initialize(NetworkSimulation simulation)
        {
            model = simulation;
            BuildMode = false;
            SelectedLinkId = -1;
            if (document == null)
            {
                document = gameObject.AddComponent<UIDocument>();
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
                document.panelSettings = panelSettings;
            }

            var root = document.rootVisualElement;
            root.Clear();
            root.style.flexDirection = FlexDirection.Row;
            root.style.justifyContent = Justify.SpaceBetween;
            root.style.height = Length.Percent(100);
            root.style.paddingTop = 14;
            root.style.paddingBottom = 14;
            root.style.paddingLeft = 14;
            root.style.paddingRight = 14;
            root.pickingMode = PickingMode.Ignore;

            var left = Panel(290);
            root.Add(left);
            left.Add(Heading("NETWORK COLLAPSE", 20));
            left.Add(Heading("СЕТЕВОЙ ОПЕРАТОР", 11));
            kpis = Body("");
            left.Add(kpis);
            objectiveLabel = Body("");
            left.Add(objectiveLabel);
            left.Add(Heading("УПРАВЛЕНИЕ", 15));
            modeButton = ActionButton("Режим строительства: выкл.", () => { BuildMode = !BuildMode; BuildModeChanged?.Invoke(BuildMode); Refresh(); });
            left.Add(modeButton);
            left.Add(Heading("ФИЛЬТР УЗЛОВ", 14));
            left.Add(ActionButton("Все категории", () => FilterChanged?.Invoke(null)));
            left.Add(ActionButton("Core", () => FilterChanged?.Invoke(EquipmentTier.Core)));
            left.Add(ActionButton("Distribution", () => FilterChanged?.Invoke(EquipmentTier.Distribution)));
            left.Add(ActionButton("Access", () => FilterChanged?.Invoke(EquipmentTier.Access)));
            routeButton = ActionButton("Маршруты: включены", () => { routesVisible = !routesVisible; RouteVisibilityChanged?.Invoke(routesVisible); Refresh(); });
            left.Add(routeButton);
            left.Add(ActionButton("Случайная авария  [SPACE]", () => RandomFailureRequested?.Invoke()));
            left.Add(ActionButton("Начать заново  [R]", () => ResetRequested?.Invoke()));
            nextLevel = ActionButton("Следующий уровень", () => NextLevelRequested?.Invoke());
            left.Add(nextLevel);
            left.Add(Heading("СЦЕНАРИИ АВАРИЙ", 14));
            left.Add(ActionButton("Обрыв волокна", () => IncidentRequested?.Invoke(IncidentScenario.FiberCut)));
            left.Add(ActionButton("Отказ питания", () => IncidentRequested?.Invoke(IncidentScenario.PowerFailure)));
            left.Add(ActionButton("Каскадный сбой", () => IncidentRequested?.Invoke(IncidentScenario.Cascade)));
            left.Add(Heading("ЖУРНАЛ СОБЫТИЙ", 14));
            eventLog = Body("");
            left.Add(eventLog);
            left.Add(Body("ЛКМ — выбрать узел или канал\nРежим строительства — выбрать два узла\nПКМ/СКМ — панорама\nКолесо — масштаб · F — центр"));

            var right = Panel(300);
            root.Add(right);
            title = Heading("ОБОРУДОВАНИЕ", 19);
            right.Add(title);
            details = Body("Выберите узел на карте.");
            right.Add(details);
            failure = ActionButton("Создать аварию", () => DeviceAction?.Invoke(SelectedId));
            repair = ActionButton("Ремонт узла (15)", () => DeviceAction?.Invoke(SelectedId));
            right.Add(failure);
            right.Add(repair);
            right.Add(Heading("КАНАЛЫ СВЯЗИ", 15));
            connectionInfo = Body("Выберите узел, затем B и второй узел.");
            right.Add(connectionInfo);
            build = ActionButton("Построить канал (35)", () => BuildLinkRequested?.Invoke(SelectedId, TargetId));
            upgrade = ActionButton("Улучшить канал (25)", () => UpgradeLinkRequested?.Invoke(ActiveLink()));
            repairLink = ActionButton("Ремонт канала (12)", () => RepairLinkRequested?.Invoke(ActiveLink()));
            right.Add(build);
            right.Add(upgrade);
            right.Add(repairLink);
            Refresh();
            if (eventLog != null) eventLog.text = string.Join("\n", events);
        }

        private static VisualElement Panel(float width)
        {
            var panel = new ScrollView(ScrollViewMode.Vertical);
            panel.style.width = width;
            panel.style.paddingLeft = 16;
            panel.style.paddingRight = 16;
            panel.style.paddingTop = 17;
            panel.style.paddingBottom = 17;
            panel.style.maxHeight = Length.Percent(96);
            panel.style.backgroundColor = new Color(.045f, .073f, .12f, .94f);
            panel.style.borderTopLeftRadius = panel.style.borderTopRightRadius = 12;
            panel.style.borderBottomLeftRadius = panel.style.borderBottomRightRadius = 12;
            return panel;
        }

        private static Label Heading(string text, int size)
        {
            var label = new Label(text);
            label.style.color = new Color(.52f, .86f, 1f);
            label.style.fontSize = size;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = 12;
            return label;
        }

        private static Label Body(string text)
        {
            var label = new Label(text);
            label.style.color = new Color(.85f, .92f, 1f);
            label.style.fontSize = 13;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 12;
            return label;
        }

        private static Button ActionButton(string text, Action action)
        {
            var button = new Button(action) { text = text };
            button.style.height = 36;
            button.style.marginBottom = 8;
            button.style.backgroundColor = new Color(.09f, .22f, .32f);
            button.style.color = Color.white;
            return button;
        }

        public void AddEvent(string message)
        {
            events.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
            if (events.Count > 6) events.RemoveAt(events.Count - 1);
            if (eventLog != null) eventLog.text = string.Join("\n", events);
        }

        public void SetBuildMode(bool value)
        {
            BuildMode = value;
            BuildModeChanged?.Invoke(value);
            Refresh();
        }

        public void SelectLink(int index)
        {
            SelectedId = -1;
            TargetId = -1;
            SelectedLinkId = index;
            Refresh();
        }

        public void SelectTarget(int id)
        {
            TargetId = id;
            Refresh();
        }

        private int FindSelectedLink() => model == null ? -1 : model.FindLink(SelectedId, TargetId);
        private int ActiveLink() => SelectedLinkId >= 0 ? SelectedLinkId : FindSelectedLink();

        public void Select(int id)
        {
            TargetId = -1;
            SelectedLinkId = -1;
            SelectedId = id;
            Refresh();
        }

        public void Refresh()
        {
            if (model == null || kpis == null) return;
            modeButton.text = BuildMode ? "Режим строительства: ВКЛ" : "Режим строительства: выкл.";
            routeButton.text = routesVisible ? "Маршруты: включены" : "Маршруты: скрыты";

            string extra = model.GameOver ? "\n⚠ СЕТЬ РУХНУЛА" :
                model.ObjectiveComplete ? "\n✓ Цель достигнута" : "";

            kpis.text =
                $"Уровень {model.Level} · Очки {model.Score}\n" +
                $"Доступность: {model.Availability:P1}\n" +
                $"Бюджет: {model.Money}\n" +
                $"Аварии: узлы {model.FailedNodes} / линки {model.FailedLinks}\n" +
                $"Потеря: {model.DroppedTraffic:0.0} · Тик {model.TickCount}{extra}";

            float prog = model.ObjectiveTimer > 0f ? model.ObjectiveProgress / model.ObjectiveTimer : 0f;
            objectiveLabel.text =
                $"Цель: ≥ {model.TargetAvailability:P0} доступности\n" +
                $"Прогресс: {prog:P0} ({model.ObjectiveProgress:0}/{model.ObjectiveTimer:0} с)";

            nextLevel.style.display = model.ObjectiveComplete && !model.GameOver ? DisplayStyle.Flex : DisplayStyle.None;
            failure.SetEnabled(!model.GameOver);
            build.SetEnabled(false);
            upgrade.SetEnabled(false);
            repairLink.SetEnabled(false);

            bool valid = SelectedId >= 0 && SelectedId < model.Nodes.Count;
            failure.style.display = valid && !model.Nodes[SelectedId].Failed ? DisplayStyle.Flex : DisplayStyle.None;
            repair.style.display = valid && model.Nodes[SelectedId].Failed ? DisplayStyle.Flex : DisplayStyle.None;

            bool canStartRepair = valid && model.Money >= NetworkSimulation.RepairCost &&
                                  model.Nodes[SelectedId].RepairRemaining <= 0f;
            repair.SetEnabled(canStartRepair && !model.GameOver);
            if (valid && model.Nodes[SelectedId].Failed && model.Nodes[SelectedId].RepairRemaining > 0f)
                repair.text = $"Ремонт… {model.Nodes[SelectedId].RepairRemaining:0.0}с";
            else
                repair.text = $"Ремонт узла ({NetworkSimulation.RepairCost})";

            int index = ActiveLink();
            bool pair = (valid && TargetId >= 0 && TargetId != SelectedId) || SelectedLinkId >= 0;
            build.SetEnabled(!model.GameOver && valid && pair && index < 0 && model.Money >= NetworkSimulation.BuildCost);
            upgrade.SetEnabled(!model.GameOver && pair && index >= 0 && !model.Links[index].Failed &&
                               model.Money >= NetworkSimulation.UpgradeCost && model.Links[index].Capacity < 240);

            bool linkRepairing = pair && index >= 0 && model.Links[index].Failed && model.Links[index].RepairRemaining > 0f;
            repairLink.SetEnabled(!model.GameOver && pair && index >= 0 && model.Links[index].Failed &&
                                  model.Money >= NetworkSimulation.LinkRepairCost && !linkRepairing);
            if (linkRepairing)
                repairLink.text = $"Ремонт канала… {model.Links[index].RepairRemaining:0.0}с";
            else
                repairLink.text = $"Ремонт канала ({NetworkSimulation.LinkRepairCost})";

            connectionInfo.text = SelectedLinkId >= 0 && index >= 0 && index < model.Links.Count
                ? $"Канал #{index}: N{model.Links[index].A:00} ↔ N{model.Links[index].B:00}\nНагрузка: {model.Links[index].Load:0.0}/{model.Links[index].Capacity:0.0}\nСостояние: {(model.Links[index].Failed ? "Авария" : "Работает")}\nПостроен игроком: {(model.Links[index].PlayerBuilt ? "Да" : "Нет")}" : !pair
                ? "Нажмите B и выберите второй узел."
                : index < 0
                    ? $"N{SelectedId:00} → N{TargetId:00}: канала нет"
                    : $"N{SelectedId:00} ↔ N{TargetId:00}: {model.Links[index].Load:0.0}/{model.Links[index].Capacity:0.0}";

            if (!valid)
            {
                title.text = SelectedLinkId >= 0 ? "КАНАЛ СВЯЗИ" : "ОБОРУДОВАНИЕ";
                details.text = SelectedLinkId >= 0 ? "Линия связи выбрана. Управление пропускной способностью и ремонт доступны ниже." : "Выберите узел на карте.\n\nДержите доступность выше цели, чтобы пройти уровень.";
                return;
            }

            var node = model.Nodes[SelectedId];
            title.text = $"УЗЕЛ N{node.Id:00} · {node.Tier}";
            string repairInfo = node.RepairRemaining > 0f ? $"\nРемонт: {node.RepairRemaining:0.0} с" : "";
            details.text =
                $"Состояние: {node.Status}{repairInfo}\n" +
                $"Нагрузка: {node.Load:0.0} / {node.Capacity:0.0}\n" +
                $"Потребление: {node.Demand:0.0}\n" +
                $"Соединений: {model.Links.FindAll(l => l.A == node.Id || l.B == node.Id).Count}";
        }

        private void OnDestroy()
        {
            if (panelSettings != null) Destroy(panelSettings);
        }
    }
}
