# Accessibility-focused AR Multimodal AI Assistant - CS560 Course Project

An accessible, multimodal AR assistant that you can interact with through speech, gestures, and sign language. The assistant appears as a humanoid avatar in your real-world environment, responding with synchronized speech, lip movements, and facial animations.

Done by:
```
Adarsh Gupta (220101003)
Sharvil Patel (220101091)
Parv Aggarwal (220101124) 
```

## Overview

This project combines augmented reality, speech recognition, and sign language detection to create an AI assistant that is:

- **Natural**: Place a humanoid avatar in your environment using AR and have natural conversations
- **Accessible**: Supports **speech input, sign language, gestures, and provides real-time subtitles**
- **Responsive**: Fast, low-latency responses powered by Cloudflare Workers and on-device processing
- **Expressive**: Realistic lip sync and animations make the avatar feel alive

### Key Features

- **Speech Recognition**: Real-time Android native speech-to-text for fast, accurate transcription
- **Text-to-Speech**: On-device TTS with synchronized lip movements via uLipSync
- **Sign Language Support**: MediaPipe-based gesture and sign language recognition
- **AR Placement**: Position the avatar anywhere in your environment using AR Foundation
- **Context-Aware**: LLM-powered responses that remember **conversation history**
- **Multimodal Input**: Voice, sign language, or gestures - choose what works best for you

## Architecture

The system consists of three main components:

1. **Unity AR Application** (Android): The main app with AR avatar, speech I/O, and LLM integration
2. **LLM Backend** (`arai-backend/`): Cloudflare Worker hosting the language model
3. **Sign Language System** (Optional):
   - **Frontend** (`sign-language-backend/`): React + MediaPipe web detector
   - **Message Broker** (`sign-language-ngrok/`): Node.js server for message passing

## Prerequisites

### For Unity AR Application
- Unity 2022.3 or later
- Android device with ARCore support
- Android SDK and build tools

### For Sign Language Support (Optional)
- Node.js 16 or later
- npm or yarn
- Webcam-enabled computer
- ngrok account (free tier works, paid tier recommended for persistent URLs)

## Setup Instructions

### 1. Clone the Repository

```bash
# clone with submodules
git clone --recurse-submodules https://github.com/CoolSunflower/ARAssistant.git

# or after cloning
# git submodule update --init --recursive
# to update submodules to latest remote commits
# git submodule update --remote --merge

cd ARAssistant
```

### 2. LLM Backend Setup

The backend is already deployed on Cloudflare Workers. If you want to modify or deploy your own:

```bash
cd arai-backend
npm install
# Follow the commands in `arai-backend/README.md`
```

Update the backend URL in your Unity project if you deploy your own instance.

### 3. Unity AR Application Setup

1. Open the project in Unity (tested with Unity 2022.3+)
2. Open the main scene from `Assets/Scenes/`
3. Build Settings -> Switch Platform to Android
4. Configure your Android SDK path in Unity preferences
5. Build and deploy to your ARCore-compatible Android device

### 4. Sign Language Support Setup (Optional)

If you want to enable sign language and gesture input, follow these steps:

#### Step 1: Start the Message Broker Server

```bash
cd sign-language-ngrok
npm install
node src/index.js
```

The server will start on `http://localhost:5000` with API key `mysecret`.

#### Step 2: Expose Server with ngrok

In a new terminal:

```bash
ngrok http 5000
```

Copy the ngrok URL (e.g., `https://abc123.ngrok-free.app`) - you'll need this for the next steps.

**Note**: Free ngrok URLs change every time you restart ngrok. For a persistent URL, consider upgrading to ngrok's paid tier.

#### Step 3: Configure the Sign Language Frontend

```bash
cd sign-language-backend/
npm install
```

Edit `.env.local` and update the ngrok URL:

```bash
REACT_APP_PUSH_API_URL=https://YOUR_NGROK_URL.ngrok-free.app/push
REACT_APP_PUSH_API_KEY=mysecret
```

Start the frontend:

```bash
npm start
```

The React app will open at `http://localhost:3000`.

#### Step 4: Update Unity Project with ngrok URL

1. Open the Unity project
2. Find the `ClaimingPollingClient` GameObject in the scene hierarchy
3. In the Inspector, update the `Base Url` field with your ngrok URL:
   ```
   https://YOUR_NGROK_URL.ngrok-free.app
   ```
4. Rebuild and deploy to your Android device

**Important**: If using free ngrok, you'll need to repeat this step each time you restart ngrok (since the URL changes). With a paid ngrok subscription, you get a persistent URL and only need to do this once.

## Usage

### Speech Interaction

1. Launch the AR Assistant app on your Android device
2. Point your camera at a flat surface to detect planes
3. Tap to place the avatar in your environment
4. Tap the microphone button to start speaking
5. The avatar will process your request and respond with speech and animations
6. Subtitles appear on screen for accessibility

### Sign Language Interaction

1. Ensure the sign language system is running (see Setup Step 4)
2. Open the React web app on your computer
3. Click "Start" to begin detection
4. Perform signs in front of your webcam
5. Hold each sign for 1/2 second to record it
6. Click "Stop" to send the recognized text to your Unity app
7. The avatar will process and respond to your signs

### Supported Signs

The system currently recognizes the following ASL signs:
- Bye
- Hello
- I Love You
- No
- Please
- Thanks
- Yes
- All the alphabets

## Project Structure

```
ARAssistant/
├── Assets/                          # Unity project assets
│   ├── Scenes/                     # Main AR scene
│   ├── Scripts/                    # C# scripts
│   │   ├── AssistantStreamController.cs
│   │   ├── ClaimingPollingClient.cs
│   │   └── ...
│   ├── Prefabs/                    # Avatar and UI prefabs
│   └── StreamingAssets/            # Model files
├── arai-backend/                   # Cloudflare Worker LLM backend
│   ├── src/
│   │   └── index.js
│   └── wrangler.toml
├── sign-language-backend/          # React + MediaPipe detector
│   └── src/
│   └── public/
└── sign-language-ngrok/            # Message broker server
    └── src/
        └── index.js
```

## Troubleshooting

### Sign Language Not Working

- Verify the message broker server is running on port 5000
- Check that ngrok is exposing the correct port
- Ensure the `.env.local` file has the correct ngrok URL
- Confirm the Unity `ClaimingPollingClient` has the updated ngrok URL
- Check browser console for any API errors

### AR Avatar Not Appearing

- Ensure your device supports ARCore
- Point camera at a well-lit, flat surface
- Move device slowly to help plane detection
- Check that AR permissions are granted

### Speech Recognition Not Working

- Grant microphone permissions in Android settings
- Ensure device has internet connection for first-time setup
- Check that Google Speech Services is installed and updated

### Lip Sync Issues

- Verify uLipSync package is properly imported
- Check that avatar has blend shapes configured
- Ensure audio is playing through the correct audio source

## Technologies Used

- **Unity 2022.3+** with AR Foundation
- **ARCore** for Android AR capabilities
- **uLipSync** for realistic lip synchronization
- **Android SpeechRecognizer** for speech-to-text
- **Android TextToSpeech** for voice output
- **Cloudflare Workers** for serverless LLM hosting
- **React** for sign language detector UI
- **MediaPipe** for hand gesture recognition
- **Node.js + Express** for message broker
- **ngrok** for local server tunneling

## Accessibility

This project prioritizes accessibility through:

- Multiple input modalities (speech, sign language, gestures)
- Real-time subtitles for all spoken responses
- Visual feedback for system state (listening, processing, speaking)
- On-device processing for offline capability
- Clear, expressive avatar animations

## Future Enhancements

- Expanded sign language vocabulary
- Support for multiple languages
- Offline LLM processing
- Custom avatar selection
- Persistent conversation history
- Improved gesture recognition accuracy

## License

This project is provided as-is for educational and research purposes.

## Acknowledgments

- MediaPipe for hand tracking and gesture recognition
- uLipSync for lip synchronization
- Cloudflare for serverless infrastructure
- AR Foundation team for AR capabilities
