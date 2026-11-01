# Gas Laws Dive

A short browser game: five scuba dives off the Florida coast, each built around one gas law
(Boyle, Charles, Gay-Lussac, Dalton and the combined gas law). On each dive you watch what happens
to the air in your vest, lungs or tank, then answer a few questions about it. Made for CHM 1025
and CHM 1045 at Daytona State College by Prof. John Picione.

**Play:** <https://neogio73.github.io/gas-laws-dive/>

The game asks for your D2L username at the start and shows a completion code at the end. The
username is used only inside your browser: it is never saved or sent anywhere.

## What is here

| Folder | What it is |
| --- | --- |
| (top level) `index.html`, `Build/`, `StreamingAssets/` | The game itself (a Unity WebGL build), served by GitHub Pages. |
| `StreamingAssets/questions.json` | The questions, answers and feedback. |
| `source/` | The game's own C# scripts, browser plugin and web page template. |
| `tools/` | Helper scripts, such as the question checker. |

## License

- The game's own code (`source/`, `tools/`): MIT License, see [LICENSE](LICENSE).
- The questions, feedback and other written text (`StreamingAssets/questions.json` and the
  game's on-screen text): Creative Commons Attribution 4.0 International (CC BY 4.0), see
  [LICENSE-CONTENT.md](LICENSE-CONTENT.md).
- Third-party models, textures, sounds, fonts and other assets, and the Unity engine code inside
  the build, are not covered by either license. They belong to their owners and are used under
  their own terms.
