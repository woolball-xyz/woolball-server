---
name: pipeline-debugger
description: "Debugs task pipeline issues: traces a task through Redis queues, WebSocket dispatch, and result delivery."
---

# Pipeline Debugger Agent

You are debugging a task that is failing, hanging, or producing incorrect results in the woolball pipeline.

## Diagnostic Steps

### 1. Identify where the task stops

The pipeline stages in order:
1. `TasksEndPoints` → HTTP entry
2. `PreProcessingQueue` → routing
3. `SplitAudioBySilenceQueue` / `SplitTextQueue` → optional splitting
4. `DistributeQueue` → WebSocket dispatch
5. Client node → AI inference
6. `TaskSockets` → result reception
7. `PostProcessingQueue` → result routing
8. Logic class → result publishing
9. `result_queue_{id}` → HTTP response

### 2. Check common failure points

**Task hangs (no response)**:
- `AwaitTaskResultAsync` has no timeout — if nothing publishes to `result_queue_{id}`, it hangs forever
- Check if `SessionTrackQueue` fired (2-min timeout) and exhausted 3 retries
- Check if `DistributeQueue` found an available node — `GetAvailableWebsocketAsync` returns null if no nodes
- Check if the Logic class error path publishes to result queue

**Split task never completes**:
- Check if ALL chunks were dispatched (look for `PrivateArgs["last"] = true`)
- Check if `SortedDictionary` in the Logic class received all orders 1..LastOrder
- Check if any chunk failed and the retry mechanism re-published it

**Retry loop**:
- `PostProcessingQueue` retries up to 3 times via `retry_count` in `PrivateArgs`
- `SessionTrackQueue` also retries up to 3 times via `_taskAttempts`
- If both retry, a task could be retried up to 6 times total
- Check `retry_count` parsing — it handles `int`, `JsonElement`, and `string` types

**Wrong result format**:
- Logic classes handle `JsonElement`, direct type, and JSON string differently
- Check the fallback chain in the Logic class's response extraction

### 3. Trace Redis pub/sub

Key channels to monitor:
```
SUBSCRIBE preprocessing_queue distribute_queue post_processing_queue sesion_tracking_queue result_queue_{taskId}
```

### 4. Check WebSocket state

- Is the node still connected? Check `_activeConnections` count
- Did the node return to the pool after processing? `TaskSockets` should call `AddWebsocketInQueueAsync` after receiving a result
- Is the node receiving ping messages? (every 30s)

### 5. Check Redis key state

```
GET task:{taskId}
```
If this key exists after the task should have completed, the cleanup didn't happen (keys have no TTL).
