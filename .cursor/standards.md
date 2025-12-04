# .NET 10 Clean Architecture Standards

**Version:** 1.0  
**Last Updated:** November 21, 2025  
**Target Framework:** .NET 10  
**Architecture Pattern:** Clean Architecture with CQRS

---

## 1. Purpose & Philosophy

### Goals

This document defines the **single source of truth** for how the .NET 10 Razor Pages web application should be structured and written. Our primary goals are:

- **Maintainability:** Code that is easy to understand, modify, and extend
- **Testability:** Architecture that enables comprehensive unit and integration testing
- **Long-term Evolvability:** Design decisions that support future growth without major rewrites
- **Consistency:** Uniform patterns across the entire codebase
- **Performance:** Efficient, allocation-aware code that scales
- **Security:** Defense-in-depth with proper input validation, authorization, and error handling

### Clean Architecture Context

The application implements Clean Architecture principles through a layered approach where:

1. **Domain Layer** (`App.Domain`) contains pure business logic with zero external dependencies
2. **Application Layer** (`App.Application`) orchestrates use cases via CQRS pattern using MediatR
3. **Infrastructure Layer** (`App.Infrastructure`) handles external concerns (database, file storage, email)
4. **Presentation Layer** (`App.Web`) is a thin UI layer using Razor Pages

**Dependency Rule:** Dependencies flow inward only. The Domain layer knows nothing about outer layers. The Application layer depends only on Domain. Infrastructure implements Application interfaces. The Web layer depends on Application and uses Infrastructure through DI.

**Key Principle:** Business logic lives in Domain and Application layers. Razor Pages are **thin orchestrators** that delegate to MediatR handlers and render responses.

---

## 2. Solution & Project Structure

### Project Responsibilities

#### App.Domain
- **Purpose:** Core business entities, value objects, domain events, and domain exceptions
- **Responsibilities:**
  - Define entities (`User`, `Role`, `UserGroup`, `EmailTemplate`, `AuthenticationScheme`, `MediaItem`, etc.)
  - Define value objects (`SortOrder`, `ConditionOperator`, etc.)
  - Define domain events (`UserCreatedEvent`, `AdminCreatedEvent`, etc.)
  - Define domain-specific exceptions
- **Must NOT contain:**
  - Any references to external libraries (except `System.Text.Json` for entity serialization)
  - Database concerns (EF Core)
  - HTTP/Web concerns
  - Application logic or use cases
- **Key Patterns:**
  - Entities inherit from `BaseEntity` or `BaseAuditableEntity`
  - Value objects inherit from `ValueObject`
  - Domain events are raised via `AddDomainEvent()` on entities

#### App.Application
- **Purpose:** Use case orchestration via CQRS, DTOs, validation, and interfaces
- **Responsibilities:**
  - Define commands and queries (CQRS pattern)
  - Define validators using FluentValidation
  - Define DTOs for data transfer
  - Define application interfaces (e.g., `IAppDbContext`, `IFileStorageProvider`)
  - Define MediatR behaviors (validation, audit, exception handling)
  - Background task definitions
- **Must NOT contain:**
  - Direct database access (use interfaces)
  - HTTP request/response handling (belongs in Web)
  - UI concerns
- **Key Patterns:**
  - Commands: `CreateUser.Command`, `EditUser.Command`, `CreateRole.Command`
  - Queries: `GetUsers.Query`, `GetUserById.Query`, `GetRoles.Query`
  - Each command/query has nested `Validator` and `Handler` classes
  - Handlers return `CommandResponseDto<T>` or `QueryResponseDto<T>`
  - Use `IRequest<TResponse>` from Mediator library

#### App.Infrastructure
- **Purpose:** Implementation of external concerns and Application interfaces
- **Responsibilities:**
  - EF Core `DbContext` implementation (`AppDbContext`)
  - Entity configurations (Fluent API)
  - Database migrations
  - File storage implementations (`LocalFileStorageProvider`, `AzureBlobFileStorageProvider`, `S3FileStorageProvider`)
  - Email service implementation
  - Background task queue implementation
- **Must NOT contain:**
  - Business logic (belongs in Domain/Application)
  - HTTP/Web concerns
  - Razor Pages or UI components
- **Key Patterns:**
  - Implements interfaces defined in `App.Application.Common.Interfaces`
  - Configuration classes implement `IEntityTypeConfiguration<T>`
  - Services registered via `AddInfrastructureServices()` extension method

#### App.Web
- **Purpose:** Razor Pages presentation layer, authentication, and HTTP concerns
- **Responsibilities:**
  - Razor Pages (`.cshtml` and `.cshtml.cs` files)
  - PageModel classes (thin orchestrators)
  - Authentication and authorization setup
  - Middleware configuration
  - Static files and frontend assets
  - API controllers (for headless mode)
- **Must NOT contain:**
  - Business logic (delegate to Application via MediatR)
  - Direct database access (use MediatR commands/queries)
  - Complex data transformations (belongs in Application)
- **Key Patterns:**
  - PageModels inherit from `BasePageModel` or `BaseAdminPageModel`
  - Use `Mediator.Send()` to execute commands/queries
  - Use `SetSuccessMessage()` / `SetErrorMessage()` for user feedback
  - Services registered via `AddWebUIServices()` extension method

### Namespace Conventions

**Pattern:** `{ProjectName}.{Feature}.{Type}`

**Examples:**
```
App.Domain.Entities.User
App.Domain.ValueObjects.SortOrder
App.Application.Users.Commands.CreateUser
App.Application.Users.Queries.GetUsers
App.Infrastructure.Persistence.AppDbContext
App.Web.Areas.Admin.Pages.Users.Edit
```

**Rules:**
- Feature folders group related functionality (e.g., `Users`, `Roles`, `EmailTemplates`)
- Type suffixes clarify purpose (e.g., `Commands`, `Queries`, `EventHandlers`)
- Nested classes are allowed for grouping (Command, Validator, Handler in same file)

### Folder Structure Rules

**Application Layer:**
```
Users/
  Commands/
    CreateUser.cs
    EditUser.cs
  Queries/
    GetUsers.cs
    GetUserById.cs
  EventHandlers/
    UserCreatedEventHandler.cs
  UserDto.cs
```

**Web Layer (Razor Pages):**
```
Areas/
  Admin/
    Pages/
      Users/
        Index.cshtml
        Index.cshtml.cs
        Create.cshtml
        Create.cshtml.cs
        Edit.cshtml
        Edit.cshtml.cs
```

**Rules:**
- Group by feature (vertical slice), not by type
- Commands and Queries live in separate folders
- Partials prefixed with underscore (`_FieldRenderer.cshtml`)
- Layouts use descriptive names (`SidebarLayout.cshtml`)

---

## 3. Razor Pages Conventions

### PageModel Structure

**Good PageModel Example:**

```csharp
[Authorize]
public class Create : BaseAdminPageModel
{
    [BindProperty]
    public FormModel Form { get; set; }

    public async Task<IActionResult> OnGet()
    {
        // 1. Set breadcrumbs for navigation
        SetBreadcrumbs(/*...*/);
        
        // 2. Load data via MediatR queries
        var roles = await Mediator.Send(new GetRoles.Query { /*...*/ });
        
        // 3. Build view model
        Form = new FormModel { /*...*/ };
        
        return Page();
    }

    public async Task<IActionResult> OnPost()
    {
        // 1. Map form data to command
        var command = new CreateUser.Command
        {
            FirstName = Form.FirstName,
            LastName = Form.LastName,
            EmailAddress = Form.EmailAddress,
        };
        
        // 2. Execute command via MediatR
        var response = await Mediator.Send(command);
        
        // 3. Handle result
        if (response.Success)
        {
            SetSuccessMessage("User was created successfully.");
            return RedirectToPage("Edit", new { id = response.Result });
        }
        else
        {
            SetErrorMessage("There was an error.", response.GetErrors());
            await RepopulateFormOnError();
            return Page();
        }
    }
    
    // Nested FormModel as record
    public record FormModel
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string EmailAddress { get; set; }
    }
}
```

**Rules:**
- **Keep PageModels thin** – no business logic
- Use `async/await` end-to-end; never block with `.Result` or `.Wait()`
- Always include `CancellationToken` in async methods (plumb through to handlers)
- Use `[BindProperty]` for forms
- Return `IActionResult` from handlers
- Use `Mediator.Send()` for all data access
- Use descriptive handler names: `OnPost`, `OnPostDelete`, `OnPostUnpublish`
- Group related data in nested `record` classes (e.g., `FormModel`)
- Use `SetSuccessMessage()` / `SetErrorMessage()` for user feedback
- Always check `.Success` property on response DTOs before accessing `.Result`

### PageModel Inheritance Hierarchy

```
PageModel (base ASP.NET Core class)
  └── BasePageModel (App base, provides Mediator, CurrentUser, logging)
      └── BaseAdminPageModel (adds [Authorize] for admin area)
```

**When to use:**
- `BasePageModel`: All pages (provides core services)
- `BaseAdminPageModel`: Admin area pages (requires authentication)

### View Organization

**Folder Structure:**
```
Pages/
  Users/
    Index.cshtml          # List view
    Index.cshtml.cs       # PageModel
    Create.cshtml         # Create form
    Create.cshtml.cs
    Edit.cshtml           # Edit form
    Edit.cshtml.cs
```

**Naming Conventions:**
- PageModel class name matches file name (e.g., `Create.cshtml` → `Create.cshtml.cs` → `public class Create`)
- Partials prefixed with underscore
- Layouts have `Layout` suffix (`SidebarLayout.cshtml`)

### Form Handling

**Pattern:**

```csharp
[BindProperty]
public FormModel Form { get; set; }

public async Task<IActionResult> OnPost()
{
    // Map FormModel → Command
    var command = new EditUser.Command
    {
        Id = Form.Id,
        FirstName = Form.FirstName,
        LastName = Form.LastName,
        EmailAddress = Form.EmailAddress,
    };
    
    // Send command
    var response = await Mediator.Send(command);
    
    // Handle response
    if (response.Success)
    {
        SetSuccessMessage("Saved successfully.");
        return RedirectToPage("Edit", new { id = response.Result });
    }
    
    SetErrorMessage("Error saving.", response.GetErrors());
    await RepopulateFormOnError();
    return Page();
}
```

**Rules:**
- Use nested `record` types for `FormModel` (immutable, value semantics)
- Separate concerns: FormModel (UI) → Command (Application) → Entity (Domain)
- Always repopulate form data on error (use `await RepopulateFormOnError()`)
- Never return raw `Page()` without checking response success
- Use `ModelState.IsValid` only at the boundary; validation logic in FluentValidation

### Partials and Tag Helpers

**Partial Usage:**

```cshtml
@* Pass model and view data *@
<partial name="_Partials/_UserRoleItem" 
         model="Model.Form.Roles[i]"
         view-data="@(new ViewDataDictionary(ViewData) { 
             { "Index", i }, 
             { "ParentModel", Model } 
         })" />
```

**Tag Helper Usage:**

```cshtml
<breadcrumbs></breadcrumbs>
```

**Rules:**
- Use partials for reusable UI components within a feature
- Use Tag Helpers for cross-cutting UI concerns (breadcrumbs, alerts)
- Partials live in `_Partials/` folder
- Tag Helpers live in `Areas/Shared/TagHelpers/`
- Prefer server-side rendering over client-side JavaScript

### ViewModels vs DTOs

- **DTOs** (`UserDto`): Data transfer between Application and Web layers
- **ViewModels**: UI-specific models with display logic

**Example:**

```csharp
// DTO from Application layer
public record UserDto
{
    public string Id { get; init; }
    public string FirstName { get; init; }
    public string LastName { get; init; }
    public string EmailAddress { get; init; }
}

// ViewModel in PageModel
public class UserViewModel
{
    public string Id { get; set; }
    public string FullName { get; set; }
    public string EmailAddress { get; set; }
    
    // UI-specific helper
    public string DisplayName => $"{FullName} ({EmailAddress})";
}
```

**Rules:**
- DTOs are immutable (`record` or `init` properties)
- ViewModels can have computed properties for UI logic
- Never expose DTOs directly in views; map to ViewModels
- Keep mapping logic in PageModel (private methods)

---

## 4. Domain & Application Layer Guidelines

### Domain Entities

**Structure:**

```csharp
public class User : BaseAuditableEntity
{
    // Public properties (EF Core navigation)
    public bool IsAdmin { get; set; }
    public bool IsActive { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string EmailAddress { get; set; } = null!;
    public Guid? AuthenticationSchemeId { get; set; }
    public virtual AuthenticationScheme? AuthenticationScheme { get; set; }
    
    // Computed property
    [NotMapped]
    public string FullName => $"{FirstName} {LastName}";
    
    // Navigation properties
    public virtual ICollection<Role> Roles { get; set; }
    
    // Override ToString for debugging
    public override string ToString()
    {
        return FullName;
    }
}
```

**Rules:**
- Inherit from `BaseEntity` (has `Id`) or `BaseAuditableEntity` (adds audit fields)
- Use `virtual` for navigation properties (EF Core lazy loading)
- Nullable reference types enabled (`AuthenticationScheme?`, `Role?`)
- Private backing fields for serialized JSON or complex computed properties
- Use `[NotMapped]` for properties not persisted to database
- Keep behavior methods minimal (domain logic goes in handlers)
- Raise domain events via `AddDomainEvent()` in Application layer handlers

### Value Objects

**Structure:**

```csharp
public class SortOrder : ValueObject
{
    protected SortOrder(string developerName)
    {
        DeveloperName = developerName;
    }
    
    public string DeveloperName { get; private set; } = string.Empty;
    
    public static SortOrder From(string developerName)
    {
        var type = SupportedTypes.FirstOrDefault(p => p.DeveloperName == developerName.ToLower());
        if (type == null)
            throw new SortOrderNotFoundException(developerName);
        return type;
    }
    
    public static implicit operator string(SortOrder order) => order.DeveloperName;
    public static explicit operator SortOrder(string type) => From(type);
    
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return DeveloperName;
    }
}
```

**Rules:**
- Inherit from `ValueObject` (provides structural equality)
- Immutable (private setters, set in constructor)
- Factory method `From()` for creation from primitives
- Implement `GetEqualityComponents()` for equality comparison
- Use implicit/explicit operators for conversions
- No public setters

### CQRS Pattern

**Command Example:**

```csharp
public class CreateUser
{
    public record Command : LoggableRequest<CommandResponseDto<ShortGuid>>
    {
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public string EmailAddress { get; init; } = string.Empty;
        public bool IsAdmin { get; init; }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IAppDbContext db)
        {
            RuleFor(x => x.EmailAddress)
                .NotEmpty()
                .EmailAddress()
                .WithMessage("Email address is required.");
            
            RuleFor(x => x).Custom((request, context) =>
            {
                var existingUser = db.Users
                    .FirstOrDefault(p => p.EmailAddress == request.EmailAddress);
                
                if (existingUser != null)
                {
                    context.AddFailure(Constants.VALIDATION_SUMMARY, 
                        "A user with this email address already exists.");
                }
            });
        }
    }

    public class Handler : IRequestHandler<Command, CommandResponseDto<ShortGuid>>
    {
        private readonly IAppDbContext _db;

        public Handler(IAppDbContext db)
        {
            _db = db;
        }

        public async ValueTask<CommandResponseDto<ShortGuid>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            var entity = new User 
            { 
                FirstName = request.FirstName,
                LastName = request.LastName,
                EmailAddress = request.EmailAddress,
                IsAdmin = request.IsAdmin
            };
            _db.Users.Add(entity);
            entity.AddDomainEvent(new UserCreatedEvent(entity));
            await _db.SaveChangesAsync(cancellationToken);
            
            return new CommandResponseDto<ShortGuid>(entity.Id);
        }
    }
}
```

**Query Example:**

```csharp
public class GetUsers
{
    public record Query : GetPagedEntitiesInputDto, 
                          IRequest<IQueryResponseDto<ListResultDto<UserDto>>>
    {
        public string? Filter { get; init; }
        public bool? IsAdmin { get; init; }
    }

    public class Handler : IRequestHandler<Query, IQueryResponseDto<ListResultDto<UserDto>>>
    {
        private readonly IAppDbContext _db;

        public Handler(IAppDbContext db)
        {
            _db = db;
        }

        public async ValueTask<IQueryResponseDto<ListResultDto<UserDto>>> Handle(
            Query request,
            CancellationToken cancellationToken)
        {
            var query = _db.Users.AsQueryable();
            
            if (request.IsAdmin.HasValue)
            {
                query = query.Where(p => p.IsAdmin == request.IsAdmin.Value);
            }
            
            var items = await query
                .Select(p => new UserDto
                {
                    Id = p.Id,
                    FirstName = p.FirstName,
                    LastName = p.LastName,
                    EmailAddress = p.EmailAddress
                })
                .ToListAsync(cancellationToken);
            
            var count = await query.CountAsync(cancellationToken);
            
            return new QueryResponseDto<ListResultDto<UserDto>>(
                new ListResultDto<UserDto>(items, count));
        }
    }
}
```

**Rules:**
- One file per use case (e.g., `CreateUser.cs`)
- Nested classes: `Command`/`Query`, `Validator`, `Handler`
- Commands return `CommandResponseDto<T>`
- Queries return `QueryResponseDto<T>`
- Use `record` for immutable Command/Query definitions
- Inject dependencies in Handler constructor (not Command/Query)
- Always include `CancellationToken` parameter
- Use `ValueTask<T>` (not `Task<T>`) per Mediator library convention
- Validators inject `IAppDbContext` for database lookups
- Throw domain exceptions (`NotFoundException`) for invalid data
- Use `LoggableRequest<T>` for commands that should be audited

### Dependency Direction

```
Web Layer (Razor Pages)
  ↓ depends on
Application Layer (CQRS Handlers, Interfaces)
  ↓ depends on
Domain Layer (Entities, Value Objects)
  
Infrastructure Layer implements → Application Interfaces
```

**Rules:**
- Domain has **zero** dependencies on outer layers
- Application depends only on Domain
- Infrastructure implements Application interfaces
- Web depends on Application (not Infrastructure directly)
- Use DI to inject Infrastructure implementations

### Application Interfaces

**Pattern:**

```csharp
// Defined in Application layer
namespace App.Application.Common.Interfaces;

public interface IFileStorageProvider
{
    Task<string> SaveFileAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken);
    Task<Stream> GetFileAsync(string objectKey, CancellationToken cancellationToken);
    Task DeleteFileAsync(string objectKey, CancellationToken cancellationToken);
}

// Implemented in Infrastructure layer
namespace App.Infrastructure.FileStorage;

public class LocalFileStorageProvider : IFileStorageProvider
{
    // Implementation
}
```

**Rules:**
- Interfaces defined in `App.Application.Common.Interfaces`
- Implementations in `App.Infrastructure`
- Use descriptive interface names (avoid `IService` suffix)
- Include `CancellationToken` on async methods
- Return abstractions, not concrete types

---

## 5. Data Access & Persistence

### DbContext Usage

**Good Example:**

```csharp
public class Handler : IRequestHandler<Command, CommandResponseDto<ShortGuid>>
{
    private readonly IAppDbContext _db;

    public Handler(IAppDbContext db)
    {
        _db = db;
    }

    public async ValueTask<CommandResponseDto<ShortGuid>> Handle(
        Command request,
        CancellationToken cancellationToken)
    {
        // Use interface methods
        var user = await _db.Users
            .Include(p => p.Roles)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        
        if (user == null)
            throw new NotFoundException("User", request.Id);
        
        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        await _db.SaveChangesAsync(cancellationToken);
        
        return new CommandResponseDto<ShortGuid>(user.Id);
    }
}
```

**Rules:**
- Inject `IAppDbContext` (interface), not `AppDbContext` (concrete)
- Use `async` methods (`FirstOrDefaultAsync`, `ToListAsync`)
- Include `CancellationToken` in all async calls
- Use `Include()` for eager loading related entities
- Throw `NotFoundException` when entity not found (caught by middleware)
- Save changes once at end of handler
- No direct EF Core calls in Razor Pages (use MediatR)

### Entity Configurations

**Good Example:**

```csharp
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.EmailAddress)
            .IsRequired()
            .HasMaxLength(255);
        
        builder.HasIndex(e => e.EmailAddress)
            .IsUnique();
        
        builder.HasOne(e => e.AuthenticationScheme)
            .WithMany()
            .HasForeignKey(e => e.AuthenticationSchemeId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasMany(e => e.Roles)
            .WithMany();
    }
}
```

**Rules:**
- One configuration class per entity
- Use Fluent API (not attributes) for complex mappings
- Specify column types explicitly for JSON/large text (`nvarchar(max)`)
- Configure relationships explicitly (avoid conventions)
- Add indexes for foreign keys and frequently queried columns
- Use `OnDelete(DeleteBehavior.Restrict)` to prevent cascading deletes
- Configuration classes auto-discovered via `ApplyConfigurationsFromAssembly()`

### Migrations

**Rules:**
- **Never** edit generated migration files manually (except for custom SQL)
- **Test** migrations on local database before committing
- Include migration SQL script in `db/` folder for production deployments
- Use descriptive migration names (e.g., `v1_4_1`)
- Migrations run automatically on startup if `APPLY_PENDING_MIGRATIONS=true`

**Commands:**

```bash
# Add migration
dotnet ef migrations add MigrationName --project src/App.Infrastructure --startup-project src/App.Web

# Generate SQL script
dotnet ef migrations script --project src/App.Infrastructure --startup-project src/App.Web --output db/migration.sql
```

### Query Performance

**Good Practices:**

```csharp
// ✅ Use AsNoTracking for read-only queries
var users = await _db.Users
    .AsNoTracking()
    .Where(p => p.IsAdmin == true)
    .ToListAsync(cancellationToken);

// ✅ Use Select projection to avoid loading full entities
var users = await _db.Users
    .Where(p => p.IsActive == true)
    .Select(p => new UserDto
    {
        Id = p.Id,
        FirstName = p.FirstName,
        LastName = p.LastName,
        EmailAddress = p.EmailAddress,
    })
    .ToListAsync(cancellationToken);

// ✅ Use Include for related data needed in one query
var user = await _db.Users
    .Include(p => p.Roles)
    .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

// ❌ Avoid N+1 queries (loading related data in loop)
foreach (var user in users)
{
    var roles = await _db.Roles.Where(r => r.Users.Contains(user)).ToListAsync(); // BAD
}
```

**Rules:**
- Use `AsNoTracking()` for read-only queries (better performance)
- Use `Select` projections instead of loading full entities
- Use `Include` / `ThenInclude` for eager loading
- Avoid N+1 queries (load related data upfront)
- Add indexes for `WHERE`, `ORDER BY`, and `JOIN` columns
- Consider pagination for large result sets

### Transaction Boundaries

**Rules:**
- `SaveChangesAsync()` is the transaction boundary
- One save per handler (at the end)
- For complex operations, wrap in explicit transaction:

```csharp
using var transaction = await _db.DbContext.Database.BeginTransactionAsync(cancellationToken);
try
{
    // Multiple operations
    await _db.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
}
catch
{
    await transaction.RollbackAsync(cancellationToken);
    throw;
}
```

---

## 6. Dependency Injection & Composition Root

### Service Registration

**Structure:**

```csharp
// Application layer
public static class ConfigureServices
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddMediator(options =>
        {
            options.Namespace = "App.Application.Mediator";
            options.ServiceLifetime = ServiceLifetime.Scoped;
        });
        services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
        return services;
    }
}

// Infrastructure layer
public static class ConfigureServices
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options => { /* ... */ });
        services.AddScoped<IAppDbContext>(provider => 
            provider.GetRequiredService<AppDbContext>());
        services.AddScoped<IFileStorageProvider, LocalFileStorageProvider>();
        return services;
    }
}

// Web layer (Startup.cs)
public void ConfigureServices(IServiceCollection services)
{
    services.AddApplicationServices();
    services.AddInfrastructureServices(Configuration);
    services.AddWebUIServices();
}
```

**Rules:**
- One `ConfigureServices` extension method per project
- Extension methods named `Add{LayerName}Services`
- Called in order: Application → Infrastructure → Web
- Register by interface, resolve by interface
- Use configuration for external dependencies (connection strings, API keys)

### Service Lifetimes

| Lifetime | Use Case | Examples |
|----------|----------|----------|
| **Singleton** | Stateless, thread-safe services | `ICurrentOrganizationConfiguration`, `IFileStorageProviderSettings`, `ICurrentVersion`, Pipeline behaviors |
| **Scoped** | Per-request state, DbContext | `IAppDbContext`, `ICurrentUser`, `ICurrentOrganization`, `IEmailer`, MediatR handlers |
| **Transient** | Lightweight, stateless, created each time | `IBackgroundTaskDb`, `IAppRawDbInfo` |

**Rules:**
- **DbContext is always Scoped** (EF Core requirement)
- **Current user/org context is Scoped** (per HTTP request)
- **Configuration/settings are Singleton** (read-only, set at startup)
- **MediatR handlers are Scoped** (access to scoped DbContext)
- **Pipeline behaviors are Singleton** (stateless cross-cutting concerns)
- Avoid capturing Scoped services in Singleton services (causes issues)

### What Can Be Injected in PageModels

**Available via BasePageModel (lazy-loaded):**
- `ISender Mediator` – Send commands/queries
- `ICurrentOrganization CurrentOrganization` – Org context
- `ICurrentUser CurrentUser` – User context
- `IFileStorageProvider FileStorageProvider` – File operations
- `IFileStorageProviderSettings FileStorageProviderSettings` – File config
- `IEmailer Emailer` – Email sending
- `IRelativeUrlBuilder RelativeUrlBuilder` – URL generation
- `ILogger Logger` – Logging

**Do NOT inject directly in PageModel:**
- `IAppDbContext` – Use MediatR instead
- `HttpClient` – Encapsulate in service
- Configuration values – Use `IOptions<T>` pattern

**Good Example:**

```csharp
public class Edit : BaseAdminPageModel
{
    // No constructor needed - use BasePageModel properties
    
    public async Task<IActionResult> OnGet(string id)
    {
        // Access via lazy-loaded properties
        var response = await Mediator.Send(new GetUserById.Query { Id = id });
        Logger.LogInformation("Loaded user {Id}", id);
        return Page();
    }
}
```

---

## 7. Validation, Error Handling & Logging

### Validation with FluentValidation

**Pattern:**

```csharp
public class Validator : AbstractValidator<Command>
{
    public Validator(IAppDbContext db)
    {
        RuleFor(x => x.EmailAddress)
            .NotEmpty()
            .EmailAddress()
            .WithMessage("Email address is required.");
        
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(255)
            .WithMessage("First name is required.");
        
        RuleFor(x => x).Custom((request, context) =>
        {
            // Complex validation with database lookups
            var existingUser = db.Users
                .FirstOrDefault(p => p.EmailAddress == request.EmailAddress);
            
            if (existingUser != null)
            {
                context.AddFailure(Constants.VALIDATION_SUMMARY, 
                    $"A user with email '{request.EmailAddress}' already exists.");
                return;
            }
        });
    }
}
```

**Rules:**
- One `Validator` class per Command/Query
- Validators can inject `IAppDbContext` for database lookups
- Use `Constants.VALIDATION_SUMMARY` for general errors (displayed at top of form)
- Use property names for field-specific errors
- Complex validation in `Custom()` method
- Validation runs automatically via `ValidationBehaviour<,>` pipeline
- Validation failures return `CommandResponseDto<T>` with errors (no exceptions)

### Exception Handling

**Application Layer Exceptions:**

```csharp
// Domain/Application exceptions (these are expected)
throw new NotFoundException("User", userId);
throw new UnauthorizedAccessException("User does not have permission.");
throw new InvalidApiKeyException();
```

**Middleware Handling:**

```csharp
// ExceptionsMiddleware.cs
if (error.Error is NotFoundException)
    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
else if (error.Error is UnauthorizedAccessException)
    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
else
    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
```

**Rules:**
- Throw domain exceptions (`NotFoundException`, `UnauthorizedAccessException`) for expected failures
- Middleware catches exceptions and maps to HTTP status codes
- Validation failures do **not** throw exceptions (use `CommandResponseDto` errors)
- Log exceptions via `ILogger` in handlers
- Never expose stack traces to users (only in Development mode)
- API responses return JSON error objects
- Web responses redirect to error pages (`/admin/error/404`)

### Logging

**Good Examples:**

```csharp
// Structured logging with context
Logger.LogInformation("Loaded user {UserId} for admin {AdminId}", 
    userId, CurrentUser.UserId);

// Warning for unexpected but handled cases
Logger.LogWarning("Role {RoleId} not found for user {UserId}", 
    request.RoleId, request.UserId);

// Error for exceptions
Logger.LogError(exception, "Failed to save user {UserId}", userId);
```

**Rules:**
- Use `ILogger<T>` (injected via `BasePageModel.Logger`)
- Use structured logging (named placeholders, not string interpolation)
- **Never log:**
  - Passwords, API keys, tokens (secrets)
  - Personally Identifiable Information (PII)
  - Full request/response payloads with sensitive data
- **Always log:**
  - User actions (create, update, delete)
  - External service calls (file storage, email)
  - Exceptions with context
- Use appropriate levels:
  - `Information` – Normal operations
  - `Warning` – Unexpected but handled
  - `Error` – Exceptions requiring investigation
  - `Debug` – Verbose details (development only)

---

## 8. Configuration, Settings & Secrets

### Configuration Structure

**appsettings.json:**

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=app;Username=postgres;Password=***"
  },
  "PATHBASE": "",
  "ENFORCE_HTTPS": "true",
  "APPLY_PENDING_MIGRATIONS": "false",
  "NUM_BACKGROUND_WORKERS": "4",
  "FILE_STORAGE_PROVIDER": "Local",
  "FILE_STORAGE_LOCAL_DIRECTORY": "user-uploads"
}
```

**Environment Variables:**

```bash
# Production deployment
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection="Host=prod-db;Database=app;Username=app;Password=***"
SMTP__HOST=smtp.sendgrid.net
SMTP__USERNAME=apikey
SMTP__PASSWORD=***
```

**Rules:**
- Configuration loaded from `appsettings.json` → `appsettings.{Environment}.json` → Environment Variables
- Use double underscore (`__`) for nested keys in env vars
- **Never commit secrets** to `appsettings.json` (use User Secrets in dev, env vars in prod)
- Use `IConfiguration` for simple values
- Use strongly-typed options (`IOptions<T>`) for complex configuration

### Strongly Typed Options

**Good Example:**

```csharp
// Define options class
public class EmailerConfiguration : IEmailerConfiguration
{
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; }
    public string? FromEmail { get; set; }
}

// Register in DI
services.Configure<EmailerConfiguration>(Configuration.GetSection("SMTP"));
services.AddScoped<IEmailerConfiguration, EmailerConfiguration>();

// Inject in service
public class Emailer : IEmailer
{
    private readonly IEmailerConfiguration _config;
    
    public Emailer(IEmailerConfiguration config)
    {
        _config = config;
    }
}
```

**Rules:**
- Use `IOptions<T>` for configuration classes
- Validate configuration at startup (throw if required values missing)
- Interface-based configuration for testability
- Scoped/Singleton based on mutability needs

### Where Secrets Must NOT Live

**❌ Never:**
- Hard-coded in source code
- Committed to `appsettings.json` in repository
- Logged or exposed in error messages
- Stored in frontend JavaScript

**✅ Always:**
- User Secrets for local development (`dotnet user-secrets set`)
- Environment variables for production
- Azure Key Vault / AWS Secrets Manager for cloud deployments
- Docker secrets for containerized deployments

---

## 9. Frontend Standards (No JS Frameworks)

### Core Principle

**The application is server-side rendered.** We do **not** use JavaScript frameworks (React, Vue, Angular).

### When JavaScript is Allowed

JavaScript is **allowed** for:
- ✅ Progressive enhancement (datepickers, WYSIWYG editors, file uploads)
- ✅ Small interactive components (dropdowns, modals, form validation)
- ✅ AJAX form submissions (where full page reload is poor UX)
- ✅ Third-party libraries (Uppy file uploads, TipTap editor)

JavaScript is **not allowed** for:
- ❌ Rendering entire pages or major sections
- ❌ Data fetching/state management (use server-side)
- ❌ Complex business logic
- ❌ Building a SPA

### JavaScript Organization

**Structure:**

```
wwwroot/
  js/
    core/              # Shared utilities
      events.js        # Event helpers (ready, delegate)
      dom.js           # DOM utilities
      net.js           # AJAX helpers
      validation.js    # Client-side validation
    pages/             # Page-specific scripts
      users/
        edit.js        # User edit page
      email-templates/
        edit.js        # Email template editor
    shared/            # Shared components
      confirm-dialog.js
```

**Good Example (ES6 Module):**

```javascript
/**
 * Users - Edit Page
 * Initializes user edit form interactions
 */

import { ready } from '/js/core/events.js';

/**
 * Initialize user edit page
 */
function init() {
    const config = window.AppUsersConfig || {};
    
    console.log('Initializing user edit page...', config);
    
    // Initialize form interactions
    const emailInput = document.querySelector('[name="EmailAddress"]');
    if (emailInput) {
        emailInput.addEventListener('blur', validateEmail);
    }
    
    console.log('User edit page initialized.');
}

function validateEmail(event) {
    const email = event.target.value;
    // Client-side validation logic
}

ready(init);
```

**Rules:**
- Use ES6 modules (`import`/`export`)
- Use `type="module"` in script tags
- Load scripts at end of body or in `@section Scripts`
- Avoid global variables (use module scope)
- Use `data-*` attributes for configuration
- Use `querySelector` / `querySelectorAll` (not jQuery)
- Document modules with JSDoc comments

### Unobtrusive JavaScript

**Good Example:**

```html
<!-- HTML with data attributes -->
<button type="button" 
        data-action="delete"
        data-id="@Model.Id"
        data-confirm="Are you sure?">
    Delete
</button>

<!-- JavaScript uses event delegation -->
<script type="module">
import { ready, delegate } from '/js/core/events.js';

ready(() => {
    delegate(document, 'click', '[data-action="delete"]', (event) => {
        const button = event.target.closest('[data-action="delete"]');
        const id = button.dataset.id;
        const message = button.dataset.confirm;
        
        if (confirm(message)) {
            // Handle delete
        }
    });
});
</script>
```

**Rules:**
- Avoid inline event handlers (`onclick="..."`)
- Use data attributes for configuration
- Use event delegation for dynamic content
- Separate concerns: HTML for structure, JS for behavior

### CSS Usage

**The application uses Bootstrap 5:**
- Use Bootstrap utility classes (`mb-3`, `btn btn-primary`)
- Custom CSS in `/wwwroot/css/` for specific needs
- Avoid inline styles (use classes)
- Prefix custom classes with `app-` to avoid conflicts

---

## 10. Testing Practices

### Testing Strategy

**Layers:**
- **Unit Tests** – Domain logic, value objects, validators (in `App.Domain.UnitTests`)
- **Integration Tests** – Handlers with database (not yet comprehensive)
- **Manual Testing** – Razor Pages and user workflows

**Current State:**
- Domain unit tests exist (`/tests/App.Domain.UnitTests`)
- Integration tests are minimal
- Focus on expanding test coverage incrementally

### What Must Be Tested

**Priority 1 (Always):**
- Domain value objects (`SortOrder`, etc.)
- Complex validators with business rules
- Critical commands (create/update users, roles)
- Authorization logic

**Priority 2 (Should):**
- Query handlers
- DTO mappings
- Utilities and extensions

**Priority 3 (Nice to have):**
- Razor Pages (via integration tests)
- UI workflows (manual testing)

### Testing Domain Logic

**Good Example:**

```csharp
[Test]
public void SortOrder_From_ValidDeveloperName_ReturnsCorrectType()
{
    // Arrange
    string developerName = "asc";
    
    // Act
    var sortOrder = SortOrder.From(developerName);
    
    // Assert
    Assert.That(sortOrder, Is.EqualTo(SortOrder.Ascending));
    Assert.That(sortOrder.DeveloperName, Is.EqualTo("asc"));
}

[Test]
public void SortOrder_From_InvalidDeveloperName_ThrowsException()
{
    // Arrange
    string developerName = "invalid";
    
    // Act & Assert
    Assert.Throws<SortOrderNotFoundException>(() => SortOrder.From(developerName));
}
```

**Rules:**
- Use NUnit (`[Test]`, `Assert.That`)
- Arrange-Act-Assert pattern
- Test happy path + failure cases
- Descriptive test names (method_scenario_expectedResult)
- No database access in unit tests (mock `IAppDbContext`)

### Testing Handlers (Integration)

**Pattern (not yet comprehensive):**

```csharp
[Test]
public async Task CreateUser_ValidInput_ReturnsSuccess()
{
    // Arrange
    using var context = CreateDbContext();
    var handler = new CreateUser.Handler(context);
    var command = new CreateUser.Command
    {
        FirstName = "John",
        LastName = "Doe",
        EmailAddress = "john.doe@example.com",
        IsAdmin = false
    };
    
    // Act
    var result = await handler.Handle(command, CancellationToken.None);
    
    // Assert
    Assert.That(result.Success, Is.True);
    Assert.That(result.Result, Is.Not.EqualTo(ShortGuid.Empty));
}
```

**Rules:**
- Use in-memory SQLite or Testcontainers for database
- Reset database state between tests
- Test command validation separately from handler execution
- Focus on critical paths first

---

## 11. Security, Performance & Observability

### Security Best Practices

#### Input Validation

**Rules:**
- Validate **all** inputs at the boundary (commands/queries)
- Use FluentValidation for structured validation
- Enforce length limits (`Truncate(200)` for route paths)
- Validate enums against allowed values
- Sanitize HTML if user-generated content is rendered

**Example:**

```csharp
RuleFor(x => x.EmailAddress)
    .NotEmpty()
    .EmailAddress()
    .MaximumLength(255);
```

#### Authorization

**Pattern:**

```csharp
[Authorize]
public class Edit : BaseAdminPageModel
{
    // Enforced at PageModel level
}

// Or check programmatically
var authResult = await AuthorizationService.AuthorizeAsync(
    User, 
    resource, 
    policy);

if (!authResult.Succeeded)
{
    return Forbid();
}
```

**Rules:**
- Use `[Authorize]` attribute at PageModel or handler level
- Define policies in `ConfigureServices` (not inline)
- Check permissions in Application layer for API access
- Never expose sensitive data to unauthorized users
- Log authorization failures

#### Anti-Forgery & CSRF

**Rules:**
- ASP.NET Core automatically includes anti-forgery tokens in forms
- Always use `<form method="post">` (not GET for mutations)
- For AJAX, include `RequestVerificationToken`
- API endpoints use API key authentication (no cookies)

#### Secrets & PII

**Rules:**
- Never log passwords, API keys, tokens
- Never log email addresses (use user IDs instead)
- Use `[ExcludePropertyFromOpenApiDocs]` for sensitive fields
- Redact sensitive data in error messages

### Performance Best Practices

#### Database Queries

**Allocation-Aware Code:**

```csharp
// ✅ Use AsNoTracking for read-only
var users = await _db.Users
    .AsNoTracking()
    .ToListAsync(cancellationToken);

// ✅ Use Select projection
var users = await _db.Users
    .Select(p => new UserDto { Id = p.Id, FirstName = p.FirstName, LastName = p.LastName })
    .ToListAsync(cancellationToken);

// ✅ Use IndexOf/Contains on collections (avoid LINQ in loops)
if (allowedValues.Contains(value))  // Fast
```

**Rules:**
- Use `AsNoTracking()` for read-only queries
- Use `Select` projections to avoid loading full entities
- Add indexes for `WHERE`, `ORDER BY`, `JOIN` columns
- Paginate large result sets (default 50, max 1000)
- Avoid N+1 queries (use `Include` for related data)

#### Caching

**The application uses IMemoryCache for read-mostly data:**

```csharp
// Cache organization settings (rarely change)
var settings = await cache.GetOrCreateAsync("org_settings", async entry =>
{
    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
    return await _db.OrganizationSettings.FirstAsync();
});
```

**Rules:**
- Cache configuration and metadata (not user-specific data)
- Use sensible TTLs (5-30 minutes)
- Invalidate cache on updates
- Avoid caching large objects (memory pressure)

### Observability

#### Logging

**Structured Logging:**

```csharp
Logger.LogInformation("User {UserId} created by admin {AdminId}", 
    userId, CurrentUser.UserId);
```

**Rules:**
- Log one line per event with context
- Include correlation IDs for request tracking
- Use log levels appropriately (Information, Warning, Error)
- Never log secrets or PII

#### Health Checks

**The application exposes `/healthz` endpoint:**

```json
{
  "status": "Healthy",
  "checks": [
    {
      "name": "postgres",
      "status": "Healthy",
      "duration": "00:00:00.0123456"
    }
  ]
}
```

**Rules:**
- Monitor database connectivity
- Monitor external dependencies (file storage, email)
- Return 200 if healthy, 503 if unhealthy
- Log health check failures

#### Audit Logs

**The application uses AuditBehavior pipeline:**

```csharp
public class AuditBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
{
    public async ValueTask<TResponse> Handle(/*...*/)
    {
        // Log command execution to AuditLogs table
        var auditLog = new AuditLog { /* ... */ };
        _db.AuditLogs.Add(auditLog);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
```

**Rules:**
- Commands marked with `LoggableRequest<T>` are audited
- Audit logs include: user, entity type, action, timestamp
- **Never delete audit logs** (append-only table)
- Display audit logs in admin UI

---

## 12. Examples & "Do / Don't"

### Razor PageModel: Do / Don't

**✅ Do:**

```csharp
[Authorize]
public class Create : BaseAdminPageModel
{
    [BindProperty]
    public FormModel Form { get; set; }

    public async Task<IActionResult> OnGet()
    {
        SetBreadcrumbs(/* ... */);
        
        var roles = await Mediator.Send(new GetRoles.Query { /* ... */ });
        
        Form = new FormModel
        {
            AvailableRoles = roles.Result.Items,
        };
        
        return Page();
    }

    public async Task<IActionResult> OnPost()
    {
        var command = new CreateUser.Command
        {
            FirstName = Form.FirstName,
            LastName = Form.LastName,
            EmailAddress = Form.EmailAddress,
            IsAdmin = Form.IsAdmin,
        };
        
        var response = await Mediator.Send(command);
        
        if (response.Success)
        {
            SetSuccessMessage("Created successfully.");
            return RedirectToPage("Edit", new { id = response.Result });
        }
        
        SetErrorMessage("Error creating.", response.GetErrors());
        await RepopulateFormOnError();
        return Page();
    }
    
    public record FormModel
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string EmailAddress { get; set; }
        public bool IsAdmin { get; set; }
        public List<RoleDto> AvailableRoles { get; set; } = new();
    }
}
```

**❌ Don't:**

```csharp
// ❌ Direct database access in PageModel
public class Create : BaseAdminPageModel
{
    private readonly AppDbContext _db;  // BAD
    
    public Create(AppDbContext db)
    {
        _db = db;
    }
    
    public async Task<IActionResult> OnPost()
    {
        // ❌ Business logic in PageModel
        var existingUser = await _db.Users
            .FirstOrDefaultAsync(p => p.EmailAddress == Form.EmailAddress);
        if (existingUser != null)
            return BadRequest();
        
        // ❌ Direct entity manipulation
        var entity = new User
        {
            FirstName = Form.FirstName,
            LastName = Form.LastName,
            EmailAddress = Form.EmailAddress,
        };
        _db.Users.Add(entity);
        await _db.SaveChangesAsync();  // BAD
        
        return RedirectToPage("Edit", new { id = entity.Id });
    }
}
```

### Command Handler: Do / Don't

**✅ Do:**

```csharp
public class CreateUser
{
    public record Command : LoggableRequest<CommandResponseDto<ShortGuid>>
    {
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public string EmailAddress { get; init; } = string.Empty;
        public bool IsAdmin { get; init; }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IAppDbContext db)
        {
            RuleFor(x => x.EmailAddress)
                .NotEmpty()
                .EmailAddress()
                .WithMessage("Email address is required.");
        }
    }

    public class Handler : IRequestHandler<Command, CommandResponseDto<ShortGuid>>
    {
        private readonly IAppDbContext _db;

        public Handler(IAppDbContext db)
        {
            _db = db;
        }

        public async ValueTask<CommandResponseDto<ShortGuid>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            var entity = new User 
            { 
                FirstName = request.FirstName,
                LastName = request.LastName,
                EmailAddress = request.EmailAddress,
                IsAdmin = request.IsAdmin
            };
            _db.Users.Add(entity);
            entity.AddDomainEvent(new UserCreatedEvent(entity));
            await _db.SaveChangesAsync(cancellationToken);
            
            return new CommandResponseDto<ShortGuid>(entity.Id);
        }
    }
}
```

**❌ Don't:**

```csharp
// ❌ Multiple responsibilities in one handler
public class Handler : IRequestHandler<Command, CommandResponseDto<ShortGuid>>
{
    private readonly AppDbContext _db;  // ❌ Use interface, not concrete
    private readonly IEmailer _emailer;
    private readonly IFileStorageProvider _storage;
    
    public async ValueTask<CommandResponseDto<ShortGuid>> Handle(Command request)  // ❌ Missing CancellationToken
    {
        // ❌ Validation in handler (should be in Validator)
        if (string.IsNullOrEmpty(request.EmailAddress))
            return new CommandResponseDto<ShortGuid>("EmailAddress", "Required");
        
        // ❌ Blocking call
        var user = _db.Users.Find(request.Id);  // Use FindAsync
        
        var entity = new User { /* ... */ };
        _db.Users.Add(entity);
        _db.SaveChanges();  // ❌ Use async
        
        // ❌ Side effects without error handling
        _emailer.SendEmail(/* ... */);  // Should be in event handler
        
        return new CommandResponseDto<ShortGuid>(entity.Id);
    }
}
```

### JavaScript: Do / Don't

**✅ Do:**

```javascript
/**
 * Confirm Dialog
 * Handles confirmation prompts for destructive actions
 */

import { delegate } from '/js/core/events.js';

/**
 * Initialize confirm dialogs
 */
export function init() {
    delegate(document, 'click', '[data-confirm]', handleConfirm);
}

/**
 * Handle confirm click
 */
function handleConfirm(event) {
    const button = event.target.closest('[data-confirm]');
    const message = button.dataset.confirm || 'Are you sure?';
    
    if (!confirm(message)) {
        event.preventDefault();
        event.stopPropagation();
    }
}

// Auto-initialize
init();
```

**❌ Don't:**

```javascript
// ❌ Global variables
var myGlobalVar = 'bad';

// ❌ jQuery (not used in the application)
$(document).ready(function() {
    $('.my-button').click(function() {
        // ...
    });
});

// ❌ Inline event handlers in HTML
<button onclick="handleClick()">Bad</button>

// ❌ Complex business logic in JS
function calculatePricing(items) {
    // ❌ This belongs on the server
    let total = 0;
    for (let item of items) {
        total += item.price * (1 - item.discount);
    }
    return total;
}

// ❌ Rendering major sections with JS
function renderContentList(items) {
    const html = items.map(item => `
        <div class="card">
            <h3>${item.title}</h3>
            <p>${item.description}</p>
        </div>
    `).join('');
    document.getElementById('content').innerHTML = html;  // BAD - use server rendering
}
```

---

## 13. Migration Notes

### Evolving Toward These Standards

**If you encounter code that doesn't follow these standards:**

1. **For new features:** Follow these standards from the start
2. **For bug fixes:** Apply standards to the file you're touching
3. **For refactoring:** Gradually update patterns (don't rewrite everything)

### Priority Improvements

**High Priority (Do Now):**
- Always use `async/await` (never `.Result` or `.Wait()`)
- Always include `CancellationToken` in async methods
- Always inject interfaces (not concrete classes)
- Always validate inputs with FluentValidation
- Never put business logic in PageModels (use MediatR)

**Medium Priority (Do When Touching Code):**
- Convert to `record` types for immutable DTOs
- Add XML doc comments for public APIs
- Add structured logging to handlers
- Use `AsNoTracking()` for read-only queries
- Add authorization policies to sensitive operations

**Low Priority (Nice to Have):**
- Expand test coverage incrementally
- Refactor large handlers into smaller methods
- Consolidate duplicate code into shared utilities
- Improve error messages for better UX

### Breaking Changes to Avoid

**Do NOT:**
- Change database column names (requires migration)
- Change API response formats (breaks integrations)
- Remove public interfaces (breaks plugins/extensions)
- Change route patterns (breaks bookmarks/links)

**Instead:**
- Add new fields/properties (backward compatible)
- Deprecate old APIs with warnings
- Extend interfaces with new methods (optional)
- Add new routes (keep old ones working)

---

## Appendix: Quick Reference

### Command Pattern

```csharp
public class CommandName
{
    public record Command : LoggableRequest<CommandResponseDto<TResult>> { }
    public class Validator : AbstractValidator<Command> { }
    public class Handler : IRequestHandler<Command, CommandResponseDto<TResult>> { }
}
```

### Query Pattern

```csharp
public class QueryName
{
    public record Query : IRequest<IQueryResponseDto<TResult>> { }
    public class Handler : IRequestHandler<Query, IQueryResponseDto<TResult>> { }
}
```

### PageModel Pattern

```csharp
[Authorize(Policy = "RequiredPolicy")]
public class PageName : BaseAdminPageModel
{
    [BindProperty]
    public FormModel Form { get; set; }
    
    public async Task<IActionResult> OnGet() { /* ... */ }
    public async Task<IActionResult> OnPost() { /* ... */ }
    
    public record FormModel { }
}
```

### Entity Pattern

```csharp
public class EntityName : BaseAuditableEntity
{
    public string Property { get; set; }
    public Guid RelatedEntityId { get; set; }
    public virtual RelatedEntity? RelatedEntity { get; set; }
}
```

### Value Object Pattern

```csharp
public class ValueObjectName : ValueObject
{
    protected ValueObjectName(string value) { Value = value; }
    public string Value { get; private set; }
    
    public static ValueObjectName From(string value) { /* ... */ }
    protected override IEnumerable<object> GetEqualityComponents() { yield return Value; }
}
```

---

## Conclusion

This document is the **single source of truth** for the .NET 10 architecture and coding standards. When in doubt:

1. Follow patterns from this document
2. Look for similar existing code in the repo
3. Ask for guidance if unclear
4. Update this document when patterns evolve

**Remember:** These standards exist to make the application **maintainable, testable, and evolvable** for years to come. Follow them consistently, and the codebase will remain a joy to work with.

---

**Document Version:** 1.0  
**Last Updated:** November 21, 2025  
**Maintained by:** Development Team

