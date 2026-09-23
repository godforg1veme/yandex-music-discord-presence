# User-facing README implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Сделать главную страницу репозитория простой инструкцией для пользователя Яндекс Музыки и Discord.

**Architecture:** `README.md` отвечает за скачивание, установку и повседневное использование. `docs/development.md` хранит команды и техническое объяснение. Краткое описание GitHub обновляется отдельно через `gh repo edit`.

**Tech Stack:** Markdown, GitHub CLI.

**Spec:** `docs/superpowers/specs/2026-09-23-user-facing-readme-design.md`

## Global Constraints

- Не менять поведение приложения, имя репозитория и релизные файлы.
- Сохранить честные сведения о приватности, поиске обложек и недоступности «Слушать вместе».
- GitHub description: «Показывает музыку из приложения Яндекс Музыки в Discord».

---

### Task 1: Разделить пользовательскую и техническую документацию

**Files:**
- Modify: `README.md`
- Create: `docs/development.md`

**Interfaces:** `README.md` ссылается на `docs/development.md`; оба ссылаются на существующие файлы и релизы.

- [ ] **Step 1:** Перенести из README команды `dotnet run`, `dotnet publish`, `dotnet test` и подробное описание поиска обложек в `docs/development.md`.
- [ ] **Step 2:** Переписать README: одно предложение о пользе, ссылка на Releases, три шага установки, автозапуск из трея, краткий раздел о неполадках и приватности, ссылка на документацию разработчика.
- [ ] **Step 3:** Применить правила Humanizer к прозе: убрать технический жаргон на первом экране, повторы и рекламные клише; не менять команды и адреса ссылок.
- [ ] **Step 4:** Проверить локальные Markdown-ссылки и `git diff --check`, затем просмотреть diff на потерянные факты.
- [ ] **Step 5:** Закоммитить документацию.

### Task 2: Обновить описание GitHub

**Files:** metadata репозитория `godforg1veme/yandex-music-discord-presence`.

**Interfaces:** не влияет на сборку или релиз.

- [ ] **Step 1:** Выполнить `gh repo edit godforg1veme/yandex-music-discord-presence --description "Показывает музыку из приложения Яндекс Музыки в Discord"`.
- [ ] **Step 2:** Проверить новое описание через `gh repo view`.
- [ ] **Step 3:** Опубликовать коммит README в `origin/main`, проверить чистое дерево и синхронизацию.
