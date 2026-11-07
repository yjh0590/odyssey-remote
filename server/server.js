const http = require('http');
const fs = require('fs');
const path = require('path');
const { execFile } = require('child_process');

const PORT = 3000;
const BIN_PATH = path.resolve(__dirname, '../bin/MonitorControl.exe');
const PUBLIC_DIR = path.resolve(__dirname, 'public');

// Samsung Odyssey G50D VCP 0x60 codes:
// PC (DP-1) = 15 (0x0F)
// Nintendo Switch (HDMI-1) = 17 (0x11)
const INPUT_CODES = {
  pc: 15,
  nintendo: 17
};

function runMonitorControl(args) {
  return new Promise((resolve, reject) => {
    execFile(BIN_PATH, args, (error, stdout, stderr) => {
      if (error) {
        // Try parsing stdout even if exit code is non-zero
        try {
          const json = JSON.parse(stdout.trim());
          return resolve(json);
        } catch {
          return reject(error);
        }
      }
      try {
        const json = JSON.parse(stdout.trim());
        resolve(json);
      } catch (e) {
        resolve({ raw: stdout.trim() });
      }
    });
  });
}

const server = http.createServer(async (req, res) => {
  // CORS & Security headers
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type');

  if (req.method === 'OPTIONS') {
    res.writeHead(204);
    res.end();
    return;
  }

  const url = new URL(req.url, `http://${req.headers.host}`);

  // API Endpoints
  if (url.pathname.startsWith('/api/')) {
    res.setHeader('Content-Type', 'application/json; charset=utf-8');

    try {
      if (url.pathname === '/api/status' && req.method === 'GET') {
        const status = await runMonitorControl(['status']);
        res.writeHead(200);
        res.end(JSON.stringify(status));
        return;
      }

      if (url.pathname === '/api/brightness' && req.method === 'POST') {
        let body = '';
        req.on('data', chunk => { body += chunk; });
        req.on('end', async () => {
          try {
            const data = JSON.parse(body || '{}');
            let result;
            if (data.action === 'up') {
              result = await runMonitorControl(['brightness-up', String(data.step || 5)]);
            } else if (data.action === 'down') {
              result = await runMonitorControl(['brightness-down', String(data.step || 5)]);
            } else if (typeof data.value === 'number') {
              result = await runMonitorControl(['set-brightness', String(data.value)]);
            } else {
              res.writeHead(400);
              res.end(JSON.stringify({ error: 'Invalid parameters' }));
              return;
            }
            res.writeHead(200);
            res.end(JSON.stringify(result));
          } catch (err) {
            res.writeHead(500);
            res.end(JSON.stringify({ error: err.message }));
          }
        });
        return;
      }

      if (url.pathname === '/api/input' && req.method === 'POST') {
        let body = '';
        req.on('data', chunk => { body += chunk; });
        req.on('end', async () => {
          try {
            const data = JSON.parse(body || '{}');
            let targetCode;
            if (data.target === 'pc') targetCode = INPUT_CODES.pc;
            else if (data.target === 'nintendo') targetCode = INPUT_CODES.nintendo;
            else if (typeof data.code === 'number') targetCode = data.code;
            else {
              res.writeHead(400);
              res.end(JSON.stringify({ error: 'Invalid input target. Use "pc", "nintendo", or numeric code.' }));
              return;
            }

            const result = await runMonitorControl(['set-input', String(targetCode)]);
            res.writeHead(200);
            res.end(JSON.stringify(result));
          } catch (err) {
            res.writeHead(500);
            res.end(JSON.stringify({ error: err.message }));
          }
        });
        return;
      }

      // Safety test cycle: switch to Nintendo, wait seconds, then switch back to PC
      if (url.pathname === '/api/test-cycle' && req.method === 'POST') {
        let body = '';
        req.on('data', chunk => { body += chunk; });
        req.on('end', async () => {
          try {
            const data = JSON.parse(body || '{}');
            const delaySec = Number(data.delaySec || 5);

            // 1. Switch to Nintendo (17)
            await runMonitorControl(['set-input', String(INPUT_CODES.nintendo)]);

            // 2. Schedule switch back to PC (15)
            setTimeout(async () => {
              try {
                console.log(`[Safety Cycle] Returning to PC (DP ${INPUT_CODES.pc})...`);
                await runMonitorControl(['set-input', String(INPUT_CODES.pc)]);
                console.log(`[Safety Cycle] Successfully returned to PC!`);
              } catch (e) {
                console.error(`[Safety Cycle] Return to PC failed:`, e);
              }
            }, delaySec * 1000);

            res.writeHead(200);
            res.end(JSON.stringify({
              success: true,
              message: `Switched to Nintendo. Will automatically switch back to PC in ${delaySec} seconds.`
            }));
          } catch (err) {
            res.writeHead(500);
            res.end(JSON.stringify({ error: err.message }));
          }
        });
        return;
      }

      res.writeHead(404);
      res.end(JSON.stringify({ error: 'API not found' }));
      return;
    } catch (e) {
      res.writeHead(500);
      res.end(JSON.stringify({ error: e.message }));
      return;
    }
  }

  // Static File Serving
  let filePath = path.join(PUBLIC_DIR, url.pathname === '/' ? 'index.html' : url.pathname);
  if (!filePath.startsWith(PUBLIC_DIR)) {
    res.writeHead(403);
    res.end('Forbidden');
    return;
  }

  const ext = path.extname(filePath).toLowerCase();
  const mimeTypes = {
    '.html': 'text/html; charset=utf-8',
    '.css': 'text/css; charset=utf-8',
    '.js': 'text/javascript; charset=utf-8',
    '.json': 'application/json; charset=utf-8',
    '.png': 'image/png',
    '.svg': 'image/svg+xml',
    '.ico': 'image/x-icon'
  };

  fs.readFile(filePath, (err, content) => {
    if (err) {
      if (err.code === 'ENOENT') {
        res.writeHead(404);
        res.end('Not Found');
      } else {
        res.writeHead(500);
        res.end('Server Error');
      }
      return;
    }
    res.writeHead(200, { 'Content-Type': mimeTypes[ext] || 'application/octet-stream' });
    res.end(content);
  });
});

server.listen(PORT, '0.0.0.0', () => {
  console.log(`=================================================`);
  console.log(` Odyssey G50D Wireless Remote Server Running!`);
  console.log(` Local PC URL : http://localhost:${PORT}`);
  console.log(` iPhone URL   : http://192.168.0.2:${PORT}`);
  console.log(`=================================================`);
});

