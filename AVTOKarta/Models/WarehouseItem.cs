using System;

namespace AVTOKarta.Models
{
    public class WarehouseItem
    {
        public string Id { get; set; }
        public DateTime Date { get; set; }
        public OilType Type { get; set; }
        public string Brand { get; set; }
        public double Quantity { get; set; }
        public string DocumentNumber { get; set; }
        public string Supplier { get; set; }
        public string SquadId { get; set; }

        public WarehouseItem()
        {
            Id = Guid.NewGuid().ToString("N").Substring(0, 8);
            Date = DateTime.Today;
            Brand = string.Empty;
            DocumentNumber = string.Empty;
            Supplier = string.Empty;
            SquadId = string.Empty;
        }

        public string TypeDisplay
        {
            get
            {
                switch (Type)
                {
                    case OilType.MotorOil: return "Моторное масло";
                    case OilType.TransmissionOil: return "Трансмиссионное масло";
                    case OilType.SpecialLiquid: return "Спец. жидкость";
                    case OilType.PlasticLubricant: return "Пластичная смазка";
                    default: return Type.ToString();
                }
            }
        }

        public string UnitDisplay
        {
            get { return Type == OilType.PlasticLubricant ? "кг" : "л"; }
        }
    }
}
