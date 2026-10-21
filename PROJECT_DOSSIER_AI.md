# 🌙 SCREENDIMMER PRO — Полный AI-Контекстный Паспорт Проекта (Sequoia Edition)

> **ИНСТРУКЦИЯ ДЛЯ ИИ (AI SYSTEM PROMPT):**  
> Ты выступаешь в роли **Senior C# / Windows Systems Engineer**.  
> Этот документ содержит 100% самодостаточный контекст проекта **ScreenDimmer Pro**.  
> Внимательно изучи архитектуру Win32 Interop, DDC/CI протоколы, хуки и Apple Dynamic Island HUD, чтобы развивать проект, добавлять фичи или собирать новые релизы.

---

## ⏱ 1. Метаданные и Статус Проекта
- **ID Проекта:** `PROJECT-02`
- **Кодовое имя:** `ScreenDimmer`
- **Абсолютный путь:** `c:\Users\mortu\OneDrive\For Work\Ideas\ScreenDimmer\`
- **Текущая фаза:** `Beta Release v2.1 / Apple Design Revamp`
- **Архитектурный стек:** C# .NET 10.0 / 8.0, Windows Forms, Win32 Native Interop, Windows CoreAudio API, DDC/CI (DXVA2), WMI
- **Дизайн-Стандарт:** `DESIGN_APPLE.md` (macOS Sequoia Dynamic Island HUD OSD)
- **Статус сборки:** ✅ **0 ошибок, 0 предупреждений (net10.0-windows Release)**

---

## 🎯 2. Главная Цель и Ценность
**ScreenDimmer Pro** — это сверхнадежная системная утилита в трее Windows для комфортной работы за компьютером ночью.  
Главные возможности:
1. **Режим «Blackout» (`Alt + Backspace`):** Мгновенное погружение всех экранов в 100% темноту (`#000000`) без перевода ПК в спящий режим. Компьютер продолжает рендерить видео, скачивать файлы, обучать нейросети или выполнять бот-задачи на 100% мощности.
2. **Строгий клавиатурный щит:** Поглощает любые случайные клики и нажатия в темноте (включая кнопку `Win`).
3. **Регулировка колесиком в трее:** Вращение колеса мыши над панелью задач / часами плавно меняет яркость на **±5%** с показом парящего индикатора в стиле macOS Sequoia Dynamic Island.
4. **Гибридный контроль:** Управляет как экранами ноутбуков (WMI), так и внешними мониторами (DDC/CI DXVA2).

---

## 🏗 3. Полная Архитектура и Структура Файлов

```
ScreenDimmer/
├── Program.cs                   # Точка входа, проверка Single Instance через системный Mutex
├── TrayApplicationContext.cs    # Контекст приложения: иконка трея, контекстное меню, координация
├── KeyboardHookManager.cs       # WH_KEYBOARD_LL: низкоуровневый перехват аппаратных скан-кодов (Alt+Backspace)
├── TrayMouseWheelHook.cs        # WH_MOUSE_LL: перехват колеса мыши над Taskbar / Shell_TrayWnd
├── BrightnessController.cs      # Движок управления яркостью: гибрид WMI + DDC/CI (dxva2.dll)
├── BrightnessHud.cs             # 🍏 Парящий индикатор в стиле Apple Dynamic Island (240x64 сквиркл)
├── BlackoutManager.cs           # Полноэкранный холст #000000 на все мониторы + SetThreadExecutionState
├── AudioEndpointController.cs   # Windows CoreAudio API: авто-приглушение звука ночью и возврат громкости
├── SleepTimerManager.cs         # Таймеры сна (автоматический Blackout через 15, 30, 45, 60, 120 мин)
├── SessionRecoveryManager.cs    # Перехват WM_QUERYENDSESSION: защита от застревания черных экранов при ребуте
├── SettingsManager.cs           # Сериализация настроек пользователя в JSON
├── NativeMethods.cs             # Импорты Win32 API (user32, gdi32, dxva2, kernel32)
└── ScreenDimmer.csproj          # Манифест проекта (.NET 10.0-windows / .NET 8.0-windows)
```

---

## ⚙️ 4. Ключевые Механики и Низкоуровневые Алгоритмы

### 4.1. Раскладконезависимый Blackout (`Alt + Backspace`)
- Использует хук `SetWindowsHookEx(WH_KEYBOARD_LL, ...)`.
- Привязка идет к **физическому скан-коду клавиши** (`KBDLLHOOKSTRUCT.scanCode == 0x0E`), а не к виртуальным кодам символов.
- Работает 100% одинаково в русской, английской, немецкой или любой другой раскладке.
- При активации вызывается `SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_AWAYMODE_REQUIRED)`, блокируя переход Windows в режим сна.
- Strict Shield поглощает клики мыши и нажатия `VK_LWIN` / `VK_RWIN` в темноте.

### 4.2. 🍏 Apple Dynamic Island HUD OSD (`BrightnessHud.cs`)
- Окно без рамок с флагами `WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST` (не перехватывает фокус у игр и видеоплееров).
- Форма: парящая капсула `240x64px` с закруглением углов сквиркла `20px`.
- Материалы: темное матовое стекло (`Color.FromArgb(240, 22, 22, 26)`), субпиксельный кант `1px` и мягкая тень.
- Интеллектуальная динамическая иконка:
  - `0%`: 🌑
  - `1–29%`: 🌘
  - `30–69%`: 🌤️
  - `70–100%`: ☀️
- Капсульный слайдер высотой `10px` с градиентным заполнением от Apple White `#f5f5f7` к Apple Vibrant Blue `#0071e3`.
- Плавная анимация появления и угасания Opacity за 1100 мс.

### 4.3. Гибридный Контроллер Яркости (`BrightnessController.cs`)
- **Внутренние дисплеи ноутбуков:** Использует пространство имен WMI `root/wmi:WmiMonitorBrightnessMethods` (`WmiSetBrightness`).
- **Внешние мониторы:** Использует `dxva2.dll`:
  1. `EnumDisplayMonitors` для перечисления мониторов.
  2. `GetPhysicalMonitorsFromHMONITOR` для получения дескрипторов мониторов.
  3. `SetMonitorBrightness` по шине I2C / DDC/CI.
- Сохраняет исходные уровни яркости для корректного восстановления при выходе из затемнения.

---

## ⌨️ 5. Горячие Клавиши
| Сочетание клавиш | Раскладка RU | Раскладка EN | Действие |
| :--- | :--- | :--- | :--- |
| **`Alt` + `Backspace`** | `Alt + Backspace` | `Alt + Backspace` | 🌑 **Blackout** (экраны в 0%, клавиатурный щит, ПК не спит) |
| **`Колесико в трее`** | *Колесо ⬆️ / ⬇️* | *Колесо ⬆️ / ⬇️* | 📊 Регулировка яркости ±5% с показом Apple HUD |
| **`Alt` + `.`** | `Alt + Ю` | `Alt + .` | 🌙 Затемнить мониторы до минимума |
| **`Alt` + `/`** | `Alt + .` | `Alt + /` | ☀️ Восстановить нормальную яркость / Выйти |

---

## 🚀 6. Сборка и Запуск

```bash
# Сборка Release версии через .NET CLI
dotnet build -c Release ScreenDimmer.csproj

# Публикация единого независимого .EXE файла (Single File)
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

# Запуск скомпилированного бинарника
./bin/Release/net10.0-windows/ScreenDimmer.exe
```

---

## 📋 7. Готовые Задачи для Следующего ИИ (AI Action Prompts)
1. **Prompt для TrueTone:** *"Добавь в BrightnessController.cs логарифмическую кривую регулировки яркости (Perceptual Brightness Curve), чтобы шаги на 0–20% были более плавными для глаз."*
2. **Prompt для Расписания:** *"Реализуй функцию Sunset-to-Sunrise для автоматического плавного затемнения экрана по географическим координатам или системному времени."*
3. **Prompt для Tray UI:** *"Добавь в контекстное меню трея переключатель пресетов (Кино, Чтение, Кодинг, Blackout) со стилизацией под Windows 11 Fluent."*
