import { useEffect, useState } from "react";
import { listen } from "@tauri-apps/api/event";
import { APP_VERSION } from "../../lib/constants";

interface StateEvent {
  state: "Idle" | "Recording" | "Processing";
  text: string | null;
  error: string | null;
}

export function Footer() {
  const [status, setStatus] = useState("Idle");

  useEffect(() => {
    const unlisten = listen<StateEvent>("state-changed", (event) => {
      setStatus(event.payload.state);
    });
    return () => {
      unlisten.then((fn) => fn()).catch(() => {});
    };
  }, []);

  return (
    <div className="px-4 py-2 border-t border-[var(--border)] text-[10px] text-[var(--text-secondary)] flex justify-between">
      <span>VoiceLite v{APP_VERSION}</span>
      <span className={status === "Recording" ? "text-[var(--error)]" : ""}>
        {status}
      </span>
    </div>
  );
}
