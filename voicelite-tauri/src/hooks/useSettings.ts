import { useEffect } from "react";
import { useSettingsStore } from "../stores/settingsStore";

export function useSettings() {
  const store = useSettingsStore();

  useEffect(() => {
    if (store.status === "idle") {
      store.loadSettings();
      store.loadDevices();
    }
  }, [store.status]);

  return store;
}
