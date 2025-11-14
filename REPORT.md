## Accessibility focused AR Multimodal AI Assistant - Project Report

### Abstract

This project introduces an AR-based AI assistant (ARAI) that users can interact with through speech, gestures, and sign-language input. The assistant is represented by a humanoid avatar placed in the real world using AR Foundation. It listens to users through Android’s native speech recognizer, processes queries using a Cloudflare Worker–hosted LLM, and responds using on-device text-to-speech. Lip sync and simple facial animations give the avatar expressive behavior, while subtitles ensure the experience remains accessible.

Gesture and sign-language input are supported through a separate React + MediaPipe detector running on a laptop. A lightweight message broker passes recognized text to the Unity app, allowing users to control the assistant without speaking.

The system emphasizes accessibility, natural interaction, and low-latency performance. Multimodal input, context-aware responses, and expressive animation work together to create an assistant that is intuitive and usable in varied environments, including situations where speech is not possible or practical.

### 1. Overview
This project is an **Augmented Reality–based AI assistant** designed to feel **natural, accessible, and multimodal**. Instead of interacting with a plain app, users speak to a humanoid avatar placed in their environment through AR. The assistant listens, understands, reasons using an LLM, and responds with synchronized speech and animations.

The system also supports **gesture and sign-language input** through an external detector, making it usable even when speech is not possible.

## 2. Core Features

### Speech Input (STT)

We use Android’s native `SpeechRecognizer` for fast, real-time speech recognition. It streams partial text as the user speaks and gives a final transcript automatically. Compared to Whisper, Android STT provides:

* Faster response time
* Word-by-word partials
* Lower mobile power usage

### Text-to-Speech Output (TTS)

Responses are spoken locally using on-device TTS. A custom Unity audio streamer converts synthesized WAV files into a smooth audio stream, which then drives:

* The avatar’s mouth via uLipSync
* The avatar’s speaking animation
* On-screen subtitles for accessibility

Because the TTS is on-device, it works even without a strong network connection.

## 3. Avatar Interaction
### AR Placement

Users place the avatar in the real world using AR Foundation’s plane detection and a reticle. Once placed, the avatar always faces the user for more natural interaction.

### Animations

A simple, effective animation setup was created:

* Idle animation when not speaking
* Speaking animation triggered automatically when audio is playing

This makes the avatar feel alive without adding unnecessary complexity.

### Lip Sync
Lip sync is handled by uLipSync, which reads phoneme energy from the audio and drives the avatar's blend shapes. This results in expressive mouth movement aligned with the assistant’s voice.

## 4. LLM Backend on Cloudflare Workers
The assistant uses a serverless backend built with Cloudflare Workers, which provides:

* Fast global response times
* Zero server maintenance
* Very low latency from mobile

We implemented two endpoints:

* `/chat` for short, clean, non-streaming replies
* `/chat-sse` for streaming responses (optional)

The client stores the last ten turns of conversation and sends them with each request, allowing the assistant to maintain context without storing any state on the server.

## 5. Sign-Language & Gesture Support
To extend accessibility, we integrated a parallel input method via a React + MediaPipe detector. Users can perform gestures or sign language in front of a laptop camera, and when they press STOP, the recognized text is sent to a small Node.js "message broker".

Unity polls this broker and:

1. Claims a message (one-time delivery)
2. Sends it to the LLM
3. Speaks the response with lip sync & subtitles
4. Acknowledges once complete

This allows the assistant to support users who cannot rely solely on speech.

## 6. Accessibility Considerations
The system supports multiple modes of input/output:

* Voice input
* Gesture / sign-language input
* Screen subtitles
* Real-time speech output
* Visual facial cues
* Clear on-screen state indicators (recording, processing, speaking)

This makes the assistant usable in noisy environments, by people with hearing impairments, or by users unable to speak.

## 7. Summary
The AR Multimodal AI Assistant brings together AR interaction, speech technologies, animation, and sign-language detection in a unified system:

* Natural AR placement
* Real-time speech recognition
* On-device TTS with lip sync
* Avatar animations
* Cloud LLM reasoning
* Gesture-based input
* Subtitles for accessibility

The result is an assistant that feels responsive, expressive, and inclusive - offering a glimpse at how multimodal AI can be woven into everyday environments.
