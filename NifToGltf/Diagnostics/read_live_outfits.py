"""Read selected appearance records on lith. No server files or database rows are written."""
import json
import base64
import zlib
import os
from pathlib import Path
import shlex
import subprocess
import sys

env = {}
for line in Path('/home/collab/MapleStory2/ops/shared/.env').read_text().splitlines():
    if '=' not in line or line.lstrip().startswith('#'):
        continue
    key, value = line.split('=', 1)
    if key.strip() not in {'DB_IP', 'DB_PORT', 'DB_USER', 'DB_PASSWORD', 'GAME_DB_NAME'}:
        continue
    parts = shlex.split(value, comments=True)
    env[key.strip()] = parts[0] if parts else ''

names = "'Blaze','EIIie','Chiisa','Skillet','Asthoria','GolemSoldier','Areki','Gelo','Tree','Robbit'"
sql = '''START TRANSACTION READ ONLY;
SELECT @@hostname, DATABASE();
SHOW COLUMNS FROM `character`;
SHOW COLUMNS FROM `item`;
ROLLBACK;'''
if '--snapshot' in sys.argv:
    sql = f'''START TRANSACTION WITH CONSISTENT SNAPSHOT, READ ONLY;
SELECT JSON_OBJECT('kind','character','name',Name,'id',CAST(Id AS CHAR),'gender',Gender,'level',Level,'skin',SkinColor,'savedAt',LastModified)
FROM `character` WHERE Name IN ({names});
SELECT JSON_OBJECT('kind','item','name',c.Name,'itemId',i.ItemId,'slot',i.Slot,'group',i.`Group`,'appearance',i.Appearance,'subType',i.SubType)
FROM `item` i JOIN `character` c ON i.OwnerId=c.Id
WHERE c.Name IN ({names}) AND i.`Group` IN (1,2,3,4) ORDER BY c.Name,i.`Group`,i.Slot;
ROLLBACK;'''
result = subprocess.run(['mysql', '--protocol=TCP', '--host='+env['DB_IP'], '--port='+env['DB_PORT'],
    '--user='+env['DB_USER'], '--database='+env['GAME_DB_NAME'], '--batch', '--raw', '--skip-column-names'],
    input=sql, text=True, capture_output=True, env={**os.environ, 'MYSQL_PWD':env['DB_PASSWORD']})
if result.returncode:
    print('Read-only database query failed.', file=sys.stderr)
    sys.exit(result.returncode)
if '--snapshot' in sys.argv:
    print(base64.b64encode(zlib.compress(result.stdout.encode())).decode())
else:
    print(result.stdout, end='')
