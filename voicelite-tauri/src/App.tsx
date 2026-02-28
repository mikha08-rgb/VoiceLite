import { useState } from "react";
import { Sidebar, Page } from "./components/Sidebar";
import { GeneralSettings } from "./components/settings/general/GeneralSettings";
import { AboutSettings } from "./components/settings/about/AboutSettings";
import { Footer } from "./components/footer/Footer";

function App() {
  const [page, setPage] = useState<Page>("general");

  return (
    <div className="flex h-screen bg-[var(--bg-primary)]">
      <Sidebar activePage={page} onNavigate={setPage} />
      <div className="flex-1 flex flex-col min-w-0">
        <main className="flex-1 overflow-y-auto p-6">{renderPage(page)}</main>
        <Footer />
      </div>
    </div>
  );
}

function renderPage(page: Page) {
  switch (page) {
    case "general":
      return <GeneralSettings />;
    case "about":
      return <AboutSettings />;
  }
}

export default App;
