import { APP_NAME, APP_VERSION } from "../../../lib/constants";
import { Button } from "../../ui/Button";
import { useSettings } from "../../../hooks/useSettings";

export function AboutSettings() {
  const { resetSettings } = useSettings();

  return (
    <div>
      <h2 className="text-lg font-semibold text-[var(--text-primary)] mb-4">
        About
      </h2>

      <div className="bg-[var(--bg-secondary)] rounded-lg border border-[var(--border)] p-4 mb-6">
        <div className="text-lg font-bold text-[var(--text-primary)]">
          {APP_NAME}
        </div>
        <div className="text-sm text-[var(--text-secondary)] mt-1">
          Version {APP_VERSION}
        </div>
        <div className="text-xs text-[var(--text-secondary)] mt-3">
          Speech-to-text powered by Whisper. Built with Tauri + React.
        </div>
      </div>

      <div className="bg-[var(--bg-secondary)] rounded-lg border border-[var(--border)] p-4">
        <div className="text-sm font-medium text-[var(--text-primary)] mb-2">
          Reset
        </div>
        <div className="text-xs text-[var(--text-secondary)] mb-3">
          Restore all settings to their default values.
        </div>
        <Button variant="danger" onClick={resetSettings}>
          Reset Settings
        </Button>
      </div>
    </div>
  );
}
