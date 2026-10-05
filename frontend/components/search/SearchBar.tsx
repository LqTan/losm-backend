"use client";

import { FormEvent, useState } from "react";
import { Search } from 'lucide-react';

type Props = {
    onSearch: (query: string) => void;
};

export default function SearchBar({ onSearch }: Props) {
    const [query, setQuery] = useState("");

    function handleSubmit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        onSearch(query.trim());
    }

    return (
        <form
            onSubmit={handleSubmit}
            className="flex h-14 w-full items-center gap-3 rounded-full bg-white px-5 shadow-lg"
        >
            <Search className="h-5 w-5 shrink-0 text-gray-500" />
            <input
                value={query}
                onChange={(event) => setQuery(event.target.value)}
                placeholder="Tìm kiếm địa điểm..."
                className="h-full flex-1 bg-transparent text-[15px] text-gray-900 outline-none placeholder:text-gray-500"
            />
        </form>
    );
}
