import { useSettings } from "../../../hooks/useSettings";
import { LANGUAGES } from "../../../lib/constants";
import { SettingContainer } from "../../ui/SettingContainer";
import { SettingsGroup } from "../../ui/SettingsGroup";
import { ToggleSwitch } from "../../ui/ToggleSwitch";
import { Select } from "../../ui/Select";

export function GeneralSettings() {
  const { settings, devices, updateSetting } = useSettings();

  const micOptions = [
    { value: "", label: "System Default" },
    ...devices.map((d) => ({ value: d.name, label: d.name })),
  ];

  return (
    <div>
      <h2 className="text-lg font-semibold text-[var(--text-primary)] mb-4">
        General
      </h2>

      <SettingsGroup title="Input">
        <SettingContainer
          label="Hotkey"
          description="Keyboard shortcut to toggle recording"
        >
          <div className="bg-[var(--bg-tertiary)] border border-[var(--border)] rounded-md px-3 py-1.5 text-sm text-[var(--text-primary)] font-mono">
            {settings.hotkey}
          </div>
        </SettingContainer>

        <SettingContainer
          label="Microphone"
          description="Select audio input device"
        >
          <Select
            value={settings.selected_microphone ?? ""}
            options={micOptions}
            onChange={(v) =>
              updateSetting("selected_microphone", v || null)
            }
          />
        </SettingContainer>
      </SettingsGroup>

      <SettingsGroup title="Transcription">
        <SettingContainer
          label="Language"
          description="Transcription language"
        >
          <Select
            value={settings.language}
            options={LANGUAGES}
            onChange={(v) => updateSetting("language", v)}
          />
        </SettingContainer>

        <SettingContainer
          label="Voice Activity Detection"
          description="Trim silence before transcribing"
        >
          <ToggleSwitch
            checked={settings.enable_vad}
            onChange={(v) => updateSetting("enable_vad", v)}
          />
        </SettingContainer>
      </SettingsGroup>

      <SettingsGroup title="Output">
        <SettingContainer
          label="Auto Paste"
          description="Automatically paste transcription via Ctrl+V"
        >
          <ToggleSwitch
            checked={settings.auto_paste}
            onChange={(v) => updateSetting("auto_paste", v)}
          />
        </SettingContainer>
      </SettingsGroup>
    </div>
  );
}
