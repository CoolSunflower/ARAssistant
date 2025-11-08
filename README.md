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

Future goals:
- Setting up tracked conversations with history
- Idle animation
- Change from default android voice, i.e. add support for better TTS models
- Remove AR Plane Manager texture after assistant is placed

## Setup instructions
1. Open Unity and install required packages
2. Download [whisper gglm-tiny.en](https://huggingface.co/ggerganov/whisper.cpp/blob/main/ggml-tiny.en.bin) bin file from HuggingFace and place it at `Assets/StreamingAssets/Whisper/gglm-tiny.en.bin`.

## Execution requirements (Optional)
**For better TTS**: Install open-source [F-Droid App Installer](https://f-droid.org/en/) and install [SherpaTTS](https://f-droid.org/en/packages/org.woheller69.ttsengine/) OR install the free and open source [RHVoice](https://github.com/RHVoice/RHVoice) from [Google Play Store](https://play.google.com/store/apps/details?id=com.github.olga_yakovleva.rhvoice.android&hl=en_IN) for better TTS output. After installation update the model in your Android's Text-to-Speech settings.