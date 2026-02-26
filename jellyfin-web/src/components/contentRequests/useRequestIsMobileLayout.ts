import React from 'react';

import layoutManager from 'components/layoutManager';
import Events from 'utils/events';

const viewportMobileQuery = '(max-width: 767px)';
const touchFallbackQuery = '(pointer: coarse) and (max-width: 1024px)';
const touchFallbackMaxWidth = 1024;

const getViewportWidth = () => {
    if (typeof window === 'undefined') {
        return Number.POSITIVE_INFINITY;
    }

    const visualViewportWidth = window.visualViewport?.width;
    if (typeof visualViewportWidth === 'number' && visualViewportWidth > 0) {
        return visualViewportWidth;
    }

    return window.innerWidth;
};

const addMediaQueryListener = (query: MediaQueryList, listener: () => void) => {
    if (typeof query.addEventListener === 'function') {
        query.addEventListener('change', listener);
        return () => query.removeEventListener('change', listener);
    }

    query.addListener(listener);
    return () => query.removeListener(listener);
};

const getIsMobileLayout = () => {
    if (typeof window === 'undefined' || typeof document === 'undefined') {
        return false;
    }

    const hasMatchMedia = typeof window.matchMedia === 'function';
    const hasLayoutMobileClass = document.documentElement.classList.contains('layout-mobile');
    const isViewportMobile = hasMatchMedia
        ? window.matchMedia(viewportMobileQuery).matches
        : getViewportWidth() <= 767;
    const isTouchLayoutFallback = hasMatchMedia && window.matchMedia(touchFallbackQuery).matches;
    const hasTouchViewportFallback = typeof navigator !== 'undefined'
        && navigator.maxTouchPoints > 0
        && getViewportWidth() <= touchFallbackMaxWidth;

    return layoutManager.mobile
        || hasLayoutMobileClass
        || isViewportMobile
        || isTouchLayoutFallback
        || hasTouchViewportFallback;
};

const useRequestIsMobileLayout = () => {
    const [ isMobileLayout, setIsMobileLayout ] = React.useState(getIsMobileLayout);

    React.useEffect(() => {
        const viewportQuery = typeof window.matchMedia === 'function'
            ? window.matchMedia(viewportMobileQuery)
            : null;
        const touchQuery = typeof window.matchMedia === 'function'
            ? window.matchMedia(touchFallbackQuery)
            : null;
        const visualViewport = window.visualViewport;

        const updateLayoutMode = () => {
            setIsMobileLayout(getIsMobileLayout());
        };

        updateLayoutMode();

        const removeViewportListener = viewportQuery
            ? addMediaQueryListener(viewportQuery, updateLayoutMode)
            : () => {};
        const removeTouchListener = touchQuery
            ? addMediaQueryListener(touchQuery, updateLayoutMode)
            : () => {};

        window.addEventListener('resize', updateLayoutMode);
        window.addEventListener('orientationchange', updateLayoutMode);
        visualViewport?.addEventListener('resize', updateLayoutMode);
        Events.on(layoutManager, 'modechange', updateLayoutMode);

        return () => {
            removeViewportListener();
            removeTouchListener();
            window.removeEventListener('resize', updateLayoutMode);
            window.removeEventListener('orientationchange', updateLayoutMode);
            visualViewport?.removeEventListener('resize', updateLayoutMode);
            Events.off(layoutManager, 'modechange', updateLayoutMode);
        };
    }, []);

    return isMobileLayout;
};

export default useRequestIsMobileLayout;
