---
name: dotnet-unit-testing
description: >-
  Unit testing conventions for Khang's .NET backend (xUnit + Moq + FluentAssertions + EF Core InMemory). MUST be used whenever writing, editing, running, or reviewing .NET unit tests — including creating test classes in test/UnitTest, mocking services, seeding EF InMemory data, asserting exceptions, or running dotnet test. Apply these rules to EVERY test file created or edited in this backend, even if the user doesn't repeat them.
---

# .NET Unit Testing — My-Tarot-Reader

Standard unit-testing conventions for the backend, matching the existing `test/UnitTest` suite. Written from the actual suite (46 tests): `AuthServiceTests` (25), `HistoryServiceTests` (8), `TarotReadingServiceTests` (13). Reference the architecture skill for the code under test: [[backend-dotnet-architecture]].

## 1. Project layout & stack

- Tests live in `Backend/test/UnitTest/` (`UnitTest.csproj`, `AssemblyName = MyTarotReader.UnitTest`), referencing `../../src/Infrastructure/Infrastructure.csproj`.
- Stack (all in the existing `.csproj` — no new packages):
  - **xUnit** 2.9.3 (`xunit`, `xunit.runner.visualstudio`)
  - **Moq** 4.20.70 — mock external seams
  - **FluentAssertions** 6.12.2 — expressive assertions
  - **EF Core InMemory** 8.0.11 — run services against a real `AppDbContext`
  - `coverlet.collector` for coverage
- Run/verify with `dotnet test test/UnitTest` (build first with `./scripts/clean-build.sh` in `Backend/`).

## 2. Test class structure

- **One test class per service**: `AuthServiceTests`, `HistoryServiceTests`, `TarotReadingServiceTests`.
- **File-scoped title**: class-level `<summary>` explains what is real vs mocked (e.g. "Authenticated-user paths use a real AppDbContext with EF InMemory; guest paths mock Redis").
- **`#region Helpers`** for all factories/seeds at the top, then one `#region` per service method (`GoogleLoginAsync`, `RefreshAsync`, `LogoutAsync`, ...).
- **Test naming**: `Method_Scenario_ExpectedOutcome` — e.g. `GoogleLoginAsync_ExistingUser_DoesNotRecreateWalletAndNoWelcomeEmail`, `RefreshAsync_DeletedToken_RevokesAllAndThrows`.
- **Every test gets a `<summary>`** stating the behavior asserted (not how).

## 3. SUT construction (the key pattern)

Services are created **directly** (no DI container), with:
- a real `AppDbContext` backed by EF InMemory,
- **Moq mocks only for external seams** (email, Google validator, JWT token generator, Redis).

### 3a. EF InMemory DbContext

```csharp
private static AppDbContext CreateInMemoryContext()
{
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()) // fresh DB per test
        .EnableServiceProviderCaching(false)            // avoid cross-test caching
        .Options;
    return new AppDbContext(options);
}
```

> Use a **real `AppDbContext`**, never a `Mock<IAppDbContext>`: global query filters (`DeletedAt == null`), `IgnoreQueryFilters`, projections (`.Select`) and aggregates must behave like production. Only external seams are mocked.

### 3b. Services that need validators / Redis

Constructors may need extra collaborators — pass the **real validators** and **mocked** infra:

```csharp
var service = new TarotReadingService(
    db,
    redis.Object,
    new CreateDrawForAuthRequestValidator(),   // real validator (cheap)
    new CreateDrawForGuestRequestValidator()
);
// Redis: mock IConnectionMultiplexer + IDatabase
```

### 3c. AuthService — mocked seams + IOptions

```csharp
var email = new Mock<IEmailHandler>();
email.Setup(e => e.SendWelcomeEmailAsync(It.IsAny<string>(), It.IsAny<string>(),
        It.IsAny<string?>(), It.IsAny<CancellationToken>()))
     .Returns(Task.CompletedTask);

var googleMock = new Mock<IGoogleAuthValidator>();
googleMock.Setup(g => g.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(DefaultPayload);

var tokens = new Mock<IJwtTokenGenerator>();
tokens.Setup(t => t.GenerateAccessToken(It.IsAny<User>())).Returns(AccessToken);

var service = new AuthService(
    Options.Create(DefaultJwt), Options.Create(DefaultWallet),
    db, email.Object, googleMock.Object, tokens.Object
);
```

> Use `Options.Create(setting)` — never read from `appsettings.json`. Put fixture settings as `static readonly` fields.

## 4. Seeding data

- Small static helper per concern, e.g. `SeedUserAsync(db, user?, walletBatches?)` builds a `User` + `Wallet` (+ `WhiteCoinBatch[]`), and `SeedRefreshToken(...)` adds a token (with a `deleted` flag).
- When a service method seeds multiple rows of the same entity for one user, only `Add` the `User` **once** (reuse `db.Users.Local.FirstOrDefault(u => u.Id == userId)`) to avoid a conflicting tracked instance.
- Call `db.SaveChangesAsync()` after seeding; the DbContext flows into the SUT, so seeded state is visible to the service (same instance, no recreate).

## 5. Assertions (FluentAssertions)

- **Behavior-focused**: `result.Should().Be(X)`, `.HaveCount(n)`, `.BeEmpty()`, `.Contain(predicate)`, `.OnlyContain(predicate)`, `Assert.Single(coll)`, `.BeCloseTo(expected, tolerance)`.
- **Collections**: order via `.Equal(expectedOrder)` for `Select(...)`; filter via `.AllSatisfy(...)`.
- **Soft-delete checks** use `db.XXX.IgnoreQueryFilters()` to see rows normally hidden by the global filter, e.g.:
  ```csharp
  db.RefreshTokens.IgnoreQueryFilters().Single(rt => rt.Token == "token").DeletedAt.Should().NotBeNull();
  ```

### 5a. Asserting exceptions — assert type AND ErrorCode

Throw → capture to an `async Func<Task>` and assert both the exception type and its `ErrorCode` (the project's AppException carries an ErrorCode i18n key):

```csharp
var act = async () => await service.SendMessageAsync(request, ...);

await act.Should()
    .ThrowAsync<BadRequestException>()
    .Where(e => e.ErrorCode == AuthErrorCode.InvalidKeyCredential);
```

Useful codes: `BadRequestException`, `UnauthorizedException`, `NotFoundException`, `TooManyRequestsException`, `ConflictException`, `InternalServerException` (all in `Application/Common/Exceptions/AppException.cs`). Error codes from `Application/Constants/Errors/*`.

- **Reproduce-and-verify style** for success paths: run the act, then pull the row from the same DbContext and assert it (`Assert.Single(db.TarotReadings)`, `.Should().HaveCount(2)`, ...).
- For idempotent no-op paths, assert **no throw**: `await act.Should().NotThrowAsync()`.

## 6. What to test per service method

Follow the shape of the existing suite:

- **Create/update**: valid input persists to DB with correct FKs/flags; invalid/missing input → `BadRequestException`; duplicate/existing → `ConflictException` or reuse behavior; nothing is written on failure.
- **Read (get)**: happy path maps all fields; empty → empty list (or null); **user scoping** (own data only); ordering; **soft-deleted excluded** by query filter.
- **Delete**: soft-delete sets `DeletedAt`; unknown id → `NotFoundException`; **record of another user → `NotFoundException`** and other user's row untouched.
- **Side effects**: exactly-once semantics (`.Verify(..., Times.Once)`), never-call (`.Verify(..., Times.Never)`), and no-op on unknown input.
- **Security-sensitive logic** (e.g. refresh-token rotation): test each theft/abuse scenario (reused deleted token, deleted user, device mismatch → revoke all) plus the happy rotation.

## 7. Mocking rules

- Mock **external seams only** — calls that leave the service (HTTP, Redis, email, token generation, Google). Never mock the DbContext (use real InMemory) unless the method touches nothing DB-related.
- Prefer `It.IsAny<T>()` for loose setup; use concrete matchers when the arrangement's value matters (`(RedisKey)"tarot:draw:guest-1"`, `When.NotExists`, `TimeSpan.FromHours(12)`).
- `Verify` with `Times.Once` / `Times.Never` for side-effect presence/absence.
- **Fire-and-forget**: if the service fires a task without awaiting (e.g. welcome email), `await Task.Yield()` after the act before verifying.

## Quick checklist for new tests

- [ ] One test class per service; class `<summary>` states what's real vs mocked
- [ ] `Method_Scenario_ExpectedOutcome` naming; `<summary>` per test describing behavior
- [ ] SUT built from real `AppDbContext` (EF InMemory) + mocks only for external seams
- [ ] Fresh InMemory DB per test (`Guid.NewGuid()`), `EnableServiceProviderCaching(false)`
- [ ] Settings via `Options.Create(...)` as static readonly fixtures, never `appsettings.json`
- [ ] Soft-deleted rows asserted via `IgnoreQueryFilters()`
- [ ] Exceptions asserted with `.ThrowAsync<T>().Where(e => e.ErrorCode == ...)`
- [ ] Side effects verified with `Times.Once` / `Times.Never`
- [ ] Runs via `dotnet test test/UnitTest`, builds via `./scripts/clean-build.sh`