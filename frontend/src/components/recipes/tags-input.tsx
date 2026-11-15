"use client";

import { useState } from "react";
import { X } from "lucide-react";

export function TagsInput({ value, onChange, id }: { value: string[]; onChange: (tags: string[]) => void; id?: string }) {
  const [draft, setDraft] = useState("");

  function commit(text: string) {
    const added = text
      .split(",")
      .map((t) => t.trim().toLowerCase())
      .filter((t) => t && !value.includes(t));
    if (added.length) onChange([...value, ...added].slice(0, 20));
    setDraft("");
  }

  return (
    <div className="flex min-h-11 flex-wrap items-center gap-1.5 rounded-lg border border-input px-2 py-1.5 focus-within:border-ring focus-within:ring-3 focus-within:ring-ring/50">
      {value.map((t) => (
        <span key={t} className="inline-flex h-7 items-center gap-1 rounded-full bg-muted pr-1 pl-2.5 text-sm">
          {t}
          <button
            type="button"
            className="flex size-5 items-center justify-center rounded-full hover:bg-background"
            aria-label={`Remove tag ${t}`}
            onClick={() => onChange(value.filter((x) => x !== t))}
          >
            <X className="size-3" />
          </button>
        </span>
      ))}
      <input
        id={id}
        value={draft}
        placeholder={value.length ? "" : "e.g. weeknight, japanese"}
        className="h-7 min-w-24 flex-1 bg-transparent px-1 text-base outline-none md:text-sm"
        onChange={(e) => (e.target.value.endsWith(",") ? commit(e.target.value) : setDraft(e.target.value))}
        onKeyDown={(e) => {
          if (e.key === "Enter") {
            e.preventDefault();
            commit(draft);
          } else if (e.key === "Backspace" && draft === "" && value.length) {
            onChange(value.slice(0, -1));
          }
        }}
        onBlur={() => draft && commit(draft)}
      />
    </div>
  );
}
