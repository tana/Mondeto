# Mondeto
[![Discord](https://img.shields.io/discord/776494294756360222?label=discord)](https://discord.gg/XKQtmT2XxP)

Languages | English | [日本語](README_ja.md)

An open, flexible, and secure online virtual reality system.

## 💡About
**Mondeto** is an online, multiplayer virtual reality system which has this three features:
1. 📖**Open:** Mondeto is an open-source software, not a service. **Everybody can setup a server** on their own computer. Also you can modify Mondeto software itself to **connect new hardware with VR worlds**.
2. 🛠**Flexible:** VR objects can be **programatically controlled** using [WebAssembly](https://webassembly.org/).
3. 🔒**Secure:** Communications are **encrypted** and WebAssembly codes are safely run inside **sandbox**. Despite being open and flexible, we do not sacrifice security.

## 💻Requirements
- 64-bit Windows computer
- For VR mode:
    - Oculus headset and controllers (🙇we are working on supporting other headsets🚧)
    - Oculus software installed

## 🚀Usage
### Client (connecting to existing server)
1. Download the zip archive and extract it.
1. Launch the executable `Mondeto.exe`.
1. Check the `Client` checkbox.
    - By default, it will connect to the server provided by the developer. However, you can connect to other servers by setting the `Signaler` URL.
1. Press `Start` to connect.

## ✉️Contacts
If you have any questions, please ask [on the Discord chat](https://discord.gg/XKQtmT2XxP) or [ask the developer on Twitter](https://twitter.com/tana_ash).

## ⚖️License
Mondeto itself is licensed under [MIT License](LICENSE). However, it uses some third-party programs and data that is licensed under various licenses (see [credits/Credits.md](credits/Credits.md)).

In addition, this repository contains some of those third-party program or data that are licensed under redistributable licenses.

## 🙏Acknowledgements
This project is supported by [The MITOU Program](https://www.ipa.go.jp/english/about/about_2_3.html) of [Information-technology Promotion Agency (IPA)](https://www.ipa.go.jp/index-e.html).
