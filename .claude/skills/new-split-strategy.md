---
name: new-split-strategy
description: "Add a new input splitting strategy for a task type (e.g., split images, split documents)."
user_invocable: true
---

# /new-split-strategy

Adds a new splitting strategy for a task type that needs to break large inputs into smaller chunks.

## Usage

```
/new-split-strategy <strategy-name>
```

Example: `/new-split-strategy split-document-by-pages`

## What This Creates

1. **Queue worker** — `Split{Name}Queue.cs` as a `BackgroundService` subscribing to `split_{name}_queue`
2. **Segment model** — Data class for the segment (like `AudioSegment` or `TextSegment`)
3. **Splitting logic** — `async IAsyncEnumerable<Segment>` method that yields chunks
4. **PreProcessingQueue route** — Adds the routing case to direct the task type to the new split queue
5. **Logic class update** — Adds reassembly logic to the corresponding Logic class (buffer + SortedDictionary pattern)
6. **DI registration** — `AddHostedService<Split{Name}Queue>()` in `BackgroundJobExtensions.cs`

## Parent/Order/Last Pattern

Every split strategy must follow the same convention:
- `PrivateArgs["parent"]` = original task ID
- `PrivateArgs["order"]` = 1-based chunk index
- `PrivateArgs["last"]` = true on final chunk
- Each sub-task gets `Id = Guid.NewGuid()`

The Logic class reassembles by collecting chunks in a `SortedDictionary<int, Result>` until all orders 1..LastOrder are present.
