# TODO

## Config Editor Parameter Hints

The current config editor exposes action parameters as a free-form text field.
This works for early testing, but the real UI should guide the user per action.

Goals:

- Show action-specific parameter hints when an action is selected. Done in the
  basic editor.
- Prefer structured controls over raw text where possible.
- Keep raw parameter editing available for advanced or unsupported cases.
- Select known inputs from `button:1` to `button:20` and `slider:1` to
  `slider:3`. Done in the basic editor.
- Manage profiles from the UI. Add, rename, and delete are available.

Initial action hints:

| Action | Required parameters | Example |
| --- | --- | --- |
| `keyboard.shortcut` | `keys` | `keys=Ctrl+Shift+Esc` |
| `system.openUrl` | `url` | `url=https://example.com` |
| `system.openApp` | `path` | `path=notepad.exe` |
| `log` | `message` | `message=Hello MakroPad` |
| `system.volume` | none, uses slider value | |
| `media.playPause` | none | |
| `media.nextTrack` | none | |
| `media.previousTrack` | none | |
| `media.stop` | none | |
| `media.volumeUp` | none | |
| `media.volumeDown` | none | |
| `media.mute` | none | |

Possible UI improvements:

- Display a short inline hint below the parameter field. Done in the basic
  editor.
- Change the parameter field placeholder based on the selected action. Done in
  the basic editor.
- Add validation before saving or applying a binding. Basic validation is done
  for input format, known actions, required parameters, and URL format.
- Add a quick way to fill the selected action's example parameters. Done with
  the `Example` button.
- Add action-specific editors:
  - Shortcut recorder for `keyboard.shortcut`. First focused-window version is
    done.
  - URL textbox for `system.openUrl`.
  - File/app picker for `system.openApp`. First file picker version is done.
- Document supported shortcut keys in the UI.
- Show a warning when parameters are ignored for actions that do not need them.
  Done in the basic editor log.

Implementation notes:

- Introduce an action metadata model, e.g. `ActionDefinition`.
- Use the same metadata for:
  - ComboBox action list.
  - Parameter placeholder and hint text.
  - Validation.
  - Future documentation generation.
- Keep metadata near the action layer if it describes execution behavior, or in
  the app layer if it only describes UI presentation.
