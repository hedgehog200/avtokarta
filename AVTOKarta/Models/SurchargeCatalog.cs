// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System.Collections.Generic;

namespace AVTOKarta.Models
{
    /// <summary>
    /// Пресеты процентных надбавок к нормам расхода ГСМ.
    /// Значения соответствуют приложению № 2 приказа ГУ МЧС России по Волгоградской
    /// области от 22.01.2024 № 40 (совпадают с АМ-23-р). Используются как начальный
    /// справочник; на уровне части значения редактируются.
    /// </summary>
    public static class SurchargeCatalog
    {
        /// <summary>Значение по умолчанию для «Горной местности», %.</summary>
        public const double DefaultMountainPercent = 10;

        /// <summary>Значение зимней надбавки по умолчанию, %.</summary>
        public const double DefaultWinterPercent = 10;

        /// <summary>
        /// Группа взаимоисключения для климатических надбавок: «Кондиционер»,
        /// «Климат-контроль» и «Зимняя» не применяются одновременно (прим. к п. 15).
        /// </summary>
        public const string ClimateGroup = "climate";

        public static List<Surcharge> GetDefaults()
        {
            return new List<Surcharge>
            {
                new Surcharge("Дороги I–III со сложным планом", 10, true, false),
                new Surcharge("Дороги IV–V категорий", 30, true, false),
                new Surcharge("Город свыше 5 млн чел.", 35, true, false),
                new Surcharge("Город 1–5 млн чел.", 25, true, false),
                new Surcharge("Город 250 тыс. – 1 млн чел.", 15, true, false),
                new Surcharge("Город 100–250 тыс. чел.", 10, true, false),
                new Surcharge("Город до 100 тыс. чел.", 5, true, false),
                new Surcharge("Частые технологические остановки", 10, true, false),
                new Surcharge("Пониженная скорость 20–40 км/ч", 15, true, false),
                new Surcharge("Пониженная скорость ниже 20 км/ч", 35, true, false),
                new Surcharge("Обкатка (новая/после капремонта)", 10, true, false),
                new Surcharge("Перегон своим ходом (одиночный/колонной)", 10, true, false),
                new Surcharge("Перегон-буксировка в спаренном состоянии", 15, true, false),
                new Surcharge("Перегон-буксировка в строенном состоянии", 20, true, false),
                new Surcharge("Эксплуатация > 5 лет или > 100 тыс. км", 5, true, false),
                new Surcharge("Эксплуатация > 8 лет или > 150 тыс. км", 10, true, false),
                new Surcharge("Спецавтомобиль: маневрирование, частые остановки", 20, true, false),
                new Surcharge("ЧС, тяжёлые дорожные условия: дороги I–III", 35, true, false),
                new Surcharge("ЧС, тяжёлые дорожные условия: дороги IV–V", 50, true, false),
                new Surcharge("Учебная езда на дорогах общего пользования", 20, true, false),
                new Surcharge("Учебная езда на специальной площадке", 40, true, false),
                new Surcharge("Климат-контроль", 7, true, false, ClimateGroup),
                new Surcharge("Кондиционер", 7, true, false, ClimateGroup),
                // п. 21: при работе оборудования применяются только надбавки на зимнее
                // время и в горной местности — поэтому горная надбавка применяется и к насосу.
                new Surcharge("Горная местность", DefaultMountainPercent, true, true)
            };
        }

        /// <summary>
        /// Возвращает справочник надбавок части, а если он не задан — пресеты по умолчанию.
        /// </summary>
        public static List<Surcharge> GetForSquad(Squad squad)
        {
            if (squad != null && squad.FuelSurcharges != null && squad.FuelSurcharges.Count > 0)
            {
                var list = new List<Surcharge>(squad.FuelSurcharges);
                NormalizeGroups(list);
                return list;
            }
            return GetDefaults();
        }

        /// <summary>
        /// Проставляет группу взаимоисключения климатическим надбавкам, если она не задана
        /// (обратная совместимость со справочниками, сохранёнными до появления групп).
        /// </summary>
        public static void NormalizeGroups(List<Surcharge> list)
        {
            if (list == null) return;
            foreach (var s in list)
            {
                if (s == null || !string.IsNullOrEmpty(s.ExclusiveGroup)) continue;
                if (string.Equals(s.Name, "Кондиционер", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s.Name, "Климат-контроль", System.StringComparison.OrdinalIgnoreCase))
                {
                    s.ExclusiveGroup = ClimateGroup;
                }
            }
        }
    }
}
