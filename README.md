# 🌙 ScreenDimmer Pro (Beta)

<div align="center">

![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-blue?style=for-the-badge&logo=windows)
![.NET](https://img.shields.io/badge/.NET-10.0%20%7C%208.0-purple?style=for-the-badge&logo=dotnet)
![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)
![Status](https://img.shields.io/badge/Status-Beta%20v2.1-orange?style=for-the-badge)

**Легковесная, отказоустойчивая утилита в системном трее Windows для комфортной работы ночью, мгновенного затемнения экранов, режима «Blackout» без сна системы и плавной регулировки яркости.**

[Возможности](#-ключевые-возможности) • [Горячие клавиши](#-горячие-клавиши) • [Архитектура](#-архитектура-и-надежность) • [Сборка](#-сборка-проекта) • [English Description](#-english-summary)

</div>

---

## ✨ Ключевые возможности

* 🌑 **Ночной режим «Blackout» (`Alt + Backspace`):**
  * Полное покрытие всех мониторов (`#000000`) без зазоров и щелей.
  * Автоматический сброс аппаратной яркости до **0%**.
  * **Предотвращение сна ПК (`SetThreadExecutionState`):** компьютер работает на 100% мощности (рендеринг, закачки, музыка, боты не прерываются).
  * **Защитный экран от случайных нажатий (Strict Shield):** поглощает любые случайные клики и нажатия клавиш в темноте (включая кнопку `Win`).
* 🖱️ **Регулировка яркости колёсиком мыши по трею:**
  * Прокрутка колесика над панелью задач / областью часов меняет общую яркость с шагом **±5%**.
* 📊 **Наэкранный индикатор (Brightness HUD / OSD):**
  * Минималистичный полупрозрачный индикатор яркости с прогресс-баром в верхней части экрана (не перехватывает фокус у игр и программ).
* 🌊 **Плавная кино-анимация (Fade In / Fade Out):**
  * Экраны мягко погружаются в темноту за 180 мс без резких ударов по глазам.
* ⏱️ **Таймер сна (Sleep Timer):**
  * Автоматическая активация Blackout через `15`, `30`, `45`, `60` или `120` минут.
* 🔇 **Умный ночной Mute (Windows CoreAudio API):**
  * Приглушение системного звука на ночь и автоматический возврат при выходе из Blackout.
* 🖥️ **Поддержка любых экранов и масштабов (Mixed DPI / Multi-Monitor):**
  * Корректная работа на мониторах с масштабом `125%`, `150%`, `200%` (Per-Monitor V2).
  * Одновременная поддержка дисплеев ноутбуков (**WMI**) и внешних мониторов (**DDC/CI / DXVA2**).

---

## ⌨️ Горячие клавиши (Раскладконезависимые)

Привязка к физическим аппаратным скан-кодам гарантирует одинаковую работу в **русской, английской и любых других раскладках**:

| Сочетание клавиш | Русская раскладка | Английская раскладка | Действие |
| :--- | :--- | :--- | :--- |
| **`Alt` + `Backspace`** | `Alt` + `Backspace` | `Alt` + `Backspace` | 🌑 **Ночной Blackout** (экраны черные, клавиатурный щит, ПК не спит) |
| **`Колёсико мыши в трее`** | *Колесо ⬆️ / ⬇️* | *Колесо ⬆️ / ⬇️* | 📊 Быстрая регулировка яркости ±5% с показом HUD |
| **`Alt` + `.`** | `Alt` + `Ю` | `Alt` + `.` | 🌙 **Затемнить экраны** до выбранного % (по умолчанию `0%`) |
| **`Alt` + `/`** | `Alt` + `.` | `Alt` + `/` | ☀️ **Восстановить нормальную яркость** / Выйти из Blackout |
| **`Alt` + `Shift`** | *Стандарт Windows* | *Стандарт Windows* | 🌐 Обычная смена языка ввода (без перехватов) |

---

## 🛡️ Архитектура и надежность

* **Защита от сбоев и перезагрузок (`SessionRecoveryManager`):** перехват `WM_QUERYENDSESSION` / `SessionEnding` гарантирует пробуждение экранов и возврат нормальной яркости при выключении или ребуте ПК.
* **0% утечек памяти:** корректный вызов Win32 `DestroyIcon` для динамических иконок трея, финализаторы системных хуков `~KeyboardHookManager` и `~TrayMouseWheelHook`.
* **Single Instance:** системный мьютекс предотвращает повторный запуск дубликатов процесса.

---

## 🚀 Сборка проекта

### Требования
* Windows 10 / 11
* .NET SDK 8.0 / 9.0 / 10.0+

### Сборка и публикация единого `.exe`:
```bash
# Клонирование репозитория
git clone https://github.com/MortuisVMain/ScreenDimmer.git
cd ScreenDimmer

# Сборка Release
dotnet build -c Release

# Публикация автономного Single-File исполняемого файла
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./publish
```

---

## 🇬🇧 English Summary

**ScreenDimmer Pro** is a lightweight Windows System Tray application designed for night owls, power users, and multi-monitor setups.

### Features
* **Night Blackout Mode (`Alt + Backspace`):** Pure-black canvas on all displays + 0% hardware brightness + Sleep prevention (`SetThreadExecutionState`) + Rogue keystroke shield.
* **Tray Mouse Wheel Scroll:** Scroll over the Windows taskbar/tray area to quickly adjust brightness with a smooth on-screen HUD.
* **Multi-DPI & Multi-Monitor support:** Per-Monitor V2 aware (supports 125%, 150%, 200% mixed scaling), WMI (laptops) + DDC/CI DXVA2 (external monitors).
* **Sleep Timer & Night Mute:** Set a timer to sleep or mute audio automatically in Blackout mode.
* **Layout Independent:** Physical scan codes (`0x0E`, `0x34`, `0x35`) work identically on English, Russian, or any other keyboard layout.

---

## 📄 Лицензия

Распространяется под лицензией [MIT](LICENSE).
