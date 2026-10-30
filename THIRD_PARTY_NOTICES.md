# Third-Party Notices

## DM Note

ImplDmNote (`apps/impl-dm-note`) is based on DM Note v1.6.1, copyright (C) 2024 lee-sihun, and is distributed under GPL-3.0-only. The complete license, upstream notices, source provenance, and modification summary are included inside that application directory.

ImplDmNote is a separate program from the ImplResourcePack Unity mod. Release archives of the mod carry a copy of it in `ImplDmNote/` next to the mod; that copy keeps its own GPL-3.0-only license and notices inside it, and its complete source code is `apps/impl-dm-note` in https://github.com/KGH1113/ImplResourcePack.

## AdofaiIpc

ImplResourcePack uses and redistributes the AdofaiIpc 0.4.1 dependency bootstrap and shim. AdofaiIpc is copyright (c) 2026 KGH1113 and distributed under the MIT License.

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to inclusion of the copyright and permission notice.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY.

## Microsoft WebView2 SDK

The Windows in-game viewer library (`native/ImplGameWebView.dll`) statically links `WebView2LoaderStatic.lib` from the Microsoft.Web.WebView2 1.0.4314.35 NuGet package and is compiled against its `WebView2.h` header. The Microsoft Edge WebView2 Runtime itself is not redistributed; Windows provides it.

Copyright (C) Microsoft Corporation. All rights reserved.

Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

- Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
- Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
- The name of Microsoft Corporation, or the names of its contributors may not be used to endorse or promote products derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

## MapleStory OTF Bold

The ImplResourcePack source repository includes the unmodified `MAPLESTORY_OTF_BOLD.OTF` font file. Its Unity editor tooling (`ImplResourcePack.Unity/Assets/Editor/FontAssetGenerator.cs`) rasterizes TextMesh Pro SDF atlases from that file; it only chooses which code points to render and does not edit the font's outlines, metrics, names, or other data. The mod's release builds redistribute only the resulting `MAPLESTORY_OTF_BOLD SDF` and `MAPLESTORY_OTF_BOLD KR SDF` assets inside the platform AssetBundles, not the font file itself.

ImplDmNote keeps its own unmodified copy of the same font file at `apps/impl-dm-note/src-tauri/assets/fonts/MAPLESTORY_OTF_BOLD.OTF` and builds it into the app, which copies it into its app data folder on first launch for the default key viewers. The ImplDmNote copy in release archives therefore contains the font, with this notice repeated in its `THIRD_PARTY_NOTICES.txt`.

The MapleStory typeface is owned by NEXON Korea Corporation. The [official font terms](https://maplestory.nexon.com/Media/Font) permit free use by individuals and businesses and permit redistribution or bundling with a copyright notice. The font itself may not be modified or edited, and it may not be sold for a fee. Attribution is recommended by the official terms.

MapleStory and NEXON are trademarks or registered marks of their respective owners. Inclusion of the font and its derived assets does not imply endorsement.

## ImplDmNote runtime artwork

ImplDmNote retains the upstream DM Note application icon and in-app logo that are required at runtime. They are included as part of the GPL-3.0-only upstream work and are attributed to the DM Note project and its original designer in `apps/impl-dm-note/UPSTREAM.md`. Documentation logos and promotional media are not redistributed by this repository.
