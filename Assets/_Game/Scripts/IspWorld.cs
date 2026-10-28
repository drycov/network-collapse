using System;
using System.Collections.Generic;
using UnityEngine;

namespace NetworkCollapse
{
    [Serializable] public sealed class IspCity
    {
        public string Name, Country;
        public Vector2 MapPosition;
        public int Population, Coverage, Customers;
        public bool Foreign;
    }
    [Serializable] public sealed class IspTariff
    {
        public string Name;
        public int SpeedMbps, MonthlyPrice;
    }
    [Serializable] public sealed class IspContract
    {
        public string City, Tariff;
        public int Subscribers;
    }
    [Serializable] public sealed class RivalOperator
    {
        public string Name, City;
        public int Customers, Assets, Reputation;
        public int Asn;
        public float OfferMultiplier = 1f;
        public List<Vector2> NetworkSites = new List<Vector2>();
        public List<RivalLink> NetworkLinks = new List<RivalLink>();
        public List<RivalTransitContract> TransitContracts = new List<RivalTransitContract>();
        public List<RivalRoute> OriginRoutes = new List<RivalRoute>();
        public int TechnicalStaff = 2;
        public bool Acquired;
    }
    [Serializable] public sealed class RivalLink { public int A, B; public float Capacity = 80f; public bool Failed; }
    [Serializable] public sealed class RivalTransitContract { public string Provider; public int Asn, Capacity, CostPerTick; public bool Active = true; }
    [Serializable] public sealed class RivalRoute { public string Prefix, AsPath; public int LocalPreference = 100; }
    [Serializable] public sealed class TransitPartner
    {
        public string Name;
        public int Asn, Cost, Capacity;
        public bool Peering;
        public bool Active;
    }
    [Serializable] public sealed class AcquiredCompany
    {
        public string Name, City;
        public int OriginalAsn, SiteCount, CustomerCount, MaintenancePerTick;
        public int LegacyFaults;
        public int TechnicalStaff, PendingRepairs;
        public List<RivalTransitContract> InheritedTransit = new List<RivalTransitContract>();
        public List<RivalRoute> InheritedRoutes = new List<RivalRoute>();
    }
    [Serializable] public sealed class IspWorld
    {
        public List<IspCity> Cities = new List<IspCity>();
        public List<IspTariff> Tariffs = new List<IspTariff>();
        public List<IspContract> Contracts = new List<IspContract>();
        public List<RivalOperator> Rivals = new List<RivalOperator>();
        public List<TransitPartner> Partners = new List<TransitPartner>();
        public List<AcquiredCompany> Subsidiaries = new List<AcquiredCompany>();
        public int Reputation = 10;
        public int Revenue, TransitCosts;
        public int OwnAsn = 64512; // Private ASN: fictional routing domain
        public int CapturedNetworks;
        public int ForeignLinks;
        public int MarketDay;
        public List<BgpRoute> Routes = new List<BgpRoute>();
        [Serializable] public sealed class BgpRoute
        {
            public int DestinationAsn;
            public string Prefix;
            public string AsPath;
            public int LocalPreference;
            public bool Reachable;
        }
        public string MarketNews = "Операторы готовятся к расширению";

        public static IspWorld Create()
        {
            var w = new IspWorld();
            var generator = new System.Random(20261028);
            string[] countries = { "Республика Альта", "Федерация Норд", "Союз Марис", "Королевство Веста" };
            string[] cityNames = { "Альта", "Ридж", "Неон", "Нордпорт", "Вектор", "Марис", "Каскад", "Вестон", "Орбита", "Северин", "Портал", "Астра" };
            for (int i = 0; i < cityNames.Length; i++)
            {
                int country = i / 3;
                w.Cities.Add(new IspCity {
                    Name = cityNames[i], Country = countries[country],
                    Foreign = country > 0,
                    MapPosition = new Vector2(country * 18f + (float)generator.NextDouble() * 10f,
                        (i % 3) * 9f + (float)generator.NextDouble() * 5f),
                    Population = 3000 + generator.Next(3000, 70000),
                    Coverage = i == 0 ? 1 : 0, Customers = i == 0 ? 12 : 0
                });
            }
            w.Tariffs.Add(new IspTariff { Name = "Домашний", SpeedMbps = 50, MonthlyPrice = 3 });
            w.Tariffs.Add(new IspTariff { Name = "Скоростной", SpeedMbps = 100, MonthlyPrice = 5 });
            w.Tariffs.Add(new IspTariff { Name = "Бизнес", SpeedMbps = 500, MonthlyPrice = 12 });
            w.Contracts.Add(new IspContract { City = "Альта", Tariff = "Домашний", Subscribers = 12 });
            w.Rivals.Add(new RivalOperator { Name = "MetroNet", City = "Ридж", Customers = 45, Assets = 2, Reputation = 25 });
            w.Rivals.Add(new RivalOperator { Name = "FiberUnion", City = "Вектор", Customers = 130, Assets = 4, Reputation = 45 });
            w.Rivals.Add(new RivalOperator { Name = "BorderLink", City = "Марис", Customers = 250, Assets = 5, Reputation = 55 });
            for (int i = 0; i < w.Rivals.Count; i++)
            {
                var rival = w.Rivals[i];
                rival.Asn = 64600 + i;
                var city = w.Cities.Find(x => x.Name == rival.City);
                Vector2 origin = city != null ? city.MapPosition : Vector2.zero;
                for (int site = 0; site < rival.Assets; site++)
                {
                    rival.NetworkSites.Add(origin + new Vector2((float)generator.NextDouble() * 4 - 2,
                        (float)generator.NextDouble() * 4 - 2));
                    if (site > 0) rival.NetworkLinks.Add(new RivalLink { A = site - 1, B = site, Capacity = 80 + site * 20, Failed = site % 3 == 0 });
                }
                rival.TransitContracts.Add(new RivalTransitContract { Provider = "Legacy upstream", Asn = 64700 + i, Capacity = 500, CostPerTick = 2 + i });
                rival.OriginRoutes.Add(new RivalRoute { Prefix = "10." + (40 + i) + ".0.0/16", AsPath = rival.Asn + " " + (64700 + i), LocalPreference = 100 });
            }
            w.Partners.Add(new TransitPartner { Name = "National Transit", Asn = 64521, Cost = 22, Capacity = 1000 });
            w.Partners.Add(new TransitPartner { Name = "Regional IX", Asn = 64522, Cost = 12, Capacity = 500, Peering = true });
            w.Partners.Add(new TransitPartner { Name = "International Carrier", Asn = 64523, Cost = 75, Capacity = 10000 });
            return w;
        }
        public string Negotiate(int index, int percent)
        {
            if (index < 0 || index >= Rivals.Count || Rivals[index].Acquired) return "Сделка недоступна";
            var rival = Rivals[index];
            // Legal commercial negotiations, not hostile network intrusion.
            if (percent < 70 || percent > 140) return "Предложение должно составлять 70–140% оценки";
            float threshold = Mathf.Clamp(1.12f - Reputation * .004f + rival.Reputation * .003f, .8f, 1.3f);
            if (percent / 100f < threshold)
            {
                rival.OfferMultiplier = Mathf.Min(1.4f, rival.OfferMultiplier + .04f);
                return rival.Name + ": предложение отклонено. Минимальная оценка повышена.";
            }
            rival.OfferMultiplier = percent / 100f;
            return rival.Name + ": предложение принято. Завершите покупку активов.";
        }
        public void RebuildBgpRoutes()
        {
            Routes.Clear();
            foreach (var partner in Partners)
            {
                if (!partner.Active) continue;
                Routes.Add(new BgpRoute {
                    DestinationAsn = partner.Asn,
                    Prefix = "203.0." + (partner.Asn % 200) + ".0/24",
                    AsPath = OwnAsn + " " + partner.Asn,
                    LocalPreference = partner.Peering ? 120 : 100,
                    Reachable = true
                });
            }
            if (Subsidiaries != null) foreach (var subsidiary in Subsidiaries)
                if (subsidiary.InheritedRoutes != null)
                    foreach (var route in subsidiary.InheritedRoutes)
                        Routes.Add(new BgpRoute { DestinationAsn = subsidiary.OriginalAsn,
                            Prefix = route.Prefix, AsPath = OwnAsn + " " + route.AsPath,
                            LocalPreference = route.LocalPreference, Reachable = true });
            foreach (var rival in Rivals)
                if (!rival.Acquired)
                    Routes.Add(new BgpRoute {
                        DestinationAsn = rival.Asn,
                        Prefix = "198.51." + (rival.Asn % 200) + ".0/24",
                        AsPath = OwnAsn + (Partners.Exists(p => p.Active) ? " " + Partners.Find(p => p.Active).Asn : "") + " " + rival.Asn,
                        LocalPreference = 80,
                        Reachable = Partners.Exists(p => p.Active)
                    });
        }
        public string BgpSummary()
        {
            RebuildBgpRoutes();
            var lines = new List<string>();
            foreach (var route in Routes)
                lines.Add((route.Reachable ? "BEST" : "DOWN") + " " + route.Prefix +
                    "  AS_PATH " + route.AsPath + "  LP " + route.LocalPreference);
            return lines.Count == 0 ? "Нет установленных BGP-соседей" : string.Join("\n", lines);
        }

        public string OpenCity(NetworkSimulation network, int index)
        {
            if (index < 0 || index >= Cities.Count) return "Город не найден";
            var city = Cities[index];
            if (city.Coverage > 0) return "Покрытие уже открыто: " + city.Name;
            int cost = city.Foreign ? 180 : 90;
            if (city.Foreign && !Partners.Exists(p => p.Active && p.Name == "International Carrier"))
                return "Для международного подключения требуется договор с International Carrier";
            if (network.Money < cost) return "Недостаточно средств: требуется " + cost;
            if (!network.ExpandNetworkFree()) return "Нет доступных ресурсов для расширения топологии";
            network.SpendCredits(cost);
            city.Coverage = 1;
            Reputation += 3;
            if (city.Foreign) ForeignLinks++;
            return "Открыто покрытие: " + city.Name;
        }
        public string SignCustomers(NetworkSimulation network, int cityIndex, int tariffIndex)
        {
            if (cityIndex < 0 || cityIndex >= Cities.Count || tariffIndex < 0 || tariffIndex >= Tariffs.Count) return "Не выбран город или тариф";
            var city = Cities[cityIndex];
            if (city.Coverage == 0) return "Сначала откройте покрытие города";
            if (network.Money < 18) return "Не хватает бюджета на подключение";
            int quantity = Mathf.Min(city.Population - city.Customers, 4 + Reputation / 12);
            if (quantity <= 0) return "Рынок города насыщен";
            if (!network.AddContractCustomers(quantity, 18)) return "Недостаточно ёмкости сети";
            city.Customers += quantity;
            var tariff = Tariffs[tariffIndex];
            var contract = Contracts.Find(x => x.City == city.Name && x.Tariff == tariff.Name);
            if (contract == null) { contract = new IspContract { City = city.Name, Tariff = tariff.Name }; Contracts.Add(contract); }
            contract.Subscribers += quantity;
            Reputation++;
            return "Подключено " + quantity + " абонентов: " + city.Name + " / " + tariff.Name;
        }
        public string AcquireRival(NetworkSimulation network, int index)
        {
            if (index < 0 || index >= Rivals.Count) return "Конкурент не найден";
            var rival = Rivals[index];
            if (rival.Acquired) return "Компания уже приобретена";
            int offer = Mathf.CeilToInt((rival.Customers * 2 + rival.Assets * 20) * rival.OfferMultiplier);
            if (Reputation < rival.Reputation / 2) return "Недостаточная репутация для сделки";
            if (network.Money < offer) return "Для поглощения потребуется " + offer + " кр.";
            if (!network.IntegrateRivalTopology(rival, offer)) return "Невозможно интегрировать приобретённую сеть: бюджет или лимит узлов";
            rival.Acquired = true;
            CapturedNetworks++;
            if (Subsidiaries == null) Subsidiaries = new List<AcquiredCompany>();
            Subsidiaries.Add(new AcquiredCompany {
                Name = rival.Name, City = rival.City, OriginalAsn = rival.Asn,
                SiteCount = rival.NetworkSites.Count, CustomerCount = rival.Customers,
                MaintenancePerTick = Mathf.Max(1, rival.NetworkSites.Count / 2),
                LegacyFaults = rival.NetworkSites.Count / 3 + (rival.NetworkSites.Count > 1 ? 1 : 0),
                TechnicalStaff = rival.TechnicalStaff,
                InheritedTransit = new List<RivalTransitContract>(rival.TransitContracts),
                InheritedRoutes = new List<RivalRoute>(rival.OriginRoutes)
            });
            var city = Cities.Find(x => x.Name == rival.City);
            if (city != null) { city.Coverage = Mathf.Max(1, city.Coverage); city.Customers += rival.Customers; }
            Contracts.Add(new IspContract { City = rival.City, Tariff = "Домашний", Subscribers = rival.Customers });
            Reputation += 10;
            // Purchased network sites are now integrated into the physical network; inherited failures require repair.
            return "Сеть " + rival.Name + " приобретена и интегрирована; +" + rival.Customers + " клиентов";
        }
        public string ConnectPartner(NetworkSimulation network, int index)
        {
            if (index < 0 || index >= Partners.Count) return "Оператор не найден";
            var partner = Partners[index];
            if (partner.Active) return "Сессия уже активна";
            if (network.Money < partner.Cost) return "Не хватает средств на подключение к AS" + partner.Asn;
            network.SpendCredits(partner.Cost);
            partner.Active = true;
            Reputation += partner.Peering ? 4 : 2;
            if (index == 2) ForeignLinks++;
            return (partner.Peering ? "Пиринг" : "Транзит") + " включён: AS" + OwnAsn + " ↔ AS" + partner.Asn;
        }
        public void Tick(NetworkSimulation network)
        {
            MarketDay++;
            Revenue = 0; TransitCosts = 0;
            if (MarketDay % 5 == 0) SimulateCompetition(network);
            foreach (var contract in Contracts)
            {
                var tariff = Tariffs.Find(x => x.Name == contract.Tariff);
                if (tariff != null) Revenue += contract.Subscribers * tariff.MonthlyPrice / 30;
            }
            foreach (var partner in Partners) if (partner.Active) TransitCosts += partner.Cost / 10;
            if (Subsidiaries != null) foreach (var subsidiary in Subsidiaries)
                if (subsidiary.InheritedTransit != null)
                    foreach (var contract in subsidiary.InheritedTransit)
                        if (contract.Active) TransitCosts += contract.CostPerTick;
            // Abstract billing tick, not real-world billing interval.
            network.CreditNet(Revenue - TransitCosts);
        }
        private void SimulateCompetition(NetworkSimulation network)
        {
            var rng = new System.Random(MarketDay * 7919 + 73);
            foreach (var rival in Rivals)
            {
                if (rival.Acquired) continue;
                var city = Cities.Find(x => x.Name == rival.City);
                if (city == null) continue;
                int gain = 1 + rng.Next(2 + Mathf.Max(1, rival.Reputation / 25));
                rival.Customers = Mathf.Min(city.Population, rival.Customers + gain);
                if (city.Coverage > 0 && city.Customers > 1 && network.Availability < .9f)
                {
                    int lost = Mathf.Min(city.Customers - 1, 1 + rng.Next(2));
                    city.Customers -= lost;
                    rival.Customers += lost;
                    network.LoseCustomers(lost);
                    foreach (var contract in Contracts)
                    {
                        if (contract.City != city.Name || contract.Subscribers <= 0) continue;
                        int moved = Mathf.Min(contract.Subscribers, lost);
                        contract.Subscribers -= moved; lost -= moved;
                        if (lost <= 0) break;
                    }
                    MarketNews = "Конкурент " + rival.Name + " переманил абонентов из " + city.Name;
                }
                else MarketNews = rival.Name + " расширил собственную сеть в " + city.Name;
                if (rival.Customers % 25 < gain)
                {
                    rival.Assets++;
                    rival.NetworkSites.Add(city.MapPosition + new Vector2(rng.Next(-4, 5), rng.Next(-4, 5)));
                }
            }
        }

        public string Summary()
        {
            int active = 0;
            foreach (var partner in Partners) if (partner.Active) active++;
            return "ДОЧЕРНИЕ ОПЕРАТОРЫ " + (Subsidiaries == null ? 0 : Subsidiaries.Count) + "\n" +
                "ГОРОДА " + Cities.FindAll(c => c.Coverage > 0).Count + "/" + Cities.Count +
                " · ASN " + OwnAsn + "\nКОНТРАКТЫ " + Contracts.Count + " · ПОГЛОЩЕНИЯ " + CapturedNetworks +
                "\nПИРИНГ/ТРАНЗИТ " + active + " · МЕЖДУНАРОДНЫЕ " + ForeignLinks +
                "\nВЫРУЧКА " + Revenue + " · ТРАНЗИТ " + TransitCosts + " кр./тик";
        }
    }
}
