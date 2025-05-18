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

## API Reference

The API is available at `http://localhost:9002` with the following endpoints:

| Endpoint | Description |
|----------|-------------|
| `/api/v1/speech-recognition` | Convert audio to text |
| `/api/v1/text-to-speech` | Convert text to audio |
| `/api/v1/translation` | Translate text between languages |
| `/api/v1/text-generation` | Generate text from prompts |

## Speech Recognition

Convert audio files to text using Whisper models.

### Example Usage

The API accepts different input formats:

```bash
# Using a local audio file
curl -X POST http://localhost:9002/api/v1/speech-recognition \
  -F "input=@/path/to/your/file.mp3" \
  -F "model=onnx-community/whisper-large-v3-turbo_timestamped" \
  -F "dtype=q4" \
  -F "language=en" \
  -F "return_timestamps=true" \
  -F "stream=false"

# Using a base64 encoded audio
curl -X POST http://localhost:9002/api/v1/speech-recognition \
  -F "input=data:audio/mp3;base64,YOUR_BASE64_ENCODED_AUDIO" \
  -F "model=onnx-community/whisper-large-v3-turbo_timestamped" \
  -F "dtype=q4" \
  -F "language=en" \
  -F "return_timestamps=true" \
  -F "stream=false"

# Using a public URL
curl -X POST http://localhost:9002/api/v1/speech-recognition \
  -F "input=https://example.com/audio.mp3" \
  -F "model=onnx-community/whisper-large-v3-turbo_timestamped" \
  -F "dtype=q4" \
  -F "language=en" \
  -F "return_timestamps=true" \
  -F "stream=false"
```

### Parameters

| Parameter            | Type                          | Description |
|----------------------|-------------------------------|-------------|
| model                | string                        | Model ID from Hugging Face (e.g., "onnx-community/whisper-large-v3-turbo_timestamped") |
| dtype                | string                        | Quantization level (e.g., "q4") |
| return_timestamps    | boolean \| 'word'             | Whether to return timestamps or not. Default is `false`. |
| stream               | boolean                       | Whether to stream results. Default is `false`. |
| chunk_length_s       | number                        | The length of audio chunks to process in seconds. Default is `0` (no chunking). |
| stride_length_s      | number                        | The length of overlap between consecutive audio chunks in seconds. If not provided, defaults to `chunk_length_s / 6`. |
| force_full_sequences | boolean                       | Whether to force outputting full sequences or not. Default is `false`. |
| language             | string                        | The source language. Default is `null`, meaning it should be auto-detected. Use this to potentially improve performance if the source language is known. |
| task                 | null \| 'transcribe' \| 'translate' | The task to perform. Default is `null`, meaning it should be auto-detected. |
| num_frames           | number                        | The number of frames in the input audio. |

## Text-to-Speech

Convert text to speech using different TTS engines.

### Transformers.js (MMS Models)

Uses Multilingual Massively Speech (MMS) models from Transformers.js for various languages.

```bash
# Standard request
curl -X POST http://localhost:9002/api/v1/text-to-speech \
  -F "input=Hello, this is a test for text to speech." \
  -F "model=Xenova/mms-tts-eng" \
  -F "dtype=q8" \
  -F "stream=false"

# Streaming request
curl -X POST http://localhost:9002/api/v1/text-to-speech \
  -F "input=Hello, this is a test for streaming text to speech." \
  -F "model=Xenova/mms-tts-eng" \
  -F "dtype=q8" \
  -F "stream=true"
```

### Kokoro

Provides diverse voices with high-quality output.

```bash
# Standard request
curl -X POST http://localhost:9002/api/v1/text-to-speech \
  -F "input=Hello, this is a test using Kokoro voices." \
  -F "model=onnx-community/Kokoro-82M-ONNX" \
  -F "dtype=q8" \
  -F "voice=af_nova" \
  -F "stream=false"

# Streaming request
curl -X POST http://localhost:9002/api/v1/text-to-speech \
  -F "input=Hello, this is a test using Kokoro voices with streaming." \
  -F "model=onnx-community/Kokoro-82M-ONNX" \
  -F "dtype=q8" \
  -F "voice=af_nova" \
  -F "stream=true"
```

### Parameters

| Parameter | Type   | Description |
|-----------|--------|-------------|
| model     | string | Model ID (e.g., "Xenova/mms-tts-eng" or "onnx-community/Kokoro-82M-ONNX") |
| dtype     | string | Quantization level (e.g., "q8") |
| voice     | string | Voice ID to use (required for Kokoro models) |
| stream    | boolean | Whether to stream the audio response. Default is `false`. |

### Kokoro Voice Options

Kokoro supports various voices with different accents and genders:

- American Female: `af_heart`, `af_alloy`, `af_aoede`, `af_bella`, `af_jessica`, `af_kore`, `af_nicole`, `af_nova`, `af_river`, `af_sarah`, `af_sky`
- American Male: `am_adam`, `am_echo`, `am_eric`, `am_fenrir`, `am_liam`, `am_michael`, `am_onyx`, `am_puck`, `am_santa`
- British Female: `bf_emma`, `bf_isabella`, `bf_alice`, `bf_lily`
- British Male: `bm_george`, `bm_lewis`, `bm_daniel`, `bm_fable`

## Translation

Translate text between languages using NLLB models.

```bash
curl -X POST http://localhost:9002/api/v1/translation \
  -F "input=Hello, how are you today?" \
  -F "model=Xenova/nllb-200-distilled-600M" \
  -F "dtype=q8" \
  -F "srcLang=eng_Latn" \
  -F "tgtLang=por_Latn"
```

### Parameters

| Parameter | Type   | Description |
|-----------|--------|-------------|
| model     | string | Model ID (e.g., "Xenova/nllb-200-distilled-600M") |
| dtype     | string | Quantization level (e.g., "q8") |
| srcLang   | string | Source language code in FLORES200 format (e.g., "eng_Latn") |
| tgtLang   | string | Target language code in FLORES200 format (e.g., "por_Latn") |

The language codes follow the FLORES200 format. See the [FLORES200 language list](https://github.com/facebookresearch/flores/blob/main/flores200/README.md#languages-in-flores-200) for all available language options.

## Text Generation

Generate text from prompts using language models.

```bash
curl -X POST http://localhost:9002/api/v1/text-generation \
  -F 'input=[{"role":"system","content":"You are a helpful assistant."},{"role":"user","content":"What is the capital of Brazil?"}]' \
  -F "model=HuggingFaceTB/SmolLM2-135M-Instruct" \
  -F "dtype=fp16" \
  -F "max_new_tokens=250" \
  -F "do_sample=true"
```

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| model | string | - | Model ID (e.g., "HuggingFaceTB/SmolLM2-135M-Instruct") |
| dtype | string | - | Quantization level (e.g., "fp16", "q4") |
| max_length | number | 20 | Maximum length the generated tokens can have (includes input prompt) |
| max_new_tokens | number | null | Maximum number of tokens to generate, ignoring prompt length |
| min_length | number | 0 | Minimum length of the sequence to be generated (includes input prompt) |
| min_new_tokens | number | null | Minimum numbers of tokens to generate, ignoring prompt length |
| do_sample | boolean | false | Whether to use sampling; use greedy decoding otherwise |
| num_beams | number | 1 | Number of beams for beam search. 1 means no beam search |
| temperature | number | 1.0 | Value used to modulate the next token probabilities |
| top_k | number | 50 | Number of highest probability vocabulary tokens to keep for top-k-filtering |
| top_p | number | 1.0 | If < 1, only tokens with probabilities adding up to top_p or higher are kept |
| repetition_penalty | number | 1.0 | Parameter for repetition penalty. 1.0 means no penalty |
| no_repeat_ngram_size | number | 0 | If > 0, all ngrams of that size can only occur once |

For additional advanced parameters, refer to the [Transformers.js documentation](https://huggingface.co/docs/transformers.js/api/generation).

## Local Development

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