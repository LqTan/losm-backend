"use client";

import { Button } from "@/components/ui/button";
import { Search } from "lucide-react";
import { FormEvent, useState } from "react";

const CATEGORIES = [
  { label: "Cà phê", query: "cafe" },
  { label: "Quán ăn", query: "nhà hàng" },
  { label: "Trà sữa", query: "trà sữa" },
  { label: "Tiệm bánh", query: "tiệm bánh" },
];

type Props = {
  onSearch: (query: string) => void;
  loading?: boolean;
};

export default function SearchBar({ onSearch, loading }: Props) {
  const [query, setQuery] = useState("");
  const [selectedCat, setSelectedCat] = useState("");

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    onSearch(query.trim());
  }

  function handleCategoryClick(catQuery: string) {
    if (selectedCat === catQuery) {
      // Nhấn lần nữa để hủy chọn
      setSelectedCat("");
      setQuery("");
      onSearch("");
    } else {
      setSelectedCat(catQuery);
      setQuery(catQuery);
      onSearch(catQuery);
    }
  }

  return (
    <div className="flex flex-col gap-2 w-full">
      {/* Main Search Input Bar */}
      <div className="flex items-center gap-2">
        <form
          onSubmit={handleSubmit}
          className="flex h-12 flex-1 items-center gap-2.5 rounded-xl bg-card/95 px-3.5 shadow-md border backdrop-blur-md transition-all focus-within:ring-2 focus-within:ring-primary/20"
        >
          <Search className="h-4 w-4 shrink-0 text-muted-foreground" />
          <input
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Tìm cafe, quán ăn, địa điểm..."
            className="h-full flex-1 bg-transparent text-sm text-foreground outline-none placeholder:text-muted-foreground"
          />
          {query && (
            <button
              type="button"
              onClick={() => {
                setQuery("");
                onSearch("");
              }}
              className="text-xs text-muted-foreground hover:text-foreground px-1"
            >
              Xóa
            </button>
          )}
          <Button
            type="submit"
            size="sm"
            disabled={loading}
            className="h-8 rounded-lg px-3 text-xs font-medium"
          >
            {loading ? "Đang tìm..." : "Tìm"}
          </Button>
        </form>
      </div>

      {/* Quick Category Chips */}
      <div className="flex items-center gap-1.5 overflow-x-auto no-scrollbar">
        {CATEGORIES.map((cat, idx) => (
          <button
            key={idx}
            type="button"
            onClick={() => handleCategoryClick(cat.query)}
            className={`rounded-lg px-3 py-1 text-xs font-medium transition border shrink-0 ${
              selectedCat === cat.query
                ? "bg-foreground text-background border-foreground"
                : "bg-card/90 text-muted-foreground border-border hover:text-foreground hover:bg-muted"
            }`}
          >
            {cat.label}
          </button>
        ))}
      </div>
    </div>
  );
}
