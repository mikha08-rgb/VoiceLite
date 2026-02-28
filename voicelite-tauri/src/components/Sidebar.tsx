import { Settings, Info } from "lucide-react";
import { ReactNode } from "react";

export type Page = "general" | "about";

interface NavItem {
  id: Page;
  label: string;
  icon: ReactNode;
}

const sections: { title: string; items: NavItem[] }[] = [
  {
    title: "Settings",
    items: [
      { id: "general", label: "General", icon: <Settings size={16} /> },
    ],
  },
  {
    title: "Info",
    items: [
      { id: "about", label: "About", icon: <Info size={16} /> },
    ],
  },
];

interface Props {
  activePage: Page;
  onNavigate: (page: Page) => void;
}

export function Sidebar({ activePage, onNavigate }: Props) {
  return (
    <nav className="w-48 bg-[var(--bg-secondary)] border-r border-[var(--border)] p-3 flex flex-col">
      <div className="text-base font-bold text-[var(--text-primary)] px-2 py-3">
        VoiceLite
      </div>
      {sections.map((section) => (
        <div key={section.title} className="mb-4">
          <div className="text-[10px] font-semibold uppercase tracking-wider text-[var(--text-secondary)] px-2 mb-1">
            {section.title}
          </div>
          {section.items.map((item) => (
            <button
              key={item.id}
              onClick={() => onNavigate(item.id)}
              className={`
                w-full flex items-center gap-2 px-2 py-1.5 rounded-md text-sm
                transition-colors duration-100
                ${
                  activePage === item.id
                    ? "bg-[var(--accent)] text-white"
                    : "text-[var(--text-secondary)] hover:bg-[var(--bg-tertiary)] hover:text-[var(--text-primary)]"
                }
              `}
            >
              {item.icon}
              {item.label}
            </button>
          ))}
        </div>
      ))}
    </nav>
  );
}
