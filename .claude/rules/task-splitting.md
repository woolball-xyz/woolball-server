---
description: "Audio and text splitting mechanics: FFmpeg audio splitting, sentence-boundary text splitting, parent/child/order/last pattern."
globs: "src/**/SplitAudio*,src/**/SplitText*,src/**/Logic/SpeechToText*,src/**/Logic/TextToSpeech*"
---

# Task Splitting and Reassembly

## When Splitting Happens

| Task type | Split condition | Split strategy |
|---|---|---|
| `automatic-speech-recognition` | Audio duration > 25 seconds | FFmpeg silence detection |
| `text-to-speech` | Text length > 100 characters | Sentence boundary splitting |
| `translation` | Never split | — |
| `text-generation` | Never split | — |

## Audio Splitting (`SplitAudioBySilenceQueue`)

1. Receives `TaskRequest` from `split_audio_by_silence_queue`
2. Reads file path from `Kwargs["input"]`
3. Converts to WAV if not already WAV format
4. Gets duration via `FFmpegManager.GetDurationAsync`
5. If duration <= 25s: publishes single task to `distribute_queue` unchanged
6. If duration > 25s: iterates `FFmpegManager.BreakAudioFile(wavFilePath)` which yields `AudioSegment` objects

Each sub-task is created with:
```
Id = Guid.NewGuid()
Kwargs["input"] = segment.FilePath       // path to chunk file
PrivateArgs["start"] = segment.StartTime // offset for timestamp correction
PrivateArgs["order"] = segment.Order     // 1-based ordering
PrivateArgs["parent"] = originalTaskId   // route results to parent queue
PrivateArgs["last"] = segment.IsLast     // true on final chunk
```

## Text Splitting (`SplitTextQueue`)

1. Receives `TaskRequest` from `split_text_queue`
2. Extracts text from `Kwargs["input"]`
3. If text <= 100 chars: publishes directly to `distribute_queue`
4. If text > 100 chars: splits via `BreakTextIntoChunks(text)`

The splitting algorithm:
- Iterates character by character
- Breaks at sentence-ending characters: `.` `!` `?` `\n`
- Each chunk is at most `MaxChunkSize = 100` characters
- Yields `TextSegment { Text, Order, IsLast }`

Sub-tasks follow the same `parent/order/last` pattern as audio.

## Reassembly in Logic Classes

### SpeechToTextLogic (STT reassembly)

For split audio, results arrive out of order. The logic class:
1. Uses `ConcurrentDictionary<string, StreamBuffer>` keyed by parent ID
2. Each `StreamBuffer` has a `SortedDictionary<int, SpeechToTextResult>` keyed by order
3. When a chunk arrives, it's stored in the sorted dict
4. Timestamps in each chunk are offset-corrected: `timestamp += PrivateArgs["start"]`
5. When `IsLast` chunk arrives, `LastOrder` is set
6. Once all orders 1..LastOrder are present, results are merged in order and published to `result_queue_{parent}`

### TextToSpeechLogic (TTS reassembly)

Same buffering pattern but for audio output:
1. `ConcurrentDictionary<string, TTSStreamBuffer>` with `SortedDictionary<int, List<TTSResponse>>`
2. When all chunks arrive, audio segments are combined via `AudioCombiner.CombineWavBase64`
3. Combined audio is published to `result_queue_{parent}`

## PrivateArgs Convention

`PrivateArgs` is a `Dictionary<string, object>` on `TaskRequest` used for internal pipeline state. It is never sent to the client node — only `Kwargs` is sent over WebSocket.

| Key | Type | Purpose |
|---|---|---|
| `parent` | `string` (Guid) | Original task ID for routing reassembled results |
| `order` | `int` | 1-based ordering of chunks |
| `last` | `bool` | `true` on the final chunk |
| `start` | `double` | Audio offset in seconds (STT only) |
| `retry_count` | `int` | Number of retry attempts |
| `node_id` | `string` | WebSocket node that processed the task |
