---
name: backend-dotnet-architecture
description: >-
  Architecture and coding conventions for .NET backend projects (Clean Architecture - Api → Application → Domain, Api → Application → Infrastructure → Domain). MUST be used whenever writing, reviewing, or editing .NET backend code for Khang — including creating Controllers, Services, DTOs, Entities, migrations, validation, exception handling, EF Core queries, or setting up a new project. Trigger even on generic requests like "create an API for...", "add a feature...", "write a service...", "fix this controller...", since these conventions apply to ALL .NET code written, not only when the user explicitly says "follow the convention".
---

# Backend .NET Architecture

Standard architecture and coding conventions for Khang's .NET backend projects (Clean Architecture). Apply these rules to EVERY .NET file created or edited in the project, even if the user doesn't repeat each rule.

## 1. Project structure (Clean Architecture)

Dependency direction:

```
Api → Application → Domain
Api → Application → Infrastructure → Application
```

```
.
├── <ProjectName>.sln
├── scripts
│   ├── add-migration.sh
│   ├── clean-build.sh
│   ├── run-local.sh
│   └── update-database.sh
├── src
│   ├── Api
│   │   ├── Controllers
│   │   ├── Extensions       (DI, Swagger, Database, ...)
│   │   ├── Helpers
│   │   ├── Middlewares
│   │   └── Program.cs
│   ├── Application
│   │   ├── Common
│   │   ├── Constants
│   │   ├── Contracts        (Service interfaces)
│   │   └── Settings
│   ├── Domain
│   │   ├── Common           (BaseEntity)
│   │   ├── Entities
│   │   └── Enums
│   └── Infrastructure
│       ├── Common
│       ├── Persistence       (entity configurations, DbContext, Migrations)
│       ├── Services          (service implementations)
│       └── Templates
```

**Always use `./scripts/run-local.sh ...`** to run the app, add migrations, update the database, etc. — do NOT call `dotnet run`, `dotnet ef ...` directly, to avoid issues across different environments.

## 2. Naming convention

| Component                | Rule                                                             | Example                                                         |
| ------------------------ | ---------------------------------------------------------------- | --------------------------------------------------------------- |
| Controller               | `<N>Controller`                                                  | `UserController`, `HistoryController`, `TarotReadingController` |
| Service (implementation) | `<N>Service`                                                     | `UserService`, `HistoryService`                                 |
| Service (interface)      | `I<N>Service`                                                    | `IUserService`, `IHistoryService`                               |
| DTO                      | `record`, named `<FunctionName>Request` / `<FunctionName>Result` | `GetHistoryRequest(param1, param2, ...)`                        |

**One controller uses one service.** Each service method uses one request (if any) and one response (if any), named to match the method name.

Example: `AuthController.LoginAsync()` calls `IAuthService.LoginAsync(LoginRequest)` returning `LoginResult`.

- Controllers **always inject the service interface** (`IUserService`), never the concrete implementation.
- Services **always inject `IAppDbContext`** — the interface declared in `Application/Contracts/Persistence/IAppDbContext.cs` — never the concrete `DbContext`. `AppDbContext` (in `Infrastructure/Persistence`) implements it; DI registers `AddScoped<IAppDbContext, AppDbContext>()`.

## 3. API response format

- `PUT`, `POST`, `DELETE` → always return `200 OK` with `data = null`.
- `GET` → return `200 OK` with `data` set to the query result.
- On error (400, 401, 404, ...): **never return directly from the controller**. Throw an exception from `AppException` (`NotFoundException`, `BadRequestException`, `UnauthorizedException`, ...) — `GlobalExceptionMiddleware` (`Api/Middlewares`) handles it and formats the error response consistently.
- Bad request (400) errors come in **two flavours**, and which part of the envelope carries the error depends on the request type:
  - **Generic single-message error** → throw `BadRequestException(errorCode)` → the error code lands in the response's **`Message`** (`data = null`).
  - **Form / field-level errors** → throw `ValidationException(fieldErrors)` → `FieldError[]` lands in the response's **`Data`**.
  - The middleware already branches on `FieldErrors.Count` (count > 0 → `Data`, else `Message`), so callers just pick the right exception. Use the `Message` flavour for simple requests, `Data` only for form-style requests that need per-field errors.

## 4. EF Core / Domain rules

- Entities in `Domain` **always inherit from `BaseEntity`**, never use raw Entity Framework Core base types.
- Read-only (get) methods → **always use `AsNoTracking()`** to avoid EF Core tracking overhead and reduce memory footprint.
- **Always use `Select`** to project only the needed fields, avoid querying every field of the entity.
- When using `ExecuteDelete` / `ExecuteUpdate` → **always wrap in a transaction** to avoid issues when multiple threads update concurrently.
- Relationships between entities: **always configure on the "weak" side** (the "many" side of a 1-n relationship).
  Example: `User` 1-n `History` → configure in `HistoryConfiguration`, not in `User`:
  ```csharp
  builder.HasOne(x => x.User).WithMany(x => x.Histories).HasForeignKey(x => x.UserId);
  ```
- Every query **must filter `DeletedAt == null`** to exclude soft-deleted records.
- **Always design methods to be testable** — extract complex/reusable logic into a separate service placed under `Common`.

## 5. Async, cancellation, config

- **Always use `async/await`**, never `.Result` or `.Wait()` (avoids deadlocks and thread blocking).
- **Always use `CancellationToken`** so requests can be cancelled when the client cancels, avoiding memory leaks.
- **Always use `IOptions<T>`** to inject settings, never read directly from `appsettings.json`.

## 6. Validation

**Always use FluentValidation** to validate requests — never validate directly inside the controller.

Follow this pattern (as in `TarotReadingService`):
- Validators live in `Application/Common/Validators` (one file may contain multiple validators for the same service) and are registered in DI via `AddScoped<IValidator<...>, ...>` in `Api/Extensions/DependencyInjectionExtension.cs`.
- Services trigger validation at the top of their method using the shared helper `ValidationHelper` (`Application/Common/Validators/ValidationHelper.cs`):
  - `ValidationHelper.ValidateOrThrow(validator, request)` → throws `BadRequestException` (generic → error in `Message`).
  - `ValidationHelper.ValidateOrThrowForm(validator, request)` → throws `ValidationException` mapping every `FieldError` (form-style → errors in `Data`).
- To avoid the name clash with `FluentValidation.ValidationException`, alias the app exception where both namespaces are in scope: `using AppValidationException = MyTarotReader.Application.Common.Exceptions.ValidationException;`.

## 7. Code comment convention (XML doc)

- `<summary>`: a concise, one-sentence comment for classes, methods, properties.
- `<remarks>`: more detailed comment, can be longer than one sentence.
- `<param>`: comment for parameters — not required for every parameter, only the important ones.
- Controllers and Services **may skip class-level comments**.
- Controller: use `[ProducesResponseType]` to document response codes:
  ```csharp
  [ProducesResponseType(typeof(GetHistoryResult), StatusCodes.Status200OK)]
  ```
- Service: if a method throws an exception, document it with `<exception>`:
  ```csharp
  /// <exception cref="NotFoundException">Thrown when the user is not found.</exception>
  ```
- Entities: don't need XML doc for every property, only the important ones.
- Enums: use <summary> for enum values

## 8. Worked example: Controller → Service flow

Full example for "get a Tarot reading history record by Id" (GET) and "create a new history record" (POST), following all the conventions above.

### 8.1. DTOs (`Application/Contracts/Services`)

```csharp
namespace Application.Contracts.Services;

/// <summary>Result containing the detail of a single history record.</summary>
public record GetHistoryResult(Guid Id, string Question, string Result, DateTime CreatedAt);

/// <summary>Request to create a new history record.</summary>
public record CreateHistoryRequest(Guid UserId, string Question, string Result);
```

> `CreateHistoryAsync` doesn't need its own result type since POST always returns `data = null` — no need to declare `CreateHistoryResult`.

### 8.2. Service interface (`Application/Contracts/Services`)

```csharp
namespace Application.Contracts.Services;

public interface IHistoryService
{
    /// <summary>Gets the detail of a single history record by Id.</summary>
    /// <exception cref="NotFoundException">Thrown when the history record is not found.</exception>
    Task<GetHistoryResult> GetHistoryAsync(Guid historyId, CancellationToken cancellationToken);

    /// <summary>Creates a new history record.</summary>
    Task CreateHistoryAsync(CreateHistoryRequest request, CancellationToken cancellationToken);
}
```

### 8.3. Validator (FluentValidation, in `Application/Common/Validators` or alongside the DTO)

```csharp
public class CreateHistoryRequestValidator : AbstractValidator<CreateHistoryRequest>
{
    public CreateHistoryRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Question).NotEmpty().MaximumLength(1000);
    }
}
```

### 8.4. Service implementation (`Infrastructure/Services`)

```csharp
namespace Infrastructure.Services;

public class HistoryService(IAppDbContext dbContext) : IHistoryService
{
    private readonly IAppDbContext _dbContext = dbContext;

    public async Task<GetHistoryResult> GetHistoryAsync(Guid historyId, CancellationToken cancellationToken)
    {
        var result = await _dbContext.Histories
            .AsNoTracking()
            .Where(x => x.Id == historyId && x.DeletedAt == null)
            .Select(x => new GetHistoryResult(x.Id, x.Question, x.Result, x.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        if (result is null)
            throw new NotFoundException("History record not found.");

        return result;
    }

    public async Task CreateHistoryAsync(CreateHistoryRequest request, CancellationToken cancellationToken)
    {
        var entity = new History
        {
            UserId = request.UserId,
            Question = request.Question,
            Result = request.Result,
        };

        _dbContext.Histories.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
```

> One file validator can contain multiple validators for same service

### 8.5. Controller (`Api/Controllers`)

```csharp
namespace Api.Controllers;

[ApiController]
[Route("api/histories")]
[ProducesErrorResponseType(typeof(ApiResponse<object>))]
public class HistoryController(IHistoryService historyService) : ControllerBase
{
    private readonly IHistoryService _historyService = historyService;

    [HttpGet("{historyId}")]
    [ProducesResponseType(typeof(ApiResponse<GetHistoryResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistoryAsync(Guid historyId, CancellationToken cancellationToken)
    {
        var result = await _historyService.GetHistoryAsync(historyId, cancellationToken);
        return Ok(ApiResponse<GetHistoryResult>.Success(result));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateHistoryAsync(CreateHistoryRequest request, CancellationToken cancellationToken)
    {
        await _historyService.CreateHistoryAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Success(null));
    }
}
```

### 8.6. Entity configuration — relationship on the weak side (`Infrastructure/Persistence`)

```csharp
namespace Infrastructure.Persistence.Configurations;

public class HistoryConfiguration : IEntityTypeConfiguration<History>
{
    public void Configure(EntityTypeBuilder<History> builder)
    {
        builder.HasOne(x => x.User)
               .WithMany(x => x.Histories)
               .HasForeignKey(x => x.UserId);

        builder.HasQueryFilter(x => x.DeletedAt == null);
    }
}
```

Full flow: `HistoryController` → `IHistoryService` (injected via DI) → `HistoryService` (implementation, using `IAppDbContext`) → returns `GetHistoryResult`/`CreateHistoryRequest` following the naming convention in section 2, the response format in section 3, and exceptions via `Application/Common/Exceptions/AppException.cs` as in sections 3/7.

## Quick checklist for new code

- [ ] Controller injects the interface, calls exactly one service
- [ ] DTO is a `record`, named `<Function>Request`/`<Function>Result`
- [ ] Service injects `IDbContext`, not `DbContext`
- [ ] Get methods use `AsNoTracking()` + `Select` only needed fields
- [ ] Bulk update/delete wrapped in a transaction
- [ ] Filter `DeletedAt == null`
- [ ] Validate with FluentValidation via `ValidationHelper` in the service, never inside the controller
- [ ] Errors thrown via `AppException`, never returned directly
- [ ] Use `async/await` + `CancellationToken`, never `.Result`/`.Wait()`
- [ ] Settings injected via `IOptions<T>`
- [ ] PUT/POST/DELETE return `data = null`, GET returns actual data
- [ ] XML doc: concise `<summary>`, `<remarks>` when needed, `<param>` for important parameters
- [ ] Run/migrate via `./scripts/*.sh`, never call `dotnet` directly
