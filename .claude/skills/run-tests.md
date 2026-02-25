---
name: run-tests
description: "Build the woolball-server solution and start services for testing."
user_invocable: true
---

# /run-tests

## Build

```bash
# Build entire solution
dotnet build monorepo.sln

# Build in Release mode with analyzers (same as CI)
dotnet build monorepo.sln --configuration Release /p:RunAnalyzersDuringBuild=true
```

## Start Services (Docker)

The easiest way to run everything:
```bash
docker compose up
```

This starts all three services + Redis.

## Start Services (Manual)

Requires Redis running on `localhost:6379`:

```bash
# Terminal 1: HTTP REST API (:9002)
dotnet run --project src/WebApi

# Terminal 2: WebSocket server (:9003)
dotnet run --project src/WebSocket

# Terminal 3: Background queue workers
dotnet run --project src/Background
```

## Manual Testing

With all three services running and at least one browser node connected:

```bash
# Speech-to-text (file upload)
curl -X POST http://localhost:9002/speech-recognition -F "file=@audio.wav" -F "model=openai/whisper-tiny"

# Translation
curl -X POST http://localhost:9002/translation -F "input=Hello world" -F "model=Xenova/nllb-200-distilled-600M" -F "srcLang=eng_Latn" -F "tgtLang=por_Latn"

# Text generation
curl -X POST http://localhost:9002/text-generation -F 'input=[{"role":"user","content":"Hello"}]' -F "model=HuggingFaceTB/SmolLM2-135M-Instruct"
```
