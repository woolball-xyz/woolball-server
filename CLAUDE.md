# CLAUDE.md

Woolball Server is a distributed AI inference network backend written in .NET 10. It orchestrates AI tasks (speech recognition, text-to-speech, translation, text generation) by routing them through a Redis Pub/Sub pipeline to browser-based worker nodes connected via WebSocket.

## Commands

```bash
# Build all projects (from woolball-server root)
dotnet build

# Build specific project
dotnet build src/WebApi/WebApi.csproj
dotnet build src/Background/Background.csproj
dotnet build src/WebSocket/WebSocket.csproj

# Run in Release mode
dotnet build --configuration Release

# Run with analyzers (used in CI)
dotnet build --configuration Release /p:RunAnalyzersDuringBuild=true

# Run tests with coverage
dotnet test --no-build /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Start all services locally with Docker
docker compose up

# Build Docker images only (no push)
docker compose -f docker-compose.yml build core-api
docker compose -f docker-compose.yml build core-websocket
docker compose -f docker-compose.yml build core-background

# Redis local connection (dev password in appsettings)
# RedisConnection=localhost:6379,password=StrongRedisPassword123!
```

## Before Every Change

- Identify which project(s) are affected: WebApi (HTTP), WebSocket (WS nodes), Background (queues), or Core (shared logic)
- Run `dotnet build` to confirm no compile errors before and after your change
- Check if the change touches Redis channel names — there is an existing typo `"sesion_tracking_queue"` (missing 's') that is intentional/load-bearing; do not fix it without fixing both sides simultaneously
- Never add `IFormCollection`, `HttpContext`, or any `Microsoft.AspNetCore.Http` types to `Domain.csproj` — the domain already violates Clean Architecture by depending on ASP.NET; do not expand this violation
- `StrongRedisPassword123!` in `appsettings.json` and `docker-compose.yml` is a dev placeholder — never replace it with real credentials

## Decision Tree: Where to Look

| Working on... | Read first |
|---|---|
| HTTP endpoint routing or request handling | `src/Core/Presentation/EndPoints/TasksEndPoints.cs` |
| Task type validation, file upload, URL download | `src/Core/Domain/Contracts/Task/TaskRequest.cs` |
| Redis pub/sub publish logic | `src/Core/Application/Logic/TaskBusinessLogic.cs` |
| WebSocket node management (connect/disconnect) | `src/Core/Presentation/WebSockets/WebSocketNodesQueue.cs` |
| WebSocket node endpoint (accept connections) | `src/Core/Presentation/WebSockets/TaskSockets.cs` |
| Background queue workers | `src/Core/Presentation/Queues/` |
| AI model names and aliases | `src/Core/Domain/AIModels.cs` |
| Redis connection setup | `src/Core/Infrastructure/DependencyInjection.cs` |
| DI registration for logic layer | `src/Core/Application/DependencyInjection.cs` |
| DI registration for queues and WebSocket pool | `src/Core/Presentation/DependencyInjection.cs` |
| HTTP service startup and middleware | `src/WebApi/Program.cs` |
| WebSocket service startup | `src/WebSocket/Program.cs` |
| Background worker service startup | `src/Background/Program.cs` |
| Docker service definitions and ports | `docker-compose.yml` |
| CI/CD pipeline | `.github/workflows/CI-CD.yml` |
| STT streaming and chunk buffering | `src/Core/Application/Logic/SpeechToTextLogic.cs` |
| TTS streaming and chunk buffering | `src/Core/Application/Logic/TextToSpeechLogic.cs` |

## Architecture (3-5 sentences)

The server is split into three deployable services that share a Redis backbone: **core-api** (HTTP REST on port 9002) accepts task requests and awaits results via Redis Pub/Sub; **core-websocket** (WebSocket on port 9003) manages browser worker node connections using `WebSocketNodesQueue`; **core-background** runs `BackgroundService` queue workers that pre-process, split, and distribute tasks. All three services share a `Core` library containing `Domain`, `Application`, `Infrastructure`, and `Presentation` layers. The task lifecycle flows: HTTP request → `preprocessing_queue` → optional `split_audio_by_silence_queue` or `split_text_queue` → `distribute_queue` → WebSocket node → `result_queue_{taskId}` → HTTP response. `IConnectionMultiplexer` from StackExchange.Redis is registered as a singleton and used across all projects. All temp files land in `./shared/temp/` which is a Docker volume shared between `core-api` and `core-background`.

## Key Directories

| Path | Purpose |
|---|---|
| `src/WebApi/` | HTTP REST API service — startup, filters, `appsettings.json` |
| `src/WebSocket/` | WebSocket service — startup, `WebSocketExtensions.cs` |
| `src/Background/` | Background worker host — startup only, all logic in Core |
| `src/Core/Domain/` | Entities, contracts, task handlers, AI model constants |
| `src/Core/Domain/Contracts/Task/` | Per-task-type contracts (`SpeechToText/`, `TextGeneration/`, etc.) |
| `src/Core/Domain/Contracts/Task/TaskRequest.cs` | God file: `ITaskHandler`, `TaskHandlerFactory`, 4 concrete handlers, `TaskRequest` — 484 lines |
| `src/Core/Application/Logic/` | Business logic interfaces and implementations |
| `src/Core/Infrastructure/` | Redis `IConnectionMultiplexer` wiring |
| `src/Core/Presentation/EndPoints/` | Minimal API endpoint registration |
| `src/Core/Presentation/Queues/` | Redis Pub/Sub background service queue consumers |
| `src/Core/Presentation/WebSockets/` | `WebSocketNodesQueue` (in-memory node pool) + `TaskSockets` (WS endpoint) |
| `src/Core/Presentation/Workers/` | Worker scaffolding (`TemplateWorker.cs` — empty placeholder) |
| `.github/workflows/CI-CD.yml` | CI: format check + SonarCloud scan + Docker build/push to ghcr.io |
| `.do/deploy.template.yaml` | DigitalOcean App Platform deployment template |

## Critical Conventions

**Redis channel names (exact strings — must match on both publish and subscribe):**
- `preprocessing_queue`
- `split_audio_by_silence_queue`
- `split_text_queue`
- `distribute_queue`
- `sesion_tracking_queue` ← intentional typo, both sides use it
- `result_queue_{taskId}` ← per-task dynamic channel

**Project references flow (Clean Architecture direction):**
```
WebApi / WebSocket / Background
  → Core/Presentation → Core/Application → Core/Domain
                     → Core/Infrastructure → Core/Domain
```
Never add a reverse reference. Domain must not import Application or Infrastructure.

**Known violations to be aware of (do not make worse):**
- `Domain.csproj` references `Microsoft.AspNetCore.Http` and `Microsoft.EntityFrameworkCore.SqlServer` — both unused correctly but present
- `TaskRequest.cs` has no namespace — all classes in that file are in the global namespace
- `WebSocketExtensions.cs` is in namespace `Infrastructure` but lives in the `WebSocket` project

**Dependency Injection lifetimes:**
- `IConnectionMultiplexer` → Singleton (registered in `Infrastructure.DependencyInjection`)
- All `*Logic` classes → Scoped (registered in `Application.DependencyInjection`)
- `WebSocketNodesQueue` → Singleton (in-memory queue, must survive across requests)

**Packages present but NOT used (do not try to wire them):**
- `MediatR` — imported in Application, never configured
- `FluentValidation` — imported in Application, never used
- `NRedisStack` — imported in Infrastructure, no usage
- `Microsoft.EntityFrameworkCore.SqlServer` — in Domain, no entities
- `Microsoft.AspNetCore.Authentication.JwtBearer` — in WebApi, JWT never configured
- `Microsoft.EntityFrameworkCore.Design` — in WebApi, no migrations
- `Serilog.AspNetCore` — in WebApi, all logging is `Console.WriteLine`

**Task type strings (from `AvailableModels` constants in Domain):**
- `"speech-to-text"` (maps to `speech-recognition` endpoint)
- `"text-to-speech"`
- `"translation"`
- `"text-generation"`

**Temp file path:** `./shared/temp/` relative to working directory — shared Docker volume between api and background services. No cleanup mechanism exists — files accumulate permanently.

**Error detection anti-pattern (do not copy):** `TasksEndPoints.cs` detects errors by calling `response.Contains("\"Status\":\"Error\"")` on the raw response body string. This is fragile — do not replicate this pattern in new code.

**Streaming responses:** `StreamTaskResultAsync` in `TaskBusinessLogic.cs` loops over Redis channel messages and breaks on `"Status":"Completed"`. The `Content-Type` for streaming is set to `text/plain` (should be `application/x-ndjson` but is not — do not change without also updating clients).

**Docker image registry:** `ghcr.io/woolball-xyz/` — three images: `server-webapi`, `server-websocket`, `server-background`. Built from context `./src/` with dockerfiles inside each service subfolder.

**Port assignments:**
- `9000` → woolball-client (nginx)
- `9002` → core-api (HTTP REST)
- `9003` → core-websocket (WebSocket)
- Redis internal only, not exposed outside Docker network

**Swagger:** Enabled unconditionally in all environments (the `IsDevelopment()` guard is commented out). Accessible at `/swagger`.

## Rules Index (.claude/rules/)

| File | Loads when | Topic |
|---|---|---|
| `task-pipeline.md` | Always | End-to-end task flow: HTTP → Redis → WebSocket → result |
| `queue-architecture.md` | Editing queues or logic | All Redis channels, BackgroundService pattern, message formats |
| `task-splitting.md` | Editing split queues or logic | Audio/text splitting, FFmpeg, parent/order/last pattern |
| `websocket-dispatch.md` | Editing WebSocket files | Node pool, dispatch cycle, session tracking |
| `task-types.md` | Editing TaskRequest or endpoints | Type system, aliases, handlers, field configs |
| `result-processing.md` | Editing logic classes | Post-processing, retry mechanism, result queue publishing |

## Agents (.claude/agents/)

| Agent | Purpose |
|---|---|
| `add-task-type` | Adds a new AI task type across the entire server pipeline |
| `pipeline-debugger` | Debugs task pipeline issues: traces tasks through queues |

## Skills (.claude/skills/)

| Skill | Purpose |
|---|---|
| `/new-task-type` | Scaffold all files for a new task type |
| `/new-split-strategy` | Add a new input splitting strategy |
| `/run-tests` | Build solution and run services |
