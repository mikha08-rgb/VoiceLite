export const APP_NAME = "VoiceLite";
export const APP_VERSION = "0.1.0";

export const LANGUAGES = [
  { value: "en", label: "English" },
  { value: "es", label: "Spanish" },
  { value: "fr", label: "French" },
  { value: "de", label: "German" },
  { value: "it", label: "Italian" },
  { value: "pt", label: "Portuguese" },
  { value: "ru", label: "Russian" },
  { value: "ja", label: "Japanese" },
  { value: "ko", label: "Korean" },
  { value: "zh", label: "Chinese" },
  { value: "ar", label: "Arabic" },
  { value: "hi", label: "Hindi" },
  { value: "auto", label: "Auto-detect" },
] as const;

export const WHISPER_MODELS = [
  { value: "ggml-base.bin", label: "Swift (Base)", size: "142 MB", tier: "free" },
] as const;
