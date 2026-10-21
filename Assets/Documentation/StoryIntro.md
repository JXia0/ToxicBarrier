# Opening story

Every **Start Game** from MainMenu plays four illustrated story cards before loading GameScene. Each card takes five seconds, including a 0.55-second fade in and fade out (about 20 seconds total). Restart Run / Play Again retries immediately. Opening GameScene directly in the editor still starts gameplay for development.

The sequence uses unscaled time while `GameState.StoryIntro` freezes simulation. Gameplay is not loaded until completion, so enemies cannot spawn and survival time cannot advance under the cinematic. Escape, Space, Enter, or the Skip button finishes the opening. Duplicate start and skip requests cannot load multiple runs. Leaving the intro cancels its completion callback.

Captions and Skip follow the saved English / Chinese setting. Chinese glyphs are baked in `Resources/Presentation/Chinese.asset`. Captions are live UI text, never embedded in artwork. Subtitles sit directly over the image with a subtle shadow: no translucent panel, numbered chapter or progress bars. The small corner control reads only **Skip / 跳过**, with no button background or shortcut legend; keyboard shortcuts still work. Images fit the screen without stretching; other aspect ratios use black letterboxing. The existing music continues at the player's saved volume.

## Story order

1. **The Fog** — Toxic fog erupts beneath a medieval kingdom.
2. **The Fallen** — Goblins take the ruins; the dead rise.
3. **The Refuge** — An allied tower shelters the remaining survivors.
4. **The Defender** — The player ventures out to gather supplies and restore it.

The player and tower are allies throughout. The story introduces the existing gathering / return / upgrade / defend loop; it does not add new gameplay rules.

## Assets and implementation

- `Assets/Resources/StoryIntro/01_TheFog.png` through `04_TheDefender.png`: four new full-screen illustrations.
- `Assets/Scripts/Core/StoryIntroUI.cs`: transient canvas owned by the persistent flow manager; art is unloaded when finished. No scene rebuilding required.
- `GameFlowManager.cs`: Start Game -> StoryIntro -> Transitioning -> Playing.
- `GameText.cs`: bilingual captions.
- `Assets/Editor/StoryIntroChecks.cs`: opt-in importer/font preparation and integration checks.

Illustrations were created with the built-in image generator using the current `Assets/Art/UI/MenuBackground.png` and the current level-zero tower sprite as references. The prompts requested four independent wide medieval story scenes, dark pixel-painted stonework, green toxic fog, burgundy foliage and amber refuge light; no baked text or UI. Reference roles: menu art for the shared world / player / monsters, tower sprite for the allied structure. The scenes depict the fog erupting from a well, goblins and skeletons in the fallen city, survivors sheltering at the tower, and the cloaked defender leaving to gather supplies. Generated originals remain in the generation output; the shipped copies live in this project's Resources folder. Existing gameplay sprites are unchanged.

## Validation

Create `Temp/ToxicBarrierStory.prepare` to configure texture importers and bake caption glyphs, or use **Tools > ToxicBarrier > Prepare Story Intro**. Create `Temp/ToxicBarrierStory.test` while not playing to run integration checks; the current scene must be saved first. Reports and screenshots go to `.utmp/story-checks/`. Tests preserve player preferences and reopen the original scene.

The checks exercise real five-second progression, automatic completion, no background simulation, English caption fit, Chinese glyphs, repeat Start Game, Skip raycasting and duplicate requests, pause, immediate restart, and cancellation. Do not ship the test marker files.
