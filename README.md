# G4 Jira & Xray MCP Service

![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=.net&logoColor=white)  
[![Build, Test & Release G4™ Jira-XRay MCP](https://github.com/g4-api/g4-xray-mcp/actions/workflows/release-pipline.yml/badge.svg)](https://github.com/g4-api/g4-xray-mcp/actions/workflows/release-pipline.yml)

A cross-platform **ASP.NET Core MCP service** that integrates with **Jira** (Cloud & On-Prem) and **Xray** (Cloud & On-Prem via Raven API).  
It exposes an MCP-compatible **HTTP endpoint** for test-management automation.

---

## Table of Contents

- [Overview](#overview)
- [Supported Platforms](#supported-platforms)
- [Prerequisites](#prerequisites)
  - [.NET 10 (LTS)](#net-10-lts)
  - [Docker](#docker)
- [Install](#install)
  - [Option A: Run Native (Recommended)](#option-a-run-native-recommended)
  - [Option B: Run with Docker](#option-b-run-with-docker)
- [Configuration](#configuration)
  - [Environment Variables](#environment-variables)
  - [MCP Client Configuration](#mcp-client-configuration)
- [Available Tools](#available-tools)
- [Security Notes](#security-notes)
- [Troubleshooting](#troubleshooting)

---

## Overview

This service provides MCP tools for working with:
- **Jira Cloud / Jira On-Prem**
- **Xray Cloud**
- **Xray On-Prem (via Raven API)**

Default MCP endpoint:
- `http://localhost:9988/api/v4/mcp`

---

## Supported Platforms

Run natively on:
- Windows
- macOS
- Linux

Or run as a container using Docker.

---

## Prerequisites

### .NET 10 (LTS)

Install .NET using the official Microsoft docs:
- Windows: see Microsoft Learn “Install .NET on Windows”. :contentReference[oaicite:0]{index=0}
- macOS: see Microsoft Learn “Install .NET on macOS”. :contentReference[oaicite:1]{index=1}
- Linux: see Microsoft Learn “Install .NET on Linux”. :contentReference[oaicite:2]{index=2}

You can also use the official .NET download page:
- “Download .NET” landing page. :contentReference[oaicite:3]{index=3}
- “Download .NET 10.0” page. :contentReference[oaicite:4]{index=4}

### Docker

Official Docker installation guides:
- Windows (Docker Desktop): :contentReference[oaicite:5]{index=5}
- macOS (Docker Desktop): :contentReference[oaicite:6]{index=6}
- Linux (Docker Desktop): :contentReference[oaicite:7]{index=7}
- Docker Engine install overview (Linux-focused): :contentReference[oaicite:8]{index=8}

---

## Install

### Option A: Run Native (Recommended)

1. Download the **latest production release** from your GitHub Releases page (choose the ZIP for your OS/architecture):
   - Example:
     ```text
     https://github.com/<org>/<repo>/releases/latest
     ```

2. Unzip the release package to a folder (example):
   - Windows: `C:\apps\g4-jira-xray-mcp\`
   - Linux/macOS: `/opt/g4-jira-xray-mcp/`

3. Configure environment variables (see [Environment Variables](#environment-variables)).

4. Run the service from the unzipped folder:
   ```bash
   dotnet Mcp.Xray.dll
   ```

> Tip: If your release contains an `appsettings.json` (or similar), keep the README aligned with the release package behavior.
> This README assumes the primary entrypoint is **`dotnet Mcp.Xray.dll`**.

---

### Option B: Run with Docker

If you publish a Docker image, run it like this (example):

```bash
docker run -d --name g4-jira-xray-mcp \
  -p 9988:9988 \
  -e JIRA_API_KEY="..." \
  -e JIRA_BASE_URL="..." \
  -e JIRA_API_VERSION="..." \
  -e JIRA_IS_CLOUD="true" \
  -e XRAY_CLOUD_BASE_URL="https://xray.cloud.getxray.app" \
  <your-image>:<tag>
```

---

## Configuration

### Environment Variables

| Name                         | Description                                                                                                 |
| ---------------------------- | ----------------------------------------------------------------------------------------------------------- |
| `JIRA_API_KEY`               | Jira API key / token used for authentication.                                                               |
| `JIRA_API_VERSION`           | Jira REST API version to target (depends on your Jira deployment).                                          |
| `JIRA_BASE_URL`              | Base URL of the Jira instance (Cloud or On-Prem).                                                           |
| `JIRA_BUCKET_SIZE`           | Batch size for list/bulk operations.                                                                        |
| `JIRA_DELAY_MILLISECONDS`    | Delay (ms) between retry attempts.                                                                          |
| `JIRA_IS_CLOUD`              | Switch implementation: `true` = Cloud, `false` = On-Prem.                                                   |
| `JIRA_MAX_ATTEMPTS`          | Max retry attempts before failing.                                                                          |
| `JIRA_RESOLVE_CUSTOM_FIELDS` | If enabled, resolves Jira custom fields via Jira API; otherwise uses provided names/keys without resolving. |
| `JIRA_USERNAME`              | Jira username (commonly required for some On-Prem auth flows).                                              |
| `XRAY_CLOUD_BASE_URL`        | Xray Cloud base URL (default: `https://xray.cloud.getxray.app`).                                            |

#### Example (Linux/macOS)

```bash
export JIRA_BASE_URL="https://your-jira"
export JIRA_API_VERSION="3"
export JIRA_IS_CLOUD="true"
export JIRA_USERNAME="your-user@company.com"
export JIRA_API_KEY="***"
export XRAY_CLOUD_BASE_URL="https://xray.cloud.getxray.app"

dotnet Mcp.Xray.dll
```

#### Example (Windows PowerShell)

```powershell
$env:JIRA_BASE_URL = "https://your-jira"
$env:JIRA_API_VERSION = "3"
$env:JIRA_IS_CLOUD = "true"
$env:JIRA_USERNAME = "your-user@company.com"
$env:JIRA_API_KEY = "***"
$env:XRAY_CLOUD_BASE_URL = "https://xray.cloud.getxray.app"

dotnet .\Mcp.Xray.dll
```

---

### MCP Client Configuration

Add this to your `mcp.json`:

```json
{
    "servers": {
        "g4-jira-xray": {
            "type": "http",
            "url": "http://localhost:9988/api/v4/mcp"
        }
    },
    "inputs": []
}
```

---

## Available Tools

These are the currently exposed MCP tools:

| Tool Name                             | Purpose                                                       | When to Use                                                                           | Key Capabilities                                                                                                                                                                                                                                                                                  |
| ------------------------------------- | ------------------------------------------------------------- | ------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **`new_xray_test`**                   | Creates a new test case in Xray.                              | Use this when you want to programmatically author new manual or automated test cases. | Supports full test definition including scenario/title, detailed specifications, setup and teardown steps, ordered test steps with expected results, priority, severity, tolerance, categories (tags), and custom fields. Returns the created test key, internal ID, and a direct Jira/Xray link. |
| **`update_xray_test`**                | Updates an existing Xray test case.                           | Use this to modify an existing test when requirements, steps, or metadata change.     | Allows partial updates. You can update scenario/title, steps, setup/teardown, specifications, categories, priority, severity, tolerance, and custom fields. Returns the updated test key, internal ID, and a direct link.                                                                         |
| **`new_xray_test_repository_folder`** | Creates a new folder in the Xray Test Repository.             | Use this to build or extend your logical folder structure.                            | Supports hierarchical paths (e.g., `Regression/UI/Login`). If no path is provided, the folder is created at the repository root. Returns the folder ID and resolved full path.                                                                                                                    |
| **`resolve_xray_folder_path`**        | Resolves a logical folder path to an internal Xray folder ID. | Use this when another operation requires a folder ID instead of a path.               | Converts human-readable paths (e.g., `Root/Backend/Auth`) into internal identifiers used by Xray APIs. Returns the normalized path and its internal ID.                                                                                                                                           |
| **`add_xray_tests_to_folder`**        | Adds multiple tests into a Test Repository folder using JQL.  | Use this to bulk-organize tests into folders.                                         | Executes a JQL query, selects matching tests, and adds them to a target folder. Returns which tests were added, which were skipped, the target folder ID, and a direct link to the folder.                                                                                                        |

---

## Security Notes

* Do not commit secrets (`JIRA_API_KEY`) to source control.
* Prefer secret managers (GitHub Actions secrets, Azure Key Vault, etc.).
* Restrict network access to the MCP endpoint if used outside localhost.

---

## Troubleshooting

**`dotnet: command not found`**

* Install .NET 10 using the platform instructions above. ([Microsoft Learn][1])

**Port already in use / cannot connect**

* Ensure nothing else is listening on `9988`.
* Verify the MCP endpoint is reachable:

  * `http://localhost:9988/api/v4/mcp`

**Auth failures**

* Re-check `JIRA_IS_CLOUD` matches your deployment type.
* Validate `JIRA_BASE_URL`, `JIRA_USERNAME`, and `JIRA_API_KEY`.

[1]: https://learn.microsoft.com/en-us/dotnet/core/install/?utm_source=chatgpt.com "Install .NET on Windows, Linux, and macOS"
