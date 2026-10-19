# Research Verification: Unity setup and walkthrough

## What is implemented

The first descent from each zone now runs Research Verification with occasional
"Who's in the Camera?" questions. This also covers the Objectives Cleared **Go**
button and first-time entries through zone selection. Choosing **Stay**, ascending,
and revisiting a zone already reached do not require another review.

| Reviewed zone | Total questions | Camera rounds when models are available | Pass score |
| --- | ---: | ---: | ---: |
| Sunlight | 5 | 1 | 4 |
| Twilight | 7 | 2 | 6 |
| Midnight | 9 | 2 | 8 |
| Abyss | 11 | 3 | 9 |
| Hadal | 13 | 3 | 11 |

Hadal has no next zone. After discovering at least half its species (12/23), the
Objectives Cleared popup offers the final review. Staying or cancelling allows
the player to start/resume it at the bottom boundary. Finishing returns to zone
selection and saves expedition research completion.

The review uses only discovered primary species in the reviewed zone. Species
choices also come only from that captured collection. Environmental landmark
facts are excluded. The current game stores discovery IDs, not player-captured
photographs: each camera answer card shows a model preview and the matching
real-life photo side by side on the left, with its species name centered in the
right-hand area. Verification questions also pair the model and real-life photo.
Neither view shows "3D Model" or "Field Photograph" headers.
Camera cards use a 60% visual / 40% name split, with a 260-high content row and
230-high model/photo previews. Verification visual rows are 320 high with
290-high previews. Live model framing allows a full turn without clipping or
changing zoom. Question prompts and answer feedback omit the speaker prefix.
Model previews use independent render targets and match the bestiary's idle
rotation speed (35 degrees per second by default). They use unscaled time so rotation continues while the research check
pauses the ocean; opening the pause menu stops rotation. Photographs remain
static. These are not interactive model-inspection windows.

`ResearchModelPreviewUI` is created automatically on each model frame, with no
prefab or scene wiring required. It uses the existing **ModelPreview** layer and
preview lighting, renders visible frames at up to 15 FPS, and releases its model,
camera and render texture when the card closes or is rebuilt. Idle yaw is retained
when the question redraws after an answer. Photos do not receive this component.
The speed can be adjusted on its runtime Inspector component. The existing
bestiary snapshot is used as a fallback if a live preview cannot initialize.

There are five separate question-bank assets containing 475 questions across 95
species. Each species has four verification records (habitat, characteristics,
ecological role, interesting fact) and one camera clue. Prompts and explanations
are phrased as Dr. Santos's dialogue; correct answers are taken from the existing bestiary. New biological
claims were not added. Verification distractors are other records from the same
zone, framed as identifying what this species' bestiary actually records.

## Unity walkthrough

### Immediate quick test (Editor only)

1. Open **Assets/Scenes/MainMenu.unity** directly and press **Play**.
2. Use **HadalDescent > Research > Quick Test > Sunlight (5 questions)** in the
   Unity menu bar. The other four zones are available in the same submenu.
3. Press **Begin Check**. This skips the objectives, hull requirements, species
   collection and radio introduction. Every species in the selected zone is
   available to this temporary test, so model/photo cards can be inspected immediately.
4. Verify idle rotation, the 60/40 split, answers and results. **Proceed** ends
   the test without changing zones. Stop Play Mode when finished.

Quick tests do not save game state, research drafts or completion flags and do
not unlock species. Existing research drafts remain intact. The button still
reads **Save & Return** to match the real flow, but in a quick test it simply
closes the temporary review. Bestiary study shows the actual discovery state.
The menu is enabled only while playing and not Editor-paused. If a tutorial or
cinematic is active, start from MainMenu as above. Rotation pauses with the
in-game pause menu, but runs while the quiz pauses the ocean.

### Normal progression

1. Open the project with Unity **6000.3.18f1** and let scripts/assets import.
2. Use **HadalDescent > Research > Validate Question Banks**. Check the Console
   for the per-zone validation results. This validates content and selection
   without regenerating or saving species assets.
3. No scene placement or prefab changes are required for the default setup.
   `ZoneManager` creates the review component when needed. The component loads
   `Assets/Resources/Research/Zone0Questions.asset` through `Zone4Questions.asset`.
   `ResearchVerificationUI` builds the themed UI at runtime.
4. To edit content, select a bank in the Project window. Its question records
   expose the species reference, source field, format, prompt, choices, correct
   choice index, and explanation. Keep question IDs stable: unfinished reviews
   reference them. Correct-choice indices in these initial banks are zero-based.
   Answers are shuffled during play.
5. Every verification answer must agree with its selected species/source field.
   If you update bestiary facts, update the corresponding bank answer, prompt
   where relevant, and explanation. Run validation again. Camera clues should
   uniquely identify their species among the captured options.
6. Enter Play Mode through the normal Main Menu/New Game flow. Complete the
   tutorial, discover at least half the current zone's species, and meet the next
   zone's hull requirement. Choose **Go** on Objectives Cleared, confirm descent
   at the bottom boundary, or choose **Verify Research** in zone selection.
7. Tap through Dr. Santos's transmission using the existing radio controls.
   Choose **Review Bestiary** or **Begin Check**. Closing the bestiary returns to
   the review; the world's controls remain paused.
8. Before answering, the only footer action is **Save & Return**. After answering,
   **Save & Return** is on the left and **Next** on the right. Read Dr. Santos's
   explanation, then press **Next**. Correct answers are highlighted only after
   submission. No upfront bestiary hint is shown, including correction questions.
   The question area scrolls for longer records/model previews/photos.
   At 80% or above, the results show corrections and offer only a centered
   **Proceed** button. Below
   80%, choose **Correct Records**; only missed concepts return. If a correction
   is wrong, **Next** presents that record again until it is verified.
9. **Save & Return** preserves the exact selected question IDs, choice order seed,
   current feedback, score, and correction progress. Choosing Proceed again
   resumes that review. There is no timer, currency penalty, or discovery loss.
10. Finish once, save/Continue, and revisit the boundary: the review must not
    repeat. New Game clears review completion and unfinished sessions.

All layout uses uGUI/TextMeshPro, the existing Antone font/theme constants, navy
panels, cyan/mint accents, and existing click/shutter/radio audio. The new canvas
uses 1920 x 1080 scaling and screen-safe-area anchors. Existing globally enforced
fonts and text spacing remain in effect. Long answer labels are nested so the
global theme's single-line button-label rule does not disable their wrapping.
Footer buttons share a standard width/height, shrink equally when required by
the available screen width, and form a centered group. Answer cards span the same
content width. All button labels are centered horizontally and vertically.

**Review Bestiary** is available only in the initial research popup and the
failed records-review/results screen. It is not available while answering
questions. Passing or finishing corrections shows only the centered **Proceed**
action, including the final expedition review.

## Remaining assets

| Zone | Species | Assigned photos | Assigned model prefabs | Remaining work |
| --- | ---: | ---: | ---: | --- |
| Sunlight | 15 | 15 | 15 | None required for this feature. |
| Twilight | 17 | 17 | 16 | One species still needs its model. |
| Midnight | 19 | 19 | 5 | Fourteen species still need models. Existing photo assignments are preserved. |
| Abyss | 21 | 0 | 0 | Supply photos/illustrations and actual species models. |
| Hadal | 23 | 0 | 0 | Supply photos/illustrations and actual species models. |

For a photograph: import it as **Sprite (2D and UI)**, use a single sprite for
the image, and assign it to that species' **Photo** field. **Full Image** is the
secondary photograph fallback. Assign the real species model to **Model Prefab**.
The two visual frames remain separate: a photo never substitutes for a model.
Missing verification visuals are labeled as unavailable; generic geometric
placeholders are excluded from camera rounds. Use accurate species imagery and
retain any required attribution.

When fewer than two captured species have actual model prefabs, the review
substitutes verification questions while keeping its full 5/7/9/11/13 length.
Abyss and Hadal therefore work now as verification reviews, and camera rounds
become eligible as models are assigned. Existing drafts retain their question IDs,
score and completed records. Photo-era camera questions that cannot be presented
with models use the matching verification record under the saved ID. An
uncommitted camera answer may be re-presented when its option pool changes.

Dr. Santos portrait art, recorded voice lines, new sound effects/music, and a
completion badge are optional. No new art is required for the default panel.

## Save compatibility and implementation boundaries

Per-zone state uses `Save_ResearchVerified_N`, `Save_ResearchDraft_N`, and
`Save_ZoneVisited_N`, alongside `Save_ResearchVersion` in the existing PlayerPrefs
namespace. Passing or completing guided corrections saves verification. Merely
opening the prompt does not. The review checkpoints the player's existing game
state on opening so its captured collection is available after a restart.

Older saves retain access through their last saved zone without being falsely
marked as having passed research. Directly opening a deeper scene in the Editor
is treated as visiting that scene; use the normal progression flow when testing
the first-entry requirement.

Changes are limited to the new research code/banks, existing progression/modal
integration, a narrow camera-input guard, and a radio-pause exception during the
Santos introduction. No existing species assets, fonts, materials, textures,
scene assets, or prefabs were edited by this implementation. Existing Midnight
and font edits present before implementation were preserved.

## Verification and remaining Play Mode checks

Portable production-logic checks can be run from the repository root:

```powershell
python Tests/ResearchVerification/run_checks.py
```

These compile the actual question-bank, selector, and review-session code with
small substitutes for Unity's data types. They check all 475 questions against
the current bestiary, 100 captured-collection samples per zone, deterministic
selection, duplicate exclusion, missing-art fallback, every pass/fail score,
guided correction, and session snapshots. They do not simulate Unity UI or
PlayerPrefs. The Editor validation menu additionally checks Unity asset loading
and `JsonUtility` serialization. Its `RunBatch` entry point also attempts a UI
preview in `scratch/ResearchVerificationPreview.png`.

At the presentation update, all **4,600 portable assertions passed**, and runtime and
new Editor code compiled against this project's Unity assemblies. Unity's batch
process exited during startup/licensing before loading the project, including
after requesting access to its licensing service. Consequently, in-editor asset
import, rendered UI, and Play Mode interactions could not be verified here.

In Play Mode check these flows before shipping:

- All three entry routes honor the review; cancelling any prior prompt starts no quiz.
- Repeated boundary callbacks cannot open overlapping reviews.
- Bestiary open/detail/close from preparation/results and pause/resume retain progress and keep controls frozen.
- Cancel during an answer explanation, restart/Continue, and resume the same feedback.
- Fail, correct one record, cancel, and resume at the next uncorrected record.
- Pass, re-enter/revisit, and confirm the checkpoint never repeats.
- New Game clears research state without clearing audio/control preferences.
- Hadal can resume its final review at the bottom boundary after choosing Stay.
- Check long records and photo choices on the supported phone/tablet aspect ratios.
- Check equal button widths and centered labels on all three preparation actions.
- Check unanswered questions offer only Save & Return; answered questions offer Save & Return on the left and Next on the right.
- Check visual headers are absent, and camera answer cards pair model/photo on the left with centered species text on the right.
- Check no bestiary hints appear before answering, including correction rounds.
- Check each model rotates in 3D while the ocean is paused, photos remain static,
  and opening the pause menu freezes rotation until Resume.
- Check all camera answer models rotate independently; opening the bestiary,
  leaving the question or cancelling the review releases the offscreen stages.
