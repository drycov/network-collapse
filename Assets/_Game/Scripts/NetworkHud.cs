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
        private ThemeStyleSheet runtimeTheme;
        private Label kpis, details, title, objectiveLabel, statusBar;
        private Button repair, failure, build, upgrade, repairLink, nextLevel;
        private Label connectionInfo, eventLog, missionLabel, storyLabel, achievementLabel, inventoryLabel;
        private VisualElement operationsTab, missionsTab, inventoryTab, deviceConfigSection;
        private string activeTab = "operations";
        private NetworkCampaign campaign;
        public event Action<string> UseInventoryRequested;
        private Button modeButton;
        public bool BuildMode { get; private set; }
        public int SelectedLinkId { get; private set; } = -1;
        public event Action<bool> BuildModeChanged;
        public event Action<EquipmentTier?> FilterChanged;
        public event Action<bool> RouteVisibilityChanged;
        private Button routeButton;
        private IntegerField vlanField, mtuField;
        private Button adminToggle, routingToggle, applyConfig;
        public event Action<int, int, int, bool, RoutingMode> ConfigureDeviceRequested;
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
            return screen.x < 340 || screen.x > Screen.width - 350;
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
                runtimeTheme = ScriptableObject.CreateInstance<ThemeStyleSheet>();
                panelSettings.themeStyleSheet = runtimeTheme;
                document.panelSettings = panelSettings;
            }

            var root = document.rootVisualElement;
            root.Clear();
            root.style.flexDirection = FlexDirection.Row;
            root.style.justifyContent = Justify.SpaceBetween;
            root.style.height = Length.Percent(100);
            root.style.paddingTop = 50;
            root.style.paddingBottom = 14;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.pickingMode = PickingMode.Ignore;

            var top = new VisualElement();
            top.style.position = Position.Absolute;
            top.style.left = 10;
            top.style.right = 10;
            top.style.top = 8;
            top.style.height = 36;
            top.style.backgroundColor = new Color(.02f, .09f, .12f, 1f);
            top.style.borderBottomWidth = 2;
            top.style.borderBottomColor = new Color(.13f, .72f, .78f);
            top.pickingMode = PickingMode.Ignore;
            statusBar = Heading("NETWORK COLLAPSE   /   OPERATIONS CONSOLE   /   SECURE LINK", 13);
            statusBar.style.marginLeft = 12;
            statusBar.style.marginTop = 8;
            top.Add(statusBar);
            root.Add(top);

            var left = Panel(328);
            root.Add(left);
            left.Add(Heading("NETWORK COLLAPSE", 20));
            left.Add(Heading("ACCESS NETWORK OPS", 11));
            kpis = Body("");
            left.Add(kpis);
            objectiveLabel = Body("");
            left.Add(objectiveLabel);

            var nav = new VisualElement();
            nav.style.flexDirection = FlexDirection.Row;
            nav.style.flexShrink = 0;
            nav.Add(TabButton("СЕТЬ", () => ShowTab("operations")));
            nav.Add(TabButton("МИССИИ", () => ShowTab("missions")));
            nav.Add(TabButton("СКЛАД", () => ShowTab("inventory")));
            left.Add(nav);
            nav.style.marginTop = 12;
            nav.style.marginBottom = 12;
            operationsTab = new VisualElement();
            missionsTab = new VisualElement();
            inventoryTab = new VisualElement();
            left.Add(operationsTab);
            left.Add(missionsTab);
            left.Add(inventoryTab);
            operationsTab.Add(Heading("УПРАВЛЕНИЕ", 15));
            modeButton = ActionButton("Режим строительства: выкл.", () => { BuildMode = !BuildMode; BuildModeChanged?.Invoke(BuildMode); Refresh(); });
            operationsTab.Add(modeButton);
            operationsTab.Add(Heading("СЛОИ ТОПОЛОГИИ", 14));
            operationsTab.Add(Body("ROUTER hex · L3 square · ACCESS ring\nCORE жёлтый · DIST синий · ACCESS зелёный\nPRIMARY жёлтый · BACKUP фиолетовый"));
            operationsTab.Add(ActionButton("Все категории", () => FilterChanged?.Invoke(null)));
            operationsTab.Add(ActionButton("Core", () => FilterChanged?.Invoke(EquipmentTier.Core)));
            operationsTab.Add(ActionButton("Distribution", () => FilterChanged?.Invoke(EquipmentTier.Distribution)));
            operationsTab.Add(ActionButton("Access", () => FilterChanged?.Invoke(EquipmentTier.Access)));
            routeButton = ActionButton("Маршруты: включены", () => { routesVisible = !routesVisible; RouteVisibilityChanged?.Invoke(routesVisible); Refresh(); });
            operationsTab.Add(routeButton);
            operationsTab.Add(ActionButton("Случайная авария  [SPACE]", () => RandomFailureRequested?.Invoke()));
            operationsTab.Add(ActionButton("Начать заново  [R]", () => ResetRequested?.Invoke()));
            nextLevel = ActionButton("Следующий уровень", () => NextLevelRequested?.Invoke());
            operationsTab.Add(nextLevel);

            operationsTab.Add(Heading("СЦЕНАРИИ АВАРИЙ", 14));
            operationsTab.Add(ActionButton("Обрыв волокна", () => IncidentRequested?.Invoke(IncidentScenario.FiberCut)));
            operationsTab.Add(ActionButton("Отказ питания", () => IncidentRequested?.Invoke(IncidentScenario.PowerFailure)));
            operationsTab.Add(ActionButton("Каскадный сбой", () => IncidentRequested?.Invoke(IncidentScenario.Cascade)));

            missionsTab.Add(Heading("MISSION CONTROL / СЮЖЕТ", 14));
            storyLabel = Body("");
            missionsTab.Add(storyLabel);
            missionLabel = Body("");
            missionsTab.Add(missionLabel);
            missionsTab.Add(Heading("ДОСТИЖЕНИЯ", 14));
            achievementLabel = Body("");
            missionsTab.Add(achievementLabel);
            inventoryTab.Add(Heading("ИНВЕНТАРЬ", 14));
            inventoryLabel = Body("");
            inventoryTab.Add(inventoryLabel);
            inventoryTab.Add(ActionButton("Ремонтный комплект", () => UseInventoryRequested?.Invoke("repair")));
            inventoryTab.Add(ActionButton("Резервный трансивер", () => UseInventoryRequested?.Invoke("reserve")));
            inventoryTab.Add(ActionButton("Патч-корд 10G", () => UseInventoryRequested?.Invoke("patch")));

            missionsTab.Add(Heading("ЖУРНАЛ СОБЫТИЙ", 14));
            eventLog = Body("");
            missionsTab.Add(eventLog);
            operationsTab.Add(Body("ЛКМ — узел/канал\nСтроительство — два узла\nПКМ/СКМ — панорама · F — центр"));

            var right = Panel(338);
            root.Add(right);
            title = Heading("ОБОРУДОВАНИЕ", 19);
            right.Add(title);
            details = Body("Выберите узел на карте.");
            right.Add(details);
            deviceConfigSection = new VisualElement();
            right.Add(deviceConfigSection);
            deviceConfigSection.Add(Heading("КОНФИГУРАЦИЯ УСТРОЙСТВА", 14));
            vlanField = new IntegerField("VLAN ID");
            mtuField = new IntegerField("MTU");
            deviceConfigSection.Add(vlanField);
            deviceConfigSection.Add(mtuField);
            adminToggle = ActionButton("Административно: включено", () => { if (SelectedId < 0) return; desiredAdmin = !desiredAdmin; UpdateConfigButtons(); });
            routingToggle = ActionButton("Routing: Static", () => { if (SelectedId < 0) return; desiredRouting = desiredRouting == RoutingMode.Static ? RoutingMode.OspfSimulated : RoutingMode.Static; UpdateConfigButtons(); });
            applyConfig = ActionButton("Применить настройки", () => ConfigureDeviceRequested?.Invoke(SelectedId, vlanField.value, mtuField.value, desiredAdmin, desiredRouting));
            deviceConfigSection.Add(adminToggle);
            deviceConfigSection.Add(routingToggle);
            deviceConfigSection.Add(applyConfig);
            failure = ActionButton("Создать аварию", () => DeviceAction?.Invoke(SelectedId));
            repair = ActionButton("Ремонт узла (15)", () => DeviceAction?.Invoke(SelectedId));
            right.Add(failure);
            right.Add(repair);
            right.Add(Heading("КАНАЛЫ СВЯЗИ", 15));
            connectionInfo = Body("Выберите узел, затем второй в режиме строительства.");
            right.Add(connectionInfo);
            build = ActionButton("Построить канал (35)", () => BuildLinkRequested?.Invoke(SelectedId, TargetId));
            upgrade = ActionButton("Улучшить канал (25)", () => UpgradeLinkRequested?.Invoke(ActiveLink()));
            repairLink = ActionButton("Ремонт канала (12)", () => RepairLinkRequested?.Invoke(ActiveLink()));
            right.Add(build);
            right.Add(upgrade);
            right.Add(repairLink);
            ShowTab(activeTab);
            Refresh();
            if (eventLog != null) eventLog.text = string.Join("\n", events);
        }

        private static VisualElement Panel(float width)
        {
            var panel = new ScrollView(ScrollViewMode.Vertical);
            panel.style.width = width;
            panel.style.paddingLeft = 14;
            panel.style.paddingRight = 14;
            panel.style.paddingTop = 14;
            panel.style.paddingBottom = 14;
            panel.style.height = Length.Percent(100);
            panel.style.maxHeight = Length.Percent(100);
            panel.style.flexShrink = 0;
            // Fully opaque terminal panels (no alpha glass)
            panel.style.backgroundColor = new Color(.012f, .05f, .07f, 1f);
            panel.style.borderLeftWidth = 3;
            panel.style.borderLeftColor = new Color(.10f, .65f, .72f);
            panel.style.borderTopWidth = 1;
            panel.style.borderTopColor = new Color(.08f, .39f, .48f);
            panel.style.borderRightWidth = 1;
            panel.style.borderRightColor = new Color(.08f, .39f, .48f);
            panel.style.borderBottomWidth = 1;
            panel.style.borderBottomColor = new Color(.08f, .39f, .48f);
            panel.style.borderTopLeftRadius = panel.style.borderTopRightRadius = 0;
            panel.style.borderBottomLeftRadius = panel.style.borderBottomRightRadius = 0;
            return panel;
        }

        private static Label Heading(string text, int size)
        {
            var label = new Label(text);
            label.style.color = new Color(.37f, .96f, .86f);
            label.style.fontSize = size;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = 10;
            label.style.flexShrink = 0;
            return label;
        }

        private static Label Body(string text)
        {
            var label = new Label(text);
            label.style.color = new Color(.72f, .88f, .86f);
            label.style.fontSize = 13;
            label.style.lineHeight = 19;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.flexShrink = 0;
            label.style.marginBottom = 10;
            return label;
        }

        private static Button ActionButton(string text, Action action)
        {
            var button = new Button(action) { text = text };
            button.style.height = 34;
            button.style.flexShrink = 0;
            button.style.marginBottom = 7;
            button.style.backgroundColor = new Color(.04f, .16f, .19f, 1f);
            button.style.color = new Color(.55f, 1f, .87f);
            button.style.borderLeftWidth = 2;
            button.style.borderLeftColor = new Color(.13f, .77f, .68f);
            button.style.borderTopLeftRadius = 0;
            button.style.borderTopRightRadius = 0;
            button.style.borderBottomLeftRadius = 0;
            button.style.borderBottomRightRadius = 0;
            return button;
        }

        private static Button TabButton(string title, Action action)
        {
            var button = ActionButton(title, action);
            button.style.flexGrow = 1;
            button.style.fontSize = 10;
            button.style.marginRight = 3;
            return button;
        }

        private void ShowTab(string tab)
        {
            activeTab = tab;
            if (operationsTab != null) operationsTab.style.display = tab == "operations" ? DisplayStyle.Flex : DisplayStyle.None;
            if (missionsTab != null) missionsTab.style.display = tab == "missions" ? DisplayStyle.Flex : DisplayStyle.None;
            if (inventoryTab != null) inventoryTab.style.display = tab == "inventory" ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetCampaign(NetworkCampaign value)
        {
            campaign = value;
            Refresh();
        }

        public void AddEvent(string message)
        {
            events.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
            if (events.Count > 8) events.RemoveAt(events.Count - 1);
            if (eventLog != null) eventLog.text = string.Join("\n", events);
        }

        public void SetBuildMode(bool value)
        {
            BuildMode = value;
            BuildModeChanged?.Invoke(value);
            Refresh();
        }

        private bool desiredAdmin = true;
        private RoutingMode desiredRouting = RoutingMode.Static;
        private void UpdateConfigButtons()
        {
            adminToggle.text = desiredAdmin ? "Административно: включено" : "Административно: выключено";
            routingToggle.text = "Routing: " + desiredRouting;
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
            if (model != null && id >= 0 && id < model.Nodes.Count)
            {
                var node = model.Nodes[id];
                vlanField.SetValueWithoutNotify(node.VlanId);
                mtuField.SetValueWithoutNotify(node.Mtu);
                desiredAdmin = node.AdminEnabled;
                desiredRouting = node.Routing;
                UpdateConfigButtons();
            }
            Refresh();
        }

        public void Refresh()
        {
            if (model == null || kpis == null) return;

            if (campaign != null)
            {
                storyLabel.text = campaign.StoryBriefing;
                missionLabel.text = campaign.MissionText();
                achievementLabel.text = campaign.AchievementText();
                inventoryLabel.text = campaign.InventoryText();
                statusBar.text =
                    $"NETWORK COLLAPSE  /  RANK {campaign.Rank}  /  REP {campaign.Reputation}  /  LVL {model.Level:00}  /  {(model.GameOver ? "CRITICAL" : "LINK ACTIVE")}";
            }
            else
            {
                statusBar.text =
                    $"NETWORK COLLAPSE  /  NETOPS  /  LVL {model.Level:00}  /  {(model.GameOver ? "CRITICAL" : "LINK ACTIVE")}";
            }

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
            if (deviceConfigSection != null) deviceConfigSection.style.display = valid ? DisplayStyle.Flex : DisplayStyle.None;
            vlanField.SetEnabled(valid && !model.GameOver);
            mtuField.SetEnabled(valid && !model.GameOver);
            adminToggle.SetEnabled(valid && !model.GameOver);
            routingToggle.SetEnabled(valid && !model.GameOver && model.Nodes[SelectedId].Role != DeviceRole.Layer2Switch);
            applyConfig.SetEnabled(valid && !model.GameOver);
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
                ? $"Канал #{index}: N{model.Links[index].A:00} ↔ N{model.Links[index].B:00}\nНагрузка: {model.Links[index].Load:0.0}/{model.Links[index].Capacity:0.0}\nСостояние: {(model.Links[index].Failed ? "Авария" : "Работает")}\nПостроен игроком: {(model.Links[index].PlayerBuilt ? "Да" : "Нет")}"
                : !pair
                    ? "Включите режим строительства и выберите два узла."
                    : index < 0
                        ? $"N{SelectedId:00} → N{TargetId:00}: канала нет"
                        : $"N{SelectedId:00} ↔ N{TargetId:00}: {model.Links[index].Load:0.0}/{model.Links[index].Capacity:0.0}";

            if (!valid)
            {
                title.text = SelectedLinkId >= 0 ? "КАНАЛ СВЯЗИ" : "ОБОРУДОВАНИЕ";
                details.text = SelectedLinkId >= 0
                    ? "Линия связи выбрана. Upgrade / ремонт доступны ниже."
                    : "Выберите узел.\n\nСюжетные задания — слева в Mission Control.";
                return;
            }

            var node = model.Nodes[SelectedId];
            title.text = $"{node.Hostname} · {node.Role}";
            string repairInfo = node.RepairRemaining > 0f ? $"\nРемонт: {node.RepairRemaining:0.0} с" : "";
            string routes = model.PrimaryPaths.TryGetValue(node.Id, out var primary)
                ? $"\nОсновной: {primary.Count} hop, {model.PrimaryTraffic[node.Id]:0.0} Mbps"
                : "\nОсновной: недоступен";
            if (model.BackupPaths.TryGetValue(node.Id, out var alternate) && model.BackupTraffic.TryGetValue(node.Id, out var amount))
                routes += $"\nРезерв/ECMP: {alternate.Count} hop, {amount:0.0} Mbps";
            else
                routes += "\nРезерв: отсутствует";

            details.text =
                $"Tier: {node.Tier} | IP: {node.ManagementIp}\n" +
                $"VLAN: {node.VlanId} | MTU: {node.Mtu} | Routing: {node.Routing}\n" +
                $"Состояние: {node.Status}{repairInfo}\n" +
                $"Нагрузка: {node.Load:0.0} / {node.Capacity:0.0}\n" +
                $"Demand: {node.Demand:0.0}" + routes;
        }

        private void OnDestroy()
        {
            if (panelSettings != null) Destroy(panelSettings);
            if (runtimeTheme != null) Destroy(runtimeTheme);
        }
    }
}
