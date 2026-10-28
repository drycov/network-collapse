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
        private Label kpis, details, title, objectiveLabel, statusBar, alertBanner, footerStatus;
        private VisualElement endingOverlay;
        private Label endingTitle, endingDescription;
        private ProgressBar availabilityBar, objectiveBar, inspectorLoad;
        private Button pauseButton;
        private VisualElement mainMenu, mainMenuCard, leftDock, rightDock;
        private bool menuOpen;
        public bool MenuOpen => menuOpen;
        public event Action ContinueGameRequested;
        public event Action NewGameRequested;
        public event Action QuitGameRequested;
        private VisualElement dialogueOverlay, settingsTab;
        private Label tutorialLabel, briefingText;
        private Slider volumeSlider;
        private Toggle muteToggle;
        public event Action<float> VolumeChanged;
        public event Action<bool> MuteChanged;
        public event Action<bool> PerformanceModeChanged;
        public event Action PauseRequested;
        public event Action MenuRequested;
        private Button repair, failure, build, upgrade, repairLink, nextLevel;
        private Label connectionInfo, eventLog, missionLabel, storyLabel, achievementLabel, inventoryLabel, boardLabel, incidentLabel, dialogueLabel;
        public event Action<int> UpgradeDeviceRequested;
        public event Action ExpandNetworkRequested;
        public event Action AcquireCustomersRequested;
        public event Action<int> DiagnoseRequested;
        public event Action ScanNetworkRequested;
        private VisualElement operationsTab, missionsTab, inventoryTab, deviceConfigSection, marketTab;
        private Label marketSummary;
        private IspWorld world;
        public event Action<int> CityExpansionRequested;
        public event Action<int, int> ContractRequested;
        public event Action<int> RivalAcquisitionRequested;
        public event Action<int> SubsidiaryRepairRequested;
        public event Action<int, int> RivalNegotiationRequested;
        private Label worldMapLabel, bgpTableLabel;
        private VisualElement geographicMap;
        private VisualElement windowLayer, currentWindow;
        public bool HasWindowOpen => windowLayer != null && windowLayer.style.display == DisplayStyle.Flex;
        private ScrollView windowContent;
        public event Action<int> PartnerConnectionRequested;
        private string activeTab = "operations";
        private int previewMission = -1;
        private VisualElement missionPicker;
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

        public event Action<int, int, LinkMedium, int> EngineeredBuildRequested;
        public LinkMedium SelectedMedium { get; private set; } = LinkMedium.Fiber;
        public int SelectedSpeed { get; private set; } = 100;
        private Button mediumButton, speedButton;
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
            if (menuOpen || (windowLayer != null && windowLayer.resolvedStyle.display != DisplayStyle.None)) return true;
            bool inspectorVisible = rightDock != null && rightDock.resolvedStyle.display != DisplayStyle.None;
            return screen.y > Screen.height - 52 || screen.x < (Screen.width < 1100 ? Mathf.Clamp(Screen.width * .24f, 195, 270) + 18 : 350) ||
                   (inspectorVisible && screen.x > Screen.width - (Screen.width < 1100 ? Mathf.Clamp(Screen.width * .24f, 195, 270) + 18 : 360));
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
            root.RegisterCallback<GeometryChangedEvent>(evt => AdjustLayout(root));
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
            var menuButton = ActionButton("МЕНЮ", () => MenuRequested?.Invoke());
            menuButton.style.position = Position.Absolute;
            menuButton.style.right = 10;
            menuButton.style.top = 2;
            menuButton.style.width = 110;
            menuButton.style.height = 30;
            top.Add(menuButton);
            alertBanner = Heading("", 12);
            alertBanner.style.position = Position.Absolute;
            alertBanner.style.top = 45;
            alertBanner.style.left = 350;
            alertBanner.style.right = 360;
            alertBanner.style.unityTextAlign = TextAnchor.MiddleCenter;
            alertBanner.style.backgroundColor = new Color(.06f, .17f, .20f, .95f);
            alertBanner.style.paddingTop = 5;
            alertBanner.style.paddingBottom = 5;
            root.Add(alertBanner);

            var left = Panel(328);
            leftDock = left;
            root.Add(left);
            left.Add(Heading("NETWORK COLLAPSE", 20));
            left.Add(Heading("ACCESS NETWORK OPS", 11));
            kpis = Body("");
            left.Add(kpis);
            availabilityBar = MakeProgress("ДОСТУПНОСТЬ"); left.Add(availabilityBar);
            objectiveBar = MakeProgress("ЦЕЛЬ УРОВНЯ"); left.Add(objectiveBar);
            objectiveLabel = Body("");
            left.Add(objectiveLabel);

            var nav = new VisualElement();
            nav.style.flexDirection = FlexDirection.Row;
            nav.style.flexShrink = 0;
            nav.style.minHeight = 34;
            nav.Add(TabButton("СЕТЬ", () => ShowTab("operations")));
            nav.Add(TabButton("МИССИИ", () => ShowTab("missions")));
            nav.Add(TabButton("СКЛАД", () => ShowTab("inventory")));
            nav.Add(TabButton("РЫНОК", () => ShowTab("market")));
            nav.Add(TabButton("НАСТР.", () => ShowTab("settings")));
            left.Add(nav);
            nav.style.marginTop = 12;
            nav.style.marginBottom = 12;
            operationsTab = new VisualElement();
            missionsTab = new VisualElement();
            inventoryTab = new VisualElement();
            left.Add(operationsTab);
            left.Add(missionsTab);
            left.Add(inventoryTab);
            marketTab = new VisualElement(); left.Add(marketTab);
            marketTab.style.flexShrink = 0;
            marketTab.Add(Heading("МИРОВОЙ РЫНОК ISP", 15));
            marketSummary = Body(""); marketTab.Add(marketSummary);
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
            foreach (var tab in new[] { operationsTab, missionsTab, inventoryTab })
            {
                tab.style.flexShrink = 0;
                tab.style.flexGrow = 0;
                tab.style.minHeight = StyleKeyword.Auto;
            }
            operationsTab.Add(Heading("РАЗВИТИЕ ISP", 15));
            operationsTab.Add(ActionButton("Открыть новый район / построить узел", () => ExpandNetworkRequested?.Invoke()));
            operationsTab.Add(ActionButton("Рекламная кампания / привлечь клиентов (20)", () => AcquireCustomersRequested?.Invoke()));
            operationsTab.Add(ActionButton("NOC / СКАНИРОВАТЬ ВСЮ СЕТЬ", () => ScanNetworkRequested?.Invoke()));
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
            pauseButton = ActionButton("ПАУЗА  [P]", () => PauseRequested?.Invoke());
            operationsTab.Add(pauseButton);
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
            missionsTab.Add(ActionButton("ОТКРЫТЬ БРИФИНГ", () => ShowBriefing()));
            missionLabel = Body("");
            missionsTab.Add(missionLabel);
            missionsTab.Add(Heading("ДОСКА ЗАДАНИЙ", 14));
            boardLabel = Body(""); missionsTab.Add(boardLabel);
            missionPicker = new VisualElement();
            missionPicker.style.flexShrink = 0;
            missionsTab.Add(missionPicker);
            missionsTab.Add(Heading("СВЯЗЬ С ДИСПЕТЧЕРСКОЙ", 14));
            dialogueLabel = Body(""); missionsTab.Add(dialogueLabel);
            missionsTab.Add(Heading("КАРТА ИНЦИДЕНТОВ", 14));
            incidentLabel = Body(""); missionsTab.Add(incidentLabel);
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
            rightDock = right;
            root.Add(right);
            title = Heading("ОБОРУДОВАНИЕ", 19);
            right.Add(title);
            details = Body("Выберите узел на карте.");
            right.Add(details);
            inspectorLoad = MakeProgress("ЗАГРУЗКА ОБЪЕКТА");
            right.Add(inspectorLoad);
            deviceConfigSection = new VisualElement();
            deviceConfigSection.style.flexShrink = 0;
            right.Add(deviceConfigSection);
            deviceConfigSection.Add(ActionButton("ДИАГНОСТИКА НЕИСПРАВНОСТЕЙ", () => DiagnoseRequested?.Invoke(SelectedId)));
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
            deviceConfigSection.Add(ActionButton("Модернизировать оборудование (40)", () => UpgradeDeviceRequested?.Invoke(SelectedId)));
            failure = ActionButton("Создать аварию", () => DeviceAction?.Invoke(SelectedId));
            repair = ActionButton("Ремонт узла (15)", () => DeviceAction?.Invoke(SelectedId));
            right.Add(failure);
            right.Add(repair);
            right.Add(Heading("КАНАЛЫ СВЯЗИ", 15));
            connectionInfo = Body("Выберите узел, затем второй в режиме строительства.");
            right.Add(connectionInfo);
            right.Add(Heading("ПРОЕКТ ЛИНИИ / МАТЕРИАЛЫ", 14));
            mediumButton = ActionButton("Среда: оптика", () => { SelectedMedium = (LinkMedium)(((int)SelectedMedium + 1) % 3); Refresh(); });
            speedButton = ActionButton("Скорость: 100 Mbps", () => { SelectedSpeed = SelectedSpeed == 10 ? 50 : SelectedSpeed == 50 ? 100 : SelectedSpeed == 100 ? 150 : SelectedSpeed == 150 ? 500 : SelectedSpeed == 500 ? 1000 : 10; Refresh(); });
            right.Add(mediumButton);
            right.Add(speedButton);
            build = ActionButton("Построить канал", () => EngineeredBuildRequested?.Invoke(SelectedId, TargetId, SelectedMedium, SelectedSpeed));
            upgrade = ActionButton("Улучшить канал (25)", () => UpgradeLinkRequested?.Invoke(ActiveLink()));
            repairLink = ActionButton("Ремонт канала (12)", () => RepairLinkRequested?.Invoke(ActiveLink()));
            right.Add(build);
            right.Add(upgrade);
            right.Add(repairLink);
            endingOverlay = new VisualElement();
            endingOverlay.style.position = Position.Absolute;
            endingOverlay.style.left = Length.Percent(35);
            endingOverlay.style.top = Length.Percent(29);
            endingOverlay.style.width = Length.Percent(30);
            endingOverlay.style.paddingTop = 22;
            endingOverlay.style.paddingBottom = 22;
            endingOverlay.style.paddingLeft = 22;
            endingOverlay.style.paddingRight = 22;
            endingOverlay.style.backgroundColor = new Color(.015f, .08f, .11f, .98f);
            endingOverlay.style.borderTopWidth = 2;
            endingOverlay.style.borderTopColor = new Color(.28f, .89f, .78f);
            endingTitle = Heading("", 21); endingOverlay.Add(endingTitle);
            endingDescription = Body(""); endingOverlay.Add(endingDescription);
            endingOverlay.Add(ActionButton("НАЧАТЬ НОВУЮ СМЕНУ", () => ResetRequested?.Invoke()));
            root.Add(endingOverlay);
            mainMenu = new VisualElement();
            mainMenu.style.position = Position.Absolute;
            mainMenu.style.left = 0; mainMenu.style.right = 0;
            mainMenu.style.top = 0; mainMenu.style.bottom = 0;
            mainMenu.style.backgroundColor = new Color(.006f, .025f, .04f, .97f);
            mainMenu.style.justifyContent = Justify.Center;
            mainMenu.style.alignItems = Align.Center;
            mainMenuCard = new VisualElement();
            mainMenuCard.style.width = 440;
            mainMenuCard.style.maxWidth = Length.Percent(90);
            mainMenuCard.style.paddingTop = 30; mainMenuCard.style.paddingBottom = 30;
            mainMenuCard.style.paddingLeft = 32; mainMenuCard.style.paddingRight = 32;
            mainMenuCard.style.backgroundColor = new Color(.025f, .1f, .13f, 1f);
            mainMenuCard.style.borderLeftWidth = 3;
            mainMenuCard.style.borderLeftColor = new Color(.27f, .82f, .75f);
            mainMenuCard.Add(Heading("NETWORK COLLAPSE", 28));
            mainMenuCard.Add(Body("ISP STRATEGY  /  OPERATIONS & EXPANSION"));
            mainMenuCard.Add(ActionButton("ПРОДОЛЖИТЬ КАМПАНИЮ", () => { ContinueGameRequested?.Invoke(); OpenMenu(false); }));
            mainMenuCard.Add(ActionButton("НОВАЯ ИГРА", () => { NewGameRequested?.Invoke(); OpenMenu(false); }));
            mainMenuCard.Add(ActionButton("НАСТРОЙКИ", () => { OpenMenu(false); ShowTab("settings"); }));
            mainMenuCard.Add(ActionButton("ВЫЙТИ ИЗ ИГРЫ", () => QuitGameRequested?.Invoke()));
            mainMenu.Add(mainMenuCard); root.Add(mainMenu);
            dialogueOverlay = new VisualElement();
            dialogueOverlay.style.position = Position.Absolute;
            dialogueOverlay.style.left = Length.Percent(24);
            dialogueOverlay.style.top = Length.Percent(19);
            dialogueOverlay.style.width = Length.Percent(52);
            dialogueOverlay.style.paddingLeft = 22;
            dialogueOverlay.style.paddingRight = 22;
            dialogueOverlay.style.paddingTop = 22;
            dialogueOverlay.style.paddingBottom = 22;
            dialogueOverlay.style.backgroundColor = new Color(.012f, .07f, .09f, .99f);
            dialogueOverlay.Add(Heading("ВХОДЯЩЕЕ СООБЩЕНИЕ / NOC", 20));
            briefingText = Body(""); dialogueOverlay.Add(briefingText);
            dialogueOverlay.Add(ActionButton("ПРИНЯТЬ ЗАДАНИЕ", () => dialogueOverlay.style.display = DisplayStyle.None));
            root.Add(dialogueOverlay);
            dialogueOverlay.style.display = DisplayStyle.None;
            tutorialLabel = Heading("", 13);
            tutorialLabel.style.position = Position.Absolute;
            tutorialLabel.style.left = Length.Percent(27);
            tutorialLabel.style.right = Length.Percent(27);
            tutorialLabel.style.bottom = 37;
            tutorialLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            root.Add(tutorialLabel);
            footerStatus = Body("");
            footerStatus.style.position = Position.Absolute;
            footerStatus.style.bottom = 3;
            footerStatus.style.left = 360;
            footerStatus.style.right = 360;
            footerStatus.style.unityTextAlign = TextAnchor.MiddleCenter;
            root.Add(footerStatus);
            CreateWindowLayer(root);
            ShowTab(activeTab);
            OpenMenu(menuOpen);
            Refresh();
            if (eventLog != null) eventLog.text = string.Join("\n", events);
        }

        private static ProgressBar MakeProgress(string label)
        {
            var bar = new ProgressBar { title = label };
            bar.lowValue = 0;
            bar.highValue = 100;
            bar.style.height = 26;
            bar.style.marginBottom = 9;
            bar.style.flexShrink = 0;
            bar.style.color = new Color(.45f, .98f, .84f);
            return bar;
        }

        public void SetPaused(bool paused)
        {
            if (pauseButton != null) pauseButton.text = paused ? "ПРОДОЛЖИТЬ  [P]" : "ПАУЗА  [P]";
            if (footerStatus != null) footerStatus.text = paused ? "● СИМУЛЯЦИЯ ПРИОСТАНОВЛЕНА" : "● ОПЕРАТОР НА ЛИНИИ   |   ЛКМ: ВЫБОР   |   КОЛЕСО: МАСШТАБ   |   F: ЦЕНТР";
        }

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
            panel.contentContainer.style.flexShrink = 0;
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
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.flexShrink = 0;
            label.style.flexGrow = 0;
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

        public void OpenMenu(bool open)
        {
            menuOpen = open;
            if (mainMenu != null) mainMenu.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (dialogueOverlay != null && open) dialogueOverlay.style.display = DisplayStyle.None;
            if (windowLayer != null) windowLayer.style.display = open || activeTab == "operations" ? DisplayStyle.None : DisplayStyle.Flex;
            if (leftDock != null) leftDock.style.display = open ? DisplayStyle.None : DisplayStyle.Flex;
            if (rightDock != null) rightDock.style.display = open || (SelectedId < 0 && SelectedLinkId < 0 && !BuildMode) ? DisplayStyle.None : DisplayStyle.Flex;
        }
        public void ToggleMenu() => OpenMenu(!menuOpen);
        public void CloseGameWindow() => ShowTab("operations");
        public void ShowInspector(bool show)
        {
            if (rightDock != null && !menuOpen) rightDock.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetTutorial(string instruction)
        {
            if (tutorialLabel != null) tutorialLabel.text = instruction ?? "";
        }
        public void ShowBriefing()
        {
            if (dialogueOverlay == null || campaign == null) return;
            briefingText.text = campaign.ChapterTitle + "\n\n" + campaign.StoryBriefing + "\n\n" + campaign.MissionText();
            dialogueOverlay.style.display = DisplayStyle.Flex;
        }
        private void AdjustLayout(VisualElement root)
        {
            if (root.resolvedStyle.width < 200) return;
            float width = root.resolvedStyle.width;
            float panelWidth = width < 1100 ? Mathf.Clamp(width * .24f, 195, 270) : 328;
            var children = root.Children();
            foreach (var element in children)
                if (element is ScrollView scroll) scroll.style.width = panelWidth;
            if (statusBar != null) statusBar.style.fontSize = width < 950 ? 10 : 13;
        }

        public void SelectScreen(int screen)
        {
            ShowTab(screen == 1 ? "missions" : screen == 2 ? "inventory" : "operations");
        }

        private void CreateWindowLayer(VisualElement root)
        {
            windowLayer = new VisualElement();
            windowLayer.style.position = Position.Absolute;
            windowLayer.style.left = 0; windowLayer.style.right = 0;
            windowLayer.style.top = 0; windowLayer.style.bottom = 0;
            windowLayer.style.justifyContent = Justify.Center;
            windowLayer.style.alignItems = Align.Center;
            windowLayer.style.backgroundColor = new Color(0f, .01f, .025f, .62f);
            windowContent = new ScrollView(ScrollViewMode.Vertical);
            windowContent.style.width = Length.Percent(100);
            windowContent.style.flexGrow = 1;
            windowContent.style.flexShrink = 1;
            windowContent.style.minHeight = 0;
            windowContent.style.overflow = Overflow.Hidden;
            windowContent.contentContainer.style.flexShrink = 0;
            windowContent.contentContainer.style.width = Length.Percent(100);
            windowContent.style.paddingLeft = 16;
            windowContent.style.paddingRight = 16;
            windowContent.style.paddingBottom = 16;
            var frame = new VisualElement();
            currentWindow = frame;
            frame.style.width = Length.Percent(72);
            frame.style.maxWidth = 860;
            frame.style.height = Length.Percent(82);
            frame.style.maxHeight = Length.Percent(88);
            frame.style.overflow = Overflow.Hidden;
            frame.style.backgroundColor = new Color(.015f, .06f, .085f, 1f);
            frame.style.borderTopWidth = 2;
            frame.style.borderTopColor = new Color(.24f, .78f, .71f);
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.paddingLeft = 16;
            header.style.paddingTop = 12;
            header.style.paddingRight = 16;
            header.style.height = 52;
            var caption = Heading("ОКНО ОПЕРАЦИЙ", 17);
            caption.name = "window-title";
            header.Add(caption);
            header.Add(ActionButton("ЗАКРЫТЬ  ×", () => ShowTab("operations")));
            frame.Add(header);
            frame.Add(windowContent);
            windowLayer.Add(frame);
            root.Add(windowLayer);
            windowLayer.style.display = DisplayStyle.None;
        }

        private void ShowTab(string tab)
        {
            activeTab = tab;
            if (operationsTab != null) operationsTab.style.display = DisplayStyle.Flex;
            foreach (var panel in new[] { missionsTab, inventoryTab, marketTab, settingsTab })
                if (panel != null) panel.style.display = DisplayStyle.None;
            if (windowLayer == null) return;
            if (tab == "operations")
            {
                windowLayer.style.display = DisplayStyle.None;
                return;
            }
            VisualElement content = tab == "missions" ? missionsTab :
                tab == "inventory" ? inventoryTab :
                tab == "market" ? marketTab : settingsTab;
            if (content == null) return;
            windowContent.Clear();
            windowContent.Add(content);
            content.style.display = DisplayStyle.Flex;
            content.style.flexShrink = 0;
            content.style.width = Length.Percent(100);
            var caption = currentWindow.Q<Label>("window-title");
            if (caption != null) caption.text = tab == "missions" ? "MISSION CONTROL" :
                tab == "inventory" ? "ИНВЕНТАРЬ И РЕСУРСЫ" :
                tab == "market" ? "СТРАТЕГИЧЕСКАЯ КАРТА / РЫНОК" : "НАСТРОЙКИ";
            windowLayer.style.display = menuOpen ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void SetWorld(IspWorld value)
        {
            world = value;
            if (marketTab == null || world == null) return;
            marketTab.Clear();
            marketTab.Add(Heading("МИРОВОЙ РЫНОК ISP", 15));
            marketSummary = Body(""); marketTab.Add(marketSummary);
            marketTab.Add(Heading("ГЕОГРАФИЯ / ГОРОДА", 14));
            geographicMap = new VisualElement();
            geographicMap.style.height = 180;
            geographicMap.style.flexShrink = 0;
            geographicMap.style.backgroundColor = new Color(.02f, .12f, .16f, 1f);
            geographicMap.style.borderBottomWidth = 1;
            geographicMap.style.borderBottomColor = new Color(.15f, .65f, .68f);
            marketTab.Add(geographicMap);
            for (int i = 0; i < world.Cities.Count; i++)
            {
                var city = world.Cities[i];
                int index = i;
                var point = new Button(() => CityExpansionRequested?.Invoke(index)) { text = "●" };
                point.tooltip = city.Country + " / " + city.Name + " / " + city.Population + " жителей";
                point.style.position = Position.Absolute;
                point.style.left = Length.Percent(Mathf.Clamp(city.MapPosition.x / 70f * 100f, 3, 91));
                point.style.top = Length.Percent(Mathf.Clamp(city.MapPosition.y / 33f * 100f, 5, 85));
                point.style.width = 21; point.style.height = 21;
                point.style.fontSize = 15;
                point.style.paddingLeft = 0; point.style.paddingTop = 0;
                point.style.backgroundColor = city.Coverage > 0 ? new Color(.06f, .4f, .3f) : new Color(.12f, .22f, .28f);
                point.style.color = city.Coverage > 0 ? new Color(.52f, 1f, .72f) : new Color(.8f, .87f, .92f);
                geographicMap.Add(point);
            }
            foreach (var rival in world.Rivals)
            {
                if (rival.Acquired || rival.NetworkSites == null) continue;
                foreach (var site in rival.NetworkSites)
                {
                    var marker = new Label("◆");
                    marker.tooltip = rival.Name + " / AS" + rival.Asn;
                    marker.style.position = Position.Absolute;
                    marker.style.left = Length.Percent(Mathf.Clamp(site.x / 70f * 100f, 2f, 94f));
                    marker.style.top = Length.Percent(Mathf.Clamp(site.y / 33f * 100f, 3f, 88f));
                    marker.style.color = new Color(1f, .45f, .48f);
                    marker.style.fontSize = 11;
                    marker.pickingMode = PickingMode.Ignore;
                    geographicMap.Add(marker);
                }
            }
            worldMapLabel = Body(""); marketTab.Add(worldMapLabel);
            marketTab.Add(Heading("ГОРОДА И ПОКРЫТИЕ", 14));
            for (int i = 0; i < world.Cities.Count; i++)
            {
                int index = i;
                marketTab.Add(ActionButton(world.Cities[i].Name + " / открыть", () => CityExpansionRequested?.Invoke(index)));
            }
            marketTab.Add(Heading("ДОГОВОРЫ И ТАРИФЫ", 14));
            for (int i = 0; i < world.Cities.Count; i++)
                for (int j = 0; j < world.Tariffs.Count; j++)
                {
                    int city = i, tariff = j;
                    marketTab.Add(ActionButton(world.Cities[i].Name + " · " + world.Tariffs[j].Name,
                        () => ContractRequested?.Invoke(city, tariff)));
                }
            marketTab.Add(Heading("СЕТИ КОНКУРЕНТОВ / ПОГЛОЩЕНИЯ", 14));
            for (int i = 0; i < world.Rivals.Count; i++)
            {
                int index = i;
                marketTab.Add(ActionButton("Переговоры: " + world.Rivals[i].Name + " / 120%", () => RivalNegotiationRequested?.Invoke(index, 120)));
                marketTab.Add(ActionButton("Выкупить " + world.Rivals[i].Name, () => RivalAcquisitionRequested?.Invoke(index)));
            }
            marketTab.Add(Heading("ТРАНЗИТ / ПИРИНГ / BGP", 14));
            for (int i = 0; i < world.Partners.Count; i++)
            {
                int index = i;
                marketTab.Add(ActionButton(world.Partners[i].Name + " / AS" + world.Partners[i].Asn,
                    () => PartnerConnectionRequested?.Invoke(index)));
            }
            marketTab.Add(Heading("ДОЧЕРНИЕ ОПЕРАТОРЫ", 14));
            if (world.Subsidiaries != null)
                foreach (var company in world.Subsidiaries)
                {
                    int subsidiaryIndex = world.Subsidiaries.IndexOf(company);
                    marketTab.Add(Body(company.Name + " · AS" + company.OriginalAsn +
                        "\nПлощадок: " + company.SiteCount + " · Клиентов: " + company.CustomerCount +
                        "\nТехслужба: " + company.TechnicalStaff + " · Содержание: " + company.MaintenancePerTick + " кр./тик" +
                        "\nУнаследованный транзит: " + (company.InheritedTransit == null ? 0 : company.InheritedTransit.Count) +
                        " · Маршрутов: " + (company.InheritedRoutes == null ? 0 : company.InheritedRoutes.Count)));
                    marketTab.Add(ActionButton("ОТПРАВИТЬ РЕМОНТНУЮ БРИГАДУ · " + company.Name,
                        () => SubsidiaryRepairRequested?.Invoke(subsidiaryIndex)));
                }
            marketTab.Add(Heading("BGP / RIB", 14));
            bgpTableLabel = Body(""); marketTab.Add(bgpTableLabel);
            Refresh();
        }

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
            if (rightDock != null && !menuOpen) rightDock.style.display = SelectedId >= 0 || SelectedLinkId >= 0 || BuildMode ? DisplayStyle.Flex : DisplayStyle.None;
            if (world != null && marketSummary != null)
            {
                marketSummary.text = world.Summary() + "\n" + world.MarketNews;
                if (worldMapLabel != null)
                {
                    var regions = new System.Collections.Generic.List<string>();
                    foreach (var city in world.Cities)
                        regions.Add(city.Country + " / " + city.Name + " [" + city.MapPosition.x.ToString("0") + "," + city.MapPosition.y.ToString("0") + "] " + (city.Coverage > 0 ? "ONLINE" : "CLOSED"));
                    worldMapLabel.text = string.Join("\n", regions);
                }
                if (bgpTableLabel != null) bgpTableLabel.text = world.BgpSummary();
            }
            availabilityBar.value = model.Availability * 100f;
            objectiveBar.value = model.ObjectiveTimer <= 0 ? 0 : Mathf.Clamp01(model.ObjectiveProgress / model.ObjectiveTimer) * 100f;
            alertBanner.text = model.GameOver ? "КРИТИЧЕСКАЯ ПОТЕРЯ СВЯЗНОСТИ" : model.FailedNodes + model.FailedLinks > 0 ? "АКТИВНЫЕ АВАРИИ: " + (model.FailedNodes + model.FailedLinks) : "СЕТЬ РАБОТАЕТ ШТАТНО";
            alertBanner.style.color = model.GameOver ? new Color(1f, .32f, .32f) : model.FailedNodes + model.FailedLinks > 0 ? new Color(1f, .69f, .3f) : new Color(.36f, .95f, .79f);
            endingOverlay.style.display = model.GameOver ? DisplayStyle.Flex : DisplayStyle.None;
            endingTitle.text = model.GameOver ? "NETWORK COLLAPSE" : "";
            endingDescription.text = model.GameOver ? "Сеть потеряла устойчивость. Восстановите управление, начав новую смену.\nИтоговый счёт: " + model.Score : "";

            if (campaign != null)
            {
                storyLabel.text = campaign.StoryBriefing;
                missionLabel.text = campaign.MissionText();
                boardLabel.text = campaign.MissionBoard();
                if (previewMission >= 0 && previewMission < campaign.Missions.Count)
                {
                    var mission = campaign.Missions[previewMission];
                    boardLabel.text += "\n\n> " + mission.Title + "\n" + mission.Briefing + "\nПрогресс: " + mission.Progress + "/" + mission.Target +
                        (mission.Completed ? "\nЗАВЕРШЕНО" : previewMission == campaign.Chapter ? "\nАКТИВНО" : "\nЗАБЛОКИРОВАНО");
                }
                dialogueLabel.text = campaign.StoryBriefing;
                var incidents = new System.Collections.Generic.List<string>();
                foreach (var incidentNode in model.Nodes) if (incidentNode.Failed) incidents.Add("[NODE] " + incidentNode.Hostname + " · " + incidentNode.Status);
                for (int i = 0; i < model.Links.Count; i++) if (model.Links[i].Failed) incidents.Add("[LINK] #" + i + " N" + model.Links[i].A + " ↔ N" + model.Links[i].B);
                incidentLabel.text = incidents.Count == 0 ? "NO ACTIVE INCIDENTS" : string.Join("\n", incidents);
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
                $"Оператор: {model.OperatorScale}\nКлиенты: {model.Customers} · Узлы: {model.Nodes.Count}\n" +
                $"Уровень {model.Level} · Очки {model.Score}\n" +
                $"Доступность: {model.Availability:P1}\n" +
                $"Бюджет: {model.Money} · Приобретено площадок: {model.AcquiredSites}\n" +
                $"Обязательства: {model.AcquiredMaintenance} кр./тик\n" +
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
            bool selectedLinkValid = SelectedLinkId >= 0 && SelectedLinkId < model.Links.Count;
            if (inspectorLoad != null)
            {
                var capacity = selectedLinkValid ? model.Links[SelectedLinkId].Capacity : valid ? model.Nodes[SelectedId].Capacity : 0f;
                var load = selectedLinkValid ? model.Links[SelectedLinkId].Load : valid ? model.Nodes[SelectedId].Load : 0f;
                inspectorLoad.value = capacity > 0 ? Mathf.Clamp01(load / capacity) * 100f : 0f;
                inspectorLoad.style.display = selectedLinkValid || valid ? DisplayStyle.Flex : DisplayStyle.None;
            }
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
            string buildError = valid && TargetId >= 0 ? model.BuildBlockReason(SelectedId, TargetId, SelectedMedium, SelectedSpeed) : "Выберите два узла";
            mediumButton.text = "Среда: " + (SelectedMedium == LinkMedium.Fiber ? "Оптика" : SelectedMedium == LinkMedium.Copper ? "Медь" : "Радиоканал");
            speedButton.text = "Скорость: " + SelectedSpeed + " Mbps";
            int quote = valid && TargetId >= 0 ? model.GetBuildCost(SelectedId, TargetId, SelectedMedium, SelectedSpeed) : NetworkSimulation.BuildCost;
            build.text = "ЗАКАЗАТЬ МОНТАЖ (" + quote + " кр.)";
            build.SetEnabled(valid && TargetId >= 0 && buildError == null);
            upgrade.SetEnabled(!model.GameOver && pair && index >= 0 && !model.Links[index].Failed &&
                               model.Money >= NetworkSimulation.UpgradeCost && model.Links[index].Capacity < 240);

            bool linkRepairing = pair && index >= 0 && model.Links[index].Failed && model.Links[index].RepairRemaining > 0f;
            repairLink.SetEnabled(!model.GameOver && pair && index >= 0 && model.Links[index].Failed &&
                                  model.Money >= model.GetLinkRepairCost(index) && !linkRepairing);
            if (linkRepairing)
                repairLink.text = $"Ремонт канала… {model.Links[index].RepairRemaining:0.0}с";
            else
                repairLink.text = $"Ремонт канала ({(index >= 0 ? model.GetLinkRepairCost(index) : NetworkSimulation.LinkRepairCost)})";

            connectionInfo.text = SelectedLinkId >= 0 && index >= 0 && index < model.Links.Count
                ? $"Канал #{index}: N{model.Links[index].A:00} ↔ N{model.Links[index].B:00}\nНагрузка: {model.Links[index].Load:0.0}/{model.Links[index].Capacity:0.0}\nСостояние: {(model.Links[index].ConstructionRemaining > 0f ? "Монтаж " + model.Links[index].ConstructionRemaining.ToString("0.0") + "с" : model.Links[index].Failed ? "Авария" : "Работает")}\nСреда: {model.Links[index].Medium} · Порты: {model.Links[index].PortA}/{model.Links[index].PortB}\nМатериалы: {model.Links[index].MaterialCost} кр.\nПостроен игроком: {(model.Links[index].PlayerBuilt ? "Да" : "Нет")}"
                : !pair
                    ? "Включите режим строительства и выберите два узла."
                    : index < 0
                        ? $"ПРОЕКТ: N{SelectedId:00} → N{TargetId:00}\nДлина: {Vector2.Distance(model.Nodes[SelectedId].Position, model.Nodes[TargetId].Position):0.0} ед.\nСмета: {quote} кр.\nПорты: {model.UsedPorts(SelectedId)}/{model.PortLimit(SelectedId)} и {model.UsedPorts(TargetId)}/{model.PortLimit(TargetId)}\nСтатус: {(buildError ?? "Готов к строительству")}"
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
                (string.IsNullOrEmpty(node.OriginOperator) ? "" : $"Приобретён у: {node.OriginOperator} / AS{node.OriginAsn}\n") +
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
