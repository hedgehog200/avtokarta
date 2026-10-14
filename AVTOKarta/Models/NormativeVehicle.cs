// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

namespace AVTOKarta.Models
{
    /// <summary>
    /// Справочная (нормативная) запись для раздела «Нормативные данные».
    /// Базовая норма расхода топлива для различных пожарных автомобилей и шасси,
    /// используемых в структуре МЧС.
    ///
    /// Источник: распоряжение Минтранса России от 14.03.2008 № АМ-23-р
    /// (ред. от 30.09.2021), приложение II, пп. 15.12, 15.12.1, 15.12.2
    /// «Автомобили пожарные». Нормы — базовые (линейные); при работе в части
    /// к ним применяются процентные надбавки.
    /// </summary>
    public class NormativeVehicle
    {
        /// <summary>Раздел справочника (например, «Пожарные ПА (до 2008)»).</summary>
        public string Category { get; set; }

        /// <summary>Модель специального/специализированного автомобиля или шасси.</summary>
        public string Model { get; set; }

        /// <summary>Базовое шасси / двигатель.</summary>
        public string Chassis { get; set; }

        /// <summary>Вид топлива: «Бензин», «Дизель» или пусто (не определено).</summary>
        public string Fuel { get; set; }

        /// <summary>Базовая норма на пробег, л/100 км (0 — не установлена).</summary>
        public double ConsumptionPer100Km { get; set; }

        /// <summary>Расход при работе двигателя со спец. агрегатом (насосом), л/мин (0 — не установлен).</summary>
        public double PumpLpm { get; set; }

        /// <summary>Расход при работе двигателя в стационарном режиме без нагрузки, л/мин (0 — не установлен).</summary>
        public double IdleLpm { get; set; }

        /// <summary>Примечание (особенности, тип КПП и т. п.).</summary>
        public string Note { get; set; }

        /// <summary>Норма на пробег в единицах ПО (л/км) — л/100 км ÷ 100.</summary>
        public double ConsumptionPerKm
        {
            get { return ConsumptionPer100Km / 100.0; }
        }

        /// <summary>Отображение вида топлива для таблицы.</summary>
        public string FuelDisplay
        {
            get { return string.IsNullOrEmpty(Fuel) ? "—" : Fuel; }
        }

        /// <summary>Базовая норма на пробег для таблицы, л/100 км.</summary>
        public string ConsumptionDisplay
        {
            get { return ConsumptionPer100Km > 0 ? ConsumptionPer100Km.ToString("0.00") : "—"; }
        }

        /// <summary>Расход со спец. агрегатом для таблицы.</summary>
        public string PumpDisplay
        {
            get { return PumpLpm > 0 ? PumpLpm.ToString("0.000") : "—"; }
        }

        /// <summary>Расход в стационарном режиме для таблицы.</summary>
        public string IdleDisplay
        {
            get { return IdleLpm > 0 ? IdleLpm.ToString("0.000") : "—"; }
        }

        /// <summary>Тип двигателя, если он однозначно определён по виду топлива.</summary>
        public EngineType? Engine
        {
            get
            {
                if (Fuel == "Дизель") return EngineType.Diesel;
                if (Fuel == "Бензин") return EngineType.Gasoline;
                return null;
            }
        }
    }
}
