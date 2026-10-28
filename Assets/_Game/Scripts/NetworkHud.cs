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
        private Button repair, failure;
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
            if (document != null) Destroy(document);
            document = gameObject.AddComponent<UIDocument>();
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            document.panelSettings = panelSettings;
            var root = document.rootVisualElement;
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

        public void Select(int id)
        {
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
            repair.SetEnabled(valid && model.Money >= 15);
            if (!valid)
            {
                title.text = "ОБОРУДОВАНИЕ";
                details.text = "Выберите узел на карте.";
                return;
            }
            var node = model.Nodes[SelectedId];
            title.text = $"УЗЕЛ N{node.Id:00}";
            details.text = $"Состояние: {node.Status}\nНагрузка: {node.Load:0.0} / {node.Capacity:0.0}\nПотребление: {node.Demand:0.0}\nСоединений: {model.Links.FindAll(l => l.A == node.Id || l.B == node.Id).Count}\n\nДействия применяются только кнопкой ниже.";
        }

        private void OnDestroy() { if (panelSettings != null) Destroy(panelSettings); }
    }
}
