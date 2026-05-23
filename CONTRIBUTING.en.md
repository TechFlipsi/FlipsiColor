# Contributing to FlipsiColor

Thank you for your interest in contributing! Here are the guidelines.

[🇦🇹 Deutsch](CONTRIBUTING.md)

## Setup

1. Fork the repository
2. Clone your fork: `git clone https://github.com/YOUR_USERNAME/FlipsiColor.git`
3. Create a feature branch: `git checkout -b feature/my-feature`
4. Build: `cmake -B build && cmake --build build`

## Code Style

- **C++20** — Use modern C++ features (concepts, ranges, structured bindings where appropriate)
- **Naming**: `PascalCase` for classes/structs, `camelCase` for methods/functions, `snake_case` for variables, `UPPER_CASE` for constants
- **Headers**: Use `#pragma once` as include guards
- **Smart pointers**: Prefer `std::unique_ptr` / `std::shared_ptr` over raw `new`/`delete`
- **Qt conventions**: Signals/slots with the new syntax (`&Class::method`)

## Commit Messages

Format: `type(scope): description`

Types: `feat`, `fix`, `docs`, `refactor`, `test`, `ci`, `chore`

Examples:
- `feat(ai): add CodeFormer face restoration with fidelity weight`
- `fix(color): correct ProPhoto RGB to sRGB conversion clipping`
- `docs(sdp): update model selection after benchmark verification`

## Pull Requests

1. Ensure the build passes: `cmake --build build --config Release`
2. Run tests: `ctest --test-dir build`
3. Keep PRs focused — one feature/fix per PR
4. Describe what changed and why

## Issues

- Use GitHub Issues for bugs and feature requests
- Include: OS, GPU, driver version, steps to reproduce
- Check existing issues before opening a new one