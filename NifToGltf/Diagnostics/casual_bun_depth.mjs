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
const page = await browser.newPage({ viewport: { width: 1478, height: 1100 } });
page.setDefaultTimeout(90000);
const output = "NifToGltf/obj/casual-bun-depth/verified";
mkdirSync(output, { recursive: true });
const errors = [],
    checks = [];
page.on("pageerror", (e) => errors.push(e.message));
page.on("console", (m) => {
    if (m.type() === "error") errors.push(m.text());
});
const preview = () => page.locator('[aria-label="Outfit preview"]');
const ready = () =>
    expect(
        page.getByRole("button", { name: "Save image", exact: true }),
    ).toBeEnabled({ timeout: 90000 });
async function equip() {
    await page
        .getByRole("searchbox", { name: "Find an item", exact: true })
        .fill("10200047");
    await page
        .getByRole("button", { name: "Equip Casual Bun", exact: true })
        .click();
    await ready();
}
try {
    await page.goto("http://100.118.72.53:4000/outfits");
    await ready();
    await equip();
    for (const length of ["default", "minimum", "maximum"]) {
        const states = await preview().evaluate((el, length) => {
            const v = el.outfitViewer;
            v.selectClip("fitting_idle_a");
            v.seek(0.65);
            for (const c of v.hairControls) {
                if (length === "default") c.reset();
                else if (c.range)
                    c.set(length === "minimum" ? c.range.min : c.range.max);
            }
            const hair = [];
            v.scene.traverse((n) => {
                if (n.isMesh)
                    for (const m of Array.isArray(n.material)
                        ? n.material
                        : [n.material])
                        if (m.userData.nifShader === "MS2CharacterHairMaterial")
                            hair.push({
                                name: n.name,
                                depthWrite: m.depthWrite,
                                depthTest: m.depthTest,
                                transparent: m.transparent,
                                state: m.userData.nifRenderState,
                            });
            });
            return {
                hair,
                lengths: v.hairControls.map((c) => ({
                    label: c.label,
                    value: c.value,
                })),
            };
        }, length);
        expect(states.hair.length).toBeGreaterThan(0);
        for (const m of states.hair) {
            expect(m.depthWrite).toBe(true);
            expect(m.depthTest).toBe(true);
            expect(m.transparent).toBe(true);
            expect(m.state).toEqual({
                alphaFlags: 4845,
                alphaThreshold: 0,
                depthFlags: 15,
            });
        }
        checks.push({ length, ...states });
        for (const angle of [0, 0.6, -0.6, Math.PI]) {
            await preview().evaluate((el, angle) => {
                const v = el.outfitViewer;
                v.view("front");
                const t = v.controls.target,
                    p = v.camera.position,
                    x = p.x - t.x,
                    z = p.z - t.z;
                p.x = t.x + x * Math.cos(angle) + z * Math.sin(angle);
                p.z = t.z - x * Math.sin(angle) + z * Math.cos(angle);
                v.controls.update();
                v.renderer.render(v.scene, v.camera);
            }, angle);
            await page
                .locator(".fitting-room")
                .screenshot({ path: `${output}/${length}-${angle}.png` });
        }
    }
    await page
        .getByRole("button", { name: "Unequip Hair", exact: true })
        .click();
    await ready();
    await equip();
    expect(
        await preview().evaluate((el) => {
            let valid = true;
            el.outfitViewer.scene.traverse((n) => {
                if (n.isMesh)
                    for (const m of Array.isArray(n.material)
                        ? n.material
                        : [n.material])
                        if (m.userData.nifShader === "MS2CharacterHairMaterial")
                            valid &&= m.depthWrite;
            });
            return valid;
        }),
    ).toBe(true);
    await page
        .getByLabel("Pose", { exact: true })
        .selectOption("emotion_dance_b");
    await page.waitForTimeout(300);
    await page.setViewportSize({ width: 390, height: 1100 });
    await page
        .locator(".fitting-room")
        .screenshot({ path: output + "/mobile-dance.png" });
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
