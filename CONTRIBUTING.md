# Contributing to ContextR

Thank you for your interest in contributing to ContextR! This document provides guidelines and instructions for contributing.

## Development

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later
- An IDE: Visual Studio 2022+, JetBrains Rider 2024+, or Visual Studio Code with C# Dev Kit

### Getting started

Fork the repository and clone your fork:

```bash
git clone https://github.com/YOUR_USERNAME/ContextR.git
cd ContextR
```

Add the upstream remote:

```bash
git remote add upstream https://github.com/arttonoyan/ContextR.git
```

Build and run tests to verify your setup:

```bash
dotnet build ContextR.slnx
dotnet test ContextR.slnx
```

## Pull Request

All contributions are welcome via GitHub pull requests.

### Workflow

1. Create a branch from `main` using the naming convention below.
2. Make your changes, commit using [Conventional Commits](#commit-convention).
3. Ensure all tests pass and there are no new warnings.
4. Push to your fork and open a pull request against `main`.

```bash
git checkout -b feat/my-new-feature
# Make your changes
git commit -s -m "feat(core): add new capability"
git push fork feat/my-new-feature
```

### Branch naming

Use `type/short-description` where `type` matches the commit convention:

- `feat/` -- new functionality
- `fix/` -- bug fixes
- `refactor/` -- code improvement without behavior change
- `docs/` -- documentation only
- `chore/` -- CI, build, dependencies, housekeeping

### Running tests locally

Run all tests:

```bash
dotnet test ContextR.slnx
```

Run tests with code coverage:

```bash
dotnet test ContextR.slnx /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
```

### How to receive comments

- If your PR is not ready for review, mark it as [draft](https://github.blog/2019-02-14-introducing-draft-pull-requests/).
- Make sure all required CI checks pass.
- Submit small, focused PRs addressing a single concern.
- Make sure the PR title follows the [commit convention](#commit-convention).
- Write a summary that helps reviewers understand the change.

### How to get PRs merged

A PR is considered **ready to merge** when:

- CI passes (build, tests, coverage).
- Major feedback is resolved.
- It has been open for review for at least one working day (trivial changes like typos or docs can merge sooner).

## Commit Convention

This project follows [Conventional Commits](https://www.conventionalcommits.org).

### Format

```
type(scope): short description

Optional body with more detail.

BREAKING CHANGE: description of breaking change (optional footer)
```

### Types

| Type | Description | Appears in changelog |
|---|---|---|
| `feat` | New feature | Yes |
| `fix` | Bug fix | Yes |
| `refactor` | Code change, no behavior change | Yes |
| `perf` | Performance improvement | Yes |
| `revert` | Revert a previous commit | Yes |
| `docs` | Documentation only | No |
| `test` | Adding or correcting tests | No |
| `ci` | CI/CD changes | No |
| `build` | Build system or dependency changes | No |
| `style` | Formatting, no code change | No |
| `chore` | Maintenance tasks | No |

### Scopes (optional)

Scopes map to project packages: `core`, `propagation`, `transport-http`, `transport-grpc`, `aspnetcore`, `resolution`, `openfeature`.

### Breaking changes

Use `!` after the type to indicate a breaking change:

```
feat!: remove generic methods from interfaces
```

Or add a `BREAKING CHANGE:` footer in the commit body.

### Examples

```
feat(core): add Type-based non-generic interface methods
fix(aspnetcore): handle null domain in middleware
docs: update ARCHITECTURE.md for new API shape
ci: add coverage comment to PRs
refactor!: change parameter order for domain-aware methods
```

## Style Guide

This project includes an [`.editorconfig`](.editorconfig) file that enforces consistent formatting. It is supported by Visual Studio, Rider, and VS Code with the EditorConfig extension.

The build enforces code style via `EnforceCodeStyleInBuild` in [`Directory.Build.props`](Directory.Build.props).

Key conventions:

- File-scoped namespaces
- 4-space indentation for C#
- Allman-style braces
- `var` where type is apparent
- PascalCase for constants

## Tests

- All existing tests must pass before a PR can be merged.
- New features and bug fixes should include tests.
- Aim to maintain or improve code coverage.

## Documentation

If your change affects public API behavior, update the relevant documentation in the [`docs/`](docs/) folder.

## License

By contributing to ContextR, you agree that your contributions will be licensed under the [Apache License 2.0](LICENSE).
