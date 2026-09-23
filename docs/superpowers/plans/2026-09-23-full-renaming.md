# Full project rename implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Убрать `Presence` из актуальных названий проекта, сборки и кода, сохранив работающее подключение к Discord.

**Architecture:** Публичный Discord Application ID остаётся прежним. .NET-проект, классы активности, исполняемый файл и архив получают новые имена. Старый автозапуск переносится на новый путь без затрагивания чужих записей.

**Tech Stack:** .NET 10, WinForms, xUnit, PowerShell, GitHub Actions, Discord Developer Portal.

**Spec:** `docs/superpowers/specs/2026-09-23-full-renaming-design.md`

## Global Constraints

- Сохранить Discord Application ID `1552113063606489088`.
- Новые имена: `Yandex Music for Discord`, `YandexMusicDiscord.exe`, `YandexMusicDiscord-win-x64.zip`, `godforg1veme/yandex-music-for-discord`.
- Не удалять старый релиз и не менять поведение передачи трека.
- Сначала проверить локальные тесты и publish, затем менять GitHub и выпускать версию.

---

### Task 1: .NET и автозапуск

**Files:** `src/YandexMusicPresence/*`, `tests/YandexMusicPresence.Tests/*`, `assets/presence-icon.svg`, `assets/presence-icon.png`.

**Interfaces:** пространство имён `YandexMusicDiscord`; типы `ActivityCoordinator` и `ActivityMapper`; выходной файл `YandexMusicDiscord.exe`.

- [ ] **Step 1:** Переименовать каталоги/проекты в `src/YandexMusicDiscord` и `tests/YandexMusicDiscord.Tests`; обновить ссылки проектов и `InternalsVisibleTo`.
- [ ] **Step 2:** Переименовать классы `PresenceCoordinator` и `PresenceMapper`, пространства имён, метаданные продукта, локальные файлы иконки и тесты; оставить `DiscordIpcClient` и существующий Application ID.
- [ ] **Step 3:** Добавить в `StartupRegistration` перенос старой записи автозапуска только при значении, оканчивающемся на `YandexMusicPresence.exe`; остальные записи не менять. Покрыть проверку тестом чистой функции распознавания старого пути.
- [ ] **Step 4:** Выполнить `dotnet test tests/YandexMusicDiscord.Tests/YandexMusicDiscord.Tests.csproj -c Release` и `dotnet publish src/YandexMusicDiscord/YandexMusicDiscord.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist/win-x64`; проверить имя exe.
- [ ] **Step 5:** Закоммитить проверенное переименование исходников.

### Task 2: Документация и сборка

**Files:** `README.md`, `docs/development.md`, `.github/workflows/build.yml`, `.github/workflows/release.yml`.

**Interfaces:** GitHub Actions публикует `YandexMusicDiscord-win-x64.zip` и `SHA256SUMS.txt`.

- [ ] **Step 1:** Обновить актуальные команды, ссылки, названия архива/exe и краткие пояснения без слова `Presence` в пользовательских названиях; исторические документы сохранить.
- [ ] **Step 2:** Обновить workflow сборки и релиза на новые пути и имена, а название релиза на `Yandex Music for Discord`.
- [ ] **Step 3:** Проверить `rg -n -i 'Presence|YandexMusicPresence|music-discord-presence' README.md docs/development.md src tests .github assets`, `git diff --check` и тесты; обоснованные технические исключения выписать.
- [ ] **Step 4:** Закоммитить документацию и CI.

### Task 3: Discord и публикация

**Files:** Discord asset, GitHub metadata, новый тег релиза.

**Interfaces:** Discord activity использует тот же Application ID и запасной asset `yandex-music`.

- [ ] **Step 1:** Загрузить тот же значок в существующее Discord-приложение с ключом `yandex-music`, затем обновить fallback key в коде и тестах. Если портал требует входа, запросить действие пользователя.
- [ ] **Step 2:** Проверить Discord API `/applications/1552113063606489088/rpc`: имя должно быть `Yandex Music`.
- [ ] **Step 3:** Переименовать GitHub-репозиторий в `yandex-music-for-discord`, обновить origin и ссылки, затем опубликовать изменения.
- [ ] **Step 4:** Создать новый тег `v0.2.0`; дождаться GitHub Actions, проверить ZIP, SHA-256 и публичную ссылку.
- [ ] **Step 5:** Закрыть старую программу, запустить новую и проверить статус на реальном треке; пользователю потребуется перезапустить настольный Discord для обновления кэша имени.
