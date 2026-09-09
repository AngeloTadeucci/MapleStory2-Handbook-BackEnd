// Export model references in one read-only snapshot. Never run GameParser for this audit.
import { createRequire } from 'node:module';
import { writeFileSync } from 'node:fs';
import { resolve } from 'node:path';

const [frontend, output] = process.argv.slice(2);
if (!frontend || !output) throw Error('Usage: node migration_database.mjs frontend output.json');
const require = createRequire(resolve(frontend, 'package.json'));
const mariadb = createRequire(require.resolve('@prisma/adapter-mariadb'))('mariadb');
process.loadEnvFile(resolve(frontend, '.env'));
const url = new URL(process.env.DATABASE_URL);
let connection;
try {
  connection = await mariadb.createConnection({
    host: url.hostname, port: Number(url.port) || 3306,
    user: decodeURIComponent(url.username), password: decodeURIComponent(url.password),
    database: url.pathname.slice(1), connectTimeout: 5000
  });
  await connection.query('START TRANSACTION WITH CONSISTENT SNAPSHOT, READ ONLY');
  const items = await connection.query('SELECT id, kfms, gender FROM items ORDER BY id');
  const npcs = await connection.query('SELECT id, kfm, animations FROM npcs ORDER BY id');
  await connection.query('ROLLBACK');
  writeFileSync(output, JSON.stringify({ version: 1, capturedAt: new Date().toISOString(), items, npcs }, null, 2));
  console.log(JSON.stringify({ items: items.length, npcs: npcs.length }));
} catch {
  // Connection errors can contain credentials or connection details.
  console.error('Read-only model reference export failed. Check local database access.');
  process.exitCode = 1;
} finally {
  await connection?.end();
}
