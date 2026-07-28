# Fonts (style bible §3)

Both faces are **SIL Open Font License 1.1** — the only license class this
project bundles. License texts ship beside the fonts and must travel with any
redistribution of the build.

| File | Face | Role (§3) | Source | License |
|---|---|---|---|---|
| `EBGaramond-Variable.ttf` | EB Garamond (variable weight) | Settlement labels, panel headers — the humanist old-style serif with an engraved feel | [google/fonts `ofl/ebgaramond`](https://github.com/google/fonts/tree/main/ofl/ebgaramond) (upstream: [octaviopardo/EBGaramond12](https://github.com/octaviopardo/EBGaramond12)) | `OFL-EBGaramond.txt` |
| `IBMPlexSerif-Regular.ttf` | IBM Plex Serif | Dense numbers and tables — the clean lining-figure companion; readability wins in data panels | [google/fonts `ofl/ibmplexserif`](https://github.com/google/fonts/tree/main/ofl/ibmplexserif) | `OFL-IBMPlexSerif.txt` |

Never bundle a face that is not OFL. If a font file is missing at runtime the
UI falls back to ImGui's built-in face and says so in the debug panel — a
missing font is never fatal.
