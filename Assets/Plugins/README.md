# Description of DLLs
- `LitJSON.dll`
    - [LitJSON](https://github.com/LitJSON/litjson) version 0.15.0
    - [Binary released on NuGet](https://www.nuget.org/packages/LitJson/0.15.0) (for net45) is used.
- `System.Threading.Channels.dll`
    - [System.Threading.Channels](https://www.nuget.org/packages/System.Threading.Channels) version 4.7.1 (for net461)
- `System.IO.Pipelines.dll`
    - [System.IO.Pipelines](https://www.nuget.org/packages/System.IO.Pipelines/) version 6.0.3 (for netstandard2.0)
- `WebAssembly.dll`
    - [dotnet-webassembly](https://github.com/RyanLamansky/dotnet-webassembly)
    - This DLL is a modified version. Source code is available at ["mondeto" branch of tana/dotnet-webassembly](https://github.com/tana/dotnet-webassembly/tree/mondeto).
- `YamlDotNet.dll`
    - [YamlDotNet](https://github.com/aaubry/YamlDotNet)
    - [version 8.1.2](https://www.nuget.org/packages/YamlDotNet/8.1.2) (for net35)
- `Concentus.dll`
    - [Concentus](https://github.com/lostromb/concentus)
    - [version 1.1.7](https://www.nuget.org/packages/Concentus/1.1.7) (for netstandard1.0)
- `Melanchall.DryWetMidi.dll`
    - [DryWetMIDI](https://github.com/melanchall/drywetmidi)
    - [version 5.1.2](https://github.com/melanchall/drywetmidi/releases/tag/v5.1.2) (for netstandard20)

- `Microsoft.NET.StringTools.dll` and `System.Runtime.CompilerServices.Unsafe.dll`
    - Included with MessagePack-CSharp.

- `x86_64/msquic.dll`
    - [MsQuic](https://github.com/microsoft/msquic) native library for x86_64 Windows (compiled with OpenSSL)