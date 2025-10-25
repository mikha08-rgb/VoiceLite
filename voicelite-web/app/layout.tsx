import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import "./globals.css";
import { ThemeProvider } from "@/components/theme-provider";
import "@/lib/env-validation"; // Validate environment on app startup

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

const GA_MEASUREMENT_ID = process.env.NEXT_PUBLIC_GA_MEASUREMENT_ID?.trim();

export const metadata: Metadata = {
  title: "VoiceLite - Instant Offline Voice Typing for Windows | Privacy-First Speech-to-Text",
  description: "Turn your voice into text instantly in ANY Windows app. 100% offline speech-to-text with OpenAI Whisper AI. Global hotkey, <200ms latency. No internet required, your voice never leaves your PC.",
  keywords: [
    "voice typing",
    "speech to text",
    "Windows voice typing",
    "offline transcription",
    "Whisper AI",
    "voice recognition",
    "privacy speech to text",
    "offline voice typing",
    "voice to text Windows",
    "dictation software",
    "global hotkey typing",
    "fast speech to text",
  ],
  authors: [{ name: "VoiceLite Team" }],
  creator: "VoiceLite",
  publisher: "VoiceLite",
  metadataBase: new URL("https://voicelite.app"),
  openGraph: {
    title: "VoiceLite - Instant Offline Voice Typing for Windows",
    description: "Turn your voice into text instantly in ANY Windows app. 100% offline with OpenAI Whisper AI. No internet required. <200ms latency. Your voice never leaves your PC.",
    type: "website",
    locale: "en_US",
    url: "https://voicelite.app",
    siteName: "VoiceLite",
    images: [
      {
        url: "/og-image.png",
        width: 1200,
        height: 630,
        alt: "VoiceLite - Instant Voice Typing for Windows",
      },
    ],
  },
  twitter: {
    card: "summary_large_image",
    title: "VoiceLite - Instant Offline Voice Typing for Windows",
    description: "Turn your voice into text instantly. 100% offline, works in any app. <200ms latency. Your voice never leaves your PC.",
    images: ["/og-image.png"],
    creator: "@voicelite",
  },
  robots: {
    index: true,
    follow: true,
    googleBot: {
      index: true,
      follow: true,
      "max-video-preview": -1,
      "max-image-preview": "large",
      "max-snippet": -1,
    },
  },
  alternates: {
    canonical: "https://voicelite.app",
  },
  category: "productivity",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" suppressHydrationWarning>
      <head>
        {GA_MEASUREMENT_ID && (
          <>
            <script
              async
              src={`https://www.googletagmanager.com/gtag/js?id=${GA_MEASUREMENT_ID}`}
            />
            <script
              dangerouslySetInnerHTML={{
                __html: `
                  window.dataLayer = window.dataLayer || [];
                  function gtag(){dataLayer.push(arguments);}
                  gtag('js', new Date());
                  gtag('config', '${GA_MEASUREMENT_ID}');
                `,
              }}
            />
          </>
        )}
      </head>
      <body className={`${geistSans.variable} ${geistMono.variable} antialiased`}>
        <ThemeProvider>{children}</ThemeProvider>
      </body>
    </html>
  );
}