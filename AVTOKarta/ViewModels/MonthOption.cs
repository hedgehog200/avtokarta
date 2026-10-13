// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AVTOKarta.ViewModels
{
    /// <summary>Элемент выбора месяца (для периода действия зимней надбавки).</summary>
    public class MonthOption : INotifyPropertyChanged
    {
        private bool _isSelected;

        public event PropertyChangedEventHandler PropertyChanged;

        public int Number { get; set; }
        public string Name { get; set; }

        public bool IsSelected
        {
            get { return _isSelected; }
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
        }

        public MonthOption(int number, string name, bool isSelected)
        {
            Number = number;
            Name = name;
            _isSelected = isSelected;
        }

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
