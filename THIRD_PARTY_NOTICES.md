# Third-party notices

Everything in this repository that isn't listed here was made for After Hours: the code, every
3D model (generated in Blender by `ArtSource/*.py`), every texture (`Tools/gen_textures.py`), every
sound effect and music track (`Tools/audio/`), and the trailer and screenshots in `docs/media/`.

## Fonts

Each font ships with its licence text in `Assets/Fonts-Licenses/` (the copies in `ArtSource/fonts/`
are used by the Blender and texture generators and carry the same licence files).

| Font | Files | Copyright | Licence |
|---|---|---|---|
| Fira Sans | `Assets/Resources/Fonts/FiraSans-*.ttf` | The Mozilla Foundation, Telefonica S.A., bBox Type GmbH, Carrois Corporate GbR | SIL Open Font License 1.1 |
| Caveat | `Caveat.ttf` | The Caveat Project Authors | SIL Open Font License 1.1 |
| Courier Prime | `CourierPrime.ttf` | The Courier Prime Project Authors | SIL Open Font License 1.1 |
| Patrick Hand | `PatrickHand.ttf` | Patrick Wagesreiter | SIL Open Font License 1.1 |
| Reenie Beanie | `ReenieBeanie.ttf` | James Grieshaber (Typeco) | SIL Open Font License 1.1 |
| Permanent Marker | `PermanentMarker.ttf` | Font Diner, Inc. | Apache License 2.0 |
| Special Elite | `SpecialElite.ttf` | Brian J. Bonislawsky (Astigmatic) | Apache License 2.0 |
| DejaVu Sans | `DejaVuSans.ttf` | Bitstream, Inc.; Tavmjong Bah; DejaVu changes public domain | Bitstream Vera / Arev fonts licence |
| Liberation Sans | `Assets/TextMesh Pro/Fonts/LiberationSans.ttf` | Google Corporation; Red Hat, Inc. | SIL Open Font License 1.1 |

## Unity

- **TextMesh Pro Essential Resources** (`Assets/TextMesh Pro/`): shaders, settings and the
  Liberation Sans SDF font asset imported from Unity's uGUI package. Unity Companion License.
- **Unity packages** (`Packages/manifest.json`: Universal Render Pipeline, Input System, uGUI, Test
  Framework, IDE integrations, Unity Pipeline). These are downloaded from the Unity package registry
  when the project opens and are not stored in this repository. Unity Companion License / Unity
  package terms.
- **The Linux player** in the release archive contains the Unity runtime (`UnityPlayer.so`, Unity
  terms of service) and `libdecor` (MIT), which Unity ships with Linux builds.

## Tools (not redistributed)

Blender 4.5 (GPL), Python with NumPy, SciPy and Pillow, and FFmpeg were used to generate assets and
media. None of their code is included here.
