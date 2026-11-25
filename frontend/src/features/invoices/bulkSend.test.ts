import { describe, expect, it } from "vitest";
import { bulkSendCounts, bulkSendNeedsMessage } from "./bulkSend";

describe("bulkSendCounts", () => {
	it("shows how many Invoices go by email, by post and not at all", () => {
		expect(bulkSendCounts({ email: 12, post: 3, notSendable: 1 })).toBe(
			"12 per E-Mail, 3 per Post, 1 nicht versendbar",
		);
	});
});

describe("bulkSendNeedsMessage", () => {
	it("asks for subject and text only when at least one Invoice goes by email", () => {
		expect(bulkSendNeedsMessage({ email: 1, post: 0, notSendable: 0 })).toBe(
			true,
		);
		expect(bulkSendNeedsMessage({ email: 0, post: 4, notSendable: 2 })).toBe(
			false,
		);
	});
});
