# .NET 10 Clean Architecture Boilerplate

[![MIT License](https://img.shields.io/badge/License-MIT-green.svg)](https://choosealicense.com/licenses/mit/)
[![Build Status](https://github.com/raythahq/raytha-core/actions/workflows/tests.yml/badge.svg?branch=main)](https://github.com/raythahq/raytha-core/actions)

A production-ready .NET 10 boilerplate built with Clean Architecture principles, designed to enable solo full-stack developers to build large systems and iterate extremely quickly. This repository is "Cursor-ready" and optimized for AI-assisted development.

## 🎯 Mission

This boilerplate provides a solid foundation that allows developers to focus on building features rather than infrastructure. The architecture is carefully structured to be intuitive, maintainable, and scalable—perfect for rapid iteration and long-term growth.

## ✨ Features

### Architecture & Patterns
- **Clean Architecture** with clear separation of concerns (Domain, Application, Infrastructure, Presentation)
- **CQRS** pattern using Mediator for command/query separation
- **Razor Pages** philosophy for all screens with minimal JavaScript dependencies
- **Vanilla JavaScript** only where needed—no heavy frontend frameworks

### Authentication & Authorization
- **Role-Based Access Control (RBAC)** with roles and user groups
- **SAML 2.0 SSO** support for enterprise authentication
- **JWT SSO** for flexible authentication schemes
- Separate authentication flows for **public users** and **administrators**
- Magic link authentication

### Data & Persistence
- **PostgreSQL** database with Entity Framework Core
- **Audit logs** for comprehensive change tracking
- Migration support with EF Core Migrations

### File Storage
Flexible file storage with multiple provider options:
- **Local file system** for development and small deployments
- **Azure Blob Storage** for cloud-native applications
- **S3/S3-compatible** storage (AWS S3, MinIO, DigitalOcean Spaces, etc.)
- Direct upload to cloud support for optimized performance

### Email & Communication
- **Email templates** with Liquid templating engine
- Email template revision history
- Configurable email service integration

### User Interface
- **TipTap WYSIWYG** editor for rich text editing
- **Uppy** for modern file uploads with drag-and-drop
- Responsive design patterns
- Clean, professional admin interface

### Project Governance & Workflow
- **GitHub SpecKit** for lightweight specs, plans, tasks, and a project constitution
- `.specify/` directory with SpecKit templates and the App Constitution for architecture rules

### Developer Experience
- "Cursor-ready" codebase optimized for AI-assisted development
- Comprehensive coding standards and conventions
- Clear project structure following domain-driven design
- Dependency injection throughout
- Background task queue for async operations

## 🏗️ Project Structure

```
src/
├── App.Domain/          # Core business entities, value objects, domain events
├── App.Application/     # Use cases, CQRS commands/queries, DTOs, interfaces
├── App.Infrastructure/  # EF Core, file storage, email, external services
└── App.Web/            # Razor Pages, authentication, HTTP concerns
```

The architecture follows the **Dependency Rule**: dependencies flow inward only. The Domain layer has zero external dependencies, Application depends only on Domain, Infrastructure implements Application interfaces, and the Web layer orchestrates via Mediator.

## 🚀 Getting Started

### Prerequisites
- .NET 10 SDK
- PostgreSQL database
- (Optional) Azure Storage Account or S3-compatible storage for file storage

### Installation

1. Clone the repository:
```bash
git clone https://github.com/yourusername/raytha-core.git
cd raytha-core
```

2. Configure your database connection in `appsettings.json` or environment variables:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=yourdb;Username=user;Password=password"
  }
}
```

3. Database migrations will run on first launch if `APPLY_PENDING_MIGRATIONS` is set to `true` in `appsettings.json`

4. Configure file storage (optional):
   - Set `FILE_STORAGE_PROVIDER` environment variable to `local`, `azureblob`, or `s3`
   - Configure corresponding storage settings as needed

5. Run the application:
```bash
dotnet run --project src/App.Web
```

## 📚 Key Concepts

### Clean Architecture Layers

- **Domain Layer**: Pure business logic with no dependencies. Contains entities, value objects, and domain events.
- **Application Layer**: Use case orchestration. Contains CQRS commands/queries, validators, and application interfaces.
- **Infrastructure Layer**: External concerns. Implements database access, file storage, and third-party integrations.
- **Web Layer**: Thin presentation layer. Razor Pages delegate to Mediator handlers.

### CQRS Pattern

All operations are separated into:
- **Commands**: Write operations (Create, Update, Delete)
- **Queries**: Read operations (Get, List)

Each command/query has:
- A `Command`/`Query` class
- A `Validator` class using FluentValidation
- A `Handler` class implementing the business logic

### Razor Pages Philosophy

- All screens are Razor Pages (`.cshtml` and `.cshtml.cs`)
- PageModels are thin orchestrators that use `Mediator.Send()` to execute commands/queries
- Minimal JavaScript—only vanilla JS where interactivity is required
- No heavy frontend frameworks—keeps the stack simple and maintainable

## 🔧 Configuration

### Environment Variables

Key configuration options:

- `FILE_STORAGE_PROVIDER`: `local`, `azureblob`, or `s3`
- `FILE_STORAGE_LOCAL_DIRECTORY`: Local storage directory (default: `user-uploads`)
- `FILE_STORAGE_AZUREBLOB_CONNECTION_STRING`: Azure Blob connection string
- `FILE_STORAGE_S3_ACCESS_KEY`: S3 access key (required for S3)
- `FILE_STORAGE_S3_SECRET_KEY`: S3 secret key (required for S3)
- `FILE_STORAGE_S3_BUCKET`: S3 bucket name (required for S3)
- `FILE_STORAGE_S3_REGION`: S3 region (required for AWS S3, ignored for S3-compatible storage)
- `FILE_STORAGE_S3_SERVICE_URL`: S3 service URL (required for S3-compatible storage like MinIO or Cloudflare R2, ignored for AWS S3)

**S3 Configuration Notes:**
- **AWS S3**: Provide `FILE_STORAGE_S3_REGION` (e.g., `us-east-1`). Do not set `FILE_STORAGE_S3_SERVICE_URL` (or leave empty).
- **S3-Compatible Storage** (MinIO, DigitalOcean Spaces, Cloudflare R2, etc.): Provide `FILE_STORAGE_S3_SERVICE_URL` (e.g., `https://minio.example.com`). The `FILE_STORAGE_S3_REGION` is ignored when using a custom service URL.

See `src/App.Application/Common/Utils/FileStorageUtility.cs` for all configuration options.

## 📖 Documentation

For architecture rules, see the [App Constitution](.specify/memory/constitution.md).  
For detailed coding standards, patterns, and examples, see
[docs/architecture-standards.md](docs/architecture-standards.md).

This repository is configured for **GitHub SpecKit**; see the `.specify/` folder
for templates (plans, specs, tasks) and the project constitution.

## 🤝 Contributing

Contributions are welcome! Please read [CONTRIBUTING.md](CONTRIBUTING.md) for details on our code of conduct and the process for submitting pull requests.

## 🔒 Security

For security concerns, please see [SECURITY.md](SECURITY.md) or email hello@raytha.com.

## 📄 License

This project is licensed under the terms specified in [LICENSE.md](LICENSE.md).

## 🙏 Acknowledgments

This project is derived from [Raytha CMS](https://github.com/raythahq/raytha), an open-source content management system. We've extracted and refined the core architecture to serve as a versatile boilerplate for building .NET applications.

## 💬 Support

For questions, issues, or contributions, please open an issue on GitHub or contact hello@raytha.com.

---

Built with ❤️ for developers who want to ship fast without compromising on architecture quality.

