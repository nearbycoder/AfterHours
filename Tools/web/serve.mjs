// A static server shaped like GitHub Pages: the site folder under a case-sensitive /AfterHours/
// path, plain files with no Content-Encoding or COOP/COEP headers, 404 for everything else.
//   node Tools/web/serve.mjs [siteDir=Builds/Pages] [port=8737]
// Also imported by Tools/web/play-test.mjs (serve(dir, port) resolves to the http.Server).
import http from "node:http";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

export const BASE = "/AfterHours/";

const TYPES = {
  ".html": "text/html; charset=utf-8", ".js": "application/javascript", ".css": "text/css",
  ".png": "image/png", ".ico": "image/x-icon", ".json": "application/json", ".wasm": "application/wasm",
  ".unityweb": "application/octet-stream", ".data": "application/octet-stream",
};

export function serve(dir, port, host = "127.0.0.1") {
  const root = path.resolve(dir);
  const server = http.createServer((req, res) => {
    const url = decodeURIComponent(new URL(req.url, "http://x").pathname);
    if (url === BASE.slice(0, -1)) { res.writeHead(301, { Location: BASE }); return res.end(); }
    if (!url.startsWith(BASE)) { res.writeHead(404); return res.end("not found"); }
    let file = path.join(root, url.slice(BASE.length));
    if (!file.startsWith(root)) { res.writeHead(403); return res.end(); }
    if (url.endsWith("/")) file = path.join(file, "index.html");
    fs.stat(file, (err, st) => {
      if (err || !st.isFile()) { res.writeHead(404); return res.end("not found"); }
      res.writeHead(200, {
        "Content-Type": TYPES[path.extname(file)] || "application/octet-stream",
        "Content-Length": st.size,
        "Cache-Control": "max-age=600",
      });
      if (req.method === "HEAD") return res.end();
      fs.createReadStream(file).pipe(res);
    });
  });
  return new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(port, host, () => resolve(server));
  });
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const here = path.dirname(fileURLToPath(import.meta.url));
  const dir = process.argv[2] || path.join(here, "..", "..", "Builds", "Pages");
  const port = Number(process.argv[3] || 8737);
  await serve(dir, port);
  console.log(`serving ${dir} at http://127.0.0.1:${port}${BASE} (pid ${process.pid})`);
}
