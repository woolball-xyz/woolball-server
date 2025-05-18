# woolball-server 🧶  
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=woolball-xyz_browser-network-server&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=woolball-xyz_browser-network-server)
[![Discord](https://img.shields.io/badge/Discord-%235865F2.svg?style=flat&logo=discord&logoColor=white)](https://discord.gg/xbSmMfmwWW)


WoolBall turns **idle browsers into inference nodes**.  

This repository contains the open-source **network server** that dispatches jobs to those nodes; 
> the client lives in [`woolball-client`](https://github.com/woolball-xyz/woolball-client).

[Next steps](https://github.com/woolball-xyz/woolball-server/issues)
---

## Features
- **WebSocket coordinator** that assigns inference tasks to browsers.
- **REST API** exposing jobs and results.
- Runs AI pipelines **directly in the user's browser (WebAssembly / WebGPU)** — no server-side GPU needed.

---

## Supported Tasks

| Provider | Task | Models | Status |
|----------|------|--------|--------|
| **[Transformers.js](https://github.com/huggingface/transformers.js)** | Speech-to-Text | [ONNX Models](https://huggingface.co/models?pipeline_tag=automatic-speech-recognition&library=transformers.js&sort=trending) | ✅ Implemented |
| **[Transformers.js](https://github.com/huggingface/transformers.js)** | Text-to-Speech | [ONNX Models](https://huggingface.co/models?pipeline_tag=text-to-speech&library=transformers.js&sort=trending&search=mms) | ✅ Implemented |
| **[Kokoro.js](https://github.com/hexgrad/kokoro)** | Text-to-Speech | [ONNX Models](https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX) | ✅ Implemented |
| **[Transformers.js](https://github.com/huggingface/transformers.js)** | Translation | [ONNX Models](https://huggingface.co/models?pipeline_tag=translation&library=transformers.js&sort=trending) | ✅ Implemented |
| **[Transformers.js](https://github.com/huggingface/transformers.js)** | Image-Text-to-Text | [ONNX Models](https://huggingface.co/models?pipeline_tag=image-text-to-text&library=transformers.js&sort=trending) | 🚧 Pending |
| **[Transformers.js](https://github.com/huggingface/transformers.js)** | Text-Generation | [ONNX Models](https://huggingface.co/models?pipeline_tag=text-generation&library=transformers.js&sort=trending) | ✅ Implemented |
| **[WebLLM](https://github.com/mlc-ai/web-llm)** | Text Generation | [MLC Models](https://mlc.ai/models) | 🚧 Pending |
| **[MediaPipe](https://ai.google.dev/edge/mediapipe/solutions/guide)** | Text Generation | [LiteRT Models](https://ai.google.dev/edge/mediapipe/solutions/genai/llm_inference#models) | 🚧 Pending |

## Quick Start

```bash
git clone --branch deploy --single-branch --depth 1 https://github.com/woolball-xyz/woolball-server.git
```
```bash
cd woolball-server && docker compose up -d
```

> To ensure it's working correctly, have at least one client-node open [http://localhost:9000](http://localhost:9000)

### API Usage Examples

You can interact with the API using curl. Here are examples for different input types:

1. **Using a local audio/video file**:
```bash
curl -X POST http://localhost:9002/api/v1/speech-recognition \
  -F "input=@/path/to/your/file.mp3" \
  -F "model=onnx-community/whisper-large-v3-turbo_timestamped" \
  -F "dtype=q4" \
  -F "language=en" \
  -F "return_timestamps=true" \
  -F "stream=false"
```

2. **Using a base64 encoded audio**:
```bash
curl -X POST http://localhost:9002/api/v1/speech-recognition \
  -F "input=data:audio/mp3;base64,YOUR_BASE64_ENCODED_AUDIO" \
  -F "model=onnx-community/whisper-large-v3-turbo_timestamped" \
  -F "dtype=q4" \
  -F "language=en" \
  -F "return_timestamps=true" \
  -F "stream=false"
```

3. **Using a public URL**:
```bash
curl -X POST http://localhost:9002/api/v1/speech-recognition \
  -F "input=https://example.com/audio.mp3" \
  -F "model=onnx-community/whisper-large-v3-turbo_timestamped" \
  -F "dtype=q4" \
  -F "language=en" \
  -F "return_timestamps=true" \
  -F "stream=false"
```

### Available fields for Speech to Text

| Field                 | Type                          | Description |
|-----------------------|-------------------------------|-------------|
| return_timestamps     | boolean \| 'word'             | Whether to return timestamps or not. Default is `false`. |
| chunk_length_s        | number                        | The length of audio chunks to process in seconds. Default is `0` (no chunking). |
| stride_length_s       | number                        | The length of overlap between consecutive audio chunks in seconds. If not provided, defaults to `chunk_length_s / 6`. |
| force_full_sequences  | boolean                       | Whether to force outputting full sequences or not. Default is `false`. |
| language              | string                        | The source language. Default is `null`, meaning it should be auto-detected. Use this to potentially improve performance if the source language is known. |
| task                  | null \| 'transcribe' \| 'translate' | The task to perform. Default is `null`, meaning it should be auto-detected. |
| num_frames            | number                        | The number of frames in the input audio. |


### Local Development

For local development, you must use Docker Compose as the services depend on a shared volume for proper operation:

```bash
git clone https://github.com/woolball-xyz/woolball-server.git
```
```bash
cd woolball-server && docker compose up --build -d
```


| Service | Port | Localhost Link |
|---------|------|----------------|
| WebSocket Service | 9003 | [http://localhost:9003](http://localhost:9003) |
| API Service | 9002 | [http://localhost:9002](http://localhost:9002) |

### Flow

![Current Network Status](current.png)
