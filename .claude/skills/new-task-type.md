---
name: new-task-type
description: "Scaffold all server-side files for a new AI task type: endpoint, handler, logic, queue routing."
user_invocable: true
---

# /new-task-type

Creates all the server-side scaffolding for a new AI task type.

## Usage

```
/new-task-type <task-type-name>
```

Example: `/new-task-type image-generation`

## What This Creates

1. **AIModels.cs** — Adds task type string to `_dict` and any aliases to `_aliases`
2. **TaskRequest.cs** — New `ITaskHandler` implementation with `ProcessInput`, `IsValidFields`, `FieldsConfig`
3. **Logic class** — `{TaskName}Logic.cs` in `src/Core/Application/Logic/`
4. **Interface** — `I{TaskName}Logic` in `src/Core/Domain/Contracts/`
5. **Response model** — DTO in `src/Core/Domain/Contracts/Task/{TaskName}/`
6. **Endpoint** — POST route in `TasksEndPoints.cs`
7. **Queue routing** — Case in `PreProcessingQueue` and `PostProcessingQueue`
8. **DI registration** — Scoped service in `BackgroundJobExtensions.cs`

## After Running

- Start the three services and test the endpoint
- Add the corresponding task processor on the client side (woolball-client)
- The task type string must match exactly between server and client
