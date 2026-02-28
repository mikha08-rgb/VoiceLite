import { useEffect, useState } from "react";
import { listen } from "@tauri-apps/api/event";
import { invoke } from "@tauri-apps/api/core";
import "./RecordingOverlay.css";

interface StateEvent {
  state: "Idle" | "Recording" | "Processing";
  text: string | null;
  error: string | null;
}

export function RecordingOverlay() {
  const [state, setState] = useState<StateEvent["state"]>("Idle");

  useEffect(() => {
    const unlisten = listen<StateEvent>("state-changed", (event) => {
      setState(event.payload.state);
    });

    return () => {
      unlisten.then((fn) => fn()).catch(() => {});
    };
  }, []);

  if (state === "Idle") return null;

  return (
    <div className="overlay-container" data-tauri-drag-region>
      {state === "Recording" && (
        <div className="overlay-content recording">
          <div className="pulse-dot" />
          <span className="overlay-text">Recording...</span>
        </div>
      )}
      {state === "Processing" && (
        <div className="overlay-content processing">
          <div className="spinner" />
          <span className="overlay-text">Transcribing...</span>
        </div>
      )}
      <button
        className="cancel-btn"
        onClick={() => invoke("cancel_recording")}
      >
        Cancel
      </button>
    </div>
  );
}
