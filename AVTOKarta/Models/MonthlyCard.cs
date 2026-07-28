// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AVTOKarta.Models
{
    public enum FuelDeliveryType
    {
        Centralized,
        OwnRC,
        OtherRC,
        LocalPurchase,
        Other
    }

    public class MonthlyCard : INotifyPropertyChanged
    {
        private string _month;
        private int _year;
        private string _vehicleLicensePlate;
        private double _chassisMileageOnFirst;
        private double _engineMileageOnFirst;
        private double _fuelRemainingOnFirst;
        private double _fuelRefueledMonth;
        private double _fuelRemainingOnLast;
        private double _fuelLevelCm;
        private FuelDeliveryType _deliveryType;
        private List<DailyRecord> _records;

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public string Month
        {
            get => _month;
            set { _month = value; OnPropertyChanged(); }
        }

        public int Year
        {
            get => _year;
            set { _year = value; OnPropertyChanged(); }
        }

        public string VehicleLicensePlate
        {
            get => _vehicleLicensePlate;
            set { _vehicleLicensePlate = value; OnPropertyChanged(); }
        }

        public double ChassisMileageOnFirst
        {
            get => _chassisMileageOnFirst;
            set { _chassisMileageOnFirst = value; OnPropertyChanged(); }
        }

        public double EngineMileageOnFirst
        {
            get => _engineMileageOnFirst;
            set { _engineMileageOnFirst = value; OnPropertyChanged(); }
        }

        public double FuelRemainingOnFirst
        {
            get => _fuelRemainingOnFirst;
            set { _fuelRemainingOnFirst = value; OnPropertyChanged(); }
        }

        public double FuelRefueledMonth
        {
            get => _fuelRefueledMonth;
            set { _fuelRefueledMonth = value; OnPropertyChanged(); }
        }

        public double FuelRemainingOnLast
        {
            get => _fuelRemainingOnLast;
            set { _fuelRemainingOnLast = value; OnPropertyChanged(); }
        }

        public double FuelLevelCm
        {
            get => _fuelLevelCm;
            set { _fuelLevelCm = value; OnPropertyChanged(); }
        }

        public FuelDeliveryType DeliveryType
        {
            get => _deliveryType;
            set { _deliveryType = value; OnPropertyChanged(); }
        }

        public List<DailyRecord> Records
        {
            get => _records;
            set { _records = value; OnPropertyChanged(); }
        }

        public MonthlyCard()
        {
            Records = new List<DailyRecord>();
            DeliveryType = FuelDeliveryType.Centralized;
        }

        public string DeliveryTypeDisplay
        {
            get
            {
                switch (DeliveryType)
                {
                    case FuelDeliveryType.Centralized: return "централизованно";
                    case FuelDeliveryType.OwnRC: return "по расчёту своего РЦ";
                    case FuelDeliveryType.OtherRC: return "по расчёту других РЦ";
                    case FuelDeliveryType.LocalPurchase: return "закуплено на местах";
                    case FuelDeliveryType.Other: return "прочий приход";
                    default: return DeliveryType.ToString();
                }
            }
        }
    }
}
