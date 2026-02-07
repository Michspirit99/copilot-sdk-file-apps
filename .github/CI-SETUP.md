# CI/CD Setup Guide

This repository includes a GitHub Actions workflow that validates all samples compile and work correctly.

## Workflow: Validate Samples

**Location:** `.github/workflows/validate-samples.yml`

**Triggers:**
- Manual trigger (workflow_dispatch)
- Push to master branch (when samples change)
- Pull requests (when samples change)

## What It Does

### 1. Compile All Samples
- Verifies every `.cs` file in `samples/` compiles successfully
- Uses .NET 10 preview SDK
- Ensures all package references are valid

### 2. Test Basic Sample
- Runs `hello-copilot.cs` with GitHub Copilot authentication
- Uses the repository's `GITHUB_TOKEN` for Copilot access
- Demonstrates samples work end-to-end

### 3. Validate Documentation
- Ensures all sample files are documented in README
- Checks for broken sample references
- Maintains documentation completeness

## Running CI Manually

1. Navigate to the **Actions** tab in GitHub
2. Select "Validate Samples" workflow
3. Click "Run workflow"
4. Select branch (usually `master`)
5. Click "Run workflow" button

## CI Badge

The README includes a status badge showing the latest validation result:

```markdown
[![Validate Samples](https://github.com/Michspirit99/copilot-sdk-file-apps/actions/workflows/validate-samples.yml/badge.svg)](https://github.com/Michspirit99/copilot-sdk-file-apps/actions/workflows/validate-samples.yml)
```

## Notes

- The workflow uses **free** GitHub-hosted runners
- Copilot authentication uses the default `GITHUB_TOKEN` (no secrets needed)
- Compilation tests run on every push to catch issues early
- Full execution tests can be run manually to demonstrate functionality
- No ongoing costs - runs only when triggered or code changes

## Troubleshooting

**If compilation fails:**
- Check that .NET 10 preview is available in GitHub Actions
- Verify package version specifiers are correct (`@*-*`)
- Review error logs in the Actions tab

**If Copilot authentication fails:**
- Ensure repository has GitHub Copilot access
- Check that the account has an active Copilot subscription
- The free tier includes API access for testing

## Local Testing

To test what CI does locally:

```bash
# Compile all samples
for file in samples/*.cs; do
  echo "Compiling: $file"
  dotnet build "$file"
done

# Run a sample
dotnet run samples/hello-copilot.cs
```
