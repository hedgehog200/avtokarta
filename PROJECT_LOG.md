# Проект АВТОКарта — Журнал изменений

## Обзор

WPF-приложение учёта пожарного автопарка (МЧС). .NET Framework 4.5.2, C#, MVVM.

---

## Ключевые решения по архитектуре

- **Multi-squad**: `Squad` (Id, Name, Number), `Vehicle` (SquadId). Хранение в `squads.json`. Старый `squad.json` мигрируется автоматически.
- **Русская локаль**: `xml:lang="ru-RU"` на App.xaml, MainWindow.xaml, CardEditView.xaml. Десятичный разделитель — запятая.
- **ClosedXML 0.94.2** вместо EPPlus. Зависимости: DocumentFormat.OpenXml 2.7.2, ExcelNumberFormat 1.0.3, FastMember 1.3.0, System.IO.Packaging 4.0.0.
- **Пользователи хотят** «название части» и «номер части» вместо «отряд».
- **Путь MSBuild**: `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`

---

## Модели данных

### FuelNorm (`Models/FuelNorm.cs`)

| Свойство | Значение по умолчанию |
|---|---|
| ConsumptionPerKmWithoutPump | 0.43 |
| ConsumptionPerKmWithPump | 0.55 |
| ConsumptionPerMinPump | 0.40 |
| ConsumptionPerMinIdle | 0.15 |
| ConsumptionPerMinShiftChange | 0.15 |
| ConsumptionPerMinMisc | 0.15 |
| ReductionCoefficient | 0.35 |

### DailyRecord (`Models/DailyRecord.cs`)

Id, Date, WorkDescription, DepartureHour/Minute, ReturnHour/Minute, OdometerBeforeDeparture, DistanceKm, TimeWithPumpMinutes, TimeWithoutPumpMinutes, ShiftChangeMinutes, MiscWorkMinutes, FuelRefueled, ActualConsumption, NormConsumption, Comments

### MonthlyCard (`Models/MonthlyCard.cs`)

Содержит `FuelNorms` (FuelNorm), `Records` (List&lt;DailyRecord&gt;), и др.

### Squad (`Models/Squad.cs`)

Id (Guid), Name, Number

### Vehicle (`Models/Vehicle.cs`)

SquadId — привязка к отряду

---

## Формулы в Excel (`Services/ExcelExportService.cs`)

### Нормы расхода топлива — отдельная таблица

Строки 119–126, столбцы B–E:

| Строка | Ячейка значения | Параметр |
|---|---|---|
| 120 | D120 | ConsumptionPerKmWithoutPump (л/км) |
| 121 | D121 | ConsumptionPerKmWithPump (л/км) |
| 122 | D122 | ConsumptionPerMinPump (л/мин) |
| 123 | D123 | ConsumptionPerMinIdle (л/мин) |
| 124 | D124 | ConsumptionPerMinShiftChange (л/мин) |
| 125 | D125 | ConsumptionPerMinMisc (л/мин) |
| 126 | D126 | ReductionCoefficient |

Строка 5 содержит только остаток топлива (как в ural.xls).

### Формулы в строках данных (18–97)

**Столбец H (Пробег):**
```
=IF(G18="","",G18-$K$4)          — строка 18 (от  показаний на 1-е число)
=IF(G{row}="","",G{row}-G{row-1}) — строки 19–97
```

**Столбец Q (Норма):**
```
=H{row}*$D$120+I{row}*$D$122+J{row}*$D$123+L{row}*$D$124+M{row}*$D$125
```

Формула пустых строк (без записей) тоже содержит формулу в Q, чтобы формулы сводки работали.

### Формулы сводки (строка 99)

| Столбец | Формула | Назначение |
|---|---|---|
| C99 | `COUNTA(C18:C97)` | Кол-во выездов |
| E99 | `COUNTIF(M18:M97,">0")` | Кол-во строк с прочими |
| G99 | `COUNTIF(L18:L97,">0")` | Кол-во строк со сменой |
| I99 | `COUNTA(A18:A97)` | Общее кол-во записей |
| K99 | `SUM(L18:L97)+SUM(M18:M97)` | Итого мин. (смена+прочие) |
| R99 | `SUM(P18:P97)` | Итого факт расхода |

### Формулы строки 101 (по элементам)

| Столбец | Формула |
|---|---|
| J101 | `ROUND(SUM(H18:H97)*$D$120,3)` — Пробег |
| M101 | `ROUND(SUM(I18:I97)*$D$122,3)` — С насосом |
| P101 | `ROUND(SUM(J18:J97)*$D$123,3)` — Без насоса |
| R101 | `ROUND(SUM(L18:L97)*$D$124+SUM(M18:M97)*$D$125,3)` — Смена+Прочие |

### Формулы строки 103

| Столбец | Формула |
|---|---|
| D103 | `ROUND(SUM(L18:L97)*$D$124,3)` — Смена караула |
| G103 | `ROUND(SUM(M18:M97)*$D$125,3)` — Прочие |
| L103 | `ROUND(SUM(Q18:Q97),3)` — Всего |

### Формула строки 105 (приведённый пробег)

```
=ROUND(SUM(H18:H97)+(SUM(L18:L97)+SUM(M18:M97))*$D$126,0)
```

### Формулы строки 111 (ИТОГО)

| Столбец | Формула |
|---|---|
| G111 | `ROUND(SUM(H18:H97),0)` — Пробег |
| J111 | `ROUND(SUM(Q18:Q97),3)` — Норма |

---

## Формат Excel (по образцу ural.xls)

- Лист: 20 столбцов
- Заголовки: строки 14–16 (многоуровневые)
- Номера столбцов: строка 17
- Данные: строки 18–97 (80 строк)
- Строка 98: "0" в столбце H
- Сводка: строки 99–111
- Подписи: строки 112–116
- Нормы: строки 119–126 (отдельная таблица)
- Границы: 84R×19C (строки 14–97), сводка 99–111, таблица норм 119–126

### Ширины столбцов

| Столбец | Ширина |
|---|---|
| A (1) | 11.56 |
| B (2) | 20.56 |
| C–F (3–6) | 4.78 |
| G (7) | 9.11 |
| H (8) | 7.22 |
| I–E (9–12) | 4.78 |
| M (13) | 6.22 |
| N–O (14–15) | 4.78 |
| P (16) | 7.22 |
| Q (17) | 7.11 |
| R–S (18–19) | 8.11 |

---

## Исправления компиляции/запуска

- EPPlus полностью удалён, заменён на ClosedXML 0.94.2
- `DocumentFormat.OpenXml` PublicKeyToken: `8fb06cb64d019a17`
- ClosedXML 0.94.2: `XLBorderStyleValues.Thin`, `XLAlignmentHorizontalValues`, `XLAlignmentVerticalValues`
- Добавлены ссылки `System.Drawing` и `System.Data` в csproj
- DataContext для SquadSetupView, SquadEditView
- `OutsideColor`/`InsideColor` не существуют в 0.94.2 — использовать только `OutsideBorder`/`InsideBorder`

---

## UI

- App.xaml: глобальные стили Material-like
- MainWindow.xaml: тёмный sidebar, верхняя информационная панель, карточка заголовка, кнопки действий, строка состояния
- ComboBox: кастомный шаблон со стрелкой, hover/focus эффекты
- CardEditView: секции с заголовками, разделители, группы полей
- Sidebar: навигация + ComboBox выбора отряда с ContentPresenter
- **CardEditView**: поля «Выезд» и «Возврат» разделены на отдельные строки (часы:минуты каждое)
- **CardEditView**: поле «Подробности» (Comments) — текстовое поле с переносом строк под полем «Работа» для адреса/места/заметок
- **CardEditView**: TimeInput стиль для полей времени (по центру, жирный, 14pt)
- **CardEditView**: SectionTitle стиль для заголовков секций (PrimaryBrush, SemiBold)
- **MainWindow DataGrid**: колонка «Примечание» (Comments) после «Наименование работы»
- **MainWindow DataGrid**: колонки времени разделены на «Выезд ч/мин» и «Возвр. ч/мин» с форматом D2

---

## Экспорт

- Автосохранение: файл `Эксплуатационная_карточка_{марка}_{месяц}_{год}.xlsx`
- Открытие файла после экспорта через `Process.Start`
- Все вычисляемые значения — формулы Excel (не захардкожены)
- `SetColumnWidths` вызывается ПОСЛЕ `ApplyTableBorders`, прямо перед `SaveAs`

---

## Файлы проекта

| Файл | Назначение |
|---|---|
| `AVTOKarta.csproj` | Ссылки на ClosedXML, System.Data, System.Drawing |
| `packages.config` | ClosedXML 0.94.2 + зависимости |
| `App.xaml` | Глобальные стили, ComboBox template |
| `MainWindow.xaml` | DataGrid 20 столбцов (с Примечанием), sidebar, навигация |
| `Models/FuelNorm.cs` | Нормы расхода топлива |
| `Models/DailyRecord.cs` | Суточная запись |
| `Models/MonthlyCard.cs` | Месячная карточка |
| `Models/Squad.cs` | Отряд (Id, Name, Number) |
| `Models/Vehicle.cs` | Автомобиль (SquadId) |
| `Services/ExcelExportService.cs` | Экспорт в xlsx с формулами |
| `Services/CalculationService.cs` | Вычисление нормы, экономии, пробега |
| `Services/DataService.cs` | CRUD операции |
| `ViewModels/MainViewModel.cs` | Главная VM, ExportToExcel |
| `tz.md` | Полное ТЗ |
| `ural.xls` | Эталонный файл (116 строк, 19 столбцов) |

---

## Журнал изменений

### 15.07.2026 — UI: поле подробностей, разделение времени выезда/возврата

- **DailyRecord.cs**: добавлено поле `Comments` (string) — примечания/подробности места работы
- **CardViewModel.cs**: добавлено свойство `Comments`, `DepartureTimeDisplay`, `ReturnTimeDisplay`
- **CardEditView.xaml**:
  - Поле «Подробности» (многострочное) добавлено сразу под полем «Работа»
  - Время выезда и возвращения разделены на две строки (каждое — часы : минуты)
  - Стиль `TimeInput` для полей времени (по центру, жирный шрифт)
  - Стиль `SectionTitle` для заголовков секций (PrimaryBrush)
  - Увеличена высота окна до 720
- **MainWindow.xaml**:
  - Добавлена колонка «Примечание» (Comments) в DataGrid после «Наименование работы»
  - Колонки времени разделены на «Выезд ч/мин» и «Возвр. ч/мин» с форматом D2
  - Заголовки переименованы (Деж. караул, Деж. нач., Смена мин.)
- **PROJECT_LOG.md**: обновлена документация
