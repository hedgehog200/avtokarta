// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System;

namespace AVTOKarta.Models
{
    public class Driver
    {
        public string Id { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        public string LicenseCategory { get; set; }
        public DateTime? HireDate { get; set; }

        public Driver()
        {
            Id = Guid.NewGuid().ToString("N").Substring(0, 8);
            FullName = string.Empty;
            Phone = string.Empty;
            LicenseCategory = string.Empty;
        }

        public override string ToString()
        {
            return FullName ?? string.Empty;
        }
    }
}
