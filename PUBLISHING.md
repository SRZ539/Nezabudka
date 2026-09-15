# Публикация Незабудки

## Загрузка через GitHub Desktop

1. Откройте GitHub Desktop и войдите в свой аккаунт.
2. File → Add local repository → выберите папку Nezabudka.
3. Проверьте подготовленные изменения, укажите понятное описание и создайте коммит.
4. Нажмите Push origin, чтобы отправить коммит на GitHub.
5. На сайте в корне должны находиться README.md, src, tests, tools, Nezabudka.slnx и остальные исходники, а готовые ZIP-сборки следует прикреплять к Releases.

Инструкция GitHub: https://docs.github.com/en/desktop/adding-and-cloning-repositories/adding-an-existing-project-to-github-using-github-desktop

## Готовая программа в Releases

1. На Windows выполните команду `dotnet publish` из README (нужен .NET 10 SDK).
2. Запустите результат и проверьте сохранение после перезапуска, напоминание в трее и переключение языков.
3. В PowerShell из корня проекта выполните:

```powershell
Compress-Archive -Path artifacts/nezabudka-win-x64/* -DestinationPath artifacts/Nezabudka-0.2.0-alpha-win-x64.zip
```

4. GitHub → репозиторий → Releases → Draft a new release / Create a new release.
5. Создайте тег `v0.2.0-alpha` на опубликованной ветке. Заголовок: `Незабудка 0.2.0 Alpha`.
6. Прикрепите ZIP готовой программы и отметьте This is a pre-release, затем опубликуйте релиз.

Пример описания релиза:

Локальный блокнот для Windows x64. Закрепление заметок, переносимый архив, резервные копии, выбор папки данных, глобальные горячие клавиши, три языка и калькулятор со вставкой в позицию курсора. Распакуйте `Nezabudka-0.2.0-alpha-win-x64.zip` целиком и запустите `Nezabudka.exe`. Это ранняя версия; об ошибках сообщайте в Issues.

Инструкция GitHub: https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository

## Лицензия и оформление

Лицензия приложения не назначена автоматически. Если хотите разрешить другим использовать, изменять и распространять ваш код, в том числе коммерчески, можно выбрать MIT; для требования сохранять открытость производных работ рассмотрите GPL. Перед выбором определите условия для собственного кода и происхождение графики. Тексты лицензий шрифтов оставляйте независимо от выбора лицензии кода.

Уточните источник anime-lineart.png и nezabudka.ico: в архиве нет сведений об их происхождении. Не приписывайте их Noto Fonts. Дополните THIRD_PARTY_NOTICES.md.

В About можно добавить темы: csharp, wpf, windows, notes, reminders, desktop-app.
После публикации закрепите репозиторий в профиле через Customize your pins.
