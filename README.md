# woolball-server 🧶  
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=woolball-xyz_browser-network-server&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=woolball-xyz_browser-network-server)

*Browser-as-a-Server orchestration layer for distributed AI inference*

WoolBall turns **idle browsers into inference nodes**.  

This repository contains the open-source **network server** that dispatches jobs to those nodes; 
> the client/worker SDK lives in [`woolball-client`](https://github.com/woolball-xyz/woolball-client).

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
| **[Transformers.js](https://github.com/huggingface/transformers.js)** | Text-to-Speech | [ONNX Models](https://huggingface.co/models?pipeline_tag=text-to-speech&library=transformers.js&sort=trending) | 🚧 Pending |
| **[Transformers.js](https://github.com/huggingface/transformers.js)** | Translation | [ONNX Models](https://huggingface.co/models?pipeline_tag=translation&library=transformers.js&sort=trending) | 🚧 Pending |
| **[Transformers.js](https://github.com/huggingface/transformers.js)** | Image-Text-to-Text | [ONNX Models](https://huggingface.co/models?pipeline_tag=image-text-to-text&library=transformers.js&sort=trending) | 🚧 Pending |
| **[Transformers.js](https://github.com/huggingface/transformers.js)** | Text-Generation | [ONNX Models](https://huggingface.co/models?pipeline_tag=text-generation&library=transformers.js&sort=trending) | 🚧 Pending |
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
