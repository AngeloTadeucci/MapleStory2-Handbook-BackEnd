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
const output = "NifToGltf/obj/character-animations/pose-faces";
mkdirSync(output, { recursive: true });
const errors = [],
    checks = [];
page.on("pageerror", (e) => errors.push(e.message));
const preview = () => page.locator('[aria-label="Outfit preview"]');
const ready = () =>
    expect(
        page.getByRole("button", { name: "Save image", exact: true }),
    ).toBeEnabled({ timeout: 90000 });
async function faceSample(time) {
    return preview().evaluate((el, time) => {
        const v = el.outfitViewer;
        v.seek(time);
        const f = v.face ?? v.defaultFace;
        const sequence =
            f.preset.poseExpressions[f.clip] ?? f.preset.sequences.default;
        let elapsed = f.elapsed,
            total = sequence.frames.reduce(
                (sum, frame) => sum + frame.duration,
                0,
            );
        if (sequence.repeat) elapsed %= total;
        let index = sequence.frames.length - 1;
        for (let i = 0; i < sequence.frames.length; i++) {
            if (elapsed < sequence.frames[i].duration) {
                index = i;
                break;
            }
            elapsed -= sequence.frames[i].duration;
        }
        const frame = sequence.frames[index],
            expected = f.images.get(frame.image + "|" + frame.mask).texture;
        return {
            clip: f.clip,
            mode: f.expression,
            elapsed: f.elapsed,
            image: frame.image,
            matched: [...f.originals.keys()].every((m) => m.map === expected),
            count: sequence.frames.length,
        };
    }, time);
}
try {
    await page.goto("http://100.118.72.53:4000/outfits");
    await ready();
    for (const body of ["female", "male"]) {
        await page.getByLabel("Body", { exact: true }).selectOption(body);
        await ready();
        for (const clip of [
            "emotion_dance_a",
            "emotion_dance_b",
            "emotion_dance_c",
            "emotion_dance_d",
            "emotion_dance_e",
            "emotion_dance_f",
            "emotion_dance_g",
            "emotion_dance_h",
            "emotion_dance_t",
            "emotion_dance_v",
        ]) {
            await page.getByLabel("Pose", { exact: true }).selectOption(clip);
            for (const t of [0.01, 0.15, 0.29, 0.43]) {
                const s = await faceSample(t);
                expect(s.mode).toBe("auto");
                expect(s.clip).toBe(clip);
                expect(s.matched).toBe(true);
                checks.push({ body, ...s });
            }
            if (["emotion_dance_a", "emotion_dance_f"].includes(clip))
                await page
                    .locator(".fitting-room")
                    .screenshot({ path: `${output}/${body}-${clip}.png` });
        }
        await page
            .getByLabel("Pose", { exact: true })
            .selectOption("emotion_dance_f");
        await page.getByRole("button", { name: "Pause", exact: true }).click();
        const before = await preview().evaluate((el) => {
            const v = el.outfitViewer;
            return {
                time: v.mixer.time,
                face: (v.face ?? v.defaultFace).elapsed,
            };
        });
        await page.waitForTimeout(250);
        expect(
            await preview().evaluate((el) => {
                const v = el.outfitViewer;
                return {
                    time: v.mixer.time,
                    face: (v.face ?? v.defaultFace).elapsed,
                };
            }),
        ).toEqual(before);
        await page.getByRole("button", { name: "Play", exact: true }).click();
        await expect
            .poll(() => preview().evaluate((el) => el.outfitViewer.mixer.time))
            .toBeGreaterThan(before.time);
        if (!(await page.locator(".studio-settings").evaluate((el) => el.open)))
            await page.locator(".studio-settings summary").click();
        await expect(
            page.getByLabel("Expression", { exact: true }),
        ).toHaveValue("auto");
        await page
            .getByLabel("Expression", { exact: true })
            .selectOption("angry");
        expect(
            await preview().evaluate(
                (el) =>
                    (el.outfitViewer.face ?? el.outfitViewer.defaultFace)
                        .expression,
            ),
        ).toBe("angry");
        await page
            .getByLabel("Pose", { exact: true })
            .selectOption("emotion_dance_b");
        await expect(
            page.getByLabel("Expression", { exact: true }),
        ).toHaveValue("auto");
        await page.getByRole("button", { name: /^Face:/ }).click();
        await page
            .getByRole("searchbox", { name: "Find an item", exact: true })
            .fill(body === "female" ? "10300004" : "10300002");
        await page
            .getByRole("button", { name: /^Equip / })
            .first()
            .click();
        await ready();
        expect((await faceSample(0.15)).matched).toBe(true);
        expect((await faceSample(0.15)).clip).toBe("emotion_dance_b");
        await page
            .getByRole("button", { name: "Unequip Face", exact: true })
            .click();
        await ready();
        expect((await faceSample(0.15)).matched).toBe(true);
    }
    await page
        .getByRole("button", { name: "Import / export", exact: true })
        .click();
    await page
        .getByRole("button", { name: "Export current outfit", exact: true })
        .click();
    const code = await page
        .getByLabel("Outfit code", { exact: true })
        .inputValue();
    await page
        .getByRole("button", { name: "Import outfit", exact: true })
        .click();
    await ready();
    await expect(
        page.getByText(
            "Outfit imported. All items and saved appearance settings restored.",
            { exact: true },
        ),
    ).toBeVisible();
    await expect(page.getByLabel("Expression", { exact: true })).toHaveValue(
        "auto",
    );
    await page
        .getByRole("button", { name: "Export current outfit", exact: true })
        .click();
    expect(
        await page.getByLabel("Outfit code", { exact: true }).inputValue(),
    ).toBe(code);
    await page
        .getByRole("button", { name: "Close sharing", exact: true })
        .click();
    const sizes = await page
        .locator(
            ".pose-control,.studio-settings .dye-control,.studio-settings .swatch-grid .swatch",
        )
        .evaluateAll((es) =>
            es
                .slice(0, 5)
                .map((e) => ({
                    class: e.className,
                    width: e.getBoundingClientRect().width,
                    height: e.getBoundingClientRect().height,
                })),
        );
    await page
        .locator(".studio-settings")
        .screenshot({ path: output + "/settings-desktop.png" });
    checks.push({ sizes });
    await page.setViewportSize({ width: 390, height: 844 });
    await page
        .getByLabel("Pose", { exact: true })
        .selectOption("emotion_dance_f");
    expect(
        await page.evaluate(
            () => document.documentElement.scrollWidth <= innerWidth,
        ),
    ).toBe(true);
    await page
        .locator(".studio-settings")
        .screenshot({ path: output + "/settings-mobile.png" });
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
    console.log(JSON.stringify({ checks: checks.length, errors }, null, 2));
    await browser.close();
}
