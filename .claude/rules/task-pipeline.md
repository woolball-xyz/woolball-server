---
description: "End-to-end task pipeline: HTTP request → Redis queues → WebSocket dispatch → result. Always loaded."
alwaysApply: true
---

# Task Pipeline — End-to-End Flow

## Overview

Every AI task in woolball follows this exact pipeline:

```
HTTP request → TasksEndPoints → PreProcessingQueue → [Split?] → DistributeQueue → WebSocket node → PostProcessingQueue → Logic class → result_queue_{id} → HTTP response
```

## Step 1: HTTP Entry Point

`TasksEndPoints.cs` exposes four POST endpoints, all routed through `HandleTaskInternalFromForm`:

| Endpoint | Task type string |
|---|---|
| `/speech-recognition` | `automatic-speech-recognition` |
| `/text-to-speech` | `text-to-speech` |
| `/translation` | `translation` |
| `/text-generation` | `text-generation` |

The handler:
1. Calls `TaskRequest.CreateFromForm(form)` → parses form fields using `TaskHandlerFactory`
2. Validates via `IsValidFields()` on the handler
3. Publishes to `preprocessing_queue` via `TaskBusinessLogic.PublishPreProcessingQueueAsync`
4. Blocks on `AwaitTaskResultAsync` (single result) or streams via `StreamTaskResultAsync` (SSE for TTS)

## Step 2: Preprocessing Routes by Task Type

`PreProcessingQueue` subscribes to `preprocessing_queue` and routes:

| Task type | Next queue |
|---|---|
| `automatic-speech-recognition` | `split_audio_by_silence_queue` |
| `text-to-speech` | `split_text_queue` (after input validation) |
| `translation` | `distribute_queue` (direct) |
| `text-generation` | `distribute_queue` (direct) |

## Step 3: Optional Splitting

Long inputs get split into sub-tasks:

- **Audio** (`SplitAudioBySilenceQueue`): If duration > 25s, FFmpeg splits at silence points. Each chunk becomes a sub-task with `PrivateArgs["parent"]`, `["order"]`, `["last"]`.
- **Text** (`SplitTextQueue`): If text > 100 chars, splits at sentence boundaries (`.!?\n`). Same parent/order/last pattern.

Short inputs pass through directly to `distribute_queue`.

## Step 4: WebSocket Dispatch

`DistributeQueue` subscribes to `distribute_queue`:
1. Gets an available node from `WebSocketNodesQueue.GetAvailableWebsocketAsync()`
2. Stores the full `TaskRequest` in Redis at `task:{taskId}` (used for retries/post-processing)
3. Calls `LoadInputIfNeeded()` to read file bytes into `Kwargs["input"]`
4. Sends `{ Id, Key=task.Task, Value=task.Kwargs }` over WebSocket to the browser node
5. Publishes to `sesion_tracking_queue` to start the 2-minute watchdog timer

## Step 5: Client Processes and Returns

The browser node (woolball-client) receives the WebSocket message, runs inference, and sends back either:
- `{ type: "PROCESS_RESULT", data: { requestId, response } }` — success
- `{ type: "ERROR", data: { requestId, error } }` — failure

`TaskSockets.cs` receives this response, deserializes it as `TaskResponseBody`, and publishes to `post_processing_queue`. Then it returns the node to the available pool.

## Step 6: Post-Processing

`PostProcessingQueue` subscribes to `post_processing_queue`:
1. Loads the original `TaskRequest` from Redis (`task:{taskResponse.Data.RequestId}`)
2. Routes to the correct Logic class based on task type
3. Logic class publishes final result to `result_queue_{id}`
4. On error: retry up to 3 times by re-publishing to `distribute_queue`

## Step 7: Result Delivery

`TaskBusinessLogic.AwaitTaskResultAsync` (or `StreamTaskResultAsync`) is subscribed to `result_queue_{taskId}`. It reads the result and returns it as the HTTP response.

## Key Invariants

- Every task MUST eventually publish to `result_queue_{id}`, even on error — otherwise the HTTP request hangs forever
- `AwaitTaskResultAsync` has NO timeout — the only timeout is the 2-minute watchdog in `SessionTrackQueue`
- Split tasks use `PrivateArgs["parent"]` to route sub-task results back to the parent's result queue
- The node is returned to the pool AFTER each task (one task per dispatch cycle)
