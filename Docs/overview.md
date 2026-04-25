# Overview

The aim of this document is to provide a high-level technical summary of the project. For more details about individual aspects of the application, refer to the other documentation files (still a work in progress):

- [`testing.md`]()
- [`database.md`]()
- [`ui-guidelines.md`]()
- [`layer-gen.md`]()
- [`pallet-gen.md`]()
- [`release-and-deployment.md`]()

> [!NOTE]
> For a reference on the provided functionalities (but also limitations and assumptions) and a user-friendly guide on using the app, refer to [`user-guide.md`]().

## Architecture

Stack Solver is a desktop WPF application targeting .NET 10 for Windows.

### Architectural Style

The project follows the layered architectural style with MVVM and .NET Generic Host.

The presentation is handled by WPF views and pages with view models (under `/Views` and `/ViewModels`). Application services are used for domain logic. Data access is done using EF Core and SQLite via repository interfaces. For validation, FluentValidation is used for input DTOs. 

### Startup/shutdown flow

1. App starts and host is started.
2. Database initialization runs (`DatabaseInitializer.InitializeAsync()`).
3. Main navigation shell and pages are resolved from DI.
4. On exit host is stopped and logs are flushed.
5. Unhandled dispatcher exceptions are logged.

## Configuration

Config is loaded from `defaults.json`. For now, it covers Serilog options, and also defaults for layer generation (such as max number of attempts, max solver time, etc.) and pallet config (default dimensions, height and weight limits, etc.).

> [!NOTE]
> `defaults.json` is optional and loaded with `reloadOnChange: true`.