---
description: "Task type system: TaskRequest, TaskHandler, field configs, aliases, and how to add new types."
globs: "src/**/Task/TaskRequest.cs,src/**/Constants/AIModels.cs,src/**/EndPoints/Tasks*"
---

# Task Type System

## Supported Task Types

| Canonical name | Aliases accepted by API | Handler class |
|---|---|---|
| `automatic-speech-recognition` | `stt`, `speech-to-text`, `speech-recognition`, `speech-recognize` | `SpeechToTextTaskHandler` |
| `text-to-speech` | `tts` | `TextToSpeechTaskHandler` |
| `translation` | (none) | `TranslationTaskHandler` |
| `text-generation` | `completions` | `TextGenerationTaskHandler` |

Alias resolution happens in `AvailableModels.GetTaskName(task)` in `AIModels.cs` and is duplicated in `TaskRequest.GetOfficialTaskType(task)`.

## TaskRequest Structure

```csharp
public class TaskRequest
{
    public Guid Id { get; set; }              // unique task ID
    public string Task { get; set; }           // canonical task type
    public Dictionary<string, object> Kwargs { get; set; }       // sent to client node
    public Dictionary<string, object> PrivateArgs { get; set; }  // internal pipeline state
}
```

- `Kwargs` = everything the browser node needs (input, model, dtype, provider, etc.)
- `PrivateArgs` = internal state (parent, order, last, retry_count, node_id) — never sent over WebSocket

## TaskHandlerFactory

`TaskHandlerFactory.GetHandler(task)` returns the correct `ITaskHandler` based on task type. Each handler implements:

- `ProcessInput(IFormFileCollection files, Dictionary<string, string> formFields)` — extracts and validates input from HTTP form
- `IsValidFields(Dictionary<string, object> kwargs)` — validates required fields are present
- `FieldsConfig` — defines form field names, types, and defaults

## Input Processing per Task Type

### SpeechToTextTaskHandler
Accepts input in 3 ways (checked in order):
1. **File upload** — `files["file"]` → saved to temp file → `Kwargs["input"] = filePath`
2. **URL** — `formFields["url"]` → downloaded to temp file → `Kwargs["input"] = filePath`
3. **Base64** — `formFields["audio"]` → `Kwargs["input"] = base64String`

Also extracts: `model`, `dtype`, `return_timestamps`, `language`

### TextToSpeechTaskHandler
- `formFields["input"]` → text to synthesize
- Also: `model`, `dtype`, `provider`, `voice`

### TranslationTaskHandler
- `formFields["input"]` → text to translate
- Required: `srcLang`, `tgtLang`
- Also: `model`, `dtype`, `provider`

### TextGenerationTaskHandler
- `formFields["input"]` → JSON array of messages `[{role, content}]`
- Also: `model`, `dtype`, `max_new_tokens`, `do_sample`, `temperature`, `stream`, `provider`

## Adding a New Task Type

1. Add the canonical string to `AvailableModels._dict` and any aliases to `_aliases` in `AIModels.cs`
2. Create a new `ITaskHandler` implementation in `TaskRequest.cs` (or separate file)
3. Register it in `TaskHandlerFactory.GetHandler()`
4. Add the routing case in `PreProcessingQueue` (does it need splitting?)
5. Create a Logic class implementing the result-processing interface
6. Register the Logic class in DI (`BackgroundJobExtensions.cs`)
7. Add the HTTP endpoint in `TasksEndPoints.cs`
8. Add the post-processing case in `PostProcessingQueue`
