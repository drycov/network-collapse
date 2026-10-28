using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NetworkCollapse
{
    public sealed partial class NetworkHud
    {
        private void RenderMarket()
        {
            if (marketTab == null || world == null) return;
            marketTab.Clear();
            marketTab.style.width = Length.Percent(100);
            marketTab.style.flexShrink = 0;
            marketTab.Add(Heading("STRATEGY / ISP MARKET", 17));
            marketSummary = Body(world.Summary());
            marketSummary.style.backgroundColor = new Color(.055f, .13f, .16f);
            marketSummary.style.paddingTop = 12;
            marketSummary.style.paddingBottom = 12;
            marketSummary.style.paddingLeft = 12;
            marketSummary.style.paddingRight = 12;
            marketTab.Add(marketSummary);
            var tabs = new VisualElement();
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.style.flexWrap = Wrap.Wrap;
            marketTab.Add(tabs);
            MarketTab(tabs, "ГОРОДА", "cities");
            MarketTab(tabs, "ТАРИФЫ", "contracts");
            MarketTab(tabs, "M&A", "rivals");
            MarketTab(tabs, "ТРАНЗИТ / BGP", "bgp");
            marketTab.Add(Body(world.MarketNews ?? ""));
            if (marketMode == "cities")
            {
                marketTab.Add(Heading("КАРТА ПОКРЫТИЯ", 15));
                geographicMap = new VisualElement();
                geographicMap.style.height = 230;
                geographicMap.style.flexShrink = 0;
                geographicMap.style.backgroundColor = new Color(.025f, .11f, .15f, 1f);
                geographicMap.style.borderBottomWidth = 1;
                geographicMap.style.borderBottomColor = new Color(.2f, .6f, .6f);
                marketTab.Add(geographicMap);
                foreach (var city in world.Cities)
                {
                    int index = world.Cities.IndexOf(city);
                    var marker = new Button(() => { marketCityIndex = index; RenderMarket(); }) { text = city.Coverage > 0 ? "●" : "○" };
                    marker.tooltip = city.Country + " / " + city.Name;
                    marker.style.position = Position.Absolute;
                    marker.style.left = Length.Percent(Mathf.Clamp(city.MapPosition.x / 70f * 100f, 3, 91));
                    marker.style.top = Length.Percent(Mathf.Clamp(city.MapPosition.y / 33f * 100f, 5, 85));
                    marker.style.width = 25; marker.style.height = 25;
                    marker.style.color = city.Coverage > 0 ? new Color(.45f, 1f, .72f) : new Color(.78f, .88f, .9f);
                    marker.style.backgroundColor = new Color(.04f, .23f, .29f);
                    geographicMap.Add(marker);
                }
                foreach (var rival in world.Rivals)
                {
                    if (rival.Acquired || rival.NetworkSites == null) continue;
                    foreach (var site in rival.NetworkSites)
                    {
                        var marker = new Label("◆");
                        marker.tooltip = rival.Name + " / AS" + rival.Asn;
                        marker.style.position = Position.Absolute;
                        marker.style.left = Length.Percent(Mathf.Clamp(site.x / 70f * 100f, 2, 94));
                        marker.style.top = Length.Percent(Mathf.Clamp(site.y / 33f * 100f, 3, 88));
                        marker.style.color = new Color(1f, .5f, .5f);
                        marker.pickingMode = PickingMode.Ignore;
                        geographicMap.Add(marker);
                    }
                }
                marketTab.Add(Heading("ГОРОДА", 15));
                var cityRow = new VisualElement();
                cityRow.style.flexDirection = FlexDirection.Row;
                cityRow.style.flexWrap = Wrap.Wrap;
                marketTab.Add(cityRow);
                for (int i = 0; i < world.Cities.Count; i++)
                {
                    int selected = i;
                    var button = ActionButton(world.Cities[i].Name, () => { marketCityIndex = selected; RenderMarket(); });
                    button.style.minWidth = 100; button.style.marginRight = 6;
                    cityRow.Add(button);
                }
                if (world.Cities.Count > 0)
                {
                    marketCityIndex = Mathf.Clamp(marketCityIndex, 0, world.Cities.Count - 1);
                    var city = world.Cities[marketCityIndex];
                    var card = SettingsSection(city.Name + " / " + city.Country, 
                        "Население: " + city.Population + "   ·   Клиенты: " + city.Customers +
                        "\nПокрытие: " + (city.Coverage > 0 ? "Активно" : "Не открыто"));
                    marketTab.Add(card);
                    int chosen = marketCityIndex;
                    card.Add(ActionButton(city.Coverage > 0 ? "ПОКРЫТИЕ ОТКРЫТО" : "ОТКРЫТЬ ПОКРЫТИЕ",
                        () => CityExpansionRequested?.Invoke(chosen)));
                }
            }
            else if (marketMode == "contracts")
            {
                marketTab.Add(Heading("ПОДКЛЮЧЕНИЕ АБОНЕНТОВ", 15));
                marketTab.Add(Body("Выберите город, затем тариф. Отображаются только подключённые территории."));
                foreach (var city in world.Cities)
                {
                    if (city.Coverage <= 0) continue;
                    int cityIndex = world.Cities.IndexOf(city);
                    var card = SettingsSection(city.Name, "Клиентов: " + city.Customers);
                    marketTab.Add(card);
                    foreach (var tariff in world.Tariffs)
                    {
                        int tariffIndex = world.Tariffs.IndexOf(tariff);
                        card.Add(ActionButton(tariff.Name + " · " + tariff.SpeedMbps + " Mbps · " + tariff.MonthlyPrice + " кр.",
                            () => ContractRequested?.Invoke(cityIndex, tariffIndex)));
                    }
                }
            }
            else if (marketMode == "rivals")
            {
                marketTab.Add(Heading("КОНКУРЕНТЫ И ПОГЛОЩЕНИЯ", 15));
                foreach (var rival in world.Rivals)
                {
                    if (rival.Acquired) continue;
                    int index = world.Rivals.IndexOf(rival);
                    int estimate = Mathf.CeilToInt((rival.Customers * 2 + rival.Assets * 20) * rival.OfferMultiplier);
                    var card = SettingsSection(rival.Name + " / AS" + rival.Asn,
                        rival.City + " · " + rival.Customers + " абонентов · " + rival.Assets +
                        " площадок\nОценка покупки: " + estimate + " кр.");
                    marketTab.Add(card);
                    card.Add(ActionButton("ПЕРЕГОВОРЫ · 120%", () => RivalNegotiationRequested?.Invoke(index, 120)));
                    card.Add(ActionButton("ПРИОБРЕСТИ КОМПАНИЮ", () => RivalAcquisitionRequested?.Invoke(index)));
                }
                marketTab.Add(Heading("ДОЧЕРНИЕ ОПЕРАТОРЫ", 15));
                if (world.Subsidiaries != null)
                    for (int i = 0; i < world.Subsidiaries.Count; i++)
                    {
                        int index = i;
                        var subsidiary = world.Subsidiaries[i];
                        var card = SettingsSection(subsidiary.Name + " / AS" + subsidiary.OriginalAsn,
                            "Узлов: " + subsidiary.SiteCount + " · Абонентов: " + subsidiary.CustomerCount +
                            "\nТехслужба: " + subsidiary.TechnicalStaff + " · Содержание: " + subsidiary.MaintenancePerTick + " кр./тик");
                        marketTab.Add(card);
                        card.Add(ActionButton("ОТПРАВИТЬ РЕМОНТНУЮ БРИГАДУ", () => SubsidiaryRepairRequested?.Invoke(index)));
                    }
            }
            else
            {
                marketTab.Add(Heading("МАГИСТРАЛИ / ПИРИНГ", 15));
                foreach (var partner in world.Partners)
                {
                    int index = world.Partners.IndexOf(partner);
                    var card = SettingsSection(partner.Name + " / AS" + partner.Asn,
                        "Пропускная способность: " + partner.Capacity + " Mbps · Стоимость: " + partner.Cost +
                        " кр. · " + (partner.Active ? "Подключён" : "Не подключён"));
                    marketTab.Add(card);
                    card.Add(ActionButton(partner.Active ? "СЕССИЯ АКТИВНА" : "ЗАКЛЮЧИТЬ ДОГОВОР",
                        () => PartnerConnectionRequested?.Invoke(index)));
                }
                marketTab.Add(Heading("МАРШРУТЫ BGP / RIB", 15));
                bgpTableLabel = Body(world.BgpSummary());
                marketTab.Add(bgpTableLabel);
            }
        }

        private void MarketTab(VisualElement container, string text, string mode)
        {
            var button = ActionButton(text, () => { marketMode = mode; RenderMarket(); });
            button.style.marginRight = 7;
            button.style.minWidth = 105;
            button.style.backgroundColor = marketMode == mode ?
                new Color(.1f, .34f, .37f) : new Color(.045f, .15f, .19f);
            container.Add(button);
        }

        public void SetWorld(IspWorld value)
        {
            world = value;
            RenderMarket();
        }
    }
}
