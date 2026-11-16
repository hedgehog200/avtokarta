# АВТОКарта v1.2.2

**Дата:** 04.09.2026

---

## Что нового

### Иконка приложения
- Новая иконка приложения на основе пользовательского изображения
- Мульти-размерный ICO (16, 24, 32, 48, 64, 128, 256 px)
- Корректное отображение в панели задач через Win32 API (`WM_SETICON`)
- Автоматическая обрезка прозрачных полей, растяжение на весь квадрат

### Иконки интерфейса
- Все иконки заменены на **Font Awesome 6 Solid** (встроенный шрифт)
- Убрана зависимость от Segoe MDL2 Assets (только Windows 10+)
- Навигационное меню: дом, пользователь, автомобиль, документы, топливо, настройки, аналитика, архив, импорт, выход
- Иконки редактирования/удаления: ручка, корзина
- Иконка меню-бургера: три полоски

### Совместные стили
- `FormLabel` — стилизованная подпись поля ввода (голубой, полужирный, 12px)
- `FieldCard` — карточка-контейнер для полей формы (тень, скругление, отступы)
- `FormControlTextBox` — единый стиль текстовых полей ввода
- `ModalCloseButton` — кнопка закрытия модальных окон с hover/press feedback
- Удалены дубликаты из SettingsView, SquadSetupView, SquadListView, WarehouseView

### Прочее
- Скролл в окне смены пароля
- Версия сборки: **1.2.2**

---

## Технические детали

### Шрифт
- Встроенный: `fa-solid-900.ttf` (Font Awesome 6 Solid)
- Регистрация в App.xaml: `FontFamily x:Key="FontAwesomeSolid"`

### Версия (обновлено везде)
- `AssemblyInfo.cs` — `AssemblyVersion` / `AssemblyFileVersion`
- `MainWindow.xaml` — заголовок окна
- `DataTransferService.cs` — `AppVersion`
- `AVTOKarta_setup.iss` — `MyAppVersion` / `VersionInfoVersion`

### Установщик
- Inno Setup: `AVTOKarta_setup.iss`
- Иконка установщика: `app.ico`
- Имя выходного файла: `AVTOKarta_v1.2.2_setup.exe`

---

## Сборка

1. Открыть `AVTOKarta.sln` в Visual Studio 2022
2. Release → Build
3. Запустить `AVTOKarta_setup.iss` для создания инсталлера

---

## Системные требования

- Windows 7+ / .NET Framework 4.5.2
- Разрешение экрана: от 1024×768
