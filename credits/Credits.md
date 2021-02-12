# Credits
## Mondeto itself
Mondeto itself is licensed under [MIT License](../LICENSE).

### Acknowledgements
This project is supported by [The MITOU Program](https://www.ipa.go.jp/english/about/about_2_3.html) of [Information-technology Promotion Agency (IPA)](https://www.ipa.go.jp/index-e.html).

## Third-party libraries and data
Mondeto uses many third-party libraries and data.
The following are credits for these third-party works.

### MixedReality-WebRTC
- [LICENSE](ThirdPartyLicenses/MixedReality-WebRTC/LICENSE)
- [NOTICE](ThirdPartyLicenses/MixedReality-WebRTC/NOTICE) (Because MixedReality-WebRTC itself uses other open source works)

### MessagePack for C#
- [LICENSE](ThirdPartyLicenses/MessagePack-CSharp/LICENSE)

### UniVRM
- [LICENSE.txt](ThirdPartyLicenses/UniVRM/LICENSE.txt)

### MToon (which is included in UniVRM)
- [LICENSE](ThirdPartyLicenses/MToon/LICENSE)

### UniTask
- [LICENSE](ThirdPartyLicenses/UniTask/LICENSE)

### LitJSON
- [COPYING](ThirdPartyLicenses/LitJSON/COPYING)

### YamlDotNet
- [LICENSE.txt](ThirdPartyLicenses/YamlDotNet/LICENSE.txt)
- [LICENSE-libyaml](ThirdPartyLicenses/YamlDotNet/LICENSE-libyaml)

### Unity-Chan
Mondeto uses animation data of [Unity-Chan](https://unity-chan.com/).
Because Mondeto uses part of data for characters other than Unity-Chan,
the Unity [Asset Store Terms of Use and EULA](https://unity3d.com/jp/legal/as_terms) applies.
However, the source code repository of Mondeto contains (redistributes) entire data of Unity-Chan, licensed
under [Unity-Chan License Terms](ThirdPartyLicenses/UCL2.0/English/01Unity-Chan%20License%20Terms%20and%20Condition_EN_UCL2.0.pdf).
The license files (in English and Japanese, full and summarized versions) are inside [docs/ThirdPartyLicenses/UCL2.0](ThirdPartyLicenses/UCL2.0) directory.

![Unity-Chan license logo](ThirdPartyLicenses/UCL2.0/License%20Logo/Others/png/Light_Frame.png)

There are some modified files (paths are relative to root directory of UnityChan asset).

- `Animators/UnityChanLocomotions_IK.controller`: Based on `Animators/UnityChanLocomotions.controller`, but IK Pass is enabled.

### System.Threading.Channels
- [LICENSE.txt](ThirdPartyLicenses/System.Threading.Channels/LICENSE.txt)
- [THIRD-PARTY-NOTICES.txt](ThirdPartyLicenses/System.Threading.Channels/THIRD-PARTY-NOTICES.txt)

### System.Threading.Tasks.Extensions
- [LICENSE.txt](ThirdPartyLicenses/System.Threading.Tasks.Extensions/LICENSE.txt)
- [THIRD-PARTY-NOTICES.txt](ThirdPartyLicenses/System.Threading.Tasks.Extensions/THIRD-PARTY-NOTICES.txt)

### System.Runtime.CompilerServices.Unsafe
- [LICENSE.txt](ThirdPartyLicenses/System.Runtime.CompilerServices.Unsafe/LICENSE.txt)
- [THIRD-PARTY-NOTICES.txt](ThirdPartyLicenses/System.Runtime.CompilerServices.Unsafe/THIRD-PARTY-NOTICES.txt)

### WebAssembly for .NET
- [LICENSE](ThirdPartyLicenses/dotnet-webassembly/LICENSE)

### Concentus
- [LICENSE](ThirdPartyLicenses/Concentus/LICENSE)

### DryWetMIDI
- [LICENSE](ThirdPartyLicenses/DryWetMIDI/LICENSE)

### Unity builtin shaders
- [license.txt](ThirdPartyLicenses/UnityBuiltinShaders/license.txt)

### MIME type database from Apache HTTP Server (`mime.types`)
Public domain. Quote from the beginning of the `mime.types` file (`Assets/StreamingAssets/confing/mime.types` in source repository):

> ```
> # This file maps Internet media types to unique file extension(s).
> # Although created for httpd, this file is used by many software systems
> # and has been placed in the public domain for unlimited redisribution.
> ```

Note: some new types were added at the end of the file.

### Earth image (`earth.jpg`)
Earth texture image (`Assets/StreamingAssets/scene_data/earth.jpg`) is a resized version of ["June, Blue Marble Next Generation w/ Topography"](https://visibleearth.nasa.gov/images/74368/june-blue-marble-next-generation-w-topography) by NASA (National Aeronautics and Space Administration).

### Skybox image (`kloofendal_48d_partly_cloudy_2048x1024.jpg`)
Skybox texture image (`Assets/StreamingAssets/scene_data/kloofendal_48d_partly_cloudy_2048x1024.jpg`) is [Kloofendal 48d Partly Cloudy](https://hdrihaven.com/hdri/?c=partly%20cloudy&h=kloofendal_48d_partly_cloudy) by Greg Zaal (licensed under CC0).
This image is a resized version of the 8K tonemapped JPG.