#!/usr/bin/env python3
"""Serve the Phase 9 Unity WebGL build with Brotli headers."""

from __future__ import annotations

import argparse
import functools
import http.server
import socketserver
from pathlib import Path
from urllib.parse import unquote, urlsplit


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_BUILD_DIR = ROOT / "webgl-phase9"


class UnityWebGLHandler(http.server.SimpleHTTPRequestHandler):
    extensions_map = {
        **http.server.SimpleHTTPRequestHandler.extensions_map,
        ".mp4": "video/mp4",
        ".js": "application/javascript",
        ".wasm": "application/wasm",
        ".data": "application/octet-stream",
        ".br": "application/octet-stream",
    }

    def guess_type(self, path: str) -> str:
        if path.endswith(".wasm.br"):
            return "application/wasm"
        if path.endswith(".js.br") or path.endswith(".loader.js"):
            return "application/javascript"
        if path.endswith(".data.br"):
            return "application/octet-stream"
        if path.endswith(".mp4"):
            return "video/mp4"
        return super().guess_type(path)

    def end_headers(self) -> None:
        request_path = unquote(urlsplit(self.path).path)

        if request_path.endswith(".br"):
            self.send_header("Content-Encoding", "br")

        if request_path in ("", "/", "/index.html"):
            self.send_header("Cache-Control", "no-store, no-cache, must-revalidate, max-age=0")
            self.send_header("Pragma", "no-cache")
            self.send_header("Expires", "0")
        elif "/Build/" in request_path:
            self.send_header("Cache-Control", "public, max-age=31536000, immutable")
        else:
            self.send_header("Cache-Control", "no-cache")

        super().end_headers()


class ReusableTCPServer(socketserver.TCPServer):
    allow_reuse_address = True


def main() -> None:
    parser = argparse.ArgumentParser(description="Serve Unity WebGL build with correct Brotli headers.")
    parser.add_argument("--dir", default=str(DEFAULT_BUILD_DIR), help="Build folder to serve.")
    parser.add_argument("--host", default="127.0.0.1", help="Host/interface to bind.")
    parser.add_argument("--port", default=8080, type=int, help="Port to bind.")
    args = parser.parse_args()

    build_dir = Path(args.dir).resolve()
    if not build_dir.is_dir():
        raise SystemExit(f"Build directory does not exist: {build_dir}")

    handler = functools.partial(UnityWebGLHandler, directory=str(build_dir))

    with ReusableTCPServer((args.host, args.port), handler) as httpd:
        url = f"http://{args.host}:{args.port}/"
        print(f"Serving Unity WebGL build from: {build_dir}", flush=True)
        print(f"Open in Chrome: {url}", flush=True)
        print("Press Control+C to stop.", flush=True)
        try:
            httpd.serve_forever()
        except KeyboardInterrupt:
            print("\nServer stopped.", flush=True)


if __name__ == "__main__":
    main()
