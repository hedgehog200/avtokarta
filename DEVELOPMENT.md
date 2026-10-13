# AVTOKarta — документация для разработчика

> Назначение: карта проекта «что где лежит», как собирать, как устроена защита/лицензирование
> и что было сделано — чтобы можно было продолжать **ручную** разработку без повторного изучения.
>
> Дата обновления документа: **14.10.2026** (последний тег: `v1.3.0`).

---

## 1. Что это за проект

Настольное приложение (WPF) для учёта автопарка и расхода ГСМ (пользователи — подразделения МЧС).

- Учёт: карточки ТС, водители, расчётки (суточные карточки), склад ГСМ, нормы расхода.
- Расчёты: норма/факт, экономия/перерасход, классификация рейсов по типу.
- Отчёты: Excel (карточка месяца + сводный отчёт по ГСМ).
- Экспорт/импорт данных в файл `.avto` (ZIP + AES-256, опционально с паролем).
- Лицензирование (офлайн, RSA-подпись файла `license.lic`) и защита от вмешательства (`tamper.sig`).
- Самообновление с GitHub Releases (пароль установщика `installer.key` необязателен).

## 2. Стек и инструменты

| Что | Значение |
|---|---|
| Язык / фреймворк | C#, **.NET Framework 4.5.2**, WPF (не SDK-style csproj) |
| UI-библиотека | HandyControl 3.5.1 (+ HandyControl.Lang.ru), иконки — Font Awesome 6 (свой шрифт в `AVTOKarta\Fonts\`) |
| Excel | ClosedXML 0.94.2, EPPlus 4.5.3.3, DocumentFormat.OpenXml 2.7.2 |
| Прочее | Newtonsoft.Json 13.0.3, FastMember 1.3.0 |
| Пакеты | `packages.config` + лежащий в корне `nuget.exe`; пакеты в `packages/` |
| Сборка | MSBuild (VS 18 Community: `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`, fallback VS 2022) |
| Установщик | Inno Setup — `AVTOKarta_setup.iss` (в `.gitignore`, но файл отслеживается в репо) |
| Обфускация | ConfuserEx (форк mkaring v1.6.0), CLI: `C:\Users\apari\AppData\Local\Temp\opencode\confuser\ext\Confuser.CLI.exe` |
| IDE/инструменты | Visual Studio 2022/18, Serena (семантические инструменты), OpenCode |
| Тестов / CI | **Нет.** Только ручная проверка + сборка |

Правила кодирования (см. память `conventions`):
- MVVM строго: логика в ViewModels/Services, не в code-behind.
- `RelayCommand` (`Helpers\RelayCommand.cs`), база `BaseViewModel` (INPC).
- Поля `_camelCase`, классы/методы/свойства — PascalCase, суффиксы `*ViewModel`, `*View`, `*Service`.
- Комментарии и тексты UI — русские.

## 3. Структура репозитория

```
C:\project\mchs gsm\
├── AVTOKarta.sln                 # решение (AVTOKarta + LicGen)
├── AVTOKarta\                    # основной проект приложения (см. §4)
├── LicGen\                       # генератор лицензий + подпись манифеста tamper.sig (отдельное WinForms/консольное приложение)
├── Shared\                       # общий код приложения и LicGen (линкуется в оба .csproj!)
│   ├── LicenseInfo.cs            #   формат лицензии, фичи, крипта лицензий, манифест tamper.sig
│   └── MachineCode.cs            #   код машины (привязка к ПК)
├── build-release.ps1             # конвейер релиза: MSBuild → ConfuserEx → sig-issue → sig-check
├── AVTOKarta_setup.iss           # скрипт установщика Inno Setup
├── Output\                       # готовые setup.exe (в .gitignore)
├── packages\                     # NuGet-пакеты (в .gitignore)
├── доп.информация\               # исходные документы заказчика (docx/xls) — НЕ код
├── README.md                     # обзор функционала для пользователя
├── RELEASE_v1.2.1.md … v1.3.0.md # заметки к релизам
├── avtokarta.crproj              # профиль обфускации ConfuserEx (в репозитории!)
├── DEVELOPMENT.md                # ЭТА документация
├── nuget.exe, opencode.json, пароль.txt, .serena\ (памяти проекта)
```

Git: ветка `master`, теги `v1.1.0` … `v1.3.0`. Удалённые репозитории: `origin` (полный, `hedgehog200/avtocartaZ`) и `release` (публичный, `hedgehog200/avtokarta`; в `master` только `README.md`, релизы — теги).

## 4. Архитектура приложения

```
App.OnStartup (App.xaml.cs)
   └─ культура ru-RU, глобальные обработчики исключений, лог → %LocalAppData%\AVTOKarta\crash.log
MainWindow.xaml(.cs)
   └─ DataContext = new MainViewModel(); одиночное окно с боковой навигацией,
      экраны переключаются флагами Is*Mode / CurrentScreenIndex
MainViewModel  ── владеет всеми сервисами и командами (RelayCommand)
   ├── DataService          — чтение/запись JSON-файлов (шифрованных)
   ├── EncryptionService    — AES-256 + PBKDF2 + DPAPI
   ├── SettingsService      — пароли, путь к данным, миграции
   ├── CacheService         — автосохранение по таймеру (60 c) + по требованию
   ├── CalculationService   — вся математика расхода (static)
   ├── ExcelExportService / FuelReportExportService — выгрузка в Excel
   ├── DataTransferService  — экспорт/импорт .avto, бэкапы
   ├── UpdateService        — GitHub Releases, скачивание, запуск установки
   ├── LicenseService       — проверка лицензии (static)
   └── TamperGuard / IntegrityChecker / AntiDebug — защита (см. §7)
Views (UserControl/Window) ←→ ViewModels (без логики в code-behind)
```

**Стартовое окно:** `LicenseView` показывается только если в `%LocalAppData%\AVTOKarta\Licenses\license.lic`
нет валидной лицензии (иначе сразу главное окно — это штатно, не баг).
Гейты функций: `MainViewModel.RefreshLicenseGates()` + `LicenseService.HasFeature(...)`.

## 5. Карта файлов

### 5.1 `AVTOKarta\Models\` — домен

| Файл | Содержимое |
|---|---|
| `Squad.cs` | Подразделение (расчётная часть) |
| `Vehicle.cs` | `Vehicle` + enum `EngineType` (бензин/дизель/электро и т.п.) |
| `Driver.cs` | Водитель |
| `MonthlyCard.cs` | Месячная карточка ТС + enum `FuelDeliveryType` (заправка/слив и т.п.) |
| `DailyRecord.cs` | Суточная запись (выезд, пробег, время, ГСМ, остатки, стоянка/груз, надбавки) + enum `OilType`, `OilTypeItem` |
| `FuelNorm.cs` | Нормы расхода (пробег/насос/холостой, стоянка л/час, груз л/100 км·т, масла, AdBlue) |
| `Surcharge.cs` | Процентная надбавка (знак, к пробегу/к насосу, группа взаимоисключения) |
| `SurchargeCatalog.cs` | Пресеты надбавок по прил. № 2 приказа ГУ МЧС № 40 + нормализация групп |
| `TripType.cs` | enum типов рейсов (город/пригород/удалёнка и т.п.) |
| `WarehouseItem.cs` | Позиция склада ГСМ + enum `WarehouseOperationType` |
| `CardHistoryItem.cs` | Запись истории карточки |

### 5.2 `AVTOKarta\ViewModels\`

| Файл | Назначение |
|---|---|
| `BaseViewModel.cs` | INPC-база |
| **`MainViewModel.cs` (~1600 строк)** | **Ядро**: все команды, навигация (флаги `IsSetupMode/IsCardMode/IsVehicleMode/IsSettingsMode/IsSquadListMode/IsFuelReportMode/IsWarehouseMode/IsDriverListMode`), данные экранов, экспорт/импорт, лицензионные гейты (`RefreshLicenseGates`) |
| `CardViewModel.cs` | Редактирование карточки/суточных записей (диалог `CardEditView`) |
| `DriverListViewModel.cs` | Список водителей + `DriverFilterMode`, `DriverTripInfo` |
| `SquadEditViewModel.cs` / `SquadSetupViewModel.cs` | Редактирование/создание подразделения |
| `VehicleViewModel.cs` | Редактирование ТС (+ `EngineTypeItem`) |
| `WarehouseViewModel.cs` | Склад ГСМ |
| `ChangePasswordViewModel.cs` | Смена/установка пароля данных |
| `UpdateViewModel.cs` | Процесс обновления |

### 5.3 `AVTOKarta\Views\`

UserControl (встраиваются в MainWindow): `SquadSetupView`, `SquadListView`, `VehicleListView`,
`CardEditView`(Window), `FuelReportView`, `SettingsView`, `WarehouseView`, `DriverListView`.

Отдельные окна (Window): `MainWindow`, `LicenseView` (активация),
`SquadEditView`, `VehicleEditView`, `ChangePasswordView`, `UpdateNotificationWindow`.

> `MainWindow.xaml` (~630 строк) содержит разметку почти всех экранов (DataGrid и т.д.) —
> при правках UI чаще всего лезть именно туда.

### 5.4 `AVTOKarta\Services\` — бизнес-логика

| Файл | Ключевой API / назначение |
|---|---|
| `DataService.cs` | Load/Save: `squads.json`, `drivers.json`, `vehicles.json`, `cards\<госномер>\<год>-<месяц>.json`, `warehouse_<squadId>.json`; `BackupCard` |
| `EncryptionService.cs` | AES-256 (CBC+PKCS7), PBKDF2 50k, ключ — DPAPI; `Encrypt/Decrypt`, `ReEncryptFile`, `MigrateFileToHmac`, `DecryptLegacyOrCurrent`, `ConstantTimeEquals`; экспорт может быть **без пароля** (детерминированный seed `OpenExportSeed`) |
| `SettingsService.cs` | `%LocalAppData%\AVTOKarta`: `password.key` (DPAPI), `default.key`, `password.hash`, `.hmac_v1_done` (маркер миграции), `DataPath`, `FixAclIfNeeded`, миграция файлов в HMAC |
| `CalculationService.cs` | `CalculateNormConsumption` (надбавки к пробегу/насосу, стоянка, груз), `CalculateActualConsumption` (по остаткам), `OilAgeFactor` (п. 18), `CalculateAdBlue`, `RecalculateAllRecords`, экономия/перерасход, `ClassifyTrip`, `CalculateByTripType` |
| `CacheService.cs` | автосохранение (`StartAutoSave`, интервал 60 с), `MarkDirty` |
| `ExcelExportService.cs` | месячная карточка → `.xlsx` |
| `FuelReportExportService.cs` | сводный отчёт по ГСМ → `.xlsx` |
| `DataTransferService.cs` | `ExportToFile(path, password)`, `ImportFromFile(...)`, `CreateBackup`; `ExportMetadata`, `ImportResult`; содержит `AppVersion` (версия!) |
| `UpdateService.cs` | GitHub API `hedgehog200/avtokarta` releases/latest; `CheckForUpdate/DownloadUpdate/ValidateDownload/ApplyUpdate/AutoUpdate`; лог `updater.log`; `installer.key` рядом с exe — **необязателен**: если есть, тихая установка идёт с `/PASSWORD=`; если нет — установщик запускается без пароля |

### 5.5 `AVTOKarta\Helpers\` и `Converters\`

- `RelayCommand.cs` — ICommand (+ дженерик).
- `DateTimeHelper.cs`, `PhoneHelper.cs`, `MarkdownHelper.cs` (отображение md в UI).
- `PasswordInputDialog.cs` — ввод пароля.
- `Converters\` — `BoolToVisibility`, `NullOrEmptyToVisibility`, `DoubleString`, `IntString`, `SquadIdToNumber`, `NavStyle` (для подсветки навигации).

### 5.6 `AVTOKarta\Security\` и `Licensing\` — защита (подробно §7)

- `Security\TamperGuard.cs` — сторож: подписи exe/dll + отладчик; событие `TamperDetected`, `Refresh()`, `IsTampered()`, `SetDevBypass()`.
- `Security\IntegrityChecker.cs` — `VerifyAppIntegrity`, `VerifyDirectory`, путь к `tamper.sig`, fallbackи пути к exe.
- `Security\AntiDebug.cs` — `Detect()` (Debugger.IsAttached, PEB, ProcessDebugPort и т.д.).
- `Licensing\LicenseService.cs` — `IsLicensed`, `TryValidateLicenseFile`, `TryInstallLicenseFile`, `GetMachineCode`, `HasFeature`, `EnsureFresh`, `PublisherPublicKeyXml` (публичный ключ **фрагментами**: `ModulusChunk1/2`, `Exponent`, `KeyFingerprint`).

### 5.7 `Shared\` — общий код (AVTOKarta ⇄ LicGen, линкуется в оба csproj)

- `LicenseInfo.cs`: enum `LicenseFeatures` (None=0, Core=1, Warehouse=2, ExportExcel=4, MultiSquad=8),
  `LicenseInfo`, `LicensePayload` (канонический JSON), `LicenseContainer` (3 строки: `AVTOKARTA-LICENSE-1` / base64 payload / base64 подпись), `LicenseCrypto` (RSA-2048 PKCS1 SHA256), `TamperManifest`/`TamperEntry` (манифест `tamper.sig`).
- `MachineCode.cs`: код машины = первые 20 hex SHA256(`AVTOKARTA|MachineGuid|MachineName`), вид `XXXXX-XXXXX-XXXXX-XXXXX`.

> ⚠️ Правка `Shared\*` меняет формат лицензий/манифеста **и в приложении, и в LicGen одновременно** — только синхронно.

### 5.8 `LicGen\` — генератор лицензий и подписей

| Файл | Назначение |
|---|---|
| `Program.cs` | CLI: `--init` (создать ключевую пару), `--install <file>`, `--sig-issue <exe>`, `--sig-check <exe>`, `--verify <file>`, интерактивный выпуск лицензии |
| `Gui\MainForm.cs` | GUI: база выданных лицензий и кодов ПК, выпуск в несколько кликов, создание ключей (+ `MachineInputDialog`) |
| `Gui\IssueLicenseDialog.cs` | Диалог выпуска лицензии |
| `KeyStore.cs` | Хранение ключей (`Keys\LicGen.key` — DPAPI CurrentUser, `Keys\LicGen.pub.xml`) |
| `LicenseStore.cs` | `LicenseRecord`/`MachineRecord`, база выданных (`Licenses\`) |
| `Signer.cs` | Подпись/проверка `tamper.sig` и лицензий |

Сборка LicGen: **только** `/p:Configuration=Release` (без `/p:Platform=x86` — иначе ошибка OutputPath).

## 6. Данные: где что лежит и как шифруется

Всё пользовательское — в `%LocalAppData%\AVTOKarta\`:

```
%LocalAppData%\AVTOKarta\
├── squads.json, squad.json (старый формат), drivers.json, vehicles.json
├── cards\<госномер>\<год>-<месяц>.json
├── warehouse_<squadId>.json
├── password.key / default.key   # ключи DPAPI
├── password.hash, .hmac_v1_done  # проверка пароля, маркер миграции HMAC
├── Licenses\license.lic          # лицензия
├── last_seen.dat                 # анти-откат даты (yyyy-MM-dd); метка в будущем => стоп
├── crash.log, updater.log
```

- Все JSON-файлы шифруются AES-256; производный ключ — PBKDF2(50000) + DPAPI-ключ.
- Миграция на HMAC выполняется однократно (маркер `.hmac_v1_done`), см. `SettingsService.MigrateAllFilesToHmac`.
- Смена пароля: `ChangePasswordView` → `SettingsService.SavePasswordChange` (перешифровка всех файлов).
- Экспорт `.avto` = ZIP из расшифрованных JSON + метаданные, весь архив шифруется AES-256;
  пароль можно оставить пустым (детерминированный seed `OpenExportSeed`). Формат файла НЕ менялся —
  новые сборки пишут ровно то, что читают старые, и наоборот.
- Импорт `.avto`: пароль проверяется методом `PreviewExport` ДО каких-либо изменений (с повтором ввода),
  затем файл целиком валидируется в памяти (типизированная нормализация `squads/vehicles/drivers/warehouse/cards`),
  делается снимок данных и выполняется запись; при сбое — откат из снимка. Пустые/старые форматы
  (`squads.json`, legacy-без-HMAC, export без заголовка) читаются автоматически (`DecryptAuto`).
- Надёжность файлов: все записи — атомарные (`EncryptionService.AtomicWriteAllBytes`, temp+Replace с повторами);
  чтение понимает старый формат без HMAC и самодо-лечивает файл (`DataService.ReadEncrypted`/`TryReadLocalJson`).
- Бэкап перед импортом — в «Документы\AVTOKarta_Backups» с запасным вариантом `%LocalAppData%\AVTOKarta\Backups`
  (не блокирует импорт при недоступности «Документов»).
- Автосохранение: `CacheService.StartAutoSave` — раз в 60 с + по Ctrl+S; при смене пароля сервисы пересоздаются.
  Во время импорта автосохранение останавливается и возобновляется после.

Код машины пользователя: `5909C-33E58-82831-4CE9E` (для теста лицензий).

## 7. Защита и лицензирование

### 7.1. Модель

1. **Лицензия**: файл `license.lic` подписывается приватным ключом `LicGen\Keys\LicGen.key` (DPAPI);
   приложение проверяет встроенным открытым ключом (фрагменты в `LicenseService.cs`, проверка отпечатка SHA-256).
   Привязка к коду машины; пустой код = «плавающая» лицензия. Фичи — битовая маска, приложение требует `Core`.
2. **Целостность**: `tamper.sig` — RSA-подписанный манифест всех exe/dll в папке приложения
   (создаётся `LicGen --sig-issue`, проверяется `TamperGuard`/`IntegrityChecker` при запуске).
3. **Антиотладка**: `AntiDebug` + ConfuserEx `anti debug`/`anti dump`.
4. **Обфускация**: ConfuserEx, набор защит: `anti debug`, `anti dump`, `ctrl flow`, `constants(decoderCount=2)`.
   **Не используем**: `rename` (ломает WPF BAML/x:Class), `anti tamper` (ломает подпись exe).
5. **Анти-откат даты**: `last_seen.dat`; удаление метки сбрасывает защиту (fail-open на IO-ошибки).

### 7.2. Символ компиляции `PROTECTION` (ВАЖНО — сделано, но ещё не закоммичено)

Раньше защита отключалась только `#if DEBUG`. Теперь включён подход **сборка без защиты**:

- `AVTOKarta.csproj`: объявлен `ProtectionConstants=PROTECTION`, подставляется в `DefineConstants`
  **только** в конфигурации `Release|x86`. Сборка с `/p:NoProtection=true` защиту выключает:
  ```powershell
  msbuild AVTOKarta\AVTOKarta.csproj /p:Configuration=Release /p:NoProtection=true
  ```
- В коде проверки обёрнуты в `#if PROTECTION`:
  - `App.xaml.cs` — `#if !PROTECTION TamperGuard.SetDevBypass(true);`
  - `Security\AntiDebug.cs` — `Detect()` вне PROTECTION сразу возвращает пустой список (P/Invoke компилируется нафиг);
  - `Security\IntegrityChecker.cs` — `VerifyAppIntegrity` вне PROTECTION → `true` (+ fallbackи `ApplicationExePath`);
  - `Security\TamperGuard.cs` — `Refresh()` вне PROTECTION не проверяет ничего;
  - `Licensing\LicenseService.cs` — `HasFeature` вне PROTECTION не смотрит на `TamperGuard.IsTampered()`;
  - `MainWindow.xaml.cs` — `StartGuard()`/`RefreshLicenseGates()` вне PROTECTION не подключают сторожа.
- Итог: в Debug (и любом не-Release|x86) бинарник **полностью без** проверок → запуск из VS, dnSpy/ILSpy,
  запуск без `tamper.sig` работают. В Release|x86 защита работает всегда.

### 7.3. Ключи и смена ключа

- Публичный ключ в `LicenseService.cs` хранится фрагментами (`ModulusChunk1/2`, `Exponent`, `KeyFingerprint`)
  и собирается `BuildPublisherPublicKeyXml()`; `ValidatePublisherKeyFingerprint()` сверяет SHA-256 XML-строки вида
  `<RSAKeyValue><Modulus>...</Modulus><Exponent>...</Exponent></RSAKeyValue>`.
- При `LicGen --init --force` нужно **одновременно** обновить все константы и пересчитать отпечаток.
- `.gitignore`: `LicGen.key`, `LicGen.pub.xml`, `*.lic`, `installer.key`.

## 8. Сборка, релиз, обновления

### Обычная сборка
```powershell
msbuild AVTOKarta.sln /p:Configuration=Release /t:Build   # или AVTOKarta\AVTOKarta.csproj /p:Platform=x86
```
MSBuild: `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`.

### Релизный конвейер — `.\build-release.ps1` (опция `-NoObfuscation`)
1. MSBuild `AVTOKarta\AVTOKarta.csproj` Release **x86**;
2. ConfuserEx: `Confuser.CLI.exe -n <*.crproj>` (ищет `.crproj` в репо/`%TEMP%\confuser\avtokarta.crproj`),
   результат `bin\Release\Confused\AVTOKarta.exe` копируется поверх;
3. `LicGen.exe --sig-issue <exe>` → генерация `tamper.sig`;
4. `LicGen.exe --sig-check <exe>` → контроль.

> ⚠️ После **любого** нового exe подпись обязательна, иначе приложение откажется работать («Программа изменена»).

### Установщик
```powershell
iscc AVTOKarta_setup.iss     # → Output\AVTOKarta_vX.Y.Z_setup.exe
```
Установщик **обязан** копировать `tamper.sig` (прописано в `.iss`) — без него установленная копия не стартует (выход 0).
Пароль установки (`Password=`/`Encryption=`) **не используется**: защита обеспечивается лицензией и манифестом целостности.

### Чек-лист повышения версии
1. `AVTOKarta\Properties\AssemblyInfo.cs` — `AssemblyVersion`/`AssemblyFileVersion`
2. `AVTOKarta\MainWindow.xaml` — заголовок окна (`... v1.3.0 - WebARTup - Studio: Technologies`)
3. `AVTOKarta\Services\DataTransferService.cs` — `AppVersion` (берётся из сборки автоматически — менять не нужно)
4. `AVTOKarta_setup.iss` — `MyAppVersion` / `VersionInfoVersion`
5. Сборка → `build-release.ps1` → `iscc` → `git tag vX.Y.Z` → релиз в GitHub
   (`installer.key` рядом с exe необязателен — нужен только если установщик защищён паролем)

### Самообновление
`UpdateService`: GitHub API `https://api.github.com/repos/hedgehog200/avtokarta/releases/latest`,
лог `updater.log` в папке данных, мьютекс от параллельных обновлений. `installer.key` рядом с exe
необязателен: если файл есть — к тихой установке добавляется `/PASSWORD=`, иначе установщик
запускается без пароля (текущий установщик пароля не требует).

## 9. Текущее состояние Git (14.10.2026)

- Последний тег: **v1.3.0** «модуль ГСМ по методике МЧС: надбавки, факт по остаткам, часовые нормы, груз, AdBlue; UI-правки».
- Ветка `master` синхронизирована с `origin` (полный репозиторий разработки).
- Публикация: код — push в `origin`; релизный тег — push в `release` (`hedgehog200/avtokarta`).
- **Не отслевается намеренно (не коммитим)**: `LicGen\Licenses\` (база выданных лицензий, коды машин),
  `доп.информация\` (документы заказчика).
- ⚠️ `.gitignore` содержит `AVTOKarta_setup.iss`, `packages/`, `*.lic`, `LicGen.key` — новые ключи/лицензии в репо не попадут.

## 10. Что сделано (история по версиям)

| Версия | Содержание |
|---|---|
| v1.1.x | Базовый функционал: подразделения, ТС, водители, карточки, расчёт ГСМ, Excel, пароли; редизайн UI |
| v1.1.9 | SSL/TLS-фикс для Windows 7, условный отчёт ГСМ |
| v1.2.0 | Точность расчётов до тысячных, Excel (Times New Roman, заправка/масло), редизайн экрана карточки, закрытие модалок по «Отмена» |
| v1.2.1 | Работа **без пароля** (экспорт без пароля), 4 знака для бензиновых ГСМ, надёжное обновление |
| v1.2.2 | UI-редизайн: Lucide-style иконки → Font Awesome 6 Solid, общие стили, скроллбары, hover; многосоставная ICO (16–256), иконка задачи через Win32 `WM_SETICON` |
| v1.2.3 | **Лицензирование** (RSA `license.lic`, экран активации, фичи-гейты), **манифест целостности `tamper.sig`**, **LicGen GUI** (база выданных, выпуск в клики), предупреждение об экспорте без пароля, постоянная энептопия DPAPI, конвейер `build-release.ps1`, обфускация ConfuserEx |
| v1.2.4 | Смена `#if DEBUG` → символ `PROTECTION` (Release\|x86), сборка без защиты через `/p:NoProtection=true` (§7.2); ускорение PBKDF2 через Windows CNG (формат `.avto` не меняется); асинхронные экспорт/импорт с индикатором прогресса; исправление имени файла склада при импорте; удаление остаточных файлов при откате импорта; `AppVersion` из сборки; честный текст диалога импорта (слияние карточек); восстановление обфускации (`avtokarta.crproj` в репо) и надёжности `build-release.ps1` |
| v1.2.5 | Установщик **без пароля** (`Password=`/`Encryption=` убраны); автообновление больше **не требует `installer.key`** — `/PASSWORD=` добавляется только если файл есть (§8) |
| v1.3.0 | **ГСМ по методике МЧС**: процентные надбавки (к пробегу — все, к насосу — «Зимняя»/«Горная», ред. справочник на уровне части), факт по остаткам топлива (наличие перед выездом/при постановке), насос в минутах, надбавки к нормам масел для ТС > 5 лет (п. 18), часовые нормы стоянки/прогрева (пп. 16–17), доп. расход на массу груза/прицепа (п. 20), AdBlue; взаимоисключение «Кондиционер/Климат-контроль» ↔ «Зимняя»; **UI**: правка справочника надбавок, кнопки вне прокрутки, подгонка окон под экран |

Детали релизов — в `RELEASE_v1.2.1.md` … `RELEASE_v1.3.0.md`, `README.md`.

## 11. Ловушки при ручной доработке

1. **После правки кода в Release-ветке обязательно переподписывать** (`LicGen --sig-issue`), иначе защита «Программа изменена».
2. **`Shared\` линкуется в оба проекта** — правки синхронизируются автоматически, но формат данных меняется для обоих.
3. **Нет тестов** — после изменений только: сборка → ручной прогон → проверка XAML-биндингов (ошибки видны в Output).
4. Числа в UI и Excel — формат **F3 / 0.000** (до тысячных) везде.
5. Версия дублируется в 4 местах (§8, чек-лист).
6. ConfuserEx-конфиг — формат `.crproj` (template project), дочерний элемент защиты = `<argument>`,
   ID: `anti debug`, `anti dump`, `ctrl flow`, `constants` (не `control flow`!). Шаблон `member('...')` не поддерживается.
7. Проект x86 (32 бит) — рефлексия над `AVTOKarta.exe` тестами делать из 32-битного шелла.
8. `LicGen` собирается **без** `/p:Platform=x86`.
9. Окно лицензии при валидном `license.lic` не показывается — это нормальное поведение.
10. Рабочие памяти Serena (`.serena/memories/`): `core`, `tech_stack`, `conventions`, `avtokarta-build-deploy`,
    `licensing-system`, `task_completion`, `suggested_commands` — обновляйте их при крупных изменениях.

## 12. Идеи / не сделано

- Автоматизировать проверку изменений (тестов нет — ручной чек-лист §8/§11).
- `packages/`, `bin/`, `obj/` в репозитории (легаси-паттерн) — можно чистить.
- ConfuserEx CLI лежит в `%TEMP%\opencode\confuser\ext\` — можно перенести в репозиторий/автоскачивание.
