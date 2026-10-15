import { useEffect, useRef } from 'react';
import { useLocation } from 'react-router-dom';

export interface UsePageMetadataOptions {
  skipHeadingFocus?: boolean;
}

export function usePageMetadata(
  title: string,
  options: UsePageMetadataOptions = {}
) {
  const headingRef = useRef<HTMLHeadingElement>(null);
  const location = useLocation();

  useEffect(() => {
    document.title = title ? `${title} | Pixel RAG` : 'Pixel RAG';

    if (!options.skipHeadingFocus) {
      // Focus the page's primary heading on route change/mount
      headingRef.current?.focus();
    }
  }, [title, location.pathname, options.skipHeadingFocus]);

  return { headingRef };
}
