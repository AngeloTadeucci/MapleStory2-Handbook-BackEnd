import { createRequire } from "node:module";
import { mkdirSync, writeFileSync } from "node:fs";
const { chromium, expect } = createRequire(
    "/home/ubuntu/repos/MapleStory2-Handbook/package.json",
)("@playwright/test");
const browser = await chromium.launch({
    executablePath:
        "/home/ubuntu/.cache/ms-playwright/chromium-1243/chrome-linux-arm64/chrome",
    headless: true,
    args: ["--use-angle=swiftshader", "--enable-unsafe-swiftshader"],
});
const page = await browser.newPage({ viewport: { width: 1440, height: 1100 } });
page.setDefaultTimeout(90000);
const output =
    process.env.ANIMATION_EVIDENCE ??
    "NifToGltf/obj/character-animations/browser";
mkdirSync(output, { recursive: true });
const errors = [],
    samples = [];
page.on("pageerror", (error) => errors.push(error.message));
page.on("console", (message) => {
    if (
        message.type() === "error" ||
        /No target node|not found.*track/i.test(message.text())
    )
        errors.push(message.text());
});
const ready = () =>
    expect(
        page.getByRole("button", { name: "Save image", exact: true }),
    ).toBeEnabled({ timeout: 90000 });
const preview = () => page.locator('[aria-label="Outfit preview"]');
async function equip(slot, id) {
    await page
        .getByRole("button", { name: new RegExp("^" + slot + ":") })
        .click();
    await page
        .getByRole("searchbox", { name: "Find an item", exact: true })
        .fill(String(id));
    await page
        .getByRole("button", { name: /^Equip / })
        .first()
        .click();
    await ready();
}
try {
    await page.goto(
        process.env.OUTFIT_URL ?? "http://100.118.72.53:4000/outfits",
    );
    await ready();
    for (const body of ["female", "male"]) {
        await page.getByLabel("Body", { exact: true }).selectOption(body);
        await ready();
        await equip("Tops", 11400001);
        await equip("Pants", 11500001);
        const names = await page
            .getByLabel("Pose", { exact: true })
            .locator("option")
            .evaluateAll((options) => options.map((option) => option.value));
        expect(names).toHaveLength(40);
        expect(
            await page
                .getByLabel("Pose", { exact: true })
                .locator("optgroup")
                .count(),
        ).toBe(4);
        for (const name of names) {
            await page.getByLabel("Pose", { exact: true }).selectOption(name);
            const sample = await preview().evaluate((element, name) => {
                const viewer = element.outfitViewer;
                const clip = viewer.body.animations.find(
                    (clip) => clip.name === name,
                );
                const snapshots = [0.15, 0.65].map((fraction) => {
                    viewer.seek(clip.duration * fraction);
                    const bones = [];
                    viewer.body.scene.traverse((node) => {
                        if (node.isBone)
                            bones.push(...node.matrixWorld.elements);
                    });
                    let vertices = 0,
                        finite = true;
                    viewer.scene.traverse((node) => {
                        if (!node.isSkinnedMesh || !node.visible) return;
                        for (
                            let i = 0;
                            i < node.geometry.attributes.position.count;
                            i += 17
                        ) {
                            const point = node.getVertexPosition(
                                i,
                                node.position.clone(),
                            );
                            finite &&= [point.x, point.y, point.z].every(
                                Number.isFinite,
                            );
                            vertices++;
                        }
                    });
                    return { bones, vertices, finite };
                });
                return {
                    name,
                    duration: clip.duration,
                    tracks: clip.tracks.length,
                    moved:
                        JSON.stringify(snapshots[0].bones) !==
                        JSON.stringify(snapshots[1].bones),
                    finite: snapshots.every((s) => s.finite && s.vertices > 0),
                    equipment: viewer.equippedItems.length,
                };
            }, name);
            expect(sample.duration).toBeGreaterThan(0);
            expect(sample.tracks).toBeGreaterThan(0);
            expect(sample.finite).toBe(true);
            expect(sample.moved, `${body} ${name} moves`).toBe(true);
            expect(sample.equipment).toBe(2);
            samples.push({ body, ...sample });
            if (
                [
                    "emotion_hello_a",
                    "emotion_dance_a",
                    "sit_ground_idle_a",
                    "emotion_gymnastics_a",
                ].includes(name)
            ) {
                await page
                    .locator(".fitting-room")
                    .screenshot({ path: `${output}/${body}-${name}.png` });
            }
        }
        await page
            .getByLabel("Pose", { exact: true })
            .selectOption("emotion_dance_b");
        await page.getByRole("button", { name: "Pause", exact: true }).click();
        const time = await preview().evaluate(
            (el) => el.outfitViewer.mixer.time,
        );
        await page.waitForTimeout(250);
        expect(
            await preview().evaluate((el) => el.outfitViewer.mixer.time),
        ).toBe(time);
        await page.getByRole("button", { name: "Play", exact: true }).click();
        await expect
            .poll(() => preview().evaluate((el) => el.outfitViewer.mixer.time))
            .toBeGreaterThan(time);
    }
    await page.setViewportSize({ width: 390, height: 844 });
    await page
        .getByLabel("Pose", { exact: true })
        .selectOption("emotion_hello_a");
    expect(
        await page.evaluate(
            () => document.documentElement.scrollWidth <= innerWidth,
        ),
    ).toBe(true);
    await page.screenshot({ path: `${output}/mobile.png` });
    expect(errors).toEqual([]);
} catch (error) {
    errors.push(error.stack ?? String(error));
    process.exitCode = 1;
    await page
        .screenshot({ path: `${output}/failure.png`, timeout: 10000 })
        .catch(() => {});
} finally {
    writeFileSync(
        `${output}/report.json`,
        JSON.stringify({ samples, errors }, null, 2),
    );
    console.log(JSON.stringify({ checked: samples.length, errors }, null, 2));
    await browser.close();
}
