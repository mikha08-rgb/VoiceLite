import React from "react";
import ReactDOM from "react-dom/client";
import { RecordingOverlay } from "./RecordingOverlay";
import { ErrorBoundary } from "../components/ErrorBoundary";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <ErrorBoundary>
      <RecordingOverlay />
    </ErrorBoundary>
  </React.StrictMode>,
);
