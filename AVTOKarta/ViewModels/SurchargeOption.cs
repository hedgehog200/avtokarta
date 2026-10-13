// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AVTOKarta.Models;

namespace AVTOKarta.ViewModels
{
    /// <summary>
    /// Элемент выбора надбавки в форме путевого листа (чекбокс + описание).
    /// </summary>
    public class SurchargeOption : INotifyPropertyChanged
    {
        private bool _isSelected;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Вызывается при изменении выбора (для пересчёта нормы).</summary>
        public event Action SelectionChanged;

        public string Name { get; set; }
        public double Percent { get; set; }
        public bool AppliesToDistance { get; set; }
        public bool AppliesToPump { get; set; }

        /// <summary>Группа взаимоисключения (см. <see cref="Surcharge.ExclusiveGroup"/>).</summary>
        public string ExclusiveGroup { get; set; }

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected == value)
                    return;
                _isSelected = value;
                OnPropertyChanged();
                SelectionChanged?.Invoke();
            }
        }

        public SurchargeOption()
        {
            Name = string.Empty;
        }

        public SurchargeOption(Surcharge surcharge)
        {
            if (surcharge == null)
            {
                Name = string.Empty;
                return;
            }
            Name = surcharge.Name;
            Percent = surcharge.Percent;
            AppliesToDistance = surcharge.AppliesToDistance;
            AppliesToPump = surcharge.AppliesToPump;
            ExclusiveGroup = surcharge.ExclusiveGroup ?? string.Empty;
        }

        /// <summary>Текст для отображения в списке.</summary>
        public string Display
        {
            get
            {
                string sign = Percent > 0 ? "+" : string.Empty;
                string applies = AppliesToPump ? " · пробег и насос" : " · пробег";
                return string.Format("{0}  ({1}{2}%){3}", Name ?? string.Empty, sign, Percent.ToString("0.##"), applies);
            }
        }

        public Surcharge ToSurcharge()
        {
            return new Surcharge(Name, Percent, AppliesToDistance, AppliesToPump, ExclusiveGroup);
        }

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
