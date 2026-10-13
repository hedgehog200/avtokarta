// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System;

namespace AVTOKarta.Models
{
    /// <summary>
    /// Процентная надбавка (снижение) к норме расхода топлива.
    /// Значение может быть отрицательным (снижение нормы).
    /// Согласно АМ-23-р при наличии нескольких надбавок проценты суммируются.
    /// </summary>
    public class Surcharge
    {
        /// <summary>Наименование надбавки (например, «Зимняя», «Работа в городе»).</summary>
        public string Name { get; set; }

        /// <summary>Величина надбавки в процентах (может быть отрицательной — снижение).</summary>
        public double Percent { get; set; }

        /// <summary>Применяется к норме на пробег.</summary>
        public bool AppliesToDistance { get; set; }

        /// <summary>Применяется к норме на работу насоса (спец. агрегата).</summary>
        public bool AppliesToPump { get; set; }

        /// <summary>
        /// Группа взаимоисключения. Надбавки с одинаковой непустой группой не могут
        /// применяться одновременно (например, «Кондиционер»/«Климат-контроль» и
        /// «Зимняя» — прим. к п. 15 прил. № 2 АМ-23-р). Пустая строка — без ограничений.
        /// </summary>
        public string ExclusiveGroup { get; set; }

        public Surcharge()
        {
            Name = string.Empty;
            Percent = 0;
            AppliesToDistance = true;
            AppliesToPump = false;
            ExclusiveGroup = string.Empty;
        }

        public Surcharge(string name, double percent, bool appliesToDistance = true, bool appliesToPump = false,
            string exclusiveGroup = null)
        {
            Name = name ?? string.Empty;
            Percent = percent;
            AppliesToDistance = appliesToDistance;
            AppliesToPump = appliesToPump;
            ExclusiveGroup = exclusiveGroup ?? string.Empty;
        }

        public Surcharge Clone()
        {
            return new Surcharge(Name, Percent, AppliesToDistance, AppliesToPump, ExclusiveGroup);
        }

        public override string ToString()
        {
            string sign = Percent > 0 ? "+" : string.Empty;
            return (Name ?? string.Empty) + " (" + sign + Percent.ToString("0.##") + "%)";
        }
    }
}
