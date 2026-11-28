import { describe, expect, it } from "vitest";
import {
	type ChannelOption,
	channelLabel,
	type DeliveryChannel,
	formatDateTime,
	preselectedChannel,
} from "./invoiceDetail";

describe("formatDateTime", () => {
	it("shows date and time of a status step", () => {
		expect(formatDateTime("2026-03-05T14:07:09.123")).toBe("05.03.2026 14:07");
	});
});

const usable = (channel: DeliveryChannel): ChannelOption => ({
	channel,
	blockedReason: null,
});
const blocked = (channel: DeliveryChannel): ChannelOption => ({
	channel,
	blockedReason: "nicht möglich",
});

describe("preselectedChannel", () => {
	it("prefers Email when both can be used", () => {
		expect(preselectedChannel([usable("Email"), usable("Post")])).toBe("Email");
	});

	it("falls back to Post when email cannot be used", () => {
		expect(preselectedChannel([blocked("Email"), usable("Post")])).toBe("Post");
	});

	it("is null when no channel can be used", () => {
		expect(preselectedChannel([blocked("Email"), blocked("Post")])).toBeNull();
	});
});

describe("channelLabel", () => {
	it("labels Email as E-Mail and Post as Post", () => {
		expect(channelLabel("Email")).toBe("E-Mail");
		expect(channelLabel("Post")).toBe("Post");
	});
});
