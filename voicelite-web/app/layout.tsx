import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import "./globals.css";
import "@/lib/env-validation"; // Validate environment on app startup

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: "VoiceLite - Instant Voice Typing for Windows",
  description: "Turn your voice into text instantly in ANY Windows app. 100% offline speech-to-text with global hotkey. No internet required. Your privacy guaranteed.",
  keywords: "voice typing, speech to text, Windows, offline, transcription, Whisper AI, voice recognition",
  openGraph: {
    title: "VoiceLite - Instant Voice Typing for Windows",
    description: "Turn your voice into text instantly. Works offline, in any Windows app.",
    type: "website",
    locale: "en_US",
    url: "https://voicelite.app",
    siteName: "VoiceLite",
  },
  twitter: {
    card: "summary_large_image",
    title: "VoiceLite - Instant Voice Typing for Windows",
    description: "Turn your voice into text instantly. Works offline, in any Windows app.",
  },
  robots: {
    index: true,
    follow: true,
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body
        className={`${geistSans.variable} ${geistMono.variable} antialiased`}
      >
        {children}
      </body>
    </html>
  );
}
