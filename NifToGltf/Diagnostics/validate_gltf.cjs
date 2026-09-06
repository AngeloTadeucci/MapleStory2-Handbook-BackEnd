// Install the optional QA dependency in the ignored research directory, see README.md.
const fs = require('node:fs/promises');
const path = require('node:path');
const validator = require('../obj/research/node_modules/gltf-validator');

async function files(input) {
    if (!(await fs.stat(input)).isDirectory()) return [input];
    const result = [];
    for (const entry of await fs.readdir(input, { withFileTypes: true })) {
        const child = path.join(input, entry.name);
        if (entry.isDirectory()) result.push(...await files(child));
        else if (entry.name.endsWith('.gltf')) result.push(child);
    }
    return result;
}

async function main() {
    const [input, output] = process.argv.slice(2);
    if (!input || !output) throw new Error('Usage: node validate_gltf.cjs input-file-or-directory report.json');
    const report = { total: 0, invalid: 0, warningFiles: 0, bad: [] };
    for (const file of await files(input)) {
        const result = await validator.validateBytes(new Uint8Array(await fs.readFile(file)), {
            uri: file,
            maxIssues: 0,
            externalResourceFunction: async (uri) => new Uint8Array(await fs.readFile(path.resolve(path.dirname(file), decodeURIComponent(uri))))
        });
        report.total++;
        if (result.issues.numErrors) report.invalid++;
        if (result.issues.numWarnings) report.warningFiles++;
        if (result.issues.numErrors || result.issues.numWarnings) {
            report.bad.push({ path: file, issues: {
                ...result.issues, messages: result.issues.messages.filter(message => message.severity < 2)
            } });
        }
        if (report.total % 250 === 0) console.log(`Validated ${report.total}`);
    }
    await fs.writeFile(output, JSON.stringify(report, null, 2) + '\n');
    console.log(JSON.stringify({ total: report.total, invalid: report.invalid, warningFiles: report.warningFiles }));
    process.exitCode = report.invalid ? 1 : 0;
}
main().catch(error => { console.error(error); process.exitCode = 1; });
