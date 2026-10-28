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
        private Label kpis, details, title;
        private Button repair, failure, build, upgrade, repairLink;
        private Label connectionInfo;
        public event Action<int, int> BuildLinkRequested;
        public event Action<int> UpgradeLinkRequested;
        public event Action<int> RepairLinkRequested;
        public event Action<IncidentScenario> IncidentRequested;
        public int TargetId { get; private set; } = -1;
        public event Action<int> DeviceAction;
        public event Action RandomFailureRequested;
        public event Action ResetRequested;
        public int SelectedId { get; private set; } = -1;
        public bool IsPointerOverHud(Vector2 screen)
        {
            // Panels occupy the left 286px and right 292px at desktop scale.
            return screen.x < 300 || screen.x > Screen.width - 310;
        }

        public void Initialize(NetworkSimulation simulation)
        {
            model = simulation;
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

            var left = Panel(282);
            root.Add(left);
            left.Add(Heading("NETWORK COLLAPSE", 20));
            left.Add(Heading("СЕТЕВОЙ ОПЕРАТОР", 11));
            kpis = Body("");
            left.Add(kpis);
            left.Add(Heading("УПРАВЛЕНИЕ", 15));
            left.Add(ActionButton("Случайная авария  [SPACE]", () => RandomFailureRequested?.Invoke()));
            left.Add(ActionButton("Начать заново  [R]", () => ResetRequested?.Invoke()));
            left.Add(Heading("СЦЕНАРИИ АВАРИЙ", 15));
            left.Add(ActionButton("Обрыв волокна", () => IncidentRequested?.Invoke(IncidentScenario.FiberCut)));
            left.Add(ActionButton("Отказ питания", () => IncidentRequested?.Invoke(IncidentScenario.PowerFailure)));
            left.Add(ActionButton("Каскадный сбой", () => IncidentRequested?.Invoke(IncidentScenario.Cascade)));
            left.Add(Body("ЛКМ — выбрать устройство\nПКМ / СКМ — панорама\nКолесо — масштаб\nF — центрировать карту"));

            var right = Panel(286);
            root.Add(right);
            title = Heading("ОБОРУДОВАНИЕ", 19);
            right.Add(title);
            details = Body("Выберите узел на карте.");
            right.Add(details);
            failure = ActionButton("Создать аварию", () => DeviceAction?.Invoke(SelectedId));
            repair = ActionButton("Восстановить (15)", () => DeviceAction?.Invoke(SelectedId));
            right.Add(failure);
            right.Add(repair);
            right.Add(Heading("КАНАЛЫ СВЯЗИ", 15));
            connectionInfo = Body("Выберите узел, затем нажмите B и выберите второй.");
            right.Add(connectionInfo);
            build = ActionButton("Построить канал (35)", () => BuildLinkRequested?.Invoke(SelectedId, TargetId));
            upgrade = ActionButton("Улучшить канал (25)", () => UpgradeLinkRequested?.Invoke(FindSelectedLink()));
            repairLink = ActionButton("Восстановить канал (15)", () => RepairLinkRequested?.Invoke(FindSelectedLink()));
            right.Add(build);
            right.Add(upgrade);
            right.Add(repairLink);
            Refresh();
        }

        private static VisualElement Panel(float width)
        {
            var panel = new VisualElement();
            panel.style.width = width;
            panel.style.paddingLeft = 16;
            panel.style.paddingRight = 16;
            panel.style.paddingTop = 17;
            panel.style.paddingBottom = 17;
            panel.style.maxHeight = Length.Percent(96);
            panel.style.overflow = Overflow.Hidden;
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
            label.style.marginBottom = 14;
            return label;
        }

        private static Label Body(string text)
        {
            var label = new Label(text);
            label.style.color = new Color(.85f, .92f, 1f);
            label.style.fontSize = 14;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 24;
            return label;
        }

        private static Button ActionButton(string text, Action action)
        {
            var button = new Button(action) { text = text };
            button.style.height = 39;
            button.style.marginBottom = 10;
            button.style.backgroundColor = new Color(.09f, .22f, .32f);
            button.style.color = Color.white;
            return button;
        }

        public void SelectTarget(int id)
        {
            TargetId = id;
            Refresh();
        }

        private int FindSelectedLink() => model == null ? -1 : model.FindLink(SelectedId, TargetId);

        public void Select(int id)
        {
            TargetId = -1;
            SelectedId = id;
            Refresh();
        }

        public void Refresh()
        {
            if (model == null || kpis == null) return;
            kpis.text = $"Доступность: {model.Availability:P1}\nБюджет: {model.Money}\nАварий: {model.FailedNodes}\nПотеря трафика: {model.DroppedTraffic:0.0}\nТик симуляции: {model.TickCount}";
            bool valid = SelectedId >= 0 && SelectedId < model.Nodes.Count;
            failure.style.display = valid && !model.Nodes[SelectedId].Failed ? DisplayStyle.Flex : DisplayStyle.None;
            repair.style.display = valid && model.Nodes[SelectedId].Failed ? DisplayStyle.Flex : DisplayStyle.None;
            repair.SetEnabled(valid && model.Money >= NetworkSimulation.RepairCost);
            int index = FindSelectedLink();
            bool pair = valid && TargetId >= 0 && TargetId != SelectedId;
            build.SetEnabled(pair && index < 0 && model.Money >= NetworkSimulation.BuildCost);
            upgrade.SetEnabled(pair && index >= 0 && !model.Links[index].Failed && model.Money >= NetworkSimulation.UpgradeCost && model.Links[index].Capacity < 240);
            repairLink.SetEnabled(pair && index >= 0 && model.Links[index].Failed && model.Money >= NetworkSimulation.RepairCost);
            connectionInfo.text = !pair ? "Нажмите B и выберите второй узел." : index < 0 ? $"N{SelectedId:00} → N{TargetId:00}: канал отсутствует" : $"N{SelectedId:00} ↔ N{TargetId:00}: {model.Links[index].Load:0.0}/{model.Links[index].Capacity:0.0} Mbps";
            if (!valid)
            {
                title.text = "ОБОРУДОВАНИЕ";
                details.text = "Выберите узел на карте.";
                return;
            }
            var node = model.Nodes[SelectedId];
            title.text = $"УЗЕЛ N{node.Id:00}";
            details.text = $"Категория: {node.Tier}\nСостояние: {node.Status}\nНагрузка: {node.Load:0.0} / {node.Capacity:0.0}\nПотребление: {node.Demand:0.0}\nСоединений: {model.Links.FindAll(l => l.A == node.Id || l.B == node.Id).Count}\n\nДействия применяются только кнопкой ниже.";
        }

        private void OnDestroy() { if (panelSettings != null) Destroy(panelSettings); }
    }
}
