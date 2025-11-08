Current Progress:
- You can place the agent on the floor
- You can tap top left button to start recording your voice and the agent will lip sync to your voice, top right button to stop
- Bottom button currently does not do anything [See Older Commit]
- Remove lip sync with human user audio as that was just for testing (disabled buttons)
- Add STT using bottom button & debug info. [In Progress]

Next steps:
- LLM backend
- Local TTS on Android -> AudioClip -> uLipSync
- Default animation of agent
- End to end testing
- Adding Sign Language and Gesture detection to backend
- Integrating Sign Language and Gesture support with agent


## Setup instructions
1. Open Unity and install required packages
2. Download [whisper gglm-tiny.en](https://huggingface.co/ggerganov/whisper.cpp/blob/main/ggml-tiny.en.bin) bin file from HuggingFace and place it at `Assets/StreamingAssets/Whisper/gglm-tiny.en.bin`.
