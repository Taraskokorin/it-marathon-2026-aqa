# Green & Tasty — E2E-тести групового pre-order

Домашнє завдання **Test Automation #8**, EPAM IT Marathon 2026. Автор: Kokorin Taras.

- **Напрям:** 1 — груповий pre-order (власна реалізація Backend ДЗ #5 + Frontend ДЗ #6, інтегровані й розгорнуті в AWS).
- **Підхід:** BDD (Reqnroll, Gherkin) + Page Object Model для UI + шар API-клієнта й «задач» (tasks) для підготовки даних.
- **Рівні:** L1 (перегляд учасників), L2 (підтвердження pre-order), L3 (видалення учасника) + наскрізний сценарій.
- **Об'єкт тестування:** код застосунку — https://github.com/Taraskokorin/it-marathon-2026

## Чому такий підхід

- Gherkin-сценарії читаються як вимоги з ДЗ: кожен пункт «Автоматизувати» — окремий сценарій, негативні випадки — `Scenario Outline` з таблицею.
- Page Objects ховають локатори; локатори — роль + доступне ім'я (`GetByRole`), CSS лише там, де в розмітці немає ролі (картка учасника, сума).
- Піраміда тестів: дані готуються й стан перевіряється через API (швидко й стабільно), а основні сценарії Owner і офіціанта проходять через інтерфейс.

## Структура

```
tests/GreenTasty.E2E/
  Features/         L1_ViewParticipants.feature, L2_SubmitPreOrder.feature, L3_RemoveParticipant.feature
  Steps/            GroupOrderApiSteps.cs (дані, API-перевірки), GroupOrderUiSteps.cs (UI-кроки)
  Pages/            LoginPage, GroupOrderPage (екран Owner), WaiterReservationsPage
  Api/              ApiClient (HTTP), RestaurantApi (ендпоінти бекенду)
  Support/          GroupOrderTasks (бізнес-дії), ScenarioState, TestData, Hooks (браузер, trace, прибирання)
  Config/Settings   змінні середовища / .env
.github/workflows/e2e.yml   пайплайн
```

## Тестові дані

Кожен сценарій створює **власне оточення** через API і після себе його прибирає:

1. локація з унікальною адресою, столик на 6 місць, меню з 4 страв (фіксовані ціни: 12.50, 7.00, 24.00, 6.50);
2. офіціант — зареєстрований користувач, доданий до локації (`POST /waiters`); без нього бронювання неможливе;
3. Owner реєструється й бронює стіл на 5 гостей на `сьогодні + 2 дні, 12:00 UTC` (дата рахується динамічно);
4. гості приєднуються за інвайт-токеном і наповнюють кошики.

Після сценарію (навіть упалого) бронювання скасовується, страви, столик і локація видаляються, офіціант прибирається з локації.
Користувачів у Cognito API видалити не дозволяє, тому email і імена унікальні на кожен запуск (`gt-aqa-<role>-<run>-<guid>@example.com`).
Сценарії не залежать один від одного й виконуються паралельно (4 потоки).

## Поведінка, яку не визначено в ДЗ (обрано й зафіксовано тестами)

| Випадок | Обрана поведінка |
|---|---|
| Група без учасників | `participants: []`, `total: 0` |
| Owner у списку учасників | Окремої сесії Owner бекенд не створює, тож у списку лише гості. Власні страви Owner додає своїм pre-order (`POST /orders`), і вони потрапляють у підсумкове замовлення разом зі стравами гостей |
| Підтвердження без замовлення Owner | `400`, повідомлення містить «not found» (умова «замовлення не знайдено») |
| Немає жодного підтвердженого кошика | `400 There are no submitted guest sessions.` |
| Підтверджений кошик без страв | Неможливий: порожній кошик гість підтвердити не може (`400`), тому окремо перевіряється саме це |
| Повторний submit | `400 Group reservation was submitted.` |
| Не-Owner (інший клієнт, офіціант) | `403` для перегляду, підтвердження й видалення |
| Без авторизації / токен гостя | `401` |
| Неіснуюча група або учасник | `404` |
| Учасник іншої групи | `400`, група не змінюється |
| Видалення останнього учасника | Дозволено, група стає порожньою (`participants: []`, `total: 0`) |

## Налаштування й запуск локально

Потрібен .NET SDK 10.

```bash
cp .env.example .env          # вписати APP_URL і API_URL свого розгортання
dotnet build tests/GreenTasty.E2E
pwsh tests/GreenTasty.E2E/bin/Debug/net10.0/playwright.ps1 install chromium
dotnet test tests/GreenTasty.E2E
```

Підмножина за тегом (теги Gherkin = категорії NUnit):

```bash
dotnet test tests/GreenTasty.E2E --filter "Category=L1"        # рівень 1
dotnet test tests/GreenTasty.E2E --filter "Category=api"       # лише API
dotnet test tests/GreenTasty.E2E --filter "Category=ui"        # лише UI
dotnet test tests/GreenTasty.E2E --filter "Category=smoke"     # основні сценарії
dotnet test tests/GreenTasty.E2E --filter "Category=L3&Category=ui"
```

Браузер з вікном і уповільненням: `HEADED=true SLOWMO=300` у `.env`.

## Звіти

- `tests/GreenTasty.E2E/bin/<config>/net10.0/TestResults/reqnroll-report.html` — звіт Reqnroll (Cucumber HTML): фічі, сценарії, кроки, помилки.
- `--logger "html;LogFileName=results.html"` / `trx` — звіт VSTest.
- Для впалих UI-сценаріїв у `artifacts/` зберігаються **скриншот**, **відео** і **Playwright trace** (`pwsh playwright.ps1 show-trace <file>.trace.zip`); вони ж прикріплені до результату тесту.

## CI (GitHub Actions)

`.github/workflows/e2e.yml`:

- кожен push — збірка фреймворку;
- повний прогін — **Actions → E2E tests → Run workflow** (можна вказати фільтр, напр. `Category=L1`) або автоматично на push, якщо змінна репозиторію `RUN_ON_PUSH=true`;
- упалий тест не зупиняє пайплайн (`continue-on-error`), звіт публікується у вкладці Checks, артефакти `test-report` і `failure-artifacts` завантажуються завжди.

Налаштування репозиторію (Settings → Secrets and variables → Actions):

| Назва | Тип | Значення |
|---|---|---|
| `APP_URL` | variable | адреса розгорнутого фронтенду |
| `API_URL` | variable | адреса API Gateway stage |
| `TEST_PASSWORD` | secret (необов'язково) | пароль тестових користувачів |
| `RUN_ON_PUSH` | variable (необов'язково) | `true` — запускати тести на кожен push |

## Сценарії

| Рівень | UI | API |
|---|---|---|
| L1 | учасники, страви, кількості й підсумки; зміна суми після зміни кошика; неіснуюче бронювання | сума = сумі учасників і змінюється; непідтверджені й без страв; порожня група; доступ лише Owner (інший клієнт, офіціант, гість, анонім); неіснуюча група |
| L2 | підтвердження з об'єднанням однакових страв без непідтверджених кошиків; офіціант бачить замовлення | статуси `SubmittedByCustomer` / `Submitted`; немає підтверджених кошиків; порожній кошик; замовлення не знайдено; неіснуюча група; повторний submit; не-Owner |
| L3 | видалення одного з кількох з перерахунком; **наскрізний**: гості → Owner переглядає → видаляє → підтверджує → офіціант бачить без видаленого | видалення підтвердженого; останнього; учасника іншої групи (400); неіснуючого (404); не-Owner |

Завдання із зірочкою (Report Portal, пайплайн у хмарі AWS) не виконувалось.
