using System;
using System.Collections.Generic;
using UnityEngine;

namespace NetworkCollapse
{
    [Serializable] public sealed class IspCity
    {
        public string Name, Country;
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
        public bool Acquired;
    }
    [Serializable] public sealed class TransitPartner
    {
        public string Name;
        public int Asn, Cost, Capacity;
        public bool Peering;
        public bool Active;
    }
    [Serializable] public sealed class IspWorld
    {
        public List<IspCity> Cities = new List<IspCity>();
        public List<IspTariff> Tariffs = new List<IspTariff>();
        public List<IspContract> Contracts = new List<IspContract>();
        public List<RivalOperator> Rivals = new List<RivalOperator>();
        public List<TransitPartner> Partners = new List<TransitPartner>();
        public int Reputation = 10;
        public int Revenue, TransitCosts;
        public int OwnAsn = 64512; // Private ASN: fictional routing domain
        public int CapturedNetworks;
        public int ForeignLinks;

        public static IspWorld Create()
        {
            var w = new IspWorld();
            w.Cities.Add(new IspCity { Name = "Стартовый город", Country = "Страна A", Population = 8000, Coverage = 1, Customers = 12 });
            w.Cities.Add(new IspCity { Name = "Промышленный город", Country = "Страна A", Population = 16000 });
            w.Cities.Add(new IspCity { Name = "Региональный центр", Country = "Страна A", Population = 52000 });
            w.Cities.Add(new IspCity { Name = "Приграничный узел", Country = "Страна B", Population = 34000, Foreign = true });
            w.Tariffs.Add(new IspTariff { Name = "Домашний", SpeedMbps = 50, MonthlyPrice = 3 });
            w.Tariffs.Add(new IspTariff { Name = "Скоростной", SpeedMbps = 100, MonthlyPrice = 5 });
            w.Tariffs.Add(new IspTariff { Name = "Бизнес", SpeedMbps = 500, MonthlyPrice = 12 });
            w.Contracts.Add(new IspContract { City = "Стартовый город", Tariff = "Домашний", Subscribers = 12 });
            w.Rivals.Add(new RivalOperator { Name = "MetroNet", City = "Промышленный город", Customers = 45, Assets = 2, Reputation = 25 });
            w.Rivals.Add(new RivalOperator { Name = "FiberUnion", City = "Региональный центр", Customers = 130, Assets = 4, Reputation = 45 });
            w.Rivals.Add(new RivalOperator { Name = "BorderLink", City = "Приграничный узел", Customers = 250, Assets = 5, Reputation = 55 });
            w.Partners.Add(new TransitPartner { Name = "National Transit", Asn = 64521, Cost = 22, Capacity = 1000 });
            w.Partners.Add(new TransitPartner { Name = "Regional IX", Asn = 64522, Cost = 12, Capacity = 500, Peering = true });
            w.Partners.Add(new TransitPartner { Name = "International Carrier", Asn = 64523, Cost = 75, Capacity = 10000 });
            return w;
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
            int offer = rival.Customers * 2 + rival.Assets * 20;
            if (Reputation < rival.Reputation / 2) return "Недостаточная репутация для сделки";
            if (network.Money < offer) return "Для поглощения потребуется " + offer + " кр.";
            if (!network.AddContractCustomers(rival.Customers, offer)) return "Недостаточно сетевой ёмкости для миграции клиентов";
            rival.Acquired = true;
            CapturedNetworks++;
            var city = Cities.Find(x => x.Name == rival.City);
            if (city != null) { city.Coverage = Mathf.Max(1, city.Coverage); city.Customers += rival.Customers; }
            Contracts.Add(new IspContract { City = rival.City, Tariff = "Домашний", Subscribers = rival.Customers });
            Reputation += 10;
            // The purchased company is represented by a market asset; new physical nodes are commissioned separately.
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
            Revenue = 0; TransitCosts = 0;
            foreach (var contract in Contracts)
            {
                var tariff = Tariffs.Find(x => x.Name == contract.Tariff);
                if (tariff != null) Revenue += contract.Subscribers * tariff.MonthlyPrice / 30;
            }
            foreach (var partner in Partners) if (partner.Active) TransitCosts += partner.Cost / 10;
            // Abstract billing tick, not real-world billing interval.
            network.CreditNet(Revenue - TransitCosts);
        }
        public string Summary()
        {
            int active = 0;
            foreach (var partner in Partners) if (partner.Active) active++;
            return "ГОРОДА " + Cities.FindAll(c => c.Coverage > 0).Count + "/" + Cities.Count +
                " · ASN " + OwnAsn + "\nКОНТРАКТЫ " + Contracts.Count + " · ПОГЛОЩЕНИЯ " + CapturedNetworks +
                "\nПИРИНГ/ТРАНЗИТ " + active + " · МЕЖДУНАРОДНЫЕ " + ForeignLinks +
                "\nВЫРУЧКА " + Revenue + " · ТРАНЗИТ " + TransitCosts + " кр./тик";
        }
    }
}
