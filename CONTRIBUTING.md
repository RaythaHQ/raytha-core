# Contributing

Thank you for your interest in contributing! We welcome contributions from the community and are grateful for your help in making this project better.

## How to Contribute

### Reporting Bugs

If you find a bug, please open an issue on GitHub with:
- A clear, descriptive title
- A detailed description of the problem
- Steps to reproduce the issue
- Expected vs. actual behavior
- Your environment (OS, .NET version, etc.)
- Any relevant logs or error messages

### Suggesting Features

We'd love to hear your ideas! Please open an issue with:
- A clear description of the feature
- The use case and motivation
- Any potential implementation considerations

### Code Contributions

1. **Fork the repository** and clone your fork
2. **Create a branch** for your changes:
   ```bash
   git checkout -b feature/your-feature-name
   ```
3. **Make your changes** following our coding standards (see `.cursor/standards.md`)
4. **Write or update tests** as needed
5. **Ensure all tests pass** and there are no linter errors
6. **Commit your changes** with clear, descriptive commit messages
7. **Push to your fork** and open a pull request

### Coding Standards

Please follow the coding standards documented in [.cursor/standards.md](.cursor/standards.md). Key principles:

- Follow Clean Architecture principles
- Use CQRS pattern for all operations
- Keep Razor Pages thin—delegate to Mediator handlers
- Use vanilla JavaScript only—avoid heavy frontend frameworks
- Write clear, self-documenting code
- Include XML documentation for public APIs
- Follow existing naming conventions and patterns

### Pull Request Process

1. Ensure your code follows the project's coding standards
2. Update documentation if you've changed functionality
3. Add tests for new features or bug fixes
4. Ensure all existing tests pass
5. Request review from maintainers
6. Address any feedback before merging

### Commit Messages

Write clear, descriptive commit messages:
- Use the present tense ("Add feature" not "Added feature")
- Use the imperative mood ("Move cursor to..." not "Moves cursor to...")
- Limit the first line to 72 characters or less
- Reference issues and pull requests when applicable

## Code of Conduct

This project adheres to the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code.

## Questions?

If you have questions about contributing, please:
- Open an issue on GitHub
- Contact the maintainers at hello@raytha.com

Thank you for contributing! 🎉
