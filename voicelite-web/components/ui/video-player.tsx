'use client';

import * as React from 'react';
import { cn } from '@/lib/utils';

export interface VideoPlayerProps extends React.VideoHTMLAttributes<HTMLVideoElement> {
  variant?: 'hero' | 'feature' | 'thumbnail';
  src: string;
  poster?: string;
  fallbackImage?: string;
}

/**
 * VideoPlayer Component
 *
 * Variants:
 * - hero: Autoplay, muted, loop, no controls (for hero section)
 * - feature: User-initiated, with controls
 * - thumbnail: Preview on hover
 *
 * States:
 * - Loading (skeleton or spinner)
 * - Playing
 * - Paused
 * - Error (fallback image)
 *
 * Accessibility:
 * - Autoplay only if muted (browser policy)
 * - Include play/pause button overlay
 * - Provide fallback image if video fails
 *
 * Usage:
 * ```tsx
 * <VideoPlayer
 *   variant="hero"
 *   src="/videos/demo.mp4"
 *   poster="/images/demo-poster.jpg"
 *   fallbackImage="/images/demo-fallback.jpg"
 * />
 * ```
 */
export function VideoPlayer({
  variant = 'feature',
  src,
  poster,
  fallbackImage,
  className,
  ...props
}: VideoPlayerProps) {
  const videoRef = React.useRef<HTMLVideoElement>(null);
  const [isPlaying, setIsPlaying] = React.useState(variant === 'hero');
  const [isLoading, setIsLoading] = React.useState(true);
  const [hasError, setHasError] = React.useState(false);

  const isHero = variant === 'hero';
  const showControls = variant === 'feature';

  const handlePlayPause = () => {
    if (!videoRef.current) return;

    if (isPlaying) {
      videoRef.current.pause();
    } else {
      videoRef.current.play();
    }

    setIsPlaying(!isPlaying);
  };

  const handleLoadedData = () => {
    setIsLoading(false);
  };

  const handleError = () => {
    setIsLoading(false);
    setHasError(true);
  };

  // Pause hero video on hover (accessibility)
  const handleMouseEnter = () => {
    if (isHero && videoRef.current) {
      videoRef.current.pause();
    }
  };

  const handleMouseLeave = () => {
    if (isHero && videoRef.current) {
      videoRef.current.play();
    }
  };

  // Error fallback
  if (hasError && fallbackImage) {
    return (
      <div className={cn('relative overflow-hidden rounded-lg', className)}>
        <img
          src={fallbackImage}
          alt="Video unavailable"
          className="w-full h-full object-cover"
        />
        <div className="absolute inset-0 flex items-center justify-center bg-black bg-opacity-50">
          <p className="text-white text-sm">Video unavailable</p>
        </div>
      </div>
    );
  }

  return (
    <div
      className={cn('relative overflow-hidden rounded-lg group', className)}
      onMouseEnter={handleMouseEnter}
      onMouseLeave={handleMouseLeave}
    >
      {/* Loading Skeleton */}
      {isLoading && (
        <div className="absolute inset-0 bg-gray-200 animate-pulse flex items-center justify-center">
          <svg
            className="h-12 w-12 text-gray-400 animate-spin"
            xmlns="http://www.w3.org/2000/svg"
            fill="none"
            viewBox="0 0 24 24"
            aria-hidden="true"
          >
            <circle
              className="opacity-25"
              cx="12"
              cy="12"
              r="10"
              stroke="currentColor"
              strokeWidth="4"
            />
            <path
              className="opacity-75"
              fill="currentColor"
              d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
            />
          </svg>
        </div>
      )}

      {/* Video */}
      <video
        ref={videoRef}
        src={src}
        poster={poster}
        autoPlay={isHero}
        muted={isHero}
        loop={isHero}
        controls={showControls}
        playsInline
        onLoadedData={handleLoadedData}
        onError={handleError}
        className={cn('w-full h-full object-cover', isLoading && 'opacity-0')}
        {...props}
      />

      {/* Play/Pause Overlay (for hero variant) */}
      {isHero && !isLoading && !hasError && (
        <button
          onClick={handlePlayPause}
          className="absolute inset-0 flex items-center justify-center bg-black bg-opacity-0 hover:bg-opacity-30 transition-all duration-200"
          aria-label={isPlaying ? 'Pause video' : 'Play video'}
        >
          <div
            className={cn(
              'flex items-center justify-center w-16 h-16 rounded-full bg-white bg-opacity-90 shadow-lg transition-all duration-200',
              'opacity-0 group-hover:opacity-100'
            )}
          >
            {isPlaying ? (
              <svg
                className="h-8 w-8 text-gray-900"
                fill="currentColor"
                viewBox="0 0 24 24"
                aria-hidden="true"
              >
                <path d="M6 4h4v16H6V4zm8 0h4v16h-4V4z" />
              </svg>
            ) : (
              <svg
                className="h-8 w-8 text-gray-900 ml-1"
                fill="currentColor"
                viewBox="0 0 24 24"
                aria-hidden="true"
              >
                <path d="M8 5v14l11-7z" />
              </svg>
            )}
          </div>
        </button>
      )}
    </div>
  );
}
