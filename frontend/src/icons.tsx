// Minimal inline SVG icon set (stroke-based, currentColor).
const P: Record<string, string> = {
  dashboard: "M3 13h8V3H3v10zm0 8h8v-6H3v6zm10 0h8V11h-8v10zm0-18v6h8V3h-8z",
  briefcase: "M3 7h18v13H3zM8 7V5a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2",
  target: "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18zm0 4a5 5 0 1 0 0 10 5 5 0 0 0 0-10zm0 4a1 1 0 1 0 0 2 1 1 0 0 0 0-2z",
  chat: "M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v10z",
  mail: "M3 5h18v14H3zM3 5l9 7 9-7",
  calendar: "M3 5h18v16H3zM3 9h18M8 3v4M16 3v4",
  building: "M4 21V3h16v18M9 7h2M13 7h2M9 11h2M13 11h2M9 15h2M13 15h2",
  tag: "M20 12l-8 8-9-9V3h8z M7.5 7.5h.01",
  search: "M11 4a7 7 0 1 0 0 14 7 7 0 0 0 0-14zM21 21l-4.3-4.3",
  link: "M10 13a5 5 0 0 0 7 0l3-3a5 5 0 0 0-7-7l-1 1M14 11a5 5 0 0 0-7 0l-3 3a5 5 0 0 0 7 7l1-1",
  user: "M12 12a5 5 0 1 0 0-10 5 5 0 0 0 0 10zM3 21a9 9 0 0 1 18 0",
  bolt: "M13 2L3 14h7l-1 8 10-12h-7z",
  chart: "M4 20V10M10 20V4M16 20v-6M20 20H2",
  doc: "M6 2h9l5 5v15H6zM14 2v6h6",
  bell: "M18 8a6 6 0 1 0-12 0c0 7-3 9-3 9h18s-3-2-3-9M13.7 21a2 2 0 0 1-3.4 0",
  gear: "M12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6zM19 12l2 1-2 4-2-1a7 7 0 0 1-2 1l-1 2H10l-1-2a7 7 0 0 1-2-1l-2 1-2-4 2-1a7 7 0 0 1 0-2L1 9l2-4 2 1a7 7 0 0 1 2-1l1-2h4l1 2a7 7 0 0 1 2 1l2-1 2 4-2 1a7 7 0 0 1 0 2z",
  shield: "M12 3l8 3v6c0 4-3 7-8 9-5-2-8-5-8-9V6z",
  check: "M20 6L9 17l-5-5",
  chevron: "M6 9l6 6 6-6",
  more: "M12 5h.01M12 12h.01M12 19h.01",
  filter: "M3 5h18M6 12h12M10 19h4",
  external: "M10 4H5v15h15v-5M14 4h6v6M20 4l-8 8",
  edit: "M4 20h4L18 10l-4-4L4 16z M14 6l4 4",
  copy: "M9 9h11v11H9zM5 15H4V4h11v1",
  refresh: "M20 11a8 8 0 1 0-2.3 5.7M20 5v6h-6",
  bookmark: "M6 3h12v18l-6-4-6 4z",
};

export function Icon({ name, size = 18 }: { name: string; size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none"
      stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round">
      <path d={P[name] ?? P.dashboard} />
    </svg>
  );
}
