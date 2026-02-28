import { create } from "zustand";
import { invoke } from "@tauri-apps/api/core";
import type { AppSettings, AudioDevice } from "../bindings";

type LoadStatus = "idle" | "loading" | "loaded" | "error";

interface SettingsState {
  settings: AppSettings;
  devices: AudioDevice[];
  status: LoadStatus;
  error: string | null;
  loadSettings: () => Promise<void>;
  loadDevices: () => Promise<void>;
  updateSetting: <K extends keyof AppSettings>(
    key: K,
    value: AppSettings[K],
  ) => Promise<void>;
  resetSettings: () => Promise<void>;
}

const defaultSettings: AppSettings = {
  record_mode: "Hold",
  hotkey: "Ctrl+Space",
  selected_microphone: null,
  whisper_model: "ggml-base.bin",
  language: "en",
  enable_vad: true,
  vad_threshold: 0.35,
  auto_paste: true,
  show_tray_icon: true,
};

export const useSettingsStore = create<SettingsState>((set, get) => ({
  settings: defaultSettings,
  devices: [],
  status: "idle",
  error: null,

  loadSettings: async () => {
    set({ status: "loading", error: null });
    try {
      const settings = await invoke<AppSettings>("get_settings");
      set({ settings, status: "loaded" });
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      console.error("Failed to load settings:", message);
      set({ status: "error", error: message });
    }
  },

  loadDevices: async () => {
    try {
      const devices = await invoke<AudioDevice[]>("get_audio_devices");
      set({ devices });
    } catch (err) {
      console.error("Failed to load audio devices:", err);
    }
  },

  updateSetting: async (key, value) => {
    const prev = get().settings;
    set({ settings: { ...prev, [key]: value } });
    try {
      await invoke("update_setting", {
        key: String(key),
        value: String(value),
      });
    } catch (err) {
      console.error("Failed to save setting:", err);
      set({ settings: prev });
    }
  },

  resetSettings: async () => {
    try {
      const settings = await invoke<AppSettings>("reset_settings");
      set({ settings });
    } catch (err) {
      console.error("Failed to reset settings:", err);
    }
  },
}));
