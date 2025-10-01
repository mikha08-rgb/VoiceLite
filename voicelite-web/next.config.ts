import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  async redirects() {
    return [
      {
        source: '/:path*',
        has: [{ type: 'host', value: 'www.voicelite.app' }],
        destination: 'https://voicelite.app/:path*',
        permanent: true,
      },
    ];
  },
};

export default nextConfig;
