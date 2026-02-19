---
description: "WebSocket node pool: how browser nodes connect, receive tasks, and return results."
globs: "src/**/WebSockets/**,src/**/Queues/Distribute*"
---

# WebSocket Node Pool and Task Dispatch

## Node Lifecycle

Browser nodes (woolball-client instances) connect via `ws/{clientId}`:

1. **Connect**: `TaskSockets.ReceiveAsync` accepts the WebSocket upgrade
2. **Register**: Node is added to the pool via `WebSocketNodesQueue.AddWebsocketInQueueAsync` and tracked via `AddConnectionAsync`
3. **Ping**: A background task sends `"ping"` every 30 seconds to keep the connection alive
4. **Dispatch**: `DistributeQueue` pulls a node from the pool, sends a task
5. **Process**: Node runs inference, sends back result
6. **Return**: `TaskSockets` receives the result, publishes to `post_processing_queue`, and adds the node back to the pool via `AddWebsocketInQueueAsync`
7. **Disconnect**: `RemoveConnectionAsync` removes from tracking, broadcasts updated count

## WebSocketNodesQueue — The Pool

The pool is an **unbounded `Channel<(string, WebSocket)>`**:

- `AddWebsocketInQueueAsync(nodeId, ws)` — writes to channel (adds to pool)
- `GetAvailableWebsocketAsync()` — reads from channel, skipping any closed sockets
- A node is in the pool ONLY when idle (not processing a task)
- After task completion, `TaskSockets` re-adds the node to the pool
- If no nodes are available, `GetAvailableWebsocketAsync` returns `(null, null)` and `DistributeQueue` throws

## Active Connection Tracking

Separate from the dispatch pool, `_activeConnections` (ConcurrentDictionary) tracks ALL connected nodes:

- `AddConnectionAsync` generates a `connectionId = "{nodeId}_{Guid}"` and increments `_connectionCount`
- `RemoveConnectionAsync` decrements `_connectionCount`
- On every connect/disconnect, `BroadcastNodeCountAsync` sends `"node_count:{n}"` to ALL active WebSocket connections

This lets the browser-ui display the total connected node count in real time.

## Task Message Format

`DistributeQueue` sends to the WebSocket node:

```json
{
  "Id": "guid-of-task",
  "Key": "automatic-speech-recognition",
  "Value": {
    "input": "base64-audio-or-text",
    "model": "openai/whisper-tiny",
    "dtype": "q8",
    "task": "automatic-speech-recognition"
  }
}
```

- `Id` — the TaskRequest ID (used to match results back)
- `Key` — the task type string (must match client's `TASK_CONFIGS`)
- `Value` — the `Kwargs` dictionary (all task parameters the client needs)

## Response Message Format

The client sends back through the same WebSocket:

```json
{
  "type": "PROCESS_RESULT",
  "data": {
    "requestId": "guid-of-task",
    "response": { /* task-specific result */ }
  }
}
```

Or on error:
```json
{
  "type": "ERROR",
  "data": {
    "requestId": "guid-of-task",
    "error": "error message"
  }
}
```

## Session Tracking (Timeout Watchdog)

After dispatching a task, `DistributeQueue` publishes the task to `sesion_tracking_queue`. `SessionTrackQueue` then:
1. Starts a 2-minute timer (`TASK_TIMEOUT_MS = 120000`)
2. On timeout: checks if the task still exists in Redis (`task:{taskId}`)
3. If yes and `attempts < 3`: re-publishes to `distribute_queue` (retry)
4. If yes and `attempts >= 3`: publishes failure to `result_queue_{taskId}`
5. On `task_completion` message: cancels the timer

## Singleton Registration

`WebSocketNodesQueue` is registered as a **Singleton** in `WebSocketExtensions.cs`. It MUST remain singleton because:
- The dispatch pool is in-memory (not Redis-backed)
- All three services (core-api, core-websocket, core-background) access it, but only the WebSocket service actually holds connections
- `DistributeQueue` is registered as a `HostedService` alongside it
