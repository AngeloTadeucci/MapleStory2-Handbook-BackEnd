// Run against a compiled Handbook server and its configured CDN, without dev hooks.
import { createRequire } from "node:module";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
const frontend =
    process.env.HANDBOOK_FRONTEND ?? "/home/ubuntu/repos/MapleStory2-Handbook";
const { chromium, expect } = createRequire(frontend + "/package.json")(
    "@playwright/test",
);
const output =
    process.env.OUTFIT_EVIDENCE ?? "NifToGltf/obj/release-15-prep/browser";
const url = process.env.OUTFIT_URL ?? "http://127.0.0.1:4003/outfits";
mkdirSync(output, { recursive: true });
const browser = await chromium.launch({
    executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH,
    args: ["--use-angle=swiftshader", "--enable-unsafe-swiftshader"],
});
const page = await browser.newPage({ viewport: { width: 1478, height: 1100 } });
page.setDefaultTimeout(90000);
const errors = [],
    checks = [],
    assets = new Set();
page.on("pageerror", (error) => errors.push(error.message));
page.on("response", (response) => {
    if (response.url().includes("/simulator-release-15/")) {
        assets.add(response.url());
        if (!response.ok())
            errors.push(`${response.status()} ${response.url()}`);
    }
});
await page.route("**/*", (route) =>
    ["GET", "HEAD"].includes(route.request().method())
        ? route.continue()
        : route.abort(),
);
const ready = () =>
    expect(
        page.getByRole("button", { name: "Save image", exact: true }),
    ).toBeEnabled({ timeout: 90000 });
async function equip(slot, id) {
    await page
        .getByRole("button", { name: new RegExp(`^${slot}:`) })
        .first()
        .click();
    await page
        .getByRole("searchbox", { name: "Find an item", exact: true })
        .fill(String(id));
    const choice = page.getByRole("button", { name: /^Equip / }).first();
    await expect(choice).toBeEnabled({ timeout: 90000 });
    const name = (await choice.getAttribute("aria-label")).replace(
        /^Equip /,
        "",
    );
    await choice.click();
    await ready();
    await expect(
        page.getByRole("button", { name: `Remove ${name}`, exact: true }),
    ).toBeVisible();
    checks.push(`Equipped ${id}: ${name}`);
}
async function settings() {
    const details = page.locator(".studio-settings");
    if (!(await details.evaluate((node) => node.open)))
        await details.locator("summary").click();
}
try {
    await page.goto(url, { waitUntil: "domcontentloaded" });
    await ready();
    expect(
        await page
            .locator('[aria-label="Outfit preview"]')
            .evaluate((node) => "outfitViewer" in node),
    ).toBe(false);
    await expect(page.getByRole("button", { name: /^Equip / })).toHaveCount(30);
    for (const body of ["female", "male"]) {
        await page.getByLabel("Body", { exact: true }).selectOption(body);
        await ready();
        await expect(
            page.getByLabel("Pose", { exact: true }).locator("option"),
        ).toHaveCount(40);
        await equip("Hair", body === "female" ? 10200047 : 10200001);
        await equip("Hats", body === "female" ? 11300750 : 11304790);
        await equip("Tops", 11400367);
        await equip("Pants", 11500004);
        await page
            .getByLabel("Pose", { exact: true })
            .selectOption("emotion_dance_b");
        await settings();
        await expect(
            page.getByLabel("Expression", { exact: true }),
        ).toHaveValue("auto");
        await page.getByRole("button", { name: "Pause", exact: true }).click();
        await expect(
            page.getByRole("button", { name: "Play", exact: true }),
        ).toBeVisible();
        for (const angle of ["Front", "Side", "Back"]) {
            await page
                .getByRole("button", { name: angle, exact: true })
                .click();
            await page
                .locator(".fitting-room")
                .screenshot({ path: `${output}/${body}-${angle}.png` });
        }
        await page
            .getByLabel("Background", { exact: true })
            .selectOption("henesys_a");
        await page
            .getByLabel("Expression", { exact: true })
            .selectOption("angry");
        await page
            .getByLabel("Pose", { exact: true })
            .selectOption("emotion_dance_f");
        await expect(
            page.getByLabel("Expression", { exact: true }),
        ).toHaveValue("auto");
        await page
            .getByRole("button", { name: "Import / export", exact: true })
            .click();
        await page
            .getByRole("button", { name: "Export current outfit", exact: true })
            .click();
        const code = await page
            .getByLabel("Outfit code", { exact: true })
            .inputValue();
        expect(code.length).toBeGreaterThan(50);
        await page
            .getByRole("button", { name: "Import outfit", exact: true })
            .click();
        await expect(
            page.getByText(
                "Outfit imported. All items and saved appearance settings restored.",
                { exact: true },
            ),
        ).toBeVisible({ timeout: 90000 });
        await page
            .getByRole("button", { name: "Export current outfit", exact: true })
            .click();
        await expect(
            page.getByLabel("Outfit code", { exact: true }),
        ).toHaveValue(code);
        await page
            .getByRole("button", { name: "Close sharing", exact: true })
            .click();
        const download = page.waitForEvent("download");
        await page
            .getByRole("button", { name: "Save image", exact: true })
            .click();
        const path = `${output}/${body}-export.png`;
        await (await download).saveAs(path);
        expect(readFileSync(path).subarray(0, 8).toString("hex")).toBe(
            "89504e470d0a1a0a",
        );
        checks.push(
            `${body}: 40 poses, expressions, backgrounds, outfit code round-trip, PNG export`,
        );
    }
    await page.setViewportSize({ width: 390, height: 1000 });
    await page
        .getByRole("searchbox", { name: "Find an item", exact: true })
        .fill("");
    await expect(page.getByRole("button", { name: /^Equip / })).toHaveCount(12);
    expect(
        await page.evaluate(
            () => document.documentElement.scrollWidth <= innerWidth,
        ),
    ).toBe(true);
    await page.screenshot({ path: `${output}/mobile.png`, fullPage: true });
    checks.push("Mobile: 12 items per page, no horizontal overflow");
    expect(errors).toEqual([]);
    expect(assets.size).toBeGreaterThan(10);
} catch (error) {
    errors.push(error.stack ?? String(error));
    process.exitCode = 1;
    await page
        .screenshot({ path: `${output}/failure.png`, fullPage: true })
        .catch(() => {});
} finally {
    writeFileSync(
        `${output}/report.json`,
        JSON.stringify({ checks, errors, assets: [...assets] }, null, 2),
    );
    console.log(
        JSON.stringify({ checks, errors, assets: assets.size }, null, 2),
    );
    await browser.close();
}
