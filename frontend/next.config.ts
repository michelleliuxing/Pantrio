import type { NextConfig } from "next";

// The browser only ever talks to this origin. /api/* is proxied server-side to the
// .NET API, so auth cookies are first-party and no CORS is needed.
const apiOrigin = (process.env.API_ORIGIN ?? "http://localhost:5080").replace(/\/$/, "");

const nextConfig: NextConfig = {
  cacheComponents: true,
  partialPrefetching: true,
  poweredByHeader: false,
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
  async rewrites() {
    return [{ source: "/api/:path*", destination: `${apiOrigin}/api/:path*` }];
  },
};

export default nextConfig;
