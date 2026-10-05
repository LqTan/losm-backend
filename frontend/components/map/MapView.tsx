"use client"
import { CircleMarker, MapContainer, Popup, TileLayer } from 'react-leaflet';
import CurrentLocation from './CurrentLocation';

const DEFAULT_CENTER: [number, number] = [
    10.7709,
    106.7009
];

type Props = {
    places: {
        id: number;
        name: string;
        category: string;
        latitude: number;
        longitude: number;
    }[];

    onSelectPlace: (place: Props["places"][number]) => void;
};

export default function MapView({ places, onSelectPlace }: Props) {
    return (
        <MapContainer
            center={DEFAULT_CENTER}
            zoom={14}
            zoomControl={false}
            className='h-full w-full'
        >
            <TileLayer
                attribution='&copy; OpenStreetMap contributors'
                url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            />

            {places.map((place) => (
                <CircleMarker
                    key={place.id}
                    center={[place.latitude, place.longitude]}
                    radius={9}
                    eventHandlers={{
                        click: () => onSelectPlace(place),
                    }}
                >
                    <Popup>{place.name}</Popup>
                </CircleMarker>
            ))}
            <CurrentLocation />
        </MapContainer>
    )
}
