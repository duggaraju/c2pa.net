# Repository Setup Guide

This document describes the GitHub Actions workflows and automation setup for the c2pa.net project.

## Workflows

### 1. CI/CD Pipeline (`ci-cd.yml`)

**Triggers:**

- Push to `main` or `develop` branches
- Pull requests to `main` or `develop` branches
- Manual workflow dispatch
- Release events

**Features:**

- Multi-platform builds (Windows, Linux, macOS)
- Rust and .NET dependency caching
- Automated testing with coverage reports
- NuGet package creation
- Security scanning with Trivy
- Code quality checks (formatting, linting)

**Build Matrix:**

- Windows: Debug & Release
- Linux: Release only (to save resources)
- macOS: Release only (to save resources)

### 2. Release Workflow (`release.yml`)

**Triggers:**

- Git tags matching `v*` pattern
- Manual workflow dispatch with version input

**Features:**

- Automatic version detection from git tags
- Project file version updates
- Comprehensive testing before release
- GitHub release creation with artifacts
- Automatic NuGet.org publication

### 3. Auto-assign and Label (`auto-assign-label.yml`)

**Triggers:**

- Pull request opened/ready for review
- Issues opened

**Features:**

- Auto-assigns PRs/issues to their authors
- Automatic labeling based on changed files
- Size-based labeling (XS, S, M, L, XL)

## Secrets Configuration

The following secrets need to be configured in your GitHub repository:

### Required Secrets

1. **`NUGET_API_KEY`**
   - Description: API key for publishing to NuGet.org
   - How to get: Visit [NuGet.org API Keys](https://www.nuget.org/account/apikeys)
   - Scope: Push new packages and package versions

2. **`GITHUB_TOKEN`**
   - Description: Automatically provided by GitHub Actions
   - Used for: GitHub Packages, creating releases, API access

## Package Publishing Strategy

### Automatic Publishing

- **GitHub Packages**: Every push to `main` branch
- **NuGet.org**: Only on official releases (git tags)

### Manual Publishing

Use the release workflow with manual dispatch to create releases without git tags.

## Setting Up Your Repository

### 1. Enable GitHub Actions

1. Go to your repository settings
2. Navigate to "Actions" → "General"
3. Ensure "Allow all actions and reusable workflows" is selected

### 2. Configure Secrets

1. Go to repository "Settings" → "Secrets and variables" → "Actions"
2. Add the required secrets listed above

### 3. Configure GitHub Packages (Optional)

1. Go to repository "Settings" → "Actions" → "General"
2. Scroll to "Workflow permissions"
3. Select "Read and write permissions"
4. Check "Allow GitHub Actions to create and approve pull requests"

### 4. Set Up Branch Protection

1. Go to repository "Settings" → "Branches"
2. Add a branch protection rule for `main`:
   - Require status checks to pass before merging
   - Require branches to be up to date before merging
   - Include administrators
   - Required status checks:
     - `Build and Test (windows-latest, Release)`
     - `Build and Test (ubuntu-latest, Release)`
     - `Build and Test (macos-latest, Release)`
     - `Code Quality`
     - `Security Scan`

### 5. Configure Dependabot

The repository includes a Dependabot configuration that will:

- Check for .NET package updates weekly
- Check for Rust dependencies in the c2pa-rs submodule
- Check for GitHub Actions updates
- Create PRs with appropriate reviewers and assignees

## Labels Setup

Create the following labels in your repository for better organization:

### Type Labels

- `bug` (red): Something isn't working
- `enhancement` (light blue): New feature or request
- `documentation` (blue): Improvements or additions to documentation
- `dependencies` (yellow): Pull requests that update dependencies

### Component Labels

- `library` (purple): Changes to the main library code
- `tests` (green): Changes to test code
- `example` (orange): Changes to example projects
- `generator` (pink): Changes to the bindings generator
- `build` (brown): Changes to build configuration
- `ci/cd` (dark blue): Changes to CI/CD workflows

### Size Labels

- `size/XS` (light green): <10 lines changed
- `size/S` (green): 10-30 lines changed
- `size/M` (yellow): 30-100 lines changed
- `size/L` (orange): 100-500 lines changed
- `size/XL` (red): >500 lines changed

### Status Labels

- `triage` (white): Needs initial review
- `ready` (green): Ready for development
- `in-progress` (yellow): Currently being worked on
- `blocked` (red): Blocked by external dependency

## Workflow Customization

### Adjusting Build Matrix

Edit `.github/workflows/ci-cd.yml` to modify the build matrix:

- Add/remove operating systems
- Change build configurations
- Adjust excluded combinations

### Changing Triggers

Modify the `on:` section in workflow files to change when workflows run:

- Add/remove branch names
- Change event types
- Add schedule triggers

### Customizing Package Settings

Edit the NuGet package configuration in `lib/ContentAuthenticity.Bindings.csproj`:

- Update package metadata
- Modify included files
- Change target frameworks

## Monitoring and Maintenance

### Workflow Status

Monitor workflow runs in the "Actions" tab of your repository. Set up notifications for failed builds.

### Dependency Updates

Review and merge Dependabot PRs regularly to keep dependencies current.

### Security Alerts

Review security scanning results in the "Security" tab and address any vulnerabilities.

## Troubleshooting

### Common Issues

1. **Build failures**: Check that all required secrets are configured
2. **Test failures**: Ensure tests pass locally before pushing
3. **Publishing failures**: Verify NuGet API key permissions and package name conflicts
4. **Submodule issues**: Ensure submodules are properly initialized and updated

### Getting Help

- Check the Actions logs for detailed error messages
- Review the workflow files for configuration issues
- Consult the GitHub Actions documentation for advanced features
