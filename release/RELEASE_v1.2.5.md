# АВТОКарта v1.2.5

**Дата:** 14.10.2026

---

## Что нового

### Установщик больше не требует пароль
- В `AVTOKarta_setup.iss` убраны директивы `Password=` и `Encryption=yes`.
  Защита обеспечивается лицензией (`license.lic`) и манифестом целостности `tamper.sig`,
  пароль установки больше не нужен.

### Автообновление больше не требует `installer.key`
- `UpdateService` (**`AVTOKarta\Services\UpdateService.cs`**) больше **не блокирует** тихую
  автоустановку `.exe`, если рядом с программой нет `installer.key`.
- Параметр `/PASSWORD=…` добавляется к команде установщика **только если** файл `installer.key`
  существует. Если файла нет — установщик запускается как есть (без пароля).
- Обратная совместимость: если пароль когда-нибудь вернут, достаточно положить `installer.key` —
  поведение станет прежним.

---

## Совместимость

- Формат файлов данных и файла выгрузки `.avto` **не изменён** — обмен со сборками
  v1.1.0 … v1.2.4 работает в обе стороны.
- Апдейтер во **всех** версиях начиная с v1.1.0 предпочитает ассет `.zip` перед `.exe`; в этом
  релизе приложен только установщик `setup.exe`.
- ⚠️ Автообновление тянет последний релиз (`/releases/latest`); релиз должен быть обычным
  (не draft, не prerelease).

---

## Технические детали

### Версия (обновлено)
- `AVTOKarta\Properties\AssemblyInfo.cs` — `AssemblyVersion` / `AssemblyFileVersion` (1.2.5)
- `AVTOKarta\MainWindow.xaml` — заголовок окна (v1.2.5)
- `AVTOKarta_setup.iss` — `MyAppVersion` / `VersionInfoVersion` (1.2.5)

### Изменённые файлы
- `AVTOKarta\Services\UpdateService.cs` — пароль установщика стал необязательным
- `AVTOKarta_setup.iss` — убраны `Password=` / `Encryption=yes`

### Установщик
- Inno Setup: `AVTOKarta_setup.iss` — **без пароля** (защита — лицензия и `tamper.sig`)
- Имя выходного файла: `AVTOKarta_v1.2.5_setup.exe`
- В состав входят все библиотеки приложения и манифест `tamper.sig`

### Порядок сборки
1. `build-release.ps1` — MSBuild (Release x86) → обфускация ConfuserEx → переподпись `tamper.sig`
2. `iscc AVTOKarta_setup.iss` → `Output\AVTOKarta_v1.2.5_setup.exe`
3. `git tag v1.2.5` → push тега в `release` → релиз в GitHub
