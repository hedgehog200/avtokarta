// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AVTOKarta.Models;
using AVTOKarta.Services;

namespace AVTOKarta.ViewModels
{
    /// <summary>
    /// ViewModel раздела «Нормативные данные»: справочник базовых норм расхода топлива
    /// для различных пожарных автомобилей и шасси, используемых в структуре МЧС.
    ///
    /// Источник: распоряжение Минтранса России от 14.03.2008 № АМ-23-р
    /// (ред. от 30.09.2021), прил. II, пп. 15.12, 15.12.1, 15.12.2.
    /// </summary>
    public class NormativeDataViewModel : BaseViewModel
    {
        private readonly List<string> _categories;
        private string _selectedCategory;
        private string _searchText;
        private NormativeVehicle _selectedItem;

        public NormativeDataViewModel()
        {
            _categories = new List<string> { "Все разделы" };
            _categories.AddRange(NormativeDataService.Categories);

            Items = new ObservableCollection<NormativeVehicle>();
            _selectedCategory = _categories[0];
            _searchText = string.Empty;

            ApplyFilter();
        }

        /// <summary>Список разделов для фильтра (первый — «Все разделы»).</summary>
        public List<string> Categories
        {
            get { return _categories; }
        }

        /// <summary>Записи, попавшие под фильтр.</summary>
        public ObservableCollection<NormativeVehicle> Items { get; private set; }

        /// <summary>Выбранный раздел справочника.</summary>
        public string SelectedCategory
        {
            get { return _selectedCategory; }
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                    ApplyFilter();
            }
        }

        /// <summary>Строка поиска (по модели, шасси, примечанию).</summary>
        public string SearchText
        {
            get { return _searchText; }
            set
            {
                if (SetProperty(ref _searchText, value))
                    ApplyFilter();
            }
        }

        /// <summary>Выбранная строка таблицы.</summary>
        public NormativeVehicle SelectedItem
        {
            get { return _selectedItem; }
            set { SetProperty(ref _selectedItem, value); }
        }

        /// <summary>Количество записей, попавших под фильтр.</summary>
        public int ItemsCount
        {
            get { return Items.Count; }
        }

        /// <summary>Текст «Показано: N из M».</summary>
        public string CountText
        {
            get { return string.Format("Показано: {0} из {1}", Items.Count, NormativeDataService.Items.Count); }
        }

        private void ApplyFilter()
        {
            IEnumerable<NormativeVehicle> query = NormativeDataService.Items;

            if (!string.IsNullOrEmpty(_selectedCategory) && _selectedCategory != _categories[0])
                query = query.Where(v => v.Category == _selectedCategory);

            string q = (_searchText ?? string.Empty).Trim();
            if (q.Length > 0)
            {
                query = query.Where(v =>
                    Contains(v.Model, q) ||
                    Contains(v.Chassis, q) ||
                    Contains(v.Note, q) ||
                    Contains(v.Category, q));
            }

            Items.Clear();
            foreach (NormativeVehicle item in query.ToList())
                Items.Add(item);

            OnPropertyChanged("ItemsCount");
            OnPropertyChanged("CountText");
        }

        private static bool Contains(string source, string value)
        {
            return !string.IsNullOrEmpty(source) &&
                   source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
