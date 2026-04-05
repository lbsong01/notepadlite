# Project Guidelines

## Project Status
This workspace is currently uninitialized. Treat it as a greenfield repository and inspect the existing files before assuming a language, framework, build command, or test command.

## Build And Test
Do not invent build, run, or test commands when the workspace does not define them.
Detect and follow the first project manifest or tool configuration that appears.
If a task adds executable code, add or update the minimal commands and documentation needed to build, run, and test that code.
If testing cannot be run because the workspace does not yet have a test setup, state that clearly.

## Repository Structure
Keep the top level clean.
Place application code in a dedicated source directory or project folder.
Place automated tests in a matching test directory or test project.
Place reusable scripts in a scripts directory.
Place project documentation in README.md and a docs directory when the project grows.

## Conventions
Prefer small, focused changes that establish the simplest workable foundation.
Do not add new dependencies unless they are required for the task.
Follow repository configuration once files such as .editorconfig, lint config, formatter config, or project manifests exist.
When introducing the first stack, create the canonical project files for that stack instead of ad hoc scripts.

## Documentation Expectations
When adding the first real project, include setup and usage instructions in README.md.
Document any non-obvious local prerequisites, environment variables, and developer workflows as they are introduced.