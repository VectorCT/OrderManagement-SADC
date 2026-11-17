import { spawnSync } from 'node:child_process';
import { readFileSync, rmSync } from 'node:fs';

const temporarySchema = 'src/api/schema.d.ts.check';
const result = spawnSync(
  process.execPath,
  ['node_modules/openapi-typescript/bin/cli.js', 'http://localhost:5145/swagger/v1/swagger.json', '-o', temporarySchema],
  { stdio: 'inherit', shell: false },
);

if (result.status !== 0) process.exit(result.status ?? 1);

const generated = readFileSync(temporarySchema, 'utf8');
const tracked = readFileSync('src/api/schema.d.ts', 'utf8');
rmSync(temporarySchema);

if (generated !== tracked) {
  console.error('API schema drift detected. Run npm run gen:api and commit src/api/schema.d.ts.');
  process.exit(1);
}
