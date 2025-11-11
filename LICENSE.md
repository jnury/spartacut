# MIT License

Copyright (c) 2025 Sparta Cut

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

---

## Third-Party Software

Sparta Cut uses third-party open-source software components. See [LICENSE-THIRD-PARTY.txt](LICENSE-THIRD-PARTY.txt) for complete license information of all dependencies.

### Key Dependencies and Their Licenses

- **LibVLC** (LGPL-2.1+) - Dynamically linked for video playback
- **LibVLCSharp** (LGPL-2.1+) - .NET wrapper for LibVLC
- **Avalonia UI** (MIT) - Cross-platform UI framework
- **FFMpegCore** (MIT) - FFmpeg wrapper for export
- **NAudio** (MIT) - Audio processing library
- **CommunityToolkit.Mvvm** (MIT) - MVVM toolkit
- **Serilog** (Apache-2.0) - Logging library
- **SkiaSharp** (MIT) - 2D graphics library

### LGPL Compliance

Sparta Cut uses LibVLC and LibVLCSharp under LGPL-2.1+ license terms by:
- **Dynamic linking only** (no static compilation)
- Providing source code access via [VideoLAN repositories](https://code.videolan.org/videolan/)
- Maintaining separation between MIT-licensed and LGPL-licensed components
- Full attribution in LICENSE-THIRD-PARTY.txt and application About dialog

Users may replace LibVLC libraries with their own builds if desired, maintaining compliance with LGPL requirements.
