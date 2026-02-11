Purpose
This file defines explicit instructions for AI coding agents working in the AIKit.Mcp repository. All automated code changes must follow these rules to ensure consistency, maintainability, and alignment with existing architectural patterns.

If any conflict exists, this file takes precedence for AI agents.

---

Project Overview
AIKit.Mcp is a .NET 10 library for building MCP (Model Context Protocol) servers in .NET.
It provides a fluent builder-based API for configuring MCP servers with support for transports, authentication, discovery, and extensibility.

The library focuses on:

- Explicit configuration
- Strong typing
- Extensibility via builders and services
- Predictable behavior for MCP server implementations

Agents must preserve these design goals.

---

Technology Stack

- .NET SDK: 10.0
- Language: C# (latest supported by .NET 10)
- Target: Library + sample MCP server
- MCP protocol SDK
- Logging: Microsoft.Extensions.Logging

Do NOT introduce:

- ORMs
- ASP.NET MVC abstractions unless already present
- Heavy frameworks unrelated to MCP or hosting

---

Build, Run & Test Commands

Restore
dotnet restore

Build
dotnet build --configuration Release

Run sample
dotnet run --project samples/AIKit.Mcp.Sample/AIKit.Mcp.Sample.csproj

Tests
dotnet test --no-build --verbosity normal

Agents must assume these commands are authoritative.

---

Project Structure

src/

- AIKit.Mcp # Main library
  - Core MCP abstractions
  - Builders and configuration
  - Transports and authentication
- AIKit.Mcp.Sample # Example MCP server usage

tests/

- AIKit.Mcp.Tests # Unit and integration tests

.github/

- CI and automation

Do not restructure folders without explicit approval.

---

Architectural Rules

- Builder pattern is the primary configuration mechanism
- Configuration must be explicit and readable
- No hidden side effects during registration
- Prefer composition over inheritance
- Constructor injection only

No service locator pattern
No reflection-based magic unless already used and justified

---

Code Style & Conventions

General

- Explicit access modifiers everywhere
- Async methods must use Async suffix
- Prefer immutability
- Avoid static mutable state

Naming

- Classes: PascalCase
- Interfaces: IPascalCase
- Methods: PascalCase
- Private fields: \_camelCase

---

Builder & MCP Conventions

- MCP servers are configured through fluent builders
- Tools, prompts, and resources should be discoverable and extensible
- Maintain sample server conventions
- Keep customization minimal and explicit

Example pattern:
builder.Services.AddAIKitMcp(mcp =>
{
mcp.ServerName = "MyServer";
mcp.WithHttpTransport(opts => { ... });
});

Agents should follow this pattern when introducing MCP extensions.

---

Transport & Authentication

- STDIO transport for CLI clients
- HTTP transport with OAuth2, JWT, or custom authentication
- Do not mix authentication strategies without explicit intent
- Preserve existing security patterns

---

Logging Rules

- Use ILogger<T> consistently
- Do NOT use Console.WriteLine in production
- Include meaningful log levels (Information, Warning, Error)
- Never log secrets, tokens, or personal data

---

Error Handling

- Bubble exceptions to meaningful boundaries
- Do not swallow exceptions silently
- Provide descriptive messages

---

Testing Rules

Use xUnit for unit and integration tests. Follow Arrange-Act-Assert. Include `ITestOutputHelper _output` for logging test progress, setup, and debugging (e.g., '\_output.WriteLine("Server started...")') to maintain visibility. Ensure tests are self-checking via assertions; logging should not replace verification.

---

Architectural Rules

Adopt clean architecture: Enforce Dependency Rule: inner layers don't reference outer. Use DIP for boundaries. Prefer composition over inheritance; avoid service locator.

---

Security and Maintainability Rules

Avoid logging secrets/tokens/personal data. Use async methods for I/O. Validate inputs; bubble exceptions without swallowing. Ensure immutability and strong typing.

---

Protocol-Specific Rules

Ensure strong typing and auto-discovery without excessive reflection. Support extensibility via builders and services.

---

Boundaries & Safety Rules

NEVER
❌ Add breaking changes without consent
❌ Remove or bypass authentication logic
❌ Reorganize project folders arbitrarily

ALWAYS
✅ Add tests for all changes
✅ Preserve builder API patterns
✅ Maintain sample apps and tests
✅ Always document generated code/classes using standard XML documentation comments

ASK BEFORE
🔶 Major API changes
🔶 Deprecation or removal of existing APIs
🔶 Structural redesigns

---

Git & Pull Request Guidelines

- Branches: feature/_, fix/_, chore/\*
- Commit messages: Conventional Commits
- PRs must:
  - Build successfully
  - Pass all tests
  - Include clear description
  - Include relevant tests

---

Agent Behavior Expectations

- Always read and follow all rules in AGENTS.md before making any changes
- Prefer small, incremental changes
- Respect existing patterns
- Do not touch unrelated modules
- Clearly explain assumptions in PR descriptions

---

Final Rule
If unsure about a change’s effect on project conventions or architecture, ask before implementation.
