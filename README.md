# Web API

[![Build](https://github.com/musicvano/WebApi/actions/workflows/build.yml/badge.svg)](https://github.com/musicvano/WebApi/actions/workflows/build.yml)
[![Deploy](https://github.com/musicvano/WebApi/actions/workflows/deploy.yml/badge.svg)](https://github.com/musicvano/WebApi/actions/workflows/deploy.yml)

This repository contains source code for Web API https://api.rux-lang.dev.

Main purpose of this API is to provide access to:

- Rux package registry
- Rux compiler CI/CD status

## API

Base URL: `https://api.rux-lang.dev`

All requests and responses use JSON. Timestamps are UTC (ISO 8601).

### Packages

The package registry. Packages are sourced from public GitHub repositories;
their metadata (name, description, license) is read from the repository.

| Method | Route              | Description                                     |
| ------ | ------------------ | ----------------------------------------------- |
| `GET`  | `/packages`        | List all packages, ordered by name              |
| `GET`  | `/packages/{name}` | Get a single package by its name                |
| `POST` | `/packages`        | Register a new package from a GitHub repository |
| `PUT`  | `/packages/{id}`   | Refresh an existing package from its repository |

`POST` and `PUT` require human verification via a [Cloudflare Turnstile](https://www.cloudflare.com/products/turnstile/) token and take the following body:

```json
{
  "repository": "https://github.com/owner/repo",
  "turnstileToken": "<turnstile-response-token>"
}
```

A package is returned as:

```json
{
  "id": "0192f0c4-...",
  "name": "example",
  "description": "An example Rux package",
  "repository": "https://github.com/owner/repo",
  "license": "MIT",
  "created": "2026-06-16T12:00:00Z"
}
```

`GET /packages` returns a JSON array of packages.

### Playground

| Method | Route             | Description                                         |
| ------ | ----------------- | --------------------------------------------------- |
| `POST` | `/playground/run` | Compile and run a Rux snippet, returning its output |

Request body (`code` is limited to 4096 characters):

```json
{
  "code": "print(\"Hello, Rux\")"
}
```

Response:

```json
{
  "stdout": "Hello, Rux\n",
  "stderr": "",
  "exitCode": 0,
  "timedOut": false,
  "durationMs": 37
}
```

### Workflows

| Method | Route        | Description                                                   |
| ------ | ------------ | ------------------------------------------------------------- |
| `GET`  | `/workflows` | CI/CD status (build, test, deploy) for each compiler workflow |

Each item reports the latest conclusion and completion time per stage:

```json
[
  {
    "name": "Compiler",
    "buildConclusion": "success",
    "buildCompleted": "2026-06-16T11:30:00Z",
    "testConclusion": "success",
    "testCompleted": "2026-06-16T11:32:00Z",
    "deployConclusion": "success",
    "deployCompleted": "2026-06-16T11:35:00Z"
  }
]
```

### Status

| Method | Route     | Description                                                                                            |
| ------ | --------- | ------------------------------------------------------------------------------------------------------ |
| `GET`  | `/status` | Service health, version, uptime and database connectivity. Returns `200` when healthy, `503` otherwise |

```json
{
  "name": "Rux WebAPI",
  "status": "Healthy",
  "time": "2026-06-16T12:00:00Z",
  "uptime": "1.02:03:04",
  "version": "1.0.0",
  "environment": "Production",
  "database": { "connected": true, "latencyMs": 3, "error": null }
}
```

### Webhooks

| Method | Route              | Description                                                                                                                                                                 |
| ------ | ------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `POST` | `/webhooks/github` | Receives completed GitHub `workflow_job` events from the `main` branch to update workflow status. Requires a valid `X-Hub-Signature-256` and is not intended for public use |

## License

[MIT](LICENSE)
