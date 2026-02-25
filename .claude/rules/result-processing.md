---
description: "Post-processing and result delivery: Logic classes, retry mechanism, result queue publishing."
globs: "src/**/Logic/**,src/**/PostProcessing*"
---

# Result Processing and Delivery

## PostProcessingQueue — The Router

`PostProcessingQueue` subscribes to `post_processing_queue` and:
1. Deserializes the `TaskResponse` from the client node
2. Loads the original `TaskRequest` from Redis (`task:{taskResponse.Data.RequestId}`)
3. Routes to the correct Logic class:

| Task type | Logic class | Interface |
|---|---|---|
| `automatic-speech-recognition` | `SpeechToTextLogic` | `ISpeechToTextLogic` |
| `text-to-speech` | `TextToSpeechLogic` | `ITextToSpeechLogic` |
| `translation` | `TranslationLogic` | `ITranslationLogic` |
| `text-generation` | `TextGenerationLogic` | `ITextGenerationLogic` |

## Logic Class Responsibilities

Each Logic class receives `(TaskResponse, TaskRequest)` and must:
1. Extract the task-specific result from `taskResponse.Data.Response`
2. Handle split tasks (reassemble chunks if the task has a parent)
3. Publish the final result to `result_queue_{id}` where `id` is the parent ID (if split) or the task ID

### Simple Logic (Translation, TextGeneration)

These tasks are never split. The Logic class:
1. Extracts the response (handles `JsonElement`, direct type, or JSON fallback)
2. Publishes directly to `result_queue_{taskRequest.Id}`
3. On error: publishes an empty response to prevent the HTTP request from hanging

### Complex Logic (STT, TTS)

These tasks may be split. The Logic class uses a `ConcurrentDictionary` buffer:
1. Checks if the task has `PrivateArgs["parent"]` — if yes, this is a chunk
2. Stores the chunk in a `SortedDictionary<int, Result>` keyed by order
3. When `PrivateArgs["last"]` arrives, records the `LastOrder`
4. When all chunks 1..LastOrder are present, reassembles and publishes

**STT-specific**: Timestamp offsets are corrected using `PrivateArgs["start"]`
**TTS-specific**: Audio chunks are combined via `AudioCombiner.CombineWavBase64`

## Retry Mechanism

On inner exception in `PostProcessingQueue.ProcessTaskResponseAsync`:

```
retry_count = taskRequest.PrivateArgs["retry_count"] (default 0)
if retry_count < 3:
    retry_count++
    re-publish to "distribute_queue"
else:
    EmitTaskRequestErrorAsync → publishes error to result_queue
```

The `retry_count` parsing is defensive — it handles `int`, `JsonElement`, and `string` types because the value type changes after Redis serialization round-trips.

## Response JSON Extraction

The `TaskResponse.Data.Response` field is `object` and arrives as either:
- A `JsonElement` (most common, from Redis deserialization)
- A direct typed object (rare)
- A nested JSON string

Logic classes handle all three cases with try/catch fallback chains. When extraction fails completely, they publish an empty result to prevent HTTP hangs.

## Critical Rule: Never Leave result_queue Empty

If a Logic class fails to publish to `result_queue_{id}`, the corresponding HTTP request in `AwaitTaskResultAsync` will hang indefinitely (there is no timeout on the subscriber). Every error path MUST publish something to the result queue — even an empty or error response.

## Result Queue Message Formats

| Task type | Success payload | Completion signal |
|---|---|---|
| STT | `{ "text": "...", "chunks": [...] }` | (single message, no completion needed) |
| TTS | `[{ "AudioBase64": "...", "Format": "wav", "SampleRate": 16000 }]` | `{ "Status": "Completed" }` |
| Translation | `{ "translatedText": "..." }` | (single message) |
| TextGeneration | `{ "generatedText": "..." }` | (single message) |

TTS uses streaming — multiple messages followed by a completion signal. `StreamTaskResultAsync` loops until it sees `"Status":"Completed"` in the message.
