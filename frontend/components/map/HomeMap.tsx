"use client";

import dynamic from 'next/dynamic'
import SearchBar from '@/components/search/SearchBar';
import { useState } from 'react';
import { mockPlaces } from '@/data/mockPlaces';
import PlaceCard from '../place/PlaceCard';
import SearchResults from '../search/SearchResults';
import type { Place } from '@/types/place';

const MapView = dynamic(() => import("./MapView"), {
    ssr: false,
    loading: () => (
        <div className='h-full w-full bg-slate-100' />
    ),
});

export default function HomeMap() {
    const [places, setPlaces] = useState<Place[]>([]);
    const [selectedPlace, setSelectedPlace] = useState<Place | null>(null);
    const [hasSearched, setHasSearched] = useState(false);

    function handleSearch(query: string) {
        const keyword = query.toLowerCase();
        const result = mockPlaces.filter(
            (place) => 
                place.name.toLowerCase().includes(keyword) ||
                place.category.toLowerCase().includes(keyword)
        );
        setPlaces(result);
        setSelectedPlace(null);
        setHasSearched(true);
    }
    return (
        <main className='fixed inset-0'>
            <MapView
                places={places}
                onSelectPlace={setSelectedPlace}
            />

            <div className='absolute left-4 right-4 top-4 z-[1000] mx-auto max-w-xl'>
                <SearchBar onSearch={handleSearch} />

                {hasSearched && (
                    <SearchResults
                        places={places}
                        onSelectPlace={setSelectedPlace}
                    />
                )}
            </div>            
        </main>
    );
}
