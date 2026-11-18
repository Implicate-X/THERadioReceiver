# 📡 THERadioReceiver

A modern .NET-powered audio platform inspired by the listening and recording culture of the 1970s and 1980s.

[<b>T</b>]InyCLR [<b>H</b>]ybrid [<b>E</b>]mbedded Radio Receiver combines FM radio, web radio, USB audio playback, and future recording capabilities in a single embedded system built entirely with C#, TinyCLR, and custom hardware.

Unlike conventional hobby projects, the hardware, firmware, protocols, synchronization mechanisms, user interface, and drivers have been developed entirely in-house. No third-party firmware, proprietary frameworks, or opaque software stacks are required.

Version 3.2 represents the final hardware generation and serves as the foundation for the complete software integration currently underway.

## 📑 Table of Contents

- Philosophy
- Overview
- Audio Sources
- Recording & Media Concept
- Web Radio Test Playback
- Hardware Architecture
- Changes from Previous Versions
- New Standby Architecture
- Binary Distance Decoder (BDD)
- Firmware Architecture
- Modules & Drivers
- Development Environment
- Outlook for Version 4.0

## 🎵 Philosophy

THERadioReceiver is not merely a radio receiver.

It is a modern reinterpretation of the listening and recording experience that shaped consumer electronics throughout the 1970s and 1980s.

Many people remember waiting for a favorite song to appear on the radio, preparing a cassette recorder, and pressing REC and PLAY at precisely the right moment.

The goal of THERadioReceiver is not nostalgia for its own sake.

The goal is to preserve the excitement of discovering, recording, collecting, and enjoying music while combining it with modern digital technology and a fully managed .NET software stack.

## 🧭 Overview

THERadioReceiver combines:

- TEF6686 automotive FM tuner
- VS1053B audio codec (MP3, AAC, Ogg, WAV)
- LS7366R quadrature encoder-counter
- Ethernet adapter for web radio
- USB host interface for local media playback
- VFD display (OLED option to follow)
- SITCore FEZ Feather as the central control CPU
- Wire-wrap underside for service-friendly wiring
- Firmware developed entirely in-house in C# / TinyCLR

The device is a true hybrid system:

Retro electronics meet modern .NET runtime.

## 🎧 Audio Sources

THERadioReceiver integrates three audio domains into a unified architecture:

### 📻 FM Radio

Reception through the TEF6686 automotive tuner DSP.

### 🌐 Web Radio

Internet radio streaming via Ethernet and custom TinyCLR networking components.

### 💾 USB Audio

Playback of locally stored audio files from USB mass storage devices.

Supported formats are provided by the VS1053B audio codec and include:

- MP3
- AAC
- Ogg Vorbis
- WAV
- WMA
- FLAC
- M4A

Together these sources create a single audio platform rather than a collection of isolated subsystems.

## 🎚 Recording & Media Concept

THERadioReceiver is designed not only to receive audio but also to preserve it.

Future firmware versions are intended to support:

- Recording FM broadcasts to USB storage
- Recording web radio streams to USB storage
- Manual recording
- Scheduled recording
- Recording management through a companion application

The goal is to recreate the classic radio-recording experience using modern storage technology and software-defined functionality.

No tape wear.
No tangled cassettes.
No mechanical limitations.

Just the same anticipation that once surrounded a favorite song on the air.

### Companion Application

A companion application based on Avalonia UI is planned for Android and desktop platforms.

Rather than exposing only playlists and file browsers, the application is intended to emulate the look and feel of a portable cassette recorder.

Planned controls include:

- PLAY
- STOP
- PAUSE
- REWIND
- FAST FORWARD
- RECORD

For a truly authentic experience, recordings may even require pressing RECORD and PLAY together.

Because some traditions deserve to survive.

## 🌐 Web Radio Test Playback

The band control switches between FM and the MP3 endpoint in `WebRadio.TestStreamUrl`.

Each playback starts from that stable station URL and follows up to four HTTP redirects with absolute HTTP or HTTPS locations. HTTPS-to-HTTP redirects are rejected.

The final response is used directly for playback, so temporary stream URLs are not reused.

Network audio uses the VS1053 buffered playback path, including its 32 KB prefill, so playback starts after buffering.

Switching back to FM cancels the HTTP request.

The display shows the configured test station name and format from `WebRadio`; these values must be updated if the endpoint changes.

Live track metadata is not yet requested or displayed.

Stream connection and playback errors are reported in the debug output; automatic reconnection is not yet implemented.

The standard VS1053 patch is loaded during codec initialization and restarted without another upload for subsequent streams.

A decoder reset or a switch to a different patch variant requires uploading the appropriate patch again.

## 🔧 Hardware Architecture

The main board, version 3.2, integrates:

- Power supply & transformer
- Audio path with switching between FM and web radio
- USB host subsystem
- Ethernet module with its own 3.3 V domain
- Battery backup for wake-up
- FEZ Feather as the central controller
- Level-shifter subsystems
- I/O expander
- Encoder subsystem
- VFD display driver
- Wire-wrap backplane

All modules are clearly separated, logically grouped, and electrically well-routed.

## 🔄 Changes from Previous Versions

✔ BSS138 level shifter removed

Replaced by:

- 2× SN74LVC8T245
- 1× SN74LVC1T45

Result:

- More stable levels
- Clean signal edges
- Fewer components
- Lower power dissipation

✔ TEF6686 I²C level conversion via PCA9306

Result:

Reliable communication even with long cables and mixed voltage domains.

✔ Ethernet module with its own 3.3 V power supply

Separate regulator for the LAN chip.

Result:

- Less interference
- Cleaner reception
- More stable connection

## 🔋 New Standby Architecture (Version 3.2)

The original FM tuner used a printed-circuit transformer permanently connected to the mains for standby.

This led to:

- Continuous power dissipation
- Thermal stress
- RF stray fields in the tuner area

The new architecture completely replaces the transformer:

- LiPo-buffered FEZ Feather as the always-on core
- Feather runs in low-power mode
- Wake-up is triggered by a reed relay that activates the switching regulator before the main transformer
- The device is completely de-energized when turned off

Result:

- No standby power loss
- No mains stray fields
- Clean RF domain

## 🔢 Binary Distance Decoder (BDD)

A proprietary IR decoding method for time-coded bit frames.

THERadioReceiver contains a custom-developed module for decoding a proprietary IR format that does not conform to the NEC specification.

The method was developed from extensive logic-analyzer analysis of a remote control's pulse-duration sequences and was subsequently named the Binary Distance Decoder (BDD).

The BDD runs entirely on TinyCLR in C# and is optimized for embedded real-time operation.

### Operating Principle

The BDD format consists of:

- A pulse-duration sequence
- A synchronization gap
- 15 data bits in fixed time slots
- A defined semantic structure

Frame structure:

| Bits | Purpose |
|--------|--------|
| 7 | Command |
| 5 | Address |
| 1 | Toggle |
| Remaining | Reserve / Normalization |

Complementary variants are merged into a canonical frame value through a normalization function.

Implementation is carried out using TinyCLR's Signal Control API.

In this project, BDD is used to map remote-control commands such as tuning functions through a DENON code map.

## 💻 Firmware Architecture

The firmware is developed entirely in C# on TinyCLR.

Characteristics:

- No C fragments
- No third-party stacks
- No proprietary libraries
- Deterministic architecture
- Clearly defined subsystems
- Modular drivers
- Full control over every layer

The project demonstrates how far modern embedded development can be pushed using a unified .NET environment.

## 🎛 Modules & Drivers

Already stable in isolated test setups:

### Radio

- TEF6686 FM tuner with automotive DSP

### Audio

- VS1053B audio codec

### Input

- LS7366R 32-bit quadrature encoder-counter

### Networking

- Ethernet subsystem
- HTTP stream processing
- Redirect handling

### User Interface

- VFD driver
- Display framework

These modules are now being orchestrated and integrated into the overall system.

## 🧰 Development Environment

All components—firmware, drivers, gateway services, server components, recording management, and companion applications—are developed in a single environment:

- Visual Studio 2026
- TinyCLR
- C#
- .NET

Advantages:

- Consistent architecture
- Identical debugging tools
- No fragmentation between embedded, desktop, and mobile
- End-to-end maintainability
- One language from device to application

## 🔭 Outlook for Version 4.0

### 🖥️ OLED Display

An OLED module with the exact form factor of the VFD is already prepared.

### 📱 Companion Application

Full control and monitoring of the device via an Avalonia-based application written entirely in C#.

### 💾 USB Media Support

- USB music library
- Media management
- Recording archive

### 🎚 Recording Engine

- FM recording
- Web radio recording
- Scheduled recording
- Future recording management features

### 🧩 Extended Firmware

- Synchronization services
- Streaming logic
- UI composition
- Logging infrastructure
- Recording services
- Network stack enhancements

## ❤️ TinyCLR OS

Designed for modern embedded development using .NET and TinyCLR OS 3.0.2.3000.

*"Retro electronics meets modern managed code."*