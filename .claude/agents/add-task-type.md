---
name: add-task-type
description: "Adds a new AI task type to the woolball server: endpoint, handler, preprocessing route, logic class, DI registration."
---

# Add Task Type Agent

You are adding a new AI task type to the woolball-server. Follow every step — skipping one breaks the pipeline.

## Checklist

### 1. Register the task type string
- Open `src/Core/Domain/Contracts/Constants/AIModels.cs`
- Add the canonical task type string to `AvailableModels._dict`
- Add any aliases to `_aliases` dictionary

### 2. Create a TaskHandler
- Open `src/Core/Domain/Contracts/Task/TaskRequest.cs`
- Create a new class implementing `ITaskHandler`
- Implement `ProcessInput(files, formFields)` — extract input from form data
- Implement `IsValidFields(kwargs)` — validate required fields
- Implement `FieldsConfig` — define expected form fields with types and defaults
- Register it in `TaskHandlerFactory.GetHandler()` switch statement

### 3. Add preprocessing route
- Open `src/Core/Presentation/Queues/PreProcessingQueue.cs`
- Add a case in the task type switch:
  - If the task needs splitting: route to appropriate split queue
  - If not: route directly to `distribute_queue`

### 4. Create a Logic class
- Create `src/Core/Application/Logic/{TaskName}Logic.cs`
- Create the interface `I{TaskName}Logic` in `src/Core/Domain/Contracts/`
- Implement `ProcessTaskResponseAsync(TaskResponse, TaskRequest)`
- Extract the result from `taskResponse.Data.Response`
- Publish to `result_queue_{taskRequest.Id}`
- Handle errors by publishing empty result (never leave the queue empty)

### 5. Register in DI
- Open `src/Background/BackgroundJobExtensions.cs`
- Register the logic class: `services.AddScoped<I{TaskName}Logic, {TaskName}Logic>()`

### 6. Add post-processing route
- Open `src/Background/PostProcessingQueue.cs`
- Add a case to route to the new Logic class in `ProcessTaskResponseAsync`

### 7. Add HTTP endpoint
- Open `src/Core/Presentation/EndPoints/TasksEndPoints.cs`
- Add a new POST endpoint following the pattern of existing endpoints
- Route through `HandleTaskInternalFromForm`

### 8. Create response model
- Create the response DTO in `src/Core/Domain/Contracts/Task/{TaskName}/`
- Follow existing patterns (e.g., `TranslationResponse`, `TextGenerationResponse`)

### 9. Verify the client supports it
- The browser node (woolball-client) must have a matching task processor for the same task type string
- Check `TASK_CONFIGS` in the client's `TaskAvailability.ts`

## Testing

After implementation:
1. Start all three services (core-api, core-websocket, core-background)
2. Start at least one browser node
3. Send a request to the new endpoint
4. Verify the result flows through the complete pipeline
