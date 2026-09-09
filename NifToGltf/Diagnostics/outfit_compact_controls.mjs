import { createRequire } from "node:module";
import { mkdirSync, writeFileSync } from "node:fs";
const { chromium, expect } = createRequire(
    "/home/ubuntu/repos/MapleStory2-Handbook/package.json",
)("@playwright/test");
const browser = await chromium.launch({
    executablePath:
        "/home/ubuntu/.cache/ms-playwright/chromium-1243/chrome-linux-arm64/chrome",
    args: ["--use-angle=swiftshader", "--enable-unsafe-swiftshader"],
});
const page = await browser.newPage({ viewport: { width: 1440, height: 1100 } });
page.setDefaultTimeout(90000);
const output = "NifToGltf/obj/character-animations/compact-controls";
mkdirSync(output, { recursive: true });
const errors = [],
    checks = [];
page.on("pageerror", (e) => errors.push(e.message));
try {
    await page.goto("http://100.118.72.53:4000/outfits");
    await expect(
        page.getByRole("button", { name: "Save image", exact: true }),
    ).toBeEnabled({ timeout: 90000 });
    await page.locator(".studio-settings summary").click();
    for (const width of [1440, 390]) {
        await page.setViewportSize({ width, height: 1100 });
        const sizes = await page
            .locator(".pose-control,.studio-settings .dye-control")
            .evaluateAll((es) =>
                es.map((e) => ({
                    width: e.getBoundingClientRect().width,
                    class: e.className,
                })),
            );
        expect(sizes[0].width).toBeLessThanOrEqual(300);
        for (const size of sizes.slice(1))
            expect(size.width).toBeLessThanOrEqual(384);
        expect(
            await page.evaluate(
                () => document.documentElement.scrollWidth <= innerWidth,
            ),
        ).toBe(true);
        checks.push({ width, sizes });
        for (const name of ["Skin", "Eyes"]) {
            const panel = page.getByRole("group", {
                name: `${name} colors`,
                exact: true,
            });
            const colors = () =>
                page
                    .locator('[aria-label="Outfit preview"]')
                    .evaluate(
                        (el, name) =>
                            el.outfitViewer.colorControls.find(
                                (c) => c.label === name,
                            ).colors,
                        name,
                    );
            const before = await colors();
            await panel
                .locator(".swatch-grid .swatch")
                .nth(width === 1440 ? 3 : 5)
                .click();
            expect(await colors()).not.toEqual(before);
            await panel
                .getByRole("radio", { name: "Custom", exact: true })
                .check();
            const red = panel.getByRole("spinbutton", {
                name: `${name} Primary R`,
                exact: true,
            });
            await red.fill("123");
            await red.blur();
            expect((await colors())[0][0]).toBeCloseTo(123 / 255);
            await panel.screenshot({
                path: `${output}/${width}-${name}-custom.png`,
            });
            await panel
                .getByRole("radio", { name: "Basic colors", exact: true })
                .check();
        }
        await page
            .locator(".studio-settings")
            .screenshot({ path: `${output}/${width}-settings.png` });
        await page.locator(".viewer-controls").count();
        await page
            .getByLabel("Pose", { exact: true })
            .selectOption("emotion_dance_t");
        await page.getByLabel("Pose", { exact: true }).scrollIntoViewIfNeeded();
        await page.screenshot({ path: `${output}/${width}-page.png` });
    }
    expect(errors).toEqual([]);
} catch (e) {
    errors.push(e.stack ?? String(e));
    process.exitCode = 1;
    await page
        .screenshot({ path: output + "/failure.png", timeout: 10000 })
        .catch(() => {});
} finally {
    writeFileSync(
        output + "/report.json",
        JSON.stringify({ checks, errors }, null, 2),
    );
    console.log(JSON.stringify({ checks, errors }, null, 2));
    await browser.close();
}
