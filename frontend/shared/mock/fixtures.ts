import type {
  AttachedPlace,
  ChatMessage,
  ChatSession,
  SessionHistory,
} from "@/entities/chat-session/model";
import type { Place } from "@/entities/place/model";
import type { PlaceId, SessionId, ActionId, UserId } from "@/shared/lib/brands";
import type { Meeting, Review, SavedPlace, GoogleCalendarStatus } from "@/entities/meeting/model";
import type { User } from "@/entities/user/model";

const TP_HCM = { lat: 10.776, lon: 106.701 };

export const MOCK_USER: User = {
  id: "u-001" as UserId,
  email: "lequantan1974@gmail.com",
  username: "lequantan",
  displayName: "Lê Quân Tân",
  createdAt: "2026-09-16T08:00:00.000Z",
};

export const MOCK_TOKEN = "mock.jwt.token.local-only";

export const MOCK_PLACES: readonly Place[] = [
  {
    id: "p-001" as PlaceId,
    name: "Highlands Coffee Vincom",
    address: "72 Lê Thánh Tôn, Quận 1, TP.HCM",
    location: { lat: 10.772, lon: 106.700 },
    rating: 4.4,
    distanceKm: 0.7,
    categories: ["amenity/cafe"],
    thumbnailUrl: null,
    finalScore: 0.86,
  },
  {
    id: "p-002" as PlaceId,
    name: "The Coffee House Nguyễn Huệ",
    address: "129 Nguyễn Huệ, Quận 1, TP.HCM",
    location: { lat: 10.774, lon: 106.702 },
    rating: 4.5,
    distanceKm: 0.9,
    categories: ["amenity/cafe"],
    thumbnailUrl: null,
    finalScore: 0.84,
  },
  {
    id: "p-003" as PlaceId,
    name: "Phở 25 Bến Nghé",
    address: "25 Bến Nghé, Quận 1, TP.HCM",
    location: { lat: 10.777, lon: 106.704 },
    rating: 4.6,
    distanceKm: 1.1,
    categories: ["amenity/restaurant"],
    thumbnailUrl: null,
    finalScore: 0.82,
  },
  {
    id: "p-004" as PlaceId,
    name: "Cà Kê Café",
    address: "188 Pasteur, Quận 1, TP.HCM",
    location: { lat: 10.775, lon: 106.706 },
    rating: 4.2,
    distanceKm: 1.3,
    categories: ["amenity/cafe"],
    thumbnailUrl: null,
    finalScore: 0.78,
  },
  {
    id: "p-005" as PlaceId,
    name: "OKKIO Cafe",
    address: "120 Nguyễn Thái Bình, Quận 1, TP.HCM",
    location: { lat: 10.770, lon: 106.700 },
    rating: 4.3,
    distanceKm: 1.5,
    categories: ["amenity/cafe"],
    thumbnailUrl: null,
    finalScore: 0.77,
  },
  {
    id: "p-006" as PlaceId,
    name: "Pizza 4P's Lê Thánh Tôn",
    address: "8 Lê Thánh Tôn, Quận 1, TP.HCM",
    location: { lat: 10.773, lon: 106.701 },
    rating: 4.7,
    distanceKm: 0.8,
    categories: ["amenity/restaurant"],
    thumbnailUrl: null,
    finalScore: 0.81,
  },
  {
    id: "p-007" as PlaceId,
    name: "Lẩu Thái Lan Quán",
    address: "32 Thái Văn Lung, Quận 1, TP.HCM",
    location: { lat: 10.776, lon: 106.705 },
    rating: 4.1,
    distanceKm: 1.4,
    categories: ["amenity/restaurant"],
    thumbnailUrl: null,
    finalScore: 0.74,
  },
];

export const MOCK_SAVED_PLACES: readonly SavedPlace[] = [
  {
    savedPlaceId: "sp-001",
    placeId: "p-001",
    name: "Highlands Coffee Vincom",
    address: "72 Lê Thánh Tôn, Quận 1, TP.HCM",
    createdAt: "2026-09-22T03:14:00.000Z",
  },
  {
    savedPlaceId: "sp-002",
    placeId: "p-003",
    name: "Phở 25 Bến Nghé",
    address: "25 Bến Nghé, Quận 1, TP.HCM",
    createdAt: "2026-09-25T10:42:00.000Z",
  },
  {
    savedPlaceId: "sp-003",
    placeId: "p-006",
    name: "Pizza 4P's Lê Thánh Tôn",
    address: "8 Lê Thánh Tôn, Quận 1, TP.HCM",
    createdAt: "2026-09-29T14:08:00.000Z",
  },
];

export const MOCK_REVIEWS: readonly Review[] = [
  {
    id: "r-001",
    placeId: "p-001",
    placeName: "Highlands Coffee Vincom",
    rating: 4,
    comment: "Cà phê ngon, yên tĩnh, hợp làm việc nhóm.",
    createdAt: "2026-09-23T08:30:00.000Z",
  },
  {
    id: "r-002",
    placeId: "p-003",
    placeName: "Phở 25 Bến Nghé",
    rating: 5,
    comment: "Phở bò tái rất ngon, nước dùng đậm đà.",
    createdAt: "2026-09-26T12:00:00.000Z",
  },
  {
    id: "r-003",
    placeId: "p-006",
    placeName: "Pizza 4P's Lê Thánh Tôn",
    rating: 5,
    comment: "Pizza kiểu Nhật, phô mai mozzarella chất lượng cao.",
    createdAt: "2026-09-30T19:45:00.000Z",
  },
];

export const MOCK_MEETINGS_UPCOMING: readonly Meeting[] = [
  {
    actionId: "a-001" as ActionId,
    title: "Họp khách A — Highlands Coffee",
    description: "Bàn về hợp đồng Q4",
    status: "Completed",
    startAt: "2026-10-05T18:00:00.000Z",
    durationMinutes: 60,
    placeId: "p-001",
    placeName: "Highlands Coffee Vincom",
    placeAddress: "72 Lê Thánh Tôn, Quận 1, TP.HCM",
    attendeeEmails: ["a@example.com", "b@example.com"],
    note: "Mang theo bản hợp đồng",
    emailsSent: true,
    createdAt: "2026-10-01T09:00:00.000Z",
  },
  {
    actionId: "a-002" as ActionId,
    title: "Họp team — The Coffee House",
    description: "Retro sprint 18",
    status: "Completed",
    startAt: "2026-10-08T14:00:00.000Z",
    durationMinutes: 90,
    placeId: "p-002",
    placeName: "The Coffee House Nguyễn Huệ",
    placeAddress: "129 Nguyễn Huệ, Quận 1, TP.HCM",
    attendeeEmails: ["team-lead@example.com"],
    note: "",
    emailsSent: false,
    createdAt: "2026-10-02T07:30:00.000Z",
  },
  {
    actionId: "a-003" as ActionId,
    title: "Ăn trưa đối tác — Phở 25",
    description: "Thử phở tái",
    status: "Pending",
    startAt: "2026-10-12T11:30:00.000Z",
    durationMinutes: 60,
    placeId: "p-003",
    placeName: "Phở 25 Bến Nghé",
    placeAddress: "25 Bến Nghé, Quận 1, TP.HCM",
    attendeeEmails: ["partner@example.com"],
    note: "Đặt bàn trước",
    emailsSent: false,
    createdAt: "2026-10-03T03:20:00.000Z",
  },
];

export const MOCK_MEETINGS_PAST: readonly Meeting[] = [
  {
    actionId: "a-101" as ActionId,
    title: "Demo sản phẩm",
    description: "Cho khách hàng Nhật",
    status: "Completed",
    startAt: "2026-09-12T15:00:00.000Z",
    durationMinutes: 60,
    placeId: null,
    placeName: "Văn phòng",
    placeAddress: null,
    attendeeEmails: ["client@example.com"],
    note: "",
    emailsSent: true,
    createdAt: "2026-09-10T09:00:00.000Z",
  },
  {
    actionId: "a-102" as ActionId,
    title: "Lunch & learn",
    description: "Chia sẻ kiến thức mới",
    status: "Completed",
    startAt: "2026-09-05T12:00:00.000Z",
    durationMinutes: 45,
    placeId: null,
    placeName: "Văn phòng",
    placeAddress: null,
    attendeeEmails: [],
    note: "",
    emailsSent: true,
    createdAt: "2026-09-04T10:00:00.000Z",
  },
];

export const MOCK_GOOGLE_CALENDAR_STATUS: GoogleCalendarStatus = {
  connected: true,
  googleEmail: "lequantan1974@gmail.com",
  accessTokenExpiresAt: "2026-11-30T08:00:00.000Z",
};

export const MOCK_CHAT_SESSIONS: readonly ChatSession[] = [
  {
    id: "s-001" as SessionId,
    title: "Tìm quán cà phê gần chợ Bến Thành",
    createdAt: "2026-10-01T03:14:00.000Z",
    updatedAt: "2026-10-01T03:22:00.000Z",
  },
  {
    id: "s-002" as SessionId,
    title: "Đặt lịch hẹn khách hàng A",
    createdAt: "2026-09-28T08:45:00.000Z",
    updatedAt: "2026-09-28T09:10:00.000Z",
  },
  {
    id: "s-003" as SessionId,
    title: "Tìm điểm gặp 3 người",
    createdAt: "2026-09-25T14:20:00.000Z",
    updatedAt: "2026-09-25T14:32:00.000Z",
  },
];

const placesFromCatalog = (ids: readonly string[]): readonly AttachedPlace[] =>
  MOCK_PLACES.filter((p) => ids.includes(p.id)).map((p) => ({
    id: p.id,
    name: p.name,
    address: p.address,
    location: p.location,
    category: p.categories[0] ?? null,
    openingHours: "07:00 - 22:00",
    distanceKm: p.distanceKm,
    finalScore: p.finalScore ?? null,
  }));

export const MOCK_SESSION_HISTORY: SessionHistory = {
  sessionId: "s-001" as SessionId,
  createdAt: "2026-10-01T03:14:00.000Z",
  updatedAt: "2026-10-01T03:22:00.000Z",
  messages: [
    {
      id: "m-001",
      role: "user",
      content: "Tìm giúp tôi nhà hàng gần chợ Bến Thành để mời đối tác ăn tối.",
      createdAt: "2026-10-01T03:14:00.000Z",
    },
    {
      id: "m-002",
      role: "assistant",
      content:
        "Dưới đây là 5 nhà hàng phù hợp gần chợ Bến Thành, được sắp xếp theo điểm phù hợp và khoảng cách.",
      places: placesFromCatalog(["p-003", "p-006", "p-007", "p-001", "p-002"]),
      createdAt: "2026-10-01T03:14:08.000Z",
    },
    {
      id: "m-003",
      role: "user",
      content: "Tôi muốn nhà hàng Việt Nam và gần hơn một chút.",
      createdAt: "2026-10-01T03:21:00.000Z",
    },
    {
      id: "m-004",
      role: "assistant",
      content: "Tôi đã thu hẹp lại theo yêu cầu: chỉ giữ nhà hàng Việt Nam và trong bán kính 1.2 km.",
      places: placesFromCatalog(["p-003"]),
      createdAt: "2026-10-01T03:21:35.000Z",
    },
  ] satisfies readonly ChatMessage[],
};

export const MOCK_CENTER = TP_HCM;

export const MOCK_DEFAULT_LOCATION = TP_HCM;
